using System.Windows.Threading;
using ClaudeUsage.Helpers;
using ClaudeUsage.Models;
using ClaudeUsage.Services;
using Wpf.Ui.Appearance;
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
        _mainWindow.Deactivated += (s, args) => _mainWindow.Hide();

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

        // Draw three bars like the Claude Usage icon
        using var brush = new Drawing.SolidBrush(color);
        g.FillRectangle(brush, 2, 8, 3, 6);   // Short bar
        g.FillRectangle(brush, 6, 4, 3, 10);  // Medium bar
        g.FillRectangle(brush, 10, 2, 3, 12); // Tall bar

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

        // Left-click shows the popup
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowPopup();
            }
        };

        // Right-click context menu
        var contextMenu = new Forms.ContextMenuStrip();

        var refreshItem = new Forms.ToolStripMenuItem("Refresh Now");
        refreshItem.Click += async (s, e) => await RefreshUsageData();

        var launchAtLoginItem = new Forms.ToolStripMenuItem("Launch at Login")
        {
            Checked = StartupHelper.IsLaunchAtLoginEnabled()
        };
        launchAtLoginItem.Click += (s, e) =>
        {
            launchAtLoginItem.Checked = !launchAtLoginItem.Checked;
            StartupHelper.SetLaunchAtLogin(launchAtLoginItem.Checked);
        };

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            _notifyIcon.Visible = false;
            Shutdown();
        };

        contextMenu.Items.Add(refreshItem);
        contextMenu.Items.Add(launchAtLoginItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowPopup()
    {
        if (_mainWindow == null) return;

        // Update the window with latest data
        _mainWindow.UpdateUsageData(_lastUsageData, _lastUpdated);

        // Position near the tray icon (bottom-right of screen)
        var workArea = System.Windows.SystemParameters.WorkArea;
        _mainWindow.Left = workArea.Right - _mainWindow.Width - 10;
        _mainWindow.Top = workArea.Bottom - _mainWindow.Height - 10;

        _mainWindow.Show();
        _mainWindow.Activate();
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

        // Update icon based on usage
        var maxUtilization = Math.Max(
            usage.FiveHour?.Utilization ?? 0,
            usage.SevenDay?.Utilization ?? 0
        );

        if (maxUtilization >= 0.9)
            _notifyIcon!.Icon = _iconRed;
        else if (maxUtilization >= 0.7)
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
