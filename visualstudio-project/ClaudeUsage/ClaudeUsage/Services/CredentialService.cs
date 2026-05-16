using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using ClaudeUsage.Models;

namespace ClaudeUsage.Services;

public class CredentialService
{
    private static readonly HttpClient _httpClient = new();
    private const string TokenRefreshUrl = "https://console.anthropic.com/v1/oauth/token";

    private static string? _cachedCredentialsPath;
    private static DateTime _cacheTimestamp = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    private static string GetWindowsNativePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            ".credentials.json"
        );
    }

    private static bool IsWslInstalled()
    {
        // See UsageApiService.IsWslInstalled — checks the WSL distro registry
        // key without invoking wsl.exe or touching \\wsl$ paths.
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

    private static async Task<string?> FindCredentialsPathAsync()
    {
        // Fast path: return cached path if still valid
        if (_cachedCredentialsPath != null
            && File.Exists(_cachedCredentialsPath)
            && DateTime.UtcNow - _cacheTimestamp < CacheLifetime)
        {
            return _cachedCredentialsPath;
        }

        var candidates = new List<(string Path, DateTime LastModified)>();

        // 1. Check Windows native path first (instant local FS check)
        var windowsPath = GetWindowsNativePath();
        if (File.Exists(windowsPath))
        {
            try
            {
                candidates.Add((windowsPath, File.GetLastWriteTimeUtc(windowsPath)));
                System.Diagnostics.Debug.WriteLine($"Found native Windows credentials at: {windowsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading native credentials timestamp: {ex.Message}");
                candidates.Add((windowsPath, DateTime.MinValue));
            }
        }

        // 2. Check WSL paths (only if WSL is actually installed — accessing
        //    \\wsl$\ or \\wsl.localhost\ UNC paths on a system without WSL
        //    triggers the Microsoft Store install prompt). See issue #4.
        if (IsWslInstalled())
        {
            await Task.Run(() =>
            {
                string[] wslDistros = ["Debian", "Ubuntu", "Ubuntu-22.04", "Ubuntu-20.04", "kali-linux"];
                string[] wslRoots = [@"\\wsl.localhost", @"\\wsl$"];

                foreach (var wslRoot in wslRoots)
                foreach (var distro in wslDistros)
                {
                    try
                    {
                        var wslHomePath = $@"{wslRoot}\{distro}\home";
                        if (!Directory.Exists(wslHomePath)) continue;

                        foreach (var userDir in Directory.GetDirectories(wslHomePath))
                        {
                            var credPath = Path.Combine(userDir, ".claude", ".credentials.json");
                            if (File.Exists(credPath))
                            {
                                try
                                {
                                    candidates.Add((credPath, File.GetLastWriteTimeUtc(credPath)));
                                }
                                catch
                                {
                                    candidates.Add((credPath, DateTime.MinValue));
                                }
                                System.Diagnostics.Debug.WriteLine($"Found WSL credentials at: {credPath}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WSL path error ({wslRoot}\\{distro}): {ex.Message}");
                    }
                }
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }

        if (candidates.Count == 0)
        {
            _cachedCredentialsPath = null;
            return null;
        }

        // Pick the most recently modified credentials file
        var best = candidates.OrderByDescending(c => c.LastModified).First();
        _cachedCredentialsPath = best.Path;
        _cacheTimestamp = DateTime.UtcNow;

        System.Diagnostics.Debug.WriteLine($"Selected credentials: {best.Path} (modified {best.LastModified:u})");
        if (candidates.Count > 1)
        {
            System.Diagnostics.Debug.WriteLine($"  ({candidates.Count} candidates found, picked most recent)");
        }

        return best.Path;
    }

    public static async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var credentialsPath = await FindCredentialsPathAsync();
            if (credentialsPath == null)
            {
                return null;
            }

            var json = File.ReadAllText(credentialsPath);
            var credentials = JsonSerializer.Deserialize<CredentialsFile>(json);

            if (credentials?.ClaudeAiOauth == null)
            {
                return null;
            }

            // Check if token is expired
            var expiresAt = credentials.ClaudeAiOauth.ExpiresAt ?? 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // If token expires in less than 5 minutes, refresh it
            if (now >= expiresAt - (5 * 60 * 1000))
            {
                System.Diagnostics.Debug.WriteLine("Token expired or expiring soon, refreshing...");
                var refreshed = await RefreshTokenAsync(credentials.ClaudeAiOauth.RefreshToken);
                if (refreshed != null)
                {
                    credentials.ClaudeAiOauth = refreshed;
                    await SaveCredentialsAsync(credentials, credentialsPath);
                    return refreshed.AccessToken;
                }
            }

            return credentials.ClaudeAiOauth.AccessToken;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting access token: {ex.Message}");
            return null;
        }
    }

    private static async Task<ClaudeOAuth?> RefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenRefreshUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var formData = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            };

            request.Content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Token refresh failed: {response.StatusCode} - {error}");
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Token refresh response: {jsonResponse}");

            var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(jsonResponse);

            if (tokenResponse == null)
            {
                return null;
            }

            return new ClaudeOAuth
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (tokenResponse.ExpiresIn * 1000),
                Scopes = tokenResponse.Scope?.Split(' ')
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception refreshing token: {ex.Message}");
            return null;
        }
    }

    private static async Task SaveCredentialsAsync(CredentialsFile credentials, string credentialsPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            await File.WriteAllTextAsync(credentialsPath, json);
            System.Diagnostics.Debug.WriteLine($"Credentials saved successfully to {credentialsPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
        }
    }

    public static bool CredentialsExist()
    {
        // Fast synchronous check: cached path or Windows native path
        if (_cachedCredentialsPath != null && File.Exists(_cachedCredentialsPath))
        {
            return true;
        }

        // Quick fallback: check Windows native path (no WSL scan, avoids blocking)
        return File.Exists(GetWindowsNativePath());
    }

    public static string? GetCredentialsPath()
    {
        return _cachedCredentialsPath;
    }
}

public class TokenRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
