using System;
using System.Drawing;
using System.IO;
using System.Windows;
using Codenotch.App.Views;
using Codenotch.Core.Services;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace Codenotch.App;

public partial class App : WpfApplication
{
    private UsageManager? _usageManager;
    private NotchWindow? _notchWindow;
    private Forms.NotifyIcon? _notifyIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _usageManager = new UsageManager();
        _notchWindow = new NotchWindow(_usageManager);

        SetupTrayIcon();

        _usageManager.ThresholdAlertTriggered += (provider, msg) =>
        {
            _notifyIcon?.ShowBalloonTip(3000, $"{provider} 쿼터 알림", msg, Forms.ToolTipIcon.Warning);
        };

        _notchWindow.Show();
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Windows Usage Dock - AI 쿼터 모니터",
            Visible = true
        };

        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        else
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        var contextMenu = new Forms.ContextMenuStrip();
        var itemShow = contextMenu.Items.Add("노치 보이기", null, (s, e) =>
        {
            _notchWindow?.Show();
            _notchWindow?.Activate();
        });

        contextMenu.Items.Add("전체 새로고침", null, async (s, e) =>
        {
            if (_usageManager != null) await _usageManager.RefreshAllAsync();
        });

        contextMenu.Items.Add("환경설정...", null, (s, e) =>
        {
            if (_usageManager != null && _notchWindow != null)
            {
                var settings = new SettingsWindow(_usageManager, _notchWindow);
                settings.Show();
            }
        });

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        contextMenu.Items.Add("Windows Usage Dock 종료", null, (s, e) =>
        {
            ExitApplication();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) =>
        {
            if (_notchWindow != null)
            {
                if (_notchWindow.IsVisible)
                    _notchWindow.Hide();
                else
                {
                    _notchWindow.Show();
                    _notchWindow.Activate();
                }
            }
        };
    }

    private void ExitApplication()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _usageManager?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _usageManager?.Dispose();
        base.OnExit(e);
    }
}
