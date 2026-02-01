using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeUsage.Helpers;
using ClaudeUsage.Models;
using ClaudeUsage.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ClaudeUsage;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _refreshTimer;
    private UsageData? _lastUsageData;
    private DateTime _lastUpdated;
    private ContextMenu? _contextMenu;
    private System.Windows.Controls.MenuItem? _launchAtLoginItem;
    private DateTime _lastDeactivated;

    private Drawing.Icon? _iconGreen;
    private Drawing.Icon? _iconYellow;
    private Drawing.Icon? _iconRed;
    private Drawing.Icon? _iconGray;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply system theme
        ApplicationThemeManager.ApplySystemTheme();

        // Create icons
        CreateIcons();

        // Create the tray icon
        CreateTrayIcon();

        // Create the main window (hidden initially)
        _mainWindow = new MainWindow();
        _mainWindow.Deactivated += (s, args) =>
        {
            _lastDeactivated = DateTime.Now;
            _mainWindow.HideWithAnimation();
        };

        // Set up auto-refresh timer (2 minutes)
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(2)
        };
        _refreshTimer.Tick += async (s, args) => await RefreshUsageData();
        _refreshTimer.Start();

        // Initial data fetch
        await RefreshUsageData();
    }

    private void CreateIcons()
    {
        // Create simple colored icons programmatically
        _iconGreen = CreateColoredIcon(Drawing.Color.FromArgb(34, 197, 94));   // Green
        _iconYellow = CreateColoredIcon(Drawing.Color.FromArgb(234, 179, 8));  // Yellow
        _iconRed = CreateColoredIcon(Drawing.Color.FromArgb(239, 68, 68));     // Red
        _iconGray = CreateColoredIcon(Drawing.Color.FromArgb(156, 163, 175));  // Gray
    }

    private static Drawing.Icon CreateColoredIcon(Drawing.Color color)
    {
        using var bitmap = new Drawing.Bitmap(16, 16);
        using var g = Drawing.Graphics.FromImage(bitmap);

        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Drawing.Color.Transparent);

        // Draw a filled circle
        using var brush = new Drawing.SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);

        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _iconGray,
            Visible = true,
            Text = "Claude Usage - Loading..."
        };

        // Create WPF context menu with Fluent styling
        CreateContextMenu();

        // Left-click shows the popup, right-click shows context menu
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowPopup();
            }
            else if (e.Button == Forms.MouseButtons.Right)
            {
                ShowContextMenu();
            }
        };
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenu();

        var refreshItem = new System.Windows.Controls.MenuItem
        {
            Header = "Refresh Now",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowClockwise24 }
        };
        refreshItem.Click += async (s, e) => await RefreshUsageData();

        _launchAtLoginItem = new System.Windows.Controls.MenuItem
        {
            Header = "Launch at Login",
            IsCheckable = true,
            IsChecked = StartupHelper.IsLaunchAtLoginEnabled()
        };
        _launchAtLoginItem.Click += (s, e) =>
        {
            StartupHelper.SetLaunchAtLogin(_launchAtLoginItem.IsChecked);
        };

        var exitItem = new System.Windows.Controls.MenuItem
        {
            Header = "Exit",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 }
        };
        exitItem.Click += (s, e) =>
        {
            _notifyIcon!.Visible = false;
            Shutdown();
        };

        _contextMenu.Items.Add(refreshItem);
        _contextMenu.Items.Add(_launchAtLoginItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(exitItem);
    }

    private void ShowContextMenu()
    {
        if (_contextMenu == null) return;

        // Update launch at login state
        if (_launchAtLoginItem != null)
        {
            _launchAtLoginItem.IsChecked = StartupHelper.IsLaunchAtLoginEnabled();
        }

        _contextMenu.IsOpen = true;
    }

    private void ShowPopup()
    {
        if (_mainWindow == null) return;

        // If window was just closed by clicking tray icon, don't reopen it
        // (the click causes Deactivated which hides it, then this runs)
        if ((DateTime.Now - _lastDeactivated).TotalMilliseconds < 500)
        {
            return;
        }

        // Update the window with latest data
        _mainWindow.UpdateUsageData(_lastUsageData, _lastUpdated);

        // Position near the tray icon (bottom-right of screen)
        var workArea = System.Windows.SystemParameters.WorkArea;
        var targetLeft = workArea.Right - _mainWindow.Width - 10;
        var targetTop = workArea.Bottom - _mainWindow.Height - 10;

        _mainWindow.ShowWithAnimation(targetLeft, targetTop);
    }

    public async Task RefreshUsageData()
    {
        if (!CredentialService.CredentialsExist())
        {
            _notifyIcon!.Icon = _iconGray;
            _notifyIcon.Text = "Claude Usage - No credentials found\nRun 'claude' to authenticate";
            return;
        }

        var usage = await UsageApiService.GetUsageAsync();

        if (usage == null)
        {
            _notifyIcon!.Icon = _iconGray;
            _notifyIcon.Text = "Claude Usage - Failed to fetch data";
            return;
        }

        _lastUsageData = usage;
        _lastUpdated = DateTime.Now;

        // Update icon based on usage (utilization is already a percentage, e.g. 8.0 = 8%)
        var maxUtilization = Math.Max(
            usage.FiveHour?.Utilization ?? 0,
            usage.SevenDay?.Utilization ?? 0
        );

        if (maxUtilization >= 90)
            _notifyIcon!.Icon = _iconRed;
        else if (maxUtilization >= 70)
            _notifyIcon!.Icon = _iconYellow;
        else
            _notifyIcon!.Icon = _iconGreen;

        // Update tooltip
        var sessionPct = usage.FiveHour?.UtilizationPercent ?? 0;
        var weeklyPct = usage.SevenDay?.UtilizationPercent ?? 0;
        var sessionReset = usage.FiveHour?.TimeUntilReset ?? "N/A";
        var weeklyReset = usage.SevenDay?.TimeUntilReset ?? "N/A";

        _notifyIcon.Text = $"Claude Usage\nSession: {sessionPct}% (resets in {sessionReset})\nWeekly: {weeklyPct}% (resets in {weeklyReset})";

        // Update popup if visible
        if (_mainWindow?.IsVisible == true)
        {
            _mainWindow.UpdateUsageData(_lastUsageData, _lastUpdated);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _iconGreen?.Dispose();
        _iconYellow?.Dispose();
        _iconRed?.Dispose();
        _iconGray?.Dispose();
        base.OnExit(e);
    }
}
