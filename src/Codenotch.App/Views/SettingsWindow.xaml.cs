using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
        _usageManager = usageManager;
        _notchWindow = notchWindow;

        BuildProviderSelector();
        Loaded += SettingsWindow_Loaded;
        Closed += SettingsWindow_Closed;
    }

    private void BtnResetPos_Click(object sender, RoutedEventArgs e)
    {
        _notchWindow.PositionAtRightEdge();
        _notchWindow.Show();
    }

    private void BuildProviderSelector()
    {
        var selectedIds = new HashSet<string>(_usageManager.SelectedProviderIds, StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _usageManager.AvailableProviders)
        {
            var statusLabel = new WpfTextBlock
            {
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(30, 1, 0, 0),
                Text = "실행 감지 대기"
            };
            var content = new System.Windows.Controls.StackPanel();
            content.Children.Add(new WpfTextBlock { Text = $"{provider.Glyph}   {provider.DisplayName}", FontSize = 12 });
            content.Children.Add(statusLabel);
            var checkBox = new WpfCheckBox
            {
                Content = content,
                IsChecked = selectedIds.Contains(provider.Id),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(8, 6, 8, 6),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#16161B"))
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
                label.Foreground = System.Windows.Media.Brushes.Gray;
                continue;
            }

            if (record.IsConnected)
            {
                label.Text = $"감지됨 · {record.PrimaryRemainingPercentage:F0}% 남음 ({record.PrimaryUsedPercentage:F0}% 사용)";
                label.Foreground = System.Windows.Media.Brushes.SkyBlue;
            }
            else
            {
                label.Text = $"감지됨 · {record.PrimaryStatusText}";
                label.Foreground = System.Windows.Media.Brushes.Orange;
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
        await _usageManager.RefreshAllAsync();
        Close();
    }
}
