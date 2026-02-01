using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClaudeUsage.Helpers;
using ClaudeUsage.Models;
using Wpf.Ui.Controls;

namespace ClaudeUsage;

public partial class MainWindow : FluentWindow
{
    private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush YellowBrush = new(System.Windows.Media.Color.FromRgb(234, 179, 8));
    private static readonly SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68));

    private double _targetTop;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize launch at login toggle
        LaunchAtLoginToggle.IsChecked = StartupHelper.IsLaunchAtLoginEnabled();
    }

    public void ShowWithAnimation(double targetLeft, double targetTop)
    {
        _targetTop = targetTop;

        // Start position (slightly below)
        Left = targetLeft;
        Top = targetTop + 20;
        Opacity = 1;

        Show();
        Activate();

        // Animate slide up
        var slideAnimation = new DoubleAnimation
        {
            From = targetTop + 20,
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(TopProperty, slideAnimation);
    }

    public void HideWithAnimation()
    {
        // Animate slide down
        var slideAnimation = new DoubleAnimation
        {
            To = Top + 20,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        slideAnimation.Completed += (s, e) =>
        {
            Hide();
            // Clear animation so position can be set normally next time
            BeginAnimation(TopProperty, null);
        };

        BeginAnimation(TopProperty, slideAnimation);
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
            HideWithAnimation();
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
            FileName = "https://github.com/sr-kai/claudeusage",
            UseShellExecute = true
        });
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        HideWithAnimation();
    }

    private void LaunchAtLoginToggle_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        StartupHelper.SetLaunchAtLogin(LaunchAtLoginToggle.IsChecked == true);
    }
}
