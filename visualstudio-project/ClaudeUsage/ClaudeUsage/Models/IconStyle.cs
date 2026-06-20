namespace ClaudeUsage.Models;

/// <summary>
/// How the session-usage percentage is drawn in the system tray.
/// </summary>
public enum IconStyle
{
    /// <summary>Original look: badge with a number, frame and severity dot (default).</summary>
    Badge,

    /// <summary>Bold percentage digits sized to fill the icon, on a color-coded background.</summary>
    Number,

    /// <summary>Circular progress arc, color-coded by severity.</summary>
    Ring,

    /// <summary>Bottom-up fill gauge, color-coded by severity.</summary>
    Bar
}
