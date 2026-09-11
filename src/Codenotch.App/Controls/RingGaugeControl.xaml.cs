using System;
using System.Globalization;
using System.Windows;
using Codenotch.Core.Models;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Pen = System.Windows.Media.Pen;
using PenLineCap = System.Windows.Media.PenLineCap;
using SweepDirection = System.Windows.Media.SweepDirection;
using StreamGeometry = System.Windows.Media.StreamGeometry;
using DrawingContext = System.Windows.Media.DrawingContext;
using Typeface = System.Windows.Media.Typeface;
using FontFamily = System.Windows.Media.FontFamily;
using FormattedText = System.Windows.Media.FormattedText;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace Codenotch.App.Controls;

public partial class RingGaugeControl : WpfUserControl
{
    // Remaining percentage (100% -> 0% decreasing)
    private double _remainingPercentage = 100.0;
    private string _glyph = "AI";
    private bool _isConnected = true;
    private SessionStatus _status = SessionStatus.Idle;
    private Brush _brandBrush = Brushes.DodgerBlue;

    public RingGaugeControl()
    {
        InitializeComponent();
    }

    public void UpdateData(UsageRecord record)
    {
        _glyph = record.Glyph;
        // 100%에서 0%로 줄어드는 잔여량 기준!
        _remainingPercentage = Math.Clamp(record.PrimaryRemainingPercentage, 0.0, 100.0);
        _isConnected = record.IsConnected;
        _status = record.Status;

        // 잔여량에 따른 직관적 색상: 여유 있으면 브랜드 색상 -> 20% 이하 주황 -> 5% 이하 빨강
        if (!_isConnected)
        {
            _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B4B52"));
        }
        else if (_remainingPercentage <= 5.0)
        {
            _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // 바닥남 (빨강)
        }
        else if (_remainingPercentage <= 20.0)
        {
            _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // 얼마 안남음 (주황)
        }
        else
        {
            try
            {
                _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(record.BrandColor));
            }
            catch
            {
                _brandBrush = Brushes.DodgerBlue;
            }
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 32.0);
        double h = ActualHeight > 0 ? ActualHeight : (Height > 0 ? Height : 32.0);
        double size = Math.Min(w, h);

        double strokeThickness = size >= 40 ? 3.2 : 2.5;
        double radius = (size - strokeThickness) / 2.0;
        Point center = new Point(w / 2.0, h / 2.0);

        // 1. Background Track Ring (Dark translucent gray track)
        var trackPen = new Pen(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), strokeThickness);
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        // 2. Remaining Progress Arc (100% -> 0% decreasing)
        if (_isConnected && _remainingPercentage > 0.5)
        {
            var progressPen = new Pen(_brandBrush, strokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            progressPen.Freeze();

            if (_remainingPercentage >= 99.5)
            {
                // Full Circle (100% remaining)
                dc.DrawEllipse(null, progressPen, center, radius, radius);
            }
            else
            {
                double angle = (_remainingPercentage / 100.0) * 360.0;
                double rad = (angle - 90.0) * Math.PI / 180.0;

                Point startPoint = new Point(center.X, center.Y - radius);
                Point endPoint = new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));

                var streamGeom = new StreamGeometry();
                using (var ctx = streamGeom.Open())
                {
                    ctx.BeginFigure(startPoint, false, false);
                    ctx.ArcTo(endPoint, new Size(radius, radius), 0, angle > 180.0, SweepDirection.Clockwise, true, false);
                }
                streamGeom.Freeze();

                dc.DrawGeometry(null, progressPen, streamGeom);
            }
        }

        // 3. Center Glyph Text (Pixel-perfect centered)
        double fontSize = size >= 40 ? 14.5 : 10.0;
        var typeface = new Typeface(new FontFamily("Segoe UI, -apple-system, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        Brush textBrush = _isConnected ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));

        var formattedText = new FormattedText(
            _glyph,
            CultureInfo.CurrentCulture,
            WpfFlowDirection.LeftToRight,
            typeface,
            fontSize,
            textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip
        );

        Point textPos = new Point(
            center.X - formattedText.Width / 2.0,
            center.Y - formattedText.Height / 2.0
        );

        dc.DrawText(formattedText, textPos);

        // 4. Status indicator dot for working sessions
        if (_status == SessionStatus.Working)
        {
            var activeDotBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            activeDotBrush.Freeze();
            dc.DrawEllipse(activeDotBrush, null, new Point(center.X, center.Y + radius - 1.5), 1.8, 1.8);
        }
    }
}
