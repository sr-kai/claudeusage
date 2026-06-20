using Microsoft.Win32;

namespace ClaudeUsage.Helpers;

public static class StartupHelper
{
    private const string AppName = "ClaudeUsage";
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string SettingsKeyPath = @"SOFTWARE\ClaudeUsage";

    public static bool IsLaunchAtLoginEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetLaunchAtLogin(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Silently fail if registry access is denied
        }
    }

    public static string? GetSavedLanguage()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false);
            return key?.GetValue("Language") as string;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveLanguage(string langCode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key.SetValue("Language", langCode);
        }
        catch
        {
            // Silently fail
        }
    }

    public static string? GetIconStyle()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false);
            return key?.GetValue("IconStyle") as string;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveIconStyle(string style)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key.SetValue("IconStyle", style);
        }
        catch
        {
            // Silently fail
        }
    }

    // Severity color thresholds (percent utilization). Defaults match the
    // previously hard-coded values: warn (yellow) at 70, critical (red) at 90.
    public static int GetWarnThreshold() => GetIntSetting("WarnThreshold", 70);
    public static int GetCritThreshold() => GetIntSetting("CritThreshold", 90);
    public static void SaveWarnThreshold(int value) => SaveIntSetting("WarnThreshold", value);
    public static void SaveCritThreshold(int value) => SaveIntSetting("CritThreshold", value);

    private static int GetIntSetting(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false);
            var value = key?.GetValue(name);
            if (value is int i) return i;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void SaveIntSetting(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
            // Silently fail
        }
    }

    public static string? GetHookSetting(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false);
            return key?.GetValue($"Hook_{name}") as string;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveHookSetting(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key.SetValue($"Hook_{name}", value);
        }
        catch
        {
            // Silently fail
        }
    }
}
