using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Codenotch.Core.Services;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace Codenotch.App.Views;

public partial class SettingsWindow : Window
{
    private readonly UsageManager _usageManager;
    private readonly NotchWindow _notchWindow;
    private readonly Dictionary<WpfCheckBox, string> _providerBoxes = new();

    public SettingsWindow(UsageManager usageManager, NotchWindow notchWindow)
    {
        InitializeComponent();
        _usageManager = usageManager;
        _notchWindow = notchWindow;

        BuildProviderSelector();
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
            var checkBox = new WpfCheckBox
            {
                Content = $"{provider.Glyph}   {provider.DisplayName}",
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
            ProviderSelectionPanel.Children.Add(checkBox);
        }

        UpdateProviderSelectionState();
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
