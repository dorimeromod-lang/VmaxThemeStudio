using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VmaxThemeStudio
{
    public static class ThemeRenderer
    {
        public static BitmapSource Render(ThemeProject project, DataSimulator sim, string backgroundOverrideImage)
        {
            int width = Math.Max(1, (int)Math.Round(project.Width));
            int height = Math.Max(1, (int)Math.Round(project.Height));
            var bounds = new Rect(0, 0, width, height);
            var visual = new DrawingVisual();
            var cache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawBackground(dc, project, bounds, backgroundOverrideImage, cache);
                foreach (var el in Order(project.Elements))
                {
                    if (!el.Visible)
                        continue;
                    if (el is TextElement)
                        DrawText(dc, (TextElement)el, sim);
                    else if (el is BarElement)
                        DrawBar(dc, (BarElement)el, sim);
                    else if (el is RingElement)
                        DrawRing(dc, (RingElement)el, sim);
                    else if (el is ImageElement)
                        DrawImage(dc, (ImageElement)el, cache);
                }
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        public static BitmapSource RenderBackground(ThemeProject project)
        {
            int width = Math.Max(1, (int)Math.Round(project.Width));
            int height = Math.Max(1, (int)Math.Round(project.Height));
            var bounds = new Rect(0, 0, width, height);
            var visual = new DrawingVisual();
            var cache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawBackground(dc, project, bounds, null, cache);
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        public static void SavePng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(fs);
        }

        public static void RenderToPng(ThemeProject project, DataSimulator sim, string backgroundOverrideImage, string path)
        {
            SavePng(Render(project, sim, backgroundOverrideImage), path);
        }

        private static IEnumerable<ThemeElement> Order(IEnumerable<ThemeElement> elements)
        {
            var list = new List<ThemeElement>(elements);
            list.Sort((a, b) =>
            {
                int c = a.Z.CompareTo(b.Z);
                return c != 0 ? c : 0;
            });
            return list;
        }

        private static void DrawBackground(DrawingContext dc, ThemeProject project, Rect bounds, string overrideImage, Dictionary<string, BitmapSource> cache)
        {
            dc.DrawRectangle(ColorUtil.Brush(project.Background.Color), null, bounds);
            string path = overrideImage;
            if (string.IsNullOrEmpty(path) && project.Background.Kind == BackgroundKind.Image)
                path = project.Background.ImagePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            var bmp = LoadCached(path, cache);
            if (bmp == null)
                return;
            dc.PushClip(new RectangleGeometry(bounds));
            var dest = RenderGeometry.ComputeDest(new Size(bmp.PixelWidth, bmp.PixelHeight), bounds, project.Background.Fit);
            dc.DrawImage(bmp, dest);
            dc.Pop();
        }

        private static BitmapSource LoadCached(string path, Dictionary<string, BitmapSource> cache)
        {
            BitmapSource bmp;
            if (cache.TryGetValue(path, out bmp))
                return bmp;
            bmp = ImageUtil.Load(path);
            cache[path] = bmp;
            return bmp;
        }

        private static void DrawText(DrawingContext dc, TextElement el, DataSimulator sim)
        {
            string text = el.GetDisplayText(sim);
            if (string.IsNullOrEmpty(text))
                return;
            FontFamily family = FontUtil.MakeFontFamily(el.FontFamily, el.FontFile);
            var typeface = new Typeface(family,
                el.Italic ? FontStyles.Italic : FontStyles.Normal,
                el.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var brush = ColorUtil.Brush(el.Color);

            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                typeface, Math.Max(1, el.FontSize), brush, 1.0);
            ft.MaxTextWidth = Math.Max(1, el.Width);
            ft.TextAlignment = el.TextAlign == 1 ? TextAlignment.Center : (el.TextAlign == 2 ? TextAlignment.Right : TextAlignment.Left);
            ft.Trimming = TextTrimming.None;
            if (el.Underline)
                ft.SetTextDecorations(TextDecorations.Underline);
            dc.DrawText(ft, new Point(el.X, el.Y));
        }

        private static void DrawBar(DrawingContext dc, BarElement el, DataSimulator sim)
        {
            var rect = new Rect(el.X, el.Y, Math.Max(1, el.Width), Math.Max(1, el.Height));
            double radius = Math.Max(0, el.CornerRadius);
            var track = ColorUtil.Brush(el.Track);
            dc.DrawGeometry(track, null, RenderGeometry.RoundedRect(rect, radius));

            double frac = el.GetFraction(sim);
            if (frac > 0)
            {
                Rect fillRect = el.Width >= el.Height
                    ? new Rect(rect.X, rect.Y, rect.Width * frac, rect.Height)
                    : new Rect(rect.X, rect.Y + rect.Height * (1.0 - frac), rect.Width, rect.Height * frac);
                dc.PushClip(RenderGeometry.RoundedRect(rect, radius));
                dc.DrawRectangle(ColorUtil.Brush(el.Fill), null, fillRect);
                dc.Pop();
            }

            if (el.BorderThickness > 0 && ColorUtil.Parse(el.BorderColor).A > 0)
            {
                var pen = new Pen(ColorUtil.Brush(el.BorderColor), el.BorderThickness);
                dc.DrawGeometry(null, pen, RenderGeometry.RoundedRect(rect, radius));
            }
        }

        private static void DrawRing(DrawingContext dc, RingElement el, DataSimulator sim)
        {
            double size = Math.Min(el.Width, el.Height);
            if (size <= 0)
                return;
            var center = new Point(el.X + el.Width / 2.0, el.Y + el.Height / 2.0);
            double thickness = Math.Max(1, Math.Min(el.Thickness, size / 2.0));
            double radius = Math.Max(0.5, (size - thickness) / 2.0);

            var trackPen = new Pen(ColorUtil.Brush(el.Track), thickness);
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            double frac = el.GetFraction(sim);
            if (frac <= 0)
                return;
            var fillPen = new Pen(ColorUtil.Brush(el.Fill), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (frac >= 0.9999)
            {
                dc.DrawEllipse(null, fillPen, center, radius, radius);
                return;
            }
            DrawArc(dc, center, radius, -90.0, 360.0 * frac, fillPen);
        }

        public static void DrawArc(DrawingContext dc, Point center, double radius, double startAngle, double sweep, Pen pen)
        {
            if (sweep <= 0.01)
                return;
            double a1 = startAngle * Math.PI / 180.0;
            double a2 = (startAngle + sweep) * Math.PI / 180.0;
            var p1 = new Point(center.X + radius * Math.Cos(a1), center.Y + radius * Math.Sin(a1));
            var p2 = new Point(center.X + radius * Math.Cos(a2), center.Y + radius * Math.Sin(a2));
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                ctx.BeginFigure(p1, false, false);
                ctx.ArcTo(p2, new Size(radius, radius), 0, sweep > 180.0, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        private static void DrawImage(DrawingContext dc, ImageElement el, Dictionary<string, BitmapSource> cache)
        {
            if (string.IsNullOrWhiteSpace(el.FilePath) || !File.Exists(el.FilePath))
                return;
            var bmp = LoadCached(el.FilePath, cache);
            if (bmp == null)
                return;
            dc.DrawImage(bmp, new Rect(el.X, el.Y, Math.Max(1, el.Width), Math.Max(1, el.Height)));
        }
    }
}
