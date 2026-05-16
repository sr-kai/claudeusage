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
        // Try native Windows first via cmd.exe so PATHEXT resolves claude.cmd
        // (npm installs Claude Code as claude.cmd, which Process.Start with
        // UseShellExecute=false cannot find directly — see issue #4).
        var version = TryGetVersionFromProcess("cmd.exe", "/c claude --version");

        // Fall back to WSL only if WSL is actually installed. Invoking wsl.exe
        // on a system without WSL triggers the Microsoft Store install prompt
        // rather than failing — see issue #4.
        if (version == null && IsWslInstalled())
        {
            version = TryGetVersionFromProcess("wsl", "claude --version");
        }

        _cachedClaudeCodeVersion = version ?? "2.1.0";
        System.Diagnostics.Debug.WriteLine($"Claude Code version detected: {_cachedClaudeCodeVersion}");
    }
    catch
    {
        _cachedClaudeCodeVersion = "2.1.0";
        System.Diagnostics.Debug.WriteLine("Claude Code version detection failed, using fallback 2.1.0");
    }

    return _cachedClaudeCodeVersion;
}

private static bool IsWslInstalled()
{
    // Enumerate registered WSL distros without invoking wsl.exe. The Lxss
    // registry key contains a subkey (GUID-named) per installed distro;
    // absent or empty means no distros are installed.
    try
    {
        using var lxss = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss");
        return lxss?.GetSubKeyNames().Length > 0;
    }
    catch
    {
        return false;
    }
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
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (!string.IsNullOrWhiteSpace(output))
            {
                // Output may be "1.2.3" or "claude-code 1.2.3" — take last token
                var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var versionString = parts[^1];

                if (System.Text.RegularExpressions.Regex.IsMatch(versionString, @"^\d+\.\d+"))
                {
                    return versionString;
                }
            }
        }
        catch
        {
            // Process not found or failed
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Exception in GetUsageAsync (attempt {attempt + 1}/{MaxRetries + 1}): {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay);
                    continue;
                }

                return null;
            }
        }

        return null;
    }
}
