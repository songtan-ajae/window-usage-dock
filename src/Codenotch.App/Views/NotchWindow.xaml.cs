using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Codenotch.App.Controls;
using Codenotch.Core.Models;
using Codenotch.Core.Services;

using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ScaleTransform = System.Windows.Media.ScaleTransform;
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
    private readonly DispatcherTimer _expandTimer;
    private bool _motionInProgress;
    private int _animationId;
    private readonly Dictionary<string, RingGaugeControl> _compactRings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RingGaugeControl> _expandedRings = new(StringComparer.OrdinalIgnoreCase);

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
        PrimaryBarContainer.SizeChanged += (_, _) => UpdateUsageBars(_selectedRecord);
        SecondaryBarContainer.SizeChanged += (_, _) => UpdateUsageBars(_selectedRecord);

        // A small grace period prevents the drawer from snapping shut while the
        // pointer crosses the rounded edge or moves between interactive controls.
        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!_isPinned && !IsMouseOver && !_isDraggingVertical)
                AnimateExpansion(false);
        };

        // Require a short, stable hover before expanding. This prevents the
        // window-resize hit-test boundary from retriggering the transition.
        _expandTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _expandTimer.Tick += (_, _) =>
        {
            _expandTimer.Stop();
            if (IsMouseOver && !_isExpanded && !_isDraggingVertical)
                AnimateExpansion(true);
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
        // Interactive controls own their click. Starting a drag from a button
        // makes the transparent HWND move and can interrupt the hover motion.
        if (e.LeftButton == MouseButtonState.Pressed &&
            FindVisualParent<WpfButton>(e.OriginalSource as DependencyObject) == null)
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
        if (!_isExpanded && !_isDraggingVertical)
        {
            _expandTimer.Stop();
            _expandTimer.Start();
        }
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

        var animationId = ++_animationId;
        _isExpanded = expand;
        _motionInProgress = true;

        var targetWidth = expand ? ExpandedWidth : CompactWidth;
        var visibleAgentCount = Math.Min(_latestRecords.Count, UsageManager.MaximumSelectedProviders);
        var targetHeight = expand
            ? Math.Max(ExpandedHeight, visibleAgentCount * 40.0 + 16.0)
            : Math.Max(CompactHeight, visibleAgentCount * 40.0 + 12.0);

        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Right - targetWidth;

        // Brief, gesture-following motion: long hover animations feel disconnected.
        var duration = SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(expand ? 240 : 190)
            : TimeSpan.FromMilliseconds(1);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (expand)
        {
            // Resize the transparent HWND once. Animating HWND geometry every
            // frame causes WPF/DWM to recalculate hit testing and composition.
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            Width = targetWidth;
            Height = targetHeight;
            Left = targetLeft;

            CompactView.Visibility = Visibility.Collapsed;
            ExpandedContent.Visibility = Visibility.Visible;
            ExpandedContent.Opacity = 0;
            ExpandedScale.ScaleX = 0.96;
            ExpandedScale.ScaleY = 0.96;

            var animFadeIn = new DoubleAnimation(0.0, 1.0, duration)
            {
                EasingFunction = easing
            };
            var animScaleX = new DoubleAnimation(0.96, 1.0, duration) { EasingFunction = easing };
            var animScaleY = new DoubleAnimation(0.96, 1.0, duration) { EasingFunction = easing };
            animFadeIn.Completed += (_, _) =>
            {
                if (animationId == _animationId) _motionInProgress = false;
            };
            Timeline.SetDesiredFrameRate(animFadeIn, DesiredFps);
            Timeline.SetDesiredFrameRate(animScaleX, DesiredFps);
            Timeline.SetDesiredFrameRate(animScaleY, DesiredFps);
            ExpandedContent.BeginAnimation(OpacityProperty, animFadeIn);
            ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
        }
        else
        {
            var animFadeOut = new DoubleAnimation(1.0, 0.0, duration)
            {
                EasingFunction = easing
            };
            var animScaleX = new DoubleAnimation(1.0, 0.96, duration) { EasingFunction = easing };
            var animScaleY = new DoubleAnimation(1.0, 0.96, duration) { EasingFunction = easing };
            Timeline.SetDesiredFrameRate(animFadeOut, DesiredFps);
            Timeline.SetDesiredFrameRate(animScaleX, DesiredFps);
            Timeline.SetDesiredFrameRate(animScaleY, DesiredFps);
            animFadeOut.Completed += (s, e) =>
            {
                if (!_isExpanded && animationId == _animationId)
                {
                    ExpandedContent.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Visible;
                    BeginAnimation(WidthProperty, null);
                    BeginAnimation(HeightProperty, null);
                    BeginAnimation(LeftProperty, null);
                    Width = CompactWidth;
                    Height = CompactHeight;
                    Left = workArea.Right - CompactWidth;
                    ExpandedContent.Opacity = 0;
                    ExpandedScale.ScaleX = 0.96;
                    ExpandedScale.ScaleY = 0.96;
                }
                if (animationId == _animationId) _motionInProgress = false;
            };
            ExpandedContent.BeginAnimation(OpacityProperty, animFadeOut);
            ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
        }

    }

    private void OnUsageUpdated(IReadOnlyList<UsageRecord> records)
    {
        var snapshot = records.Take(UsageManager.MaximumSelectedProviders).ToList();
        Dispatcher.BeginInvoke(() =>
        {
            _latestRecords = snapshot;
            SyncRingPanel(VerticalRingsPanel, _compactRings, snapshot);
            SyncRingPanel(ExpandedRingsPanel, _expandedRings, snapshot);

            if (!_isExpanded)
            {
                Height = Math.Max(CompactHeight, snapshot.Count * 40.0 + 12.0);
                PositionAtRightEdge();
            }

            if (snapshot.Count == 0)
            {
                _selectedRecord = null;
                ShowNoActiveAgentsState();
            }
            else if (_selectedRecord != null)
            {
                var updated = snapshot.FirstOrDefault(r => r.ProviderId == _selectedRecord.ProviderId);
                if (updated != null)
                {
                    SelectRecord(updated);
                }
                else
                {
                    _selectedRecord = null;
                    SelectRecord(snapshot[0]);
                }
            }
            else
            {
                SelectRecord(snapshot[0]);
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
            Tag = rec.ProviderId
        };
        UpdateRingControl(ring, rec);
        var providerId = rec.ProviderId;

        ring.MouseEnter += (s, e) =>
        {
            SelectLatestRecord(providerId);
        };

        ring.MouseLeftButtonUp += (s, e) =>
        {
            SelectLatestRecord(providerId);
            e.Handled = true;
        };

        return ring;
    }

    private void SyncRingPanel(
        WpfPanel panel,
        Dictionary<string, RingGaugeControl> cache,
        IReadOnlyList<UsageRecord> records)
    {
        var ids = records.Select(record => record.ProviderId).ToArray();
        var structureChanged = panel.Children.Count != ids.Length ||
            panel.Children.Cast<RingGaugeControl>().Select(ring => ring.Tag as string)
                .SequenceEqual(ids, StringComparer.OrdinalIgnoreCase) == false;

        if (structureChanged)
        {
            panel.Children.Clear();
            foreach (var record in records)
            {
                if (!cache.TryGetValue(record.ProviderId, out var ring))
                {
                    ring = CreateRingControl(record);
                    cache[record.ProviderId] = ring;
                }

                panel.Children.Add(ring);
            }

            var activeIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var staleId in cache.Keys.Where(id => !activeIds.Contains(id)).ToList())
                cache.Remove(staleId);
        }

        foreach (var record in records)
        {
            if (cache.TryGetValue(record.ProviderId, out var ring))
                UpdateRingControl(ring, record);
        }
    }

    private static void UpdateRingControl(RingGaugeControl ring, UsageRecord record)
    {
        var primaryQuota = record.SubWindows.Count > 0 ? record.SubWindows[0] : null;
        var primaryUsed = primaryQuota?.UsedPercentage ?? record.PrimaryUsedPercentage;
        var primaryRemaining = Math.Max(0, 100.0 - primaryUsed);
        var primaryReset = primaryQuota?.ResetsInText ?? record.ResetTimeText;
        ring.ToolTip = record.IsConnected
            ? $"{record.DisplayName} ({record.PlanName})\n{primaryRemaining:F0}% 남음 ({primaryUsed:F0}% 사용됨)\n{primaryReset}"
            : $"{record.DisplayName} (미연결)\n활성 세션이 없습니다.\n{record.ConnectionHint}";
        ring.UpdateData(record);
    }

    private void SelectLatestRecord(string providerId)
    {
        var record = _latestRecords.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (record != null)
            SelectRecord(record);
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match)
                return match;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void SelectRecord(UsageRecord record)
    {
        _selectedRecord = record;
        BadgePlan.Visibility = Visibility.Visible;
        TxtDetailName.Text = record.DisplayName;

        if (!record.IsConnected)
        {
            TxtBadgePlan.Text = "미연결";
            BadgePlan.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
            TxtPrimaryLabel.Text = "연결 상태";
            TxtPrimaryReset.Text = "로그인 대기";
            PrimaryBarFill.Width = 0;
            SecondaryBarFill.Width = 0;
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

        // Keep the compact dock, primary label, text and bar on exactly the
        // same quota object. This avoids a stale PrimaryUsedPercentage being
        // paired with a freshly refreshed SubWindows label.
        var primary = record.SubWindows.Count > 0 ? record.SubWindows[0] : null;
        var primaryUsed = Math.Clamp(primary?.UsedPercentage ?? record.PrimaryUsedPercentage, 0, 100);
        var primaryRemaining = Math.Max(0, 100.0 - primaryUsed);
        TxtPrimaryUsage.Text = $"{primaryRemaining:F0}% 남음 · {primaryUsed:F0}% 사용됨";
        TxtPrimaryReset.Text = primary?.ResetsInText ?? record.ResetTimeText;
        TxtPrimaryLabel.Text = primary?.Label ?? "5시간 사용 제한";

        // 2. 주간 사용 제한 (Secondary Window - 함께 전부 다 표시!)
        var secondary = record.SubWindows.Count > 1 ? record.SubWindows[1] : null;
        if (secondary != null)
        {
            SecondarySection.Visibility = Visibility.Visible;
            double secRemaining = Math.Clamp(secondary.RemainingPercentage, 0, 100);
            TxtSecondaryLabel.Text = secondary.Label;
            TxtSecondaryUsage.Text = $"{secRemaining:F0}% 남음 · {secondary.UsedPercentage:F0}% 사용됨";
            TxtSecondaryReset.Text = secondary.ResetsInText;
        }
        else
        {
            SecondarySection.Visibility = Visibility.Collapsed;
        }

        UpdateUsageBars(record);
    }

    private void ShowNoActiveAgentsState()
    {
        TxtDetailName.Text = "실행 중인 에이전트 없음";
        BadgePlan.Visibility = Visibility.Collapsed;
        TxtPrimaryLabel.Text = "대기 중";
        TxtPrimaryReset.Text = string.Empty;
        TxtPrimaryUsage.Text = "Codex, Antigravity 또는 Claude Code를 실행하면 표시됩니다.";
        PrimaryBarFill.Width = 0;
        SecondaryBarFill.Width = 0;
        SecondarySection.Visibility = Visibility.Collapsed;
    }

    private void UpdateUsageBars(UsageRecord? record)
    {
        if (record == null || !record.IsConnected)
        {
            PrimaryBarFill.Width = 0;
            SecondaryBarFill.Width = 0;
            return;
        }

        var primary = record.SubWindows.Count > 0 ? record.SubWindows[0] : null;
        var primaryUsed = primary?.UsedPercentage ?? record.PrimaryUsedPercentage;
        var primaryRemaining = Math.Max(0, 100.0 - primaryUsed);
        var primaryWidth = PrimaryBarContainer.ActualWidth;
        if (primaryWidth > 0)
        {
            PrimaryBarFill.Width = primaryWidth * primaryRemaining / 100.0;
        }

        var secondary = record.SubWindows.Count > 1 ? record.SubWindows[1] : null;
        if (secondary != null && SecondaryBarContainer.ActualWidth > 0)
        {
            SecondaryBarFill.Width = SecondaryBarContainer.ActualWidth *
                Math.Clamp(secondary.RemainingPercentage, 0, 100) / 100.0;
        }
        else if (secondary == null)
        {
            SecondaryBarFill.Width = 0;
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
