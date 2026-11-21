using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace VmaxThemeStudio
{
    public class ExportResult
    {
        public bool Ok { get; set; }
        public string ThemeDir { get; set; }
        public string PackagePath { get; set; }
        public string Error { get; set; }
        public List<string> Warnings { get; set; }

        public ExportResult()
        {
            Warnings = new List<string>();
        }
    }

    public static class ThemeExporter
    {
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Моя тема";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
            {
                if (Array.IndexOf(invalid, c) >= 0 || c == ',' || c == '\r' || c == '\n' || c == '\t')
                    sb.Append(' ');
                else
                    sb.Append(c);
            }
            string result = sb.ToString().Trim();
            while (result.Contains("  "))
                result = result.Replace("  ", " ");
            return string.IsNullOrWhiteSpace(result) ? "Моя тема" : result;
        }

        public static string FindNacodexRoot()
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string root = Path.Combine(local, "NACODEX");
                if (Directory.Exists(Path.Combine(root, "Programme")))
                    return root;
            }
            catch
            {
            }
            return null;
        }

        public static ExportResult Export(ThemeProject project, string parentDir)
        {
            var result = new ExportResult();
            try
            {
                if (string.IsNullOrWhiteSpace(parentDir))
                    throw new ArgumentException("Не выбрана папка для экспорта.");
                if (!Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);

                string name = SanitizeName(project.ThemeName);
                string themeDir = Path.Combine(parentDir, name);
                if (Directory.Exists(themeDir))
                    Directory.Delete(themeDir, true);
                Directory.CreateDirectory(themeDir);
                string fontDir = Path.Combine(themeDir, "font");
                Directory.CreateDirectory(fontDir);

                var sim = new DataSimulator();
                var lines = new List<KeyValuePair<double, string>>();

                bool hasVideo = project.Background.Kind == BackgroundKind.Video
                    && !string.IsNullOrWhiteSpace(project.Background.VideoPath)
                    && File.Exists(project.Background.VideoPath);

                string poster = null;
                if (hasVideo)
                {
                    string videoCopy = Path.Combine(themeDir, "default.mp4");
                    try
                    {
                        if (FfmpegUtil.IsMp4(project.Background.VideoPath))
                        {
                            File.Copy(project.Background.VideoPath, videoCopy, true);
                        }
                        else if (!FfmpegUtil.TranscodeToMp4(project.Background.VideoPath, videoCopy))
                        {
                            result.Warnings.Add("Не удалось перекодировать видео в MP4 — тема будет без видео-фона.");
                            hasVideo = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("Не удалось подготовить видео-фон: " + ex.Message);
                        hasVideo = false;
                    }
                    if (hasVideo)
                    {
                        try
                        {
                            string posterPath = Path.Combine(Path.GetTempPath(), "vmax_poster_" + Guid.NewGuid().ToString("N") + ".png");
                            poster = FfmpegUtil.ExtractPoster(project.Background.VideoPath, posterPath);
                            if (poster == null)
                                result.Warnings.Add("Не удалось извлечь кадр видео для demo.png — превью будет без кадра.");
                        }
                        catch
                        {
                        }
                    }
                }

                if (!hasVideo)
                {
                    var bg = ThemeRenderer.RenderBackground(project);
                    ThemeRenderer.SavePng(bg, Path.Combine(themeDir, "back.png"));
                }

                var usedFonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int imageIndex = 0;
                Color autoBackground = GetAutomaticBackgroundColor(project);

                foreach (var el in project.Elements.OrderBy(e => e.Z).ThenBy(e => project.Elements.IndexOf(e)))
                {
                    if (!el.Visible)
                        continue;
                    if (el is ImageElement)
                    {
                        var img = (ImageElement)el;
                        if (string.IsNullOrWhiteSpace(img.FilePath) || !File.Exists(img.FilePath))
                        {
                            result.Warnings.Add("Картинка не найдена и пропущена: " + img.FilePath);
                            continue;
                        }
                        imageIndex++;
                        string fileName = "img" + imageIndex + ".png";
                        try
                        {
                            ImageUtil.ResizeToPng(img.FilePath, (int)Math.Round(img.Width), (int)Math.Round(img.Height),
                                Path.Combine(themeDir, fileName));
                            lines.Add(new KeyValuePair<double, string>(img.Z,
                                fileName + ":x@" + N(img.X) + ",y@" + N(img.Y) + ",z@" + N(img.Z)));
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add("Не удалось конвертировать картинку " + Path.GetFileName(img.FilePath) + ": " + ex.Message);
                        }
                        continue;
                    }

                    if (el is TextElement)
                    {
                        var t = (TextElement)el;
                        string family = CleanValue(string.IsNullOrWhiteSpace(t.FontFamily) ? "Segoe UI" : t.FontFamily);
                        if (!string.IsNullOrWhiteSpace(t.FontFile) && File.Exists(t.FontFile))
                        {
                            string exported;
                            if (!usedFonts.TryGetValue(t.FontFile, out exported))
                            {
                                string ext = Path.GetExtension(t.FontFile);
                                if (string.IsNullOrEmpty(ext))
                                    ext = ".ttf";
                                exported = "font" + (usedFonts.Count + 1) + ext;
                                try
                                {
                                    File.Copy(t.FontFile, Path.Combine(fontDir, exported), true);
                                    usedFonts[t.FontFile] = exported;
                                }
                                catch (Exception ex)
                                {
                                    result.Warnings.Add("Не удалось скопировать шрифт " + Path.GetFileName(t.FontFile) + ": " + ex.Message);
                                }
                            }
                            string realFamily = FontUtil.ReadFamilyName(t.FontFile);
                            if (!string.IsNullOrWhiteSpace(realFamily))
                                family = CleanValue(realFamily);
                        }

                        string color = t.AutoColor
                            ? ColorUtil.ToHex(ColorUtil.GetReadableColor(autoBackground, ColorUtil.Parse(t.AutoColorTarget, Colors.White)))
                            : NormalizeColor(t.Color);
                        t.Color = color;
                        var sb = new StringBuilder();
                        sb.Append("Text:x@").Append(N(t.X))
                          .Append(",y@").Append(N(t.Y))
                          .Append(",z@").Append(N(t.Z))
                          .Append(",height@").Append(N(t.Height))
                          .Append(",width@").Append(N(t.Width))
                          .Append(",TextAlignment@").Append(t.TextAlign)
                          .Append(",FontSize@").Append(N(t.FontSize))
                          .Append(",FontFamily@#").Append(family)
                           .Append(",Foreground@").Append(color);


                        if (t.Source == ValueSource.Static)
                        {
                            string text = CleanText(t.StaticText);
                            if (text.Length != (t.StaticText ?? "").Length)
                                result.Warnings.Add("В статичном тексте запятые заменены пробелами (не поддерживаются форматом).");
                            sb.Append(",IsDefaultText@true,data@").Append(text);
                        }
                        else
                        {
                            string data = DataCatalog.NacodexName(t.Source);
                            if (!string.IsNullOrEmpty(t.Prefix))
                            {
                                string prefix = CleanText(t.Prefix);
                                if (prefix.Length != t.Prefix.Length)
                                    result.Warnings.Add("В префиксе запятые заменены пробелами.");
                                sb.Append(",Title@").Append(prefix);
                            }
                            sb.Append(",data@").Append(data);
                            if (!string.IsNullOrEmpty(t.Suffix))
                            {
                                string suffix = CleanText(t.Suffix);
                                if (suffix.Length != t.Suffix.Length)
                                    result.Warnings.Add("В суффиксе запятые заменены пробелами.");
                                sb.Append(",unit@").Append(suffix);
                            }
                        }
                        lines.Add(new KeyValuePair<double, string>(t.Z, sb.ToString()));
                        continue;
                    }

                    if (el is BarElement)
                    {
                        var b = (BarElement)el;
                        string data = DataCatalog.NacodexName(b.Source);
                        if (string.IsNullOrEmpty(data))
                        {
                            result.Warnings.Add("Полоса без привязки к данным пропущена.");
                            continue;
                        }
                        string radius = N(Math.Max(0, b.CornerRadius));
                        string line =
                            "BorderLine:x@" + N(b.X) + ",y@" + N(b.Y) + ",z@" + N(b.Z) +
                            ",maxheight@" + N(b.Height) + ",maxwidth@" + N(b.Width) +
                            ",MaxNum@" + N(b.MaxValue > 0 ? b.MaxValue : 100) +
                            ",CornerRadius@" + radius + " " + radius + " " + radius + " " + radius +
                            ",Fill@" + NormalizeColor(b.Fill) +
                            ",IsAnimationEnabled@" + (b.Animated ? "True" : "False") +
                            ",BorderThicknes@" + N(b.BorderThickness) +
                            ",BorderFill@" + NormalizeColor(b.BorderColor) +
                            ",BackColor@" + NormalizeColor(b.Track) +
                            ",data@" + data;
                        lines.Add(new KeyValuePair<double, string>(b.Z, line));
                        continue;
                    }

                    if (el is RingElement)
                    {
                        var r = (RingElement)el;
                        string data = DataCatalog.NacodexName(r.Source);
                        if (string.IsNullOrEmpty(data))
                        {
                            result.Warnings.Add("Кольцо без привязки к данным пропущено.");
                            continue;
                        }
                        string line =
                            "RingProgressBar:x@" + N(r.X) + ",y@" + N(r.Y) + ",z@" + N(r.Z) +
                            ",RingWidth@" + N(r.Thickness) +
                            ",Perimeter@" + N(Math.Max(1, Math.Min(r.Width, r.Height))) +
                            ",MaxValue@" + N(r.MaxValue > 0 ? r.MaxValue : 100) +
                            ",IndexColor@" + NormalizeColor(r.Fill) +
                            ",BackColor@" + NormalizeColor(r.Track) +
                            ",data@" + data;
                        lines.Add(new KeyValuePair<double, string>(r.Z, line));
                    }
                }

                var sbSetting = new StringBuilder();
                sbSetting.Append("name:").Append(name).Append("\r\n");
                sbSetting.Append("width:").Append(((int)Math.Round(project.Width)).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                sbSetting.Append("height:").Append(((int)Math.Round(project.Height)).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                foreach (var kv in lines.OrderBy(k => k.Key))
                    sbSetting.Append(kv.Value).Append("\r\n");
                File.WriteAllText(Path.Combine(themeDir, "Setting.txt"), sbSetting.ToString(), new UTF8Encoding(false));
                WriteSchemeJson(themeDir, name, project.Width, project.Height, hasVideo);

                try
                {
                    ThemeRenderer.RenderToPng(project, sim, poster, Path.Combine(themeDir, "demo.png"));
                }
                catch (Exception ex)
                {
                    result.Warnings.Add("Не удалось создать demo.png: " + ex.Message);
                }

                if (poster != null)
                {
                    try { File.Delete(poster); } catch { }
                }

                result.Ok = true;
                result.ThemeDir = themeDir;
                return result;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.Message;
                return result;
            }
        }

        public static ExportResult ExportVmax(ThemeProject project, string filePath)
        {
            var result = new ExportResult();
            string work = null;
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("Не указан путь к файлу .vmax.");
                if (!string.Equals(Path.GetExtension(filePath), ".vmax", StringComparison.OrdinalIgnoreCase))
                    filePath += ".vmax";
                string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                work = Path.Combine(Path.GetTempPath(), "VmaxThemeStudio_" + Guid.NewGuid().ToString("N"));
                string themeParent = Path.Combine(work, "theme");
                Directory.CreateDirectory(themeParent);

                var exp = Export(project, themeParent);
                result.Warnings.AddRange(exp.Warnings);
                if (!exp.Ok)
                {
                    result.Error = exp.Error;
                    return result;
                }

                string zipTemp = Path.Combine(work, "package.vmax");
                CreateVmaxZip(exp.ThemeDir, zipTemp);
                File.Copy(zipTemp, filePath, true);

                result.Ok = true;
                result.ThemeDir = exp.ThemeDir;
                result.PackagePath = filePath;
                return result;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                if (work != null)
                {
                    try { Directory.Delete(work, true); } catch { }
                }
            }
        }

        private static void CreateVmaxZip(string themeDir, string zipPath)
        {
            string root = Path.GetFileName(themeDir);
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                foreach (string file in Directory.EnumerateFiles(themeDir, "*", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    string rel = file.Substring(themeDir.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    var entry = zip.CreateEntry(root + "/" + rel, CompressionLevel.Optimal);
                    using (var src = File.OpenRead(file))
                    using (var dst = entry.Open())
                        src.CopyTo(dst);
                }
            }
        }

        public static ExportResult Install(ThemeProject project, string nacodexRoot)
        {
            var result = new ExportResult();
            try
            {
                if (string.IsNullOrWhiteSpace(nacodexRoot))
                    throw new ArgumentException("Не найден NACODEX.");
                string programme = Path.Combine(nacodexRoot, "Programme");
                if (!Directory.Exists(programme))
                    throw new DirectoryNotFoundException("Не найдена папка Programme в NACODEX.");

                string tempRoot = Path.Combine(Path.GetTempPath(), "VmaxThemeStudio_" + Guid.NewGuid().ToString("N"));
                var exp = Export(project, tempRoot);
                if (!exp.Ok)
                    return exp;

                string name = Path.GetFileName(exp.ThemeDir);
                string target = Path.Combine(programme, name);
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
                CopyDirectory(exp.ThemeDir, target);
                try { Directory.Delete(tempRoot, true); } catch { }

                result.Ok = true;
                result.ThemeDir = target;
                result.Warnings.AddRange(exp.Warnings);
                return result;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.Message;
                return result;
            }
        }

        private static void WriteSchemeJson(string themeDir, string name, double width, double height, bool hasVideo)
        {
            var sb = new StringBuilder();
            sb.Append("{\"Type\":1,\"Name\":\"").Append(JsonEscape(name)).Append('"')
              .Append(",\"Width\":").Append(J(width))
              .Append(",\"Height\":").Append(J(height))
              .Append(",\"FillMode\":0,\"CropArea\":\"0, 0, 0, 0\",\"ThumbnailImageData\":\"\"")
              .Append(",\"DisplayDirection\":0,\"FontDirection\":0,\"BackgroundImage\":null,\"BackgroundVideoFile\":")
              .Append(hasVideo ? "\"default.mp4\"" : "null")
              .Append(",\"DisplayTexts\":[],\"DisplayImages\":[],\"TouchPoints\":[]")
              .Append(",\"ShutDownCancelPoint\":{\"TPType\":0,\"IconPath\":null,\"Content\":null,\"X1\":0,\"X2\":0,\"Y1\":0,\"Y2\":0}")
              .Append(",\"IsTouch\":0,\"IsApp\":0}");
            File.WriteAllText(Path.Combine(themeDir, name + ".json"), sb.ToString(), new UTF8Encoding(true));
        }

        private static string JsonEscape(string value)
        {
            var sb = new StringBuilder();
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string J(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(source.Length).TrimStart('\\', '/');
                Directory.CreateDirectory(Path.Combine(target, rel));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(source.Length).TrimStart('\\', '/');
                string dest = Path.Combine(target, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(file, dest, true);
            }
        }

        private static string N(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string CleanValue(string value)
        {
            if (value == null)
                return "";
            return value.Replace(',', ' ').Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string CleanText(string value)
        {
            if (value == null)
                return "";
            return value.Replace(',', ' ').Replace("\r", " ").Replace("\n", " ");
        }

        private static Color GetAutomaticBackgroundColor(ThemeProject project)
        {
            Color background = ColorUtil.Parse(project.Background.Color, Colors.Black);
            if (project.Background.Kind == BackgroundKind.Image
                && !string.IsNullOrWhiteSpace(project.Background.ImagePath)
                && File.Exists(project.Background.ImagePath))
            {
                var bmp = ImageUtil.Load(project.Background.ImagePath);
                if (bmp != null)
                    background = ImageUtil.GetAverageColor(bmp);
            }
            return background;
        }

        private static string NormalizeColor(string value)
        {
            return ColorUtil.ToHex(ColorUtil.Parse(value));
        }
    }
}
