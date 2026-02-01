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

    private static string? FindCredentialsPath()
    {
        if (_cachedCredentialsPath != null && File.Exists(_cachedCredentialsPath))
        {
            return _cachedCredentialsPath;
        }

        // Try WSL paths first (common distros)
        string[] wslDistros = ["Debian", "Ubuntu", "Ubuntu-22.04", "Ubuntu-20.04", "kali-linux"];

        foreach (var distro in wslDistros)
        {
            var wslHomePath = $@"\\wsl$\{distro}\home";
            if (Directory.Exists(wslHomePath))
            {
                try
                {
                    foreach (var userDir in Directory.GetDirectories(wslHomePath))
                    {
                        var credPath = Path.Combine(userDir, ".claude", ".credentials.json");
                        if (File.Exists(credPath))
                        {
                            _cachedCredentialsPath = credPath;
                            System.Diagnostics.Debug.WriteLine($"Found credentials at: {credPath}");
                            return credPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error searching {wslHomePath}: {ex.Message}");
                }
            }
        }

        // Fallback to Windows path
        var windowsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            ".credentials.json"
        );

        if (File.Exists(windowsPath))
        {
            _cachedCredentialsPath = windowsPath;
            return windowsPath;
        }

        return null;
    }

    public static async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var credentialsPath = FindCredentialsPath();
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
        return FindCredentialsPath() != null;
    }

    public static string? GetCredentialsPath()
    {
        return FindCredentialsPath();
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
