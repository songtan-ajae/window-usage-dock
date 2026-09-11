using System.Windows;
using Codenotch.Core.Services;

namespace Codenotch.App.Views;

public partial class SettingsWindow : Window
{
    private readonly UsageManager _usageManager;
    private readonly NotchWindow _notchWindow;

    public SettingsWindow(UsageManager usageManager, NotchWindow notchWindow)
    {
        InitializeComponent();
        _usageManager = usageManager;
        _notchWindow = notchWindow;

        ListProviders.ItemsSource = _usageManager.CurrentRecords;
    }

    private void BtnResetPos_Click(object sender, RoutedEventArgs e)
    {
        _notchWindow.PositionAtRightEdge();
        _notchWindow.Show();
    }

    private void BtnDone_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
