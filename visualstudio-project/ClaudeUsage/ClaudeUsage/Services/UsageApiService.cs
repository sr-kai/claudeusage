using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsage.Models;

namespace ClaudeUsage.Services;

public class UsageApiService
{
    private static readonly HttpClient _httpClient = new();
    private const string UsageApiUrl = "https://api.anthropic.com/api/oauth/usage";
    private const int MaxRetries = 5;

    private static string? _cachedClaudeCodeVersion;

    private static string GetClaudeCodeVersion()
    {
        if (_cachedClaudeCodeVersion != null)
            return _cachedClaudeCodeVersion;

        try
        {
            // Try native Windows first, then WSL
            var version = TryGetVersionFromProcess("claude", "--version")
                       ?? TryGetVersionFromProcess("wsl", "claude --version");

            _cachedClaudeCodeVersion = version ?? "2.1.100";
            System.Diagnostics.Debug.WriteLine($"Claude Code version detected: {_cachedClaudeCodeVersion}");
        }
        catch
        {
            _cachedClaudeCodeVersion = "2.1.100";
            System.Diagnostics.Debug.WriteLine("Claude Code version detection failed, using fallback 2.1.100");
        }

        return _cachedClaudeCodeVersion;
    }

    private static string? TryGetVersionFromProcess(string fileName, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var stdout = process.StandardOutput.ReadToEnd().Trim();
            var stderr = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit(5000);

            // Extract first dotted-version substring. Handles "2.1.143",
            // "claude-code 1.2.3", and "2.1.143 (Claude Code)" — the last
            // shape broke the previous split-and-take-last-token approach.
            var match = System.Text.RegularExpressions.Regex.Match(stdout, @"\d+\.\d+(?:\.\d+)?");
            if (match.Success)
            {
                return match.Value;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Version probe [{fileName} {arguments}] no match: exit={process.ExitCode}, stdout='{stdout}', stderr='{stderr}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Version probe [{fileName} {arguments}] threw: {ex.Message}");
        }

        return null;
    }

    public static async Task<UsageData?> GetUsageAsync()
    {
        var token = await CredentialService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var claudeVersion = GetClaudeCodeVersion();

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, UsageApiUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("User-Agent", $"claude-code/{claudeVersion}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

                System.Diagnostics.Debug.WriteLine($"Request: User-Agent=claude-code/{claudeVersion}, Token={token?[..Math.Min(20, token.Length)]}...");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"API Response: {json}");
                    return JsonSerializer.Deserialize<UsageData>(json);
                }

                var statusCode = (int)response.StatusCode;
                var errorBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"API Error (attempt {attempt + 1}/{MaxRetries + 1}): {response.StatusCode} - {errorBody}");

                // Retry on 429 (rate limit) or 5xx (server error)
                if ((statusCode == 429 || statusCode >= 500) && attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 1s, 2s, 4s, 8s, 16s
                    System.Diagnostics.Debug.WriteLine($"Retrying in {delay.TotalSeconds}s...");
                    await Task.Delay(delay);
                    continue;
                }

                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Transient transport/timeout failures — retry with backoff.
                System.Diagnostics.Debug.WriteLine(
                    $"Transient exception in GetUsageAsync (attempt {attempt + 1}/{MaxRetries + 1}): {ex.Message}");

                if (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay);
                    continue;
                }

                return null;
            }
            catch (Exception ex)
            {
                // Non-transient (e.g. JsonException): the request succeeded but
                // we can't use the response. Retrying won't fix it and burns
                // rate-limit budget — fail fast.
                System.Diagnostics.Debug.WriteLine(
                    $"Non-retryable exception in GetUsageAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        return null;
    }
}
