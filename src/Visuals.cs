using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace VmaxThemeStudio
{
    public class BarVisual : FrameworkElement
    {
        public BarElement Model { get; private set; }
        public DataSimulator Sim { get; private set; }

        public BarVisual(BarElement model, DataSimulator sim)
        {
            Model = model;
            Sim = sim;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var rect = new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            dc.DrawRectangle(Brushes.Transparent, null, rect);
            double radius = Math.Max(0, Model.CornerRadius);
            dc.DrawGeometry(ColorUtil.Brush(Model.Track), null, RenderGeometry.RoundedRect(rect, radius));

            double frac = Model.GetFraction(Sim);
            if (frac > 0)
            {
                Rect fillRect = rect.Width >= rect.Height
                    ? new Rect(0, 0, rect.Width * frac, rect.Height)
                    : new Rect(0, rect.Height * (1.0 - frac), rect.Width, rect.Height * frac);
                dc.PushClip(RenderGeometry.RoundedRect(rect, radius));
                dc.DrawRectangle(ColorUtil.Brush(Model.Fill), null, fillRect);
                dc.Pop();
            }

            if (Model.BorderThickness > 0 && ColorUtil.Parse(Model.BorderColor).A > 0)
            {
                var pen = new Pen(ColorUtil.Brush(Model.BorderColor), Model.BorderThickness);
                dc.DrawGeometry(null, pen, RenderGeometry.RoundedRect(rect, radius));
            }
        }
    }

    public class RingVisual : FrameworkElement
    {
        public RingElement Model { get; private set; }
        public DataSimulator Sim { get; private set; }

        public RingVisual(RingElement model, DataSimulator sim)
        {
            Model = model;
            Sim = sim;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            dc.DrawRectangle(Brushes.Transparent, null, bounds);
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0)
                return;
            var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double thickness = Math.Max(1, Math.Min(Model.Thickness, size / 2.0));
            double radius = Math.Max(0.5, (size - thickness) / 2.0);

            var trackPen = new Pen(ColorUtil.Brush(Model.Track), thickness);
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            double frac = Model.GetFraction(Sim);
            if (frac <= 0)
                return;
            var fillPen = new Pen(ColorUtil.Brush(Model.Fill), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (frac >= 0.9999)
            {
                dc.DrawEllipse(null, fillPen, center, radius, radius);
                return;
            }
            ThemeRenderer.DrawArc(dc, center, radius, -90.0, 360.0 * frac, fillPen);
        }
    }

    public class VideoBackgroundElement : FrameworkElement
    {
        private readonly MediaPlayer _player = new MediaPlayer();
        private string _path;
        private bool _paused;

        public BackgroundFit Fit { get; set; }
        public Color FallbackColor { get; set; }

        public VideoBackgroundElement()
        {
            Fit = BackgroundFit.Cover;
            FallbackColor = Color.FromRgb(16, 17, 20);
            _player.IsMuted = true;
            _player.Volume = 0;
            _player.MediaOpened += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!_paused)
                        _player.Play();
                    InvalidateVisual();
                }));
            };
            _player.MediaEnded += delegate
            {
                if (_paused)
                    return;
                _player.Position = TimeSpan.Zero;
                _player.Play();
            };
            _player.MediaFailed += delegate
            {
                Dispatcher.BeginInvoke(new Action(InvalidateVisual));
            };
        }

        public void Pause()
        {
            _paused = true;
            try { _player.Pause(); } catch { }
            InvalidateVisual();
        }

        public void Resume()
        {
            _paused = false;
            try { _player.Play(); } catch { }
        }

        public void SetVideo(string path, BackgroundFit fit)
        {
            Fit = fit;
            if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
            {
                InvalidateVisual();
                return;
            }
            _path = path;
            try { _player.Close(); } catch { }
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    _player.Open(new Uri(Path.GetFullPath(path)));
                    if (!_paused)
                        _player.Play();
                }
                catch
                {
                }
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            dc.DrawRectangle(new SolidColorBrush(FallbackColor), null, bounds);
            if (_player.NaturalVideoWidth > 0 && _player.NaturalVideoHeight > 0)
            {
                dc.PushClip(new RectangleGeometry(bounds));
                var dest = RenderGeometry.ComputeDest(
                    new Size(_player.NaturalVideoWidth, _player.NaturalVideoHeight), bounds, Fit);
                dc.DrawVideo(_player, dest);
                dc.Pop();
            }
        }
    }
}
