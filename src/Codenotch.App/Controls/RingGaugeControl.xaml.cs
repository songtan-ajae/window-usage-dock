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
    // Usage percentage (0% -> 100% increasing), shared with the detail bar.
    private double _usedPercentage;
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
        // The compact dock must always mirror the first quota shown in the
        // expanded view (the short / 5-hour window).  Providers that only
        // expose a single aggregate quota still fall back to the legacy field.
        var primaryQuota = record.SubWindows.Count > 0 ? record.SubWindows[0] : null;
        _usedPercentage = Math.Clamp(primaryQuota?.UsedPercentage ?? record.PrimaryUsedPercentage, 0.0, 100.0);
        _isConnected = record.IsConnected;
        _status = record.Status;

        // Usage-based color: brand color while healthy, amber/red as the quota fills.
        if (!_isConnected)
        {
            _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B4B52"));
        }
        else if (_usedPercentage >= 95.0)
        {
            _brandBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // 바닥남 (빨강)
        }
        else if (_usedPercentage >= 80.0)
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

        // 2. Used Progress Arc (0% -> 100% increasing)
        if (_isConnected && _usedPercentage > 0.5)
        {
            var progressPen = new Pen(_brandBrush, strokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            progressPen.Freeze();

            if (_usedPercentage >= 99.5)
            {
                // Full Circle (100% used)
                dc.DrawEllipse(null, progressPen, center, radius, radius);
            }
            else
            {
                double angle = (_usedPercentage / 100.0) * 360.0;
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

        // Status is communicated by the ring color and tooltip; no extra dot.
    }
}
