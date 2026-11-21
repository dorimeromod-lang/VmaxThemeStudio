using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VmaxThemeStudio
{
    public static class ColorUtil
    {
        public static Color Parse(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(value.Trim());
                return c;
            }
            catch
            {
                return fallback;
            }
        }

        public static Color Parse(string value)
        {
            return Parse(value, Colors.White);
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        public static double GetLuminance(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        public static Color GetContrastColor(Color background)
        {
            return GetContrastRatio(background, Colors.White) >= GetContrastRatio(background, Colors.Black)
                ? Colors.White
                : Colors.Black;
        }

        public static double GetRelativeLuminance(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        public static double GetContrastRatio(Color a, Color b)
        {
            double l1 = GetRelativeLuminance(a);
            double l2 = GetRelativeLuminance(b);
            if (l1 < l2)
            {
                double t = l1;
                l1 = l2;
                l2 = t;
            }
            return (l1 + 0.05) / (l2 + 0.05);
        }

        public static Color GetReadableColor(Color background, Color target)
        {
            const double minContrast = 4.5;
            if (GetContrastRatio(background, target) >= minContrast)
                return target;
            Color toward = GetContrastRatio(background, Colors.White) >= GetContrastRatio(background, Colors.Black)
                ? Colors.White
                : Colors.Black;
            for (int i = 1; i <= 24; i++)
            {
                double t = i / 24.0;
                var candidate = Color.FromArgb(
                    target.A,
                    (byte)Math.Round(target.R + (toward.R - target.R) * t),
                    (byte)Math.Round(target.G + (toward.G - target.G) * t),
                    (byte)Math.Round(target.B + (toward.B - target.B) * t));
                if (GetContrastRatio(background, candidate) >= minContrast)
                    return candidate;
            }
            return toward;
        }

        private static readonly Dictionary<string, SolidColorBrush> _textBrushCache =
            new Dictionary<string, SolidColorBrush>();
        private static readonly Dictionary<Color, SolidColorBrush> _colorBrushCache =
            new Dictionary<Color, SolidColorBrush>();

        public static SolidColorBrush Brush(string value)
        {
            string key = value ?? "";
            SolidColorBrush b;
            if (_textBrushCache.TryGetValue(key, out b))
                return b;
            b = new SolidColorBrush(Parse(key));
            b.Freeze();
            _textBrushCache[key] = b;
            return b;
        }

        public static SolidColorBrush Brush(Color c)
        {
            SolidColorBrush b;
            if (_colorBrushCache.TryGetValue(c, out b))
                return b;
            b = new SolidColorBrush(c);
            b.Freeze();
            _colorBrushCache[c] = b;
            return b;
        }
    }

    public class FontChoice
    {
        public string Display { get; set; }
        public string Family { get; set; }
        public string File { get; set; }

        public override string ToString()
        {
            return Display;
        }
    }

    public static class FontUtil
    {
        private static List<FontChoice> _systemFonts;

        public static List<FontChoice> GetSystemFonts()
        {
            if (_systemFonts != null)
                return _systemFonts;
            _systemFonts = new List<FontChoice>();
            try
            {
                foreach (var family in Fonts.SystemFontFamilies)
                {
                    string name = family.Source;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    _systemFonts.Add(new FontChoice { Display = name, Family = name, File = "" });
                }
            }
            catch
            {
            }
            _systemFonts.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase));
            return _systemFonts;
        }

        public static FontFamily MakeFontFamily(string family, string file)
        {
            if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
            {
                try
                {
                    string dir = Path.GetDirectoryName(file);
                    if (!dir.EndsWith("\\", StringComparison.Ordinal))
                        dir += "\\";
                    return new FontFamily(new Uri(dir), "./#" + family);
                }
                catch
                {
                }
            }
            if (string.IsNullOrWhiteSpace(family))
                family = "Segoe UI";
            return new FontFamily(family);
        }

        public static string ReadFamilyName(string file)
        {
            try
            {
                var uri = new Uri(Path.GetFullPath(file));
                var glyph = new GlyphTypeface(uri);
                foreach (var pair in glyph.FamilyNames)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Value))
                        return pair.Value;
                }
            }
            catch
            {
            }
            return null;
        }

        public static string ImportFont(string file, string storageDir)
        {
            if (!File.Exists(file))
                return null;
            if (!Directory.Exists(storageDir))
                Directory.CreateDirectory(storageDir);
            string name = Path.GetFileName(file);
            string dest = Path.Combine(storageDir, name);
            int i = 1;
            while (File.Exists(dest) && !string.Equals(Path.GetFullPath(dest), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
            {
                dest = Path.Combine(storageDir, Path.GetFileNameWithoutExtension(name) + "_" + i + Path.GetExtension(name));
                i++;
            }
            if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
                File.Copy(file, dest, true);
            return dest;
        }
    }

    public static class ImageUtil
    {
        public static BitmapSource Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(Path.GetFullPath(path));
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static Size GetPixelSize(string path)
        {
            var bmp = Load(path);
            if (bmp == null)
                return new Size(0, 0);
            return new Size(bmp.PixelWidth, bmp.PixelHeight);
        }

        public static Color GetAverageColor(BitmapSource bmp)
        {
            if (bmp == null)
                return Colors.Black;
            try
            {
                BitmapSource src = bmp.Format == PixelFormats.Bgra32
                    ? bmp
                    : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                int width = src.PixelWidth;
                int height = src.PixelHeight;
                if (width <= 0 || height <= 0)
                    return Colors.Black;
                int stepX = Math.Max(1, width / 64);
                int stepY = Math.Max(1, height / 64);
                int stride = width * 4;
                var pixels = new byte[stride * height];
                src.CopyPixels(pixels, stride, 0);
                double r = 0, g = 0, b = 0;
                int count = 0;
                for (int y = 0; y < height; y += stepY)
                {
                    for (int x = 0; x < width; x += stepX)
                    {
                        int i = y * stride + x * 4;
                        b += pixels[i];
                        g += pixels[i + 1];
                        r += pixels[i + 2];
                        count++;
                    }
                }
                if (count == 0)
                    return Colors.Black;
                return Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
            }
            catch
            {
                return Colors.Black;
            }
        }

        public static void ResizeToPng(string source, int width, int height, string target)
        {
            using (var img = System.Drawing.Image.FromFile(source))
            {
                using (var bmp = new System.Drawing.Bitmap(Math.Max(1, width), Math.Max(1, height), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.DrawImage(img, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height));
                    }
                    bmp.Save(target, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
    }

    public static class RenderGeometry
    {
        public static Rect ComputeDest(Size src, Rect bounds, BackgroundFit fit)
        {
            if (src.Width <= 0 || src.Height <= 0)
                return bounds;
            double sw = bounds.Width / src.Width;
            double sh = bounds.Height / src.Height;
            double scale;
            switch (fit)
            {
                case BackgroundFit.Stretch:
                    return bounds;
                case BackgroundFit.Contain:
                    scale = Math.Min(sw, sh);
                    break;
                default:
                    scale = Math.Max(sw, sh);
                    break;
            }
            double w = src.Width * scale;
            double h = src.Height * scale;
            return new Rect(bounds.X + (bounds.Width - w) / 2.0, bounds.Y + (bounds.Height - h) / 2.0, w, h);
        }

        public static Geometry RoundedRect(Rect r, double radius)
        {
            double max = Math.Min(r.Width, r.Height) / 2.0;
            if (radius < 0) radius = 0;
            if (radius > max) radius = max;
            if (radius <= 0.01)
                return new RectangleGeometry(r);
            return new RectangleGeometry(r, radius, radius);
        }
    }

    public static class FfmpegUtil
    {
        public static string FindFfmpeg()
        {
            var candidates = new List<string>();
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                candidates.Add(Path.Combine(local, "NACODEX", "bin", "Release", "ffmpeg.exe"));
            }
            catch
            {
            }
            try
            {
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"));
            }
            catch
            {
            }
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "ffmpeg.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });
                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (File.Exists(line.Trim()))
                            return line.Trim();
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static bool IsMp4(string path)
        {
            return string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TranscodeToMp4(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                return false;
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
                return false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = "-y -i \"" + source + "\" -c:v libx264 -preset veryfast -crf 20 " +
                                "-pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart \"" + target + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                        return false;
                    p.OutputDataReceived += delegate { };
                    p.ErrorDataReceived += delegate { };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(900000))
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    return p.ExitCode == 0
                        && File.Exists(target)
                        && new FileInfo(target).Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string ExtractPoster(string videoPath, string targetPng)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                return null;
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
                return null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = "-y -ss 0.4 -i \"" + videoPath + "\" -frames:v 1 -vf scale=1920:-2 \"" + targetPng + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                        return null;
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(20000))
                    {
                        try { p.Kill(); } catch { }
                        return null;
                    }
                    if (File.Exists(targetPng) && new FileInfo(targetPng).Length > 0)
                        return targetPng;
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
