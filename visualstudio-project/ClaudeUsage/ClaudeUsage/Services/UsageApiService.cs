using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsage.Models;

namespace ClaudeUsage.Services;

public class UsageApiService
{
    private static readonly HttpClient _httpClient = new();
    private const string UsageApiUrl = "https://api.anthropic.com/api/oauth/usage";

    public static async Task<UsageData?> GetUsageAsync()
    {
        var token = await CredentialService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("User-Agent", "ClaudeUsageWindows/1.0");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Error: {response.StatusCode} - {errorBody}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"API Response: {json}");
            return JsonSerializer.Deserialize<UsageData>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in GetUsageAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }
}
