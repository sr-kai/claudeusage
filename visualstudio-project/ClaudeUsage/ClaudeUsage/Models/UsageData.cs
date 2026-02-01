using System.Text.Json.Serialization;

namespace ClaudeUsage.Models;

public class UsageData
{
    [JsonPropertyName("five_hour")]
    public UsageWindow? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public UsageWindow? SevenDay { get; set; }

    [JsonPropertyName("sonnet_only")]
    public UsageWindow? SonnetOnly { get; set; }
}

public class UsageWindow
{
    [JsonPropertyName("utilization")]
    public double Utilization { get; set; }

    [JsonPropertyName("resets_at")]
    public DateTimeOffset ResetsAt { get; set; }

    public int UtilizationPercent => (int)Utilization;

    public string TimeUntilReset
    {
        get
        {
            var remaining = ResetsAt - DateTimeOffset.UtcNow;
            if (remaining.TotalSeconds <= 0)
                return "now";

            if (remaining.TotalDays >= 1)
                return $"{(int)remaining.TotalDays}d {remaining.Hours}h";

            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";

            return $"{remaining.Minutes}m";
        }
    }
}

public class CredentialsFile
{
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeOAuth? ClaudeAiOauth { get; set; }
}

public class ClaudeOAuth
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expiresAt")]
    public long? ExpiresAt { get; set; }

    [JsonPropertyName("scopes")]
    public string[]? Scopes { get; set; }

    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; set; }
}
