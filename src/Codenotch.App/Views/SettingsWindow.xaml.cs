using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Codenotch.Core.Services;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace Codenotch.App.Views;

public partial class SettingsWindow : Window
{
    private readonly UsageManager _usageManager;
    private readonly NotchWindow _notchWindow;
    private readonly Dictionary<WpfCheckBox, string> _providerBoxes = new();
    private readonly Dictionary<string, WpfTextBlock> _providerStatusLabels = new(StringComparer.OrdinalIgnoreCase);

    public SettingsWindow(UsageManager usageManager, NotchWindow notchWindow)
    {
        InitializeComponent();
        MaxHeight = Math.Max(450, SystemParameters.WorkArea.Height - 24);
        Height = Math.Min(Height, MaxHeight);
        _usageManager = usageManager;
        _notchWindow = notchWindow;
        ChkAlwaysShow.IsChecked = _usageManager.AlwaysExpanded;
        ChkAlerts.IsChecked = _usageManager.AlertsEnabled;

        BuildProviderSelector();
        SourceInitialized += SettingsWindow_SourceInitialized;
        Loaded += SettingsWindow_Loaded;
        Closed += SettingsWindow_Closed;
    }

    private void BtnResetPos_Click(object sender, RoutedEventArgs e)
    {
        _notchWindow.ResetPositionAtRightEdge();
        _notchWindow.Show();
    }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        const int darkModeAttribute = 20;
        const int legacyDarkModeAttribute = 19;
        if (DwmSetWindowAttribute(hwnd, darkModeAttribute, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, legacyDarkModeAttribute, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private void BuildProviderSelector()
    {
        var selectedIds = new HashSet<string>(_usageManager.SelectedProviderIds, StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _usageManager.AvailableProviders)
        {
            var statusLabel = new WpfTextBlock
            {
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AFC1D6")),
                Margin = new Thickness(30, 1, 0, 0),
                Text = "실행 감지 대기"
            };
            var content = new System.Windows.Controls.StackPanel();
            content.Children.Add(new WpfTextBlock { Text = $"{provider.Glyph}   {provider.DisplayName}", FontSize = 12 });
            content.Children.Add(statusLabel);
            var checkBox = new WpfCheckBox
            {
                Style = (Style)FindResource("DockCheckBox"),
                Content = content,
                IsChecked = selectedIds.Contains(provider.Id),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(10, 7, 10, 7),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B34485E"))
            };
            checkBox.Checked += ProviderSelectionChanged;
            checkBox.Unchecked += ProviderSelectionChanged;
            _providerBoxes.Add(checkBox, provider.Id);
            _providerStatusLabels[provider.Id] = statusLabel;
            ProviderSelectionPanel.Children.Add(checkBox);
        }

        UpdateProviderStatusLabels(_usageManager.CurrentRecords);
        UpdateProviderSelectionState();
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _usageManager.UsageUpdated += OnUsageUpdated;
        await _usageManager.RefreshAllAsync();
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        _usageManager.UsageUpdated -= OnUsageUpdated;
    }

    private void OnUsageUpdated(IReadOnlyList<Codenotch.Core.Models.UsageRecord> records)
    {
        Dispatcher.BeginInvoke(() => UpdateProviderStatusLabels(records));
    }

    private void UpdateProviderStatusLabels(IReadOnlyList<Codenotch.Core.Models.UsageRecord> records)
    {
        var byId = records.ToDictionary(record => record.ProviderId, StringComparer.OrdinalIgnoreCase);
        foreach (var (providerId, label) in _providerStatusLabels)
        {
            if (!byId.TryGetValue(providerId, out var record))
            {
                label.Text = "실행 감지 대기";
                label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AFC1D6"));
                continue;
            }

            if (record.IsConnected)
            {
                label.Text = $"감지됨 · {record.PrimaryRemainingPercentage:F0}% 남음 ({record.PrimaryUsedPercentage:F0}% 사용)";
                label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8DC5FF"));
            }
            else
            {
                label.Text = $"감지됨 · {record.PrimaryStatusText}";
                label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F9B875"));
            }
        }
    }

    private void ProviderSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateProviderSelectionState();
    }

    private void UpdateProviderSelectionState()
    {
        var selectedCount = _providerBoxes.Keys.Count(box => box.IsChecked == true);
        foreach (var box in _providerBoxes.Keys)
            box.IsEnabled = box.IsChecked == true || selectedCount < UsageManager.MaximumSelectedProviders;

        TxtProviderSelectionStatus.Text = selectedCount == 0
            ? "최소 1개를 선택하세요."
            : $"{selectedCount} / {UsageManager.MaximumSelectedProviders}개 선택됨";
        TxtProviderSelectionStatus.Foreground = selectedCount == 0
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.SkyBlue;
    }

    private async void BtnDone_Click(object sender, RoutedEventArgs e)
    {
        var selectedIds = _providerBoxes
            .Where(entry => entry.Key.IsChecked == true)
            .Select(entry => entry.Value)
            .ToList();

        if (selectedIds.Count == 0)
        {
            UpdateProviderSelectionState();
            return;
        }

        _usageManager.SetSelectedProviderIds(selectedIds);
        _usageManager.SetPreferences(ChkAlwaysShow.IsChecked == true, ChkAlerts.IsChecked == true);
        _notchWindow.SetPinned(ChkAlwaysShow.IsChecked == true);
        await _usageManager.RefreshAllAsync();
        Close();
    }
}
