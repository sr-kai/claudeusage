using System.Text.Json.Serialization;

namespace ClaudeUsage.Models;

public class UsageData
{
    [JsonPropertyName("five_hour")]
    public UsageWindow? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public UsageWindow? SevenDay { get; set; }

    [JsonPropertyName("seven_day_sonnet")]
    public UsageWindow? SevenDaySonnet { get; set; }

    [JsonPropertyName("sonnet_only")]
    public UsageWindow? SonnetOnly { get; set; }

    [JsonPropertyName("extra_usage")]
    public ExtraUsageData? ExtraUsage { get; set; }

    /// <summary>
    /// Returns sonnet data from seven_day_sonnet (primary) or sonnet_only (fallback).
    /// </summary>
    public UsageWindow? Sonnet => SevenDaySonnet ?? SonnetOnly;
}

public class UsageWindow
{
    [JsonPropertyName("utilization")]
    public double Utilization { get; set; }

    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }

    public int UtilizationPercent => (int)Utilization;

    public double? GetElapsedPercent(int periodSeconds)
    {
        if (ResetsAt == null) return null;
        var remaining = (ResetsAt.Value - DateTimeOffset.UtcNow).TotalSeconds;
        var elapsed = periodSeconds - remaining;
        return Math.Clamp(elapsed / periodSeconds * 100, 0, 100);
    }

    public string TimeUntilReset
    {
        get
        {
            if (ResetsAt == null) return "--";

            var remaining = ResetsAt.Value - DateTimeOffset.UtcNow;
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

public class ExtraUsageData
{
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    // monthly_limit and used_credits arrive as null when extra usage is not enabled.
    [JsonPropertyName("monthly_limit")]
    public double? MonthlyLimit { get; set; }

    [JsonPropertyName("used_credits")]
    public double? UsedCredits { get; set; }

    public double LimitDollars => (MonthlyLimit ?? 0) / 100.0;
    public double UsedDollars => (UsedCredits ?? 0) / 100.0;
    public int UtilizationPercent =>
        MonthlyLimit is > 0 && UsedCredits is not null
            ? (int)(UsedCredits.Value / MonthlyLimit.Value * 100)
            : 0;
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
