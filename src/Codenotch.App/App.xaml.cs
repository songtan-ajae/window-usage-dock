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
    private Icon? _trayIcon;

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
                _trayIcon = new Icon(iconPath);
            }
            catch
            {
                _trayIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);
            }
        }
        else
        {
            _trayIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);
        }

        _notifyIcon.Icon = _trayIcon ?? SystemIcons.Application;

        var contextMenu = new Forms.ContextMenuStrip
        {
            BackColor = Color.FromArgb(27, 29, 36),
            ForeColor = Color.FromArgb(235, 238, 243),
            ShowImageMargin = false,
            Renderer = new Forms.ToolStripProfessionalRenderer(new DockMenuColors()),
            Font = new Font("Segoe UI", 10f),
            Padding = new Forms.Padding(4)
        };
        contextMenu.Items.Add("노치 보이기", null, (s, e) =>
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

        foreach (Forms.ToolStripItem item in contextMenu.Items)
        {
            item.BackColor = contextMenu.BackColor;
            item.ForeColor = contextMenu.ForeColor;
            if (item is Forms.ToolStripMenuItem)
                item.Padding = new Forms.Padding(10, 5, 10, 5);
        }

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
        _trayIcon?.Dispose();
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
        _trayIcon?.Dispose();
        _usageManager?.Dispose();
        base.OnExit(e);
    }

    private sealed class DockMenuColors : Forms.ProfessionalColorTable
    {
        private static readonly Color Surface = Color.FromArgb(27, 29, 36);
        private static readonly Color Hover = Color.FromArgb(48, 54, 66);
        private static readonly Color Outline = Color.FromArgb(63, 69, 81);

        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Outline;
        public override Color MenuItemBorder => Outline;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientMiddle => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Outline;
        public override Color SeparatorLight => Outline;
    }
}
