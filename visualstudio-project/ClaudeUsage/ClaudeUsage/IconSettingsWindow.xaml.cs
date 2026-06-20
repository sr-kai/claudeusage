using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClaudeUsage.Helpers;
using ClaudeUsage.Models;
using ClaudeUsage.Services;
using Wpf.Ui.Controls;

namespace ClaudeUsage;

public partial class IconSettingsWindow : FluentWindow
{
    // Invoked whenever a setting changes so the tray icon can re-render live.
    private readonly Action? _onChanged;

    // Suppresses save/refresh while controls are being initialised from saved state.
    private bool _loading;

    public IconSettingsWindow(Action? onChanged)
    {
        _onChanged = onChanged;
        // Slider/RadioButton events fire during InitializeComponent() as the XAML
        // sets Min/Max/Value; guard them until the controls and saved state are ready.
        _loading = true;
        InitializeComponent();
        ApplyLocalization();
        PopulateState();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("icon_settings");
        HeaderTitle.Text = LocalizationService.T("icon_settings");
        StyleHeader.Text = LocalizationService.T("icon_style");
        ThresholdsHeader.Text = LocalizationService.T("severity_thresholds");

        BadgeTitle.Text = LocalizationService.T("icon_style_badge");
        BadgeDesc.Text = LocalizationService.T("icon_style_badge_desc");
        NumberTitle.Text = LocalizationService.T("icon_style_number");
        NumberDesc.Text = LocalizationService.T("icon_style_number_desc");
        RingTitle.Text = LocalizationService.T("icon_style_ring");
        RingDesc.Text = LocalizationService.T("icon_style_ring_desc");
        BarTitle.Text = LocalizationService.T("icon_style_bar");
        BarDesc.Text = LocalizationService.T("icon_style_bar_desc");

        WarnTitle.Text = LocalizationService.T("threshold_warning");
        WarnDesc.Text = LocalizationService.T("threshold_warning_desc");
        CritTitle.Text = LocalizationService.T("threshold_critical");
        CritDesc.Text = LocalizationService.T("threshold_critical_desc");
        OrderNote.Text = LocalizationService.T("threshold_order_note");
    }

    private void PopulateState()
    {
        _loading = true;

        var style = Enum.TryParse<IconStyle>(StartupHelper.GetIconStyle(), out var s) ? s : IconStyle.Badge;
        var radio = style switch
        {
            IconStyle.Number => NumberRadio,
            IconStyle.Ring => RingRadio,
            IconStyle.Bar => BarRadio,
            _ => BadgeRadio
        };
        radio.IsChecked = true;

        WarnSlider.Value = StartupHelper.GetWarnThreshold();
        CritSlider.Value = StartupHelper.GetCritThreshold();
        UpdateValueTexts();
        UpdateOrderNote();

        _loading = false;
    }

    private void StyleRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is System.Windows.Controls.RadioButton { Tag: string tag } && Enum.TryParse<IconStyle>(tag, out _))
        {
            StartupHelper.SaveIconStyle(tag);
            _onChanged?.Invoke();
        }
    }

    private void WarnSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        StartupHelper.SaveWarnThreshold((int)Math.Round(WarnSlider.Value));
        UpdateValueTexts();
        UpdateOrderNote();
        _onChanged?.Invoke();
    }

    private void CritSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        StartupHelper.SaveCritThreshold((int)Math.Round(CritSlider.Value));
        UpdateValueTexts();
        UpdateOrderNote();
        _onChanged?.Invoke();
    }

    private void UpdateValueTexts()
    {
        WarnValueText.Text = $"≥ {(int)Math.Round(WarnSlider.Value)}%";
        CritValueText.Text = $"≥ {(int)Math.Round(CritSlider.Value)}%";
    }

    private void UpdateOrderNote()
    {
        OrderNote.Visibility = WarnSlider.Value >= CritSlider.Value
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
