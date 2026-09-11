using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Codenotch.App.Controls;
using Codenotch.Core.Models;
using Codenotch.Core.Services;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Cursors = System.Windows.Input.Cursors;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Codenotch.App.Views;

public partial class NotchWindow : Window
{
    private readonly UsageManager _usageManager;
    private bool _isPinned = false;
    private bool _isExpanded = false;
    private UsageRecord? _selectedRecord = null;
    private IReadOnlyList<UsageRecord> _latestRecords = Array.Empty<UsageRecord>();
    private readonly DispatcherTimer _collapseTimer;
    private bool _motionInProgress;

    private const double CompactWidth = 40.0;
    private const double CompactHeight = 136.0;
    private const double ExpandedWidth = 380.0;
    private const double ExpandedHeight = 180.0; // 5시간 + 주간 제한 모두 들어가도록 여유 있는 높이
    private const int DesiredFps = 60;

    // Vertical Drag State
    private bool _isDraggingVertical = false;
    private Point _dragStartScreenPoint;
    private double _dragStartTop;

    public NotchWindow(UsageManager usageManager)
    {
        InitializeComponent();
        _usageManager = usageManager;

        Width = CompactWidth;
        Height = CompactHeight;

        Loaded += NotchWindow_Loaded;
        MouseEnter += NotchWindow_MouseEnter;
        MouseLeave += NotchWindow_MouseLeave;

        MouseDown += NotchWindow_MouseDown;
        MouseMove += NotchWindow_MouseMove;
        MouseUp += NotchWindow_MouseUp;
        MouseDoubleClick += NotchWindow_MouseDoubleClick;

        _usageManager.UsageUpdated += OnUsageUpdated;

        // A small grace period prevents the drawer from snapping shut while the
        // pointer crosses the rounded edge or moves between interactive controls.
        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!_isPinned && !IsMouseOver && !_isDraggingVertical)
                AnimateExpansion(false);
        };
    }

    private void NotchWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAtRightEdge();
    }

    public void PositionAtRightEdge(double targetWidth = CompactWidth)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - targetWidth;
        if (double.IsNaN(Top) || Top <= 0)
        {
            Top = (workArea.Height - Height) / 2.0;
        }
    }

    private void NotchWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDraggingVertical = true;
            _dragStartScreenPoint = PointToScreen(e.GetPosition(this));
            _dragStartTop = Top;
            CaptureMouse();
        }
    }

    private void NotchWindow_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDraggingVertical && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentScreenPoint = PointToScreen(e.GetPosition(this));
            double deltaY = currentScreenPoint.Y - _dragStartScreenPoint.Y;

            var workArea = SystemParameters.WorkArea;
            double newTop = _dragStartTop + deltaY;
            newTop = Math.Clamp(newTop, workArea.Top, workArea.Bottom - Height);

            Top = newTop;
            Left = workArea.Right - Width;
        }
    }

    private void NotchWindow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingVertical)
        {
            _isDraggingVertical = false;
            ReleaseMouseCapture();

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width;
        }
    }

    private void NotchWindow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Top = (workArea.Height - Height) / 2.0;
        Left = workArea.Right - Width;
    }

    private void NotchWindow_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (!_isExpanded && !_isDraggingVertical) AnimateExpansion(true);
    }

    private void NotchWindow_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (!_isPinned && _isExpanded && !_isDraggingVertical)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private void AnimateExpansion(bool expand)
    {
        if (_motionInProgress && ((expand && _isExpanded) || (!expand && !_isExpanded))) return;
        if (expand == _isExpanded && !(_motionInProgress && expand)) return;

        _isExpanded = expand;
        _motionInProgress = true;

        var targetWidth = expand ? ExpandedWidth : CompactWidth;
        var targetHeight = expand ? ExpandedHeight : CompactHeight;

        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Right - targetWidth;

        // Brief, gesture-following motion: long hover animations feel disconnected.
        var duration = SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(expand ? 240 : 190)
            : TimeSpan.FromMilliseconds(1);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var animWidth = new DoubleAnimation { From = Width, To = targetWidth, Duration = duration, EasingFunction = easing };
        var animHeight = new DoubleAnimation { From = Height, To = targetHeight, Duration = duration, EasingFunction = easing };
        var animLeft = new DoubleAnimation { From = Left, To = targetLeft, Duration = duration, EasingFunction = easing };

        Timeline.SetDesiredFrameRate(animWidth, DesiredFps);
        Timeline.SetDesiredFrameRate(animHeight, DesiredFps);
        Timeline.SetDesiredFrameRate(animLeft, DesiredFps);

        if (expand)
        {
            ExpandedContent.Visibility = Visibility.Visible;
            CompactView.Visibility = Visibility.Collapsed;

            var animFadeIn = new DoubleAnimation(0.0, 1.0, duration)
            {
                EasingFunction = easing
            };
            Timeline.SetDesiredFrameRate(animFadeIn, DesiredFps);
            ExpandedContent.BeginAnimation(OpacityProperty, animFadeIn);
        }
        else
        {
            var animFadeOut = new DoubleAnimation(1.0, 0.0, duration)
            {
                EasingFunction = easing
            };
            Timeline.SetDesiredFrameRate(animFadeOut, DesiredFps);
            animFadeOut.Completed += (s, e) =>
            {
                if (!_isExpanded)
                {
                    ExpandedContent.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Visible;
                }
            };
            ExpandedContent.BeginAnimation(OpacityProperty, animFadeOut);
        }

        animWidth.Completed += (_, _) =>
        {
            _motionInProgress = false;
            if (!expand && !_isExpanded)
            {
                ExpandedContent.Visibility = Visibility.Collapsed;
                CompactView.Visibility = Visibility.Visible;
            }
        };

        BeginAnimation(WidthProperty, animWidth);
        BeginAnimation(HeightProperty, animHeight);
        BeginAnimation(LeftProperty, animLeft);
    }

    private void OnUsageUpdated(IReadOnlyList<UsageRecord> records)
    {
        Dispatcher.Invoke(() =>
        {
            _latestRecords = records;
            VerticalRingsPanel.Children.Clear();
            ExpandedRingsPanel.Children.Clear();

            foreach (var rec in records.Take(3))
            {
                var ringCompact = CreateRingControl(rec);
                VerticalRingsPanel.Children.Add(ringCompact);

                var ringExpanded = CreateRingControl(rec);
                ExpandedRingsPanel.Children.Add(ringExpanded);
            }

            if (_selectedRecord != null)
            {
                var updated = records.FirstOrDefault(r => r.ProviderId == _selectedRecord.ProviderId);
                if (updated != null) SelectRecord(updated);
            }
            else if (records.Count > 0)
            {
                SelectRecord(records[0]);
            }

            TxtLastUpdated.Text = $"{DateTime.Now:tt h:mm:ss} 갱신됨";
        });
    }

    private RingGaugeControl CreateRingControl(UsageRecord rec)
    {
        var ring = new RingGaugeControl
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 4, 0, 4),
            Cursor = Cursors.Hand,
            ToolTip = rec.IsConnected
                ? $"{rec.DisplayName} ({rec.PlanName})\n{rec.PrimaryRemainingPercentage:F0}% 남음 ({rec.PrimaryUsedPercentage:F0}% 사용됨)\n{rec.ResetTimeText}"
                : $"{rec.DisplayName} (미연결)\n활성 세션이 없습니다.\n{rec.ConnectionHint}"
        };
        ring.UpdateData(rec);

        var captured = rec;

        ring.MouseEnter += (s, e) =>
        {
            SelectRecord(captured);
        };

        ring.MouseLeftButtonUp += (s, e) =>
        {
            SelectRecord(captured);
            e.Handled = true;
        };

        return ring;
    }

    private void SelectRecord(UsageRecord record)
    {
        _selectedRecord = record;
        TxtDetailName.Text = record.DisplayName;

        if (!record.IsConnected)
        {
            TxtBadgePlan.Text = "미연결";
            BadgePlan.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
            TxtPrimaryLabel.Text = "연결 상태";
            TxtPrimaryReset.Text = "로그인 대기";
            PrimaryBarFill.Width = 0;
            TxtPrimaryUsage.Text = "활성 세션 없음 · 터미널에서 `claude` 로그인 필요";
            SecondarySection.Visibility = Visibility.Collapsed;
            return;
        }

        TxtBadgePlan.Text = record.PlanName;

        SolidColorBrush brandBrush;
        try
        {
            brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(record.BrandColor));
        }
        catch
        {
            brandBrush = Brushes.DodgerBlue;
        }

        BadgePlan.Background = brandBrush;
        PrimaryBarFill.Background = brandBrush;
        SecondaryBarFill.Background = brandBrush;

        double maxBarWidth = 240.0;

        // 1. 5시간 사용 제한 (Primary Window)
        double primaryRemaining = Math.Clamp(record.PrimaryRemainingPercentage, 0, 100);
        PrimaryBarFill.Width = (primaryRemaining / 100.0) * maxBarWidth;
        TxtPrimaryUsage.Text = $"{primaryRemaining:F0}% 남음 · {record.PrimaryUsedPercentage:F0}% 사용됨";
        TxtPrimaryReset.Text = record.ResetTimeText;
        TxtPrimaryLabel.Text = record.SubWindows.Count > 0 ? record.SubWindows[0].Label : "5시간 사용 제한";

        // 2. 주간 사용 제한 (Secondary Window - 함께 전부 다 표시!)
        if (record.SubWindows.Count > 1)
        {
            SecondarySection.Visibility = Visibility.Visible;
            var sec = record.SubWindows[1];
            double secRemaining = Math.Clamp(sec.RemainingPercentage, 0, 100);
            SecondaryBarFill.Width = (secRemaining / 100.0) * maxBarWidth;
            TxtSecondaryLabel.Text = sec.Label;
            TxtSecondaryUsage.Text = $"{secRemaining:F0}% 남음 · {sec.UsedPercentage:F0}% 사용됨";
            TxtSecondaryReset.Text = sec.ResetsInText;
        }
        else
        {
            SecondarySection.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        BtnPin.Foreground = _isPinned ? Brushes.SkyBlue : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        BtnPin.Content = _isPinned ? "📌 고정됨" : "📌 고정";
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        TxtLastUpdated.Text = "새로고침 중...";
        await _usageManager.RefreshAllAsync();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWin = new SettingsWindow(_usageManager, this);
        settingsWin.Owner = this;
        settingsWin.ShowDialog();
    }
}
