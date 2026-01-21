using System.Diagnostics;
using System.Reflection;
using System.Windows.Media;
using ClaudeUsage.Helpers;
using ClaudeUsage.Models;
using Wpf.Ui.Controls;

namespace ClaudeUsage;

public partial class MainWindow : FluentWindow
{
    private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush YellowBrush = new(System.Windows.Media.Color.FromRgb(234, 179, 8));
    private static readonly SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68));

    public MainWindow()
    {
        InitializeComponent();

        // Set version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

        // Initialize launch at login toggle
        LaunchAtLoginToggle.IsChecked = StartupHelper.IsLaunchAtLoginEnabled();
    }

    public void UpdateUsageData(UsageData? data, DateTime lastUpdated)
    {
        if (data == null)
        {
            SessionPercentText.Text = "--%";
            WeeklyPercentText.Text = "--%";
            SessionProgressBar.Value = 0;
            WeeklyProgressBar.Value = 0;
            SessionResetText.Text = "Resets in --";
            WeeklyResetText.Text = "Resets in --";
            LastUpdatedText.Text = "No data";
            return;
        }

        // Session data
        var sessionPct = data.FiveHour?.UtilizationPercent ?? 0;
        SessionPercentText.Text = $"{sessionPct}%";
        SessionProgressBar.Value = sessionPct;
        SessionResetText.Text = $"Resets in {data.FiveHour?.TimeUntilReset ?? "--"}";
        SessionPercentText.Foreground = GetColorForPercent(sessionPct);

        // Weekly data
        var weeklyPct = data.SevenDay?.UtilizationPercent ?? 0;
        WeeklyPercentText.Text = $"{weeklyPct}%";
        WeeklyProgressBar.Value = weeklyPct;
        WeeklyResetText.Text = $"Resets in {data.SevenDay?.TimeUntilReset ?? "--"}";
        WeeklyPercentText.Foreground = GetColorForPercent(weeklyPct);

        // Last updated
        var secondsAgo = (int)(DateTime.Now - lastUpdated).TotalSeconds;
        LastUpdatedText.Text = secondsAgo < 60
            ? $"Updated {secondsAgo} seconds ago"
            : $"Updated {(int)(DateTime.Now - lastUpdated).TotalMinutes} minutes ago";
    }

    private static SolidColorBrush GetColorForPercent(int percent)
    {
        if (percent >= 90) return RedBrush;
        if (percent >= 70) return YellowBrush;
        return GreenBrush;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
        }
    }

    private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            await app.RefreshUsageData();
        }
    }

    private void GitHubButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/anthropics/claude-code",
            UseShellExecute = true
        });
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Hide();
    }

    private void CheckUpdatesButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // For now, just open the GitHub releases page
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/anthropics/claude-code/releases",
            UseShellExecute = true
        });
    }

    private void LaunchAtLoginToggle_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        StartupHelper.SetLaunchAtLogin(LaunchAtLoginToggle.IsChecked == true);
    }
}
