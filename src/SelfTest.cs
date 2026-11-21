using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace VmaxThemeStudio
{
    public static class SelfTest
    {
        private static int _failed;
        private static readonly List<string> _log = new List<string>();
        private static readonly string _liveLog =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest_live.log");

        public static bool IsRequested(string[] args)
        {
            if (args == null)
                return false;
            foreach (var a in args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--selftest-video", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static int Run(string[] args)
        {
            bool withVideo = HasArg(args, "--selftest-video");
            EnsureConsole();
            try { if (File.Exists(_liveLog)) File.Delete(_liveLog); } catch { }

            Line("VMAX Theme Studio — самотест");
            Line("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            string baseDir = Path.Combine(Path.GetTempPath(), "VmaxSelfTest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(baseDir);
            Line("Каталог теста: " + baseDir);
            Line("");

            TestSanitize();

            ThemeProject project = null;
            try
            {
                project = BuildProject(Path.Combine(baseDir, "test_image.png"));
                TestExport(project, baseDir);
                TestInstall(project, baseDir);
                TestRoundTrip(project, baseDir);
                TestUiSmoke();
                if (withVideo)
                    TestVideo(baseDir);
                else
                    Line("[ПРОПУСК] Видео-тест не запущен (добавьте --selftest-video).");
            }
            catch (Exception ex)
            {
                _failed++;
                Line("[FAIL] Исключение в самотесте: " + ex);
            }

            string nacodex = ThemeExporter.FindNacodexRoot();
            Line("");
            Line(nacodex == null
                ? "[INFO] NACODEX не найден в LocalAppData."
                : "[INFO] NACODEX найден: " + nacodex);

            Line("");
            Line(_failed == 0 ? "ИТОГ: все проверки пройдены." : "ИТОГ: провалено проверок: " + _failed);

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            try
            {
                File.WriteAllLines(logPath, _log, new UTF8Encoding(false));
                Line("Лог: " + logPath);
            }
            catch
            {
            }

            return _failed == 0 ? 0 : 1;
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null)
                return false;
            foreach (var a in args)
            {
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void TestSanitize()
        {
            Line("— Проверка имени темы —");
            string s = ThemeExporter.SanitizeName("A,B:C");
            Check("Запятые/двоеточия заменяются", s == "A B C", "получено: \"" + s + "\"");
            string e = ThemeExporter.SanitizeName("   ");
            Check("Пустое имя заменяется", !string.IsNullOrWhiteSpace(e), "получено: \"" + e + "\"");
            Line("");
        }

        private static ThemeProject BuildProject(string imagePath)
        {
            CreateTestImage(imagePath);
            string fontFile = FindTestFont();

            var p = new ThemeProject
            {
                ThemeName = "Selftest: тема, тест",
                Width = 1920,
                Height = 462
            };
            p.Background.Kind = BackgroundKind.None;
            p.Background.Color = "#FF101114";

            p.Elements.Add(new BarElement
            {
                Name = "Полоса CPU",
                X = 40,
                Y = 220,
                Width = 700,
                Height = 26,
                Source = ValueSource.CpuUsage,
                MaxValue = 100,
                Fill = "#FF00C8FF",
                Track = "#50000000",
                CornerRadius = 8,
                Animated = true,
                Z = 1
            });
            p.Elements.Add(new RingElement
            {
                Name = "Кольцо GPU",
                X = 820,
                Y = 200,
                Width = 160,
                Height = 160,
                Source = ValueSource.GpuUsage,
                Thickness = 14,
                MaxValue = 100,
                Fill = "#FF22E08C",
                Track = "#40FFFFFF",
                Z = 1
            });
            p.Elements.Add(new TextElement
            {
                Name = "Статичный текст",
                Source = ValueSource.Static,
                StaticText = "  ПРИВЕТ МИР  ",
                X = 40,
                Y = 30,
                Width = 600,
                Height = 90,
                FontFamily = "Segoe UI",
                FontSize = 56,
                Bold = true,
                Color = "#FFFFFFFF",
                TextAlign = 0,
                Z = 2
            });
            p.Elements.Add(new TextElement
            {
                Name = "Температура CPU",
                Source = ValueSource.CpuTemp,
                Prefix = "CPU",
                Suffix = "°C",
                X = 40,
                Y = 130,
                Width = 420,
                Height = 60,
                FontFamily = "Segoe UI",
                FontSize = 36,
                Color = "#FFFFC44D",
                AutoColorTarget = "#FFFFC44D",
                TextAlign = 0,
                Z = 3
            });
            p.Elements.Add(new TextElement
            {
                Name = "Время",
                Source = ValueSource.CurrentTime,
                X = 1400,
                Y = 40,
                Width = 400,
                Height = 80,
                FontFamily = "Segoe UI",
                FontSize = 52,
                Color = "#FFFFFFFF",
                TextAlign = 2,
                Z = 3
            });
            p.Elements.Add(new ImageElement
            {
                Name = "Картинка",
                FilePath = imagePath,
                X = 1100,
                Y = 200,
                Width = 120,
                Height = 120,
                Z = 2
            });
            p.Elements.Add(new TextElement
            {
                Name = "Свой шрифт",
                Source = ValueSource.Static,
                StaticText = "FONT",
                X = 1300,
                Y = 340,
                Width = 300,
                Height = 80,
                FontFamily = "Segoe UI",
                FontFile = fontFile,
                FontSize = 34,
                Color = "#FFAAAAAA",
                TextAlign = 1,
                Z = 4
            });
            return p;
        }

        private static string FindTestFont()
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var name in new[] { "segoeui.ttf", "arial.ttf", "tahoma.ttf" })
            {
                string path = Path.Combine(fonts, name);
                if (File.Exists(path))
                    return path;
            }
            return "";
        }

        private static void TestExport(ThemeProject project, string baseDir)
        {
            Line("— Проверка экспорта —");
            string exportRoot = Path.Combine(baseDir, "Export");
            ExportResult result = ThemeExporter.Export(project, exportRoot);
            Check("Экспорт без ошибок", result.Ok, result.Error);
            if (!result.Ok || string.IsNullOrEmpty(result.ThemeDir))
            {
                Line("");
                return;
            }

            string dir = result.ThemeDir;
            string expectedName = ThemeExporter.SanitizeName(project.ThemeName);
            Check("Папка темы с очищенным именем",
                string.Equals(Path.GetFileName(dir), expectedName, StringComparison.Ordinal),
                "ожидалось: " + expectedName + ", получено: " + Path.GetFileName(dir));

            string settingPath = Path.Combine(dir, "Setting.txt");
            Check("Setting.txt создан", File.Exists(settingPath));
            Check("back.png создан", File.Exists(Path.Combine(dir, "back.png")));
            Check("demo.png создан", File.Exists(Path.Combine(dir, "demo.png")));
            Check("img1.png создан", File.Exists(Path.Combine(dir, "img1.png")));

            string jsonPath = Path.Combine(dir, expectedName + ".json");
            Check("Схема темы создана", File.Exists(jsonPath));
            if (File.Exists(jsonPath))
            {
                byte[] jb = File.ReadAllBytes(jsonPath);
                Check("Схема темы с BOM", jb.Length >= 3 && jb[0] == 0xEF && jb[1] == 0xBB && jb[2] == 0xBF);
                string jt = Encoding.UTF8.GetString(jb);
                Check("Схема: Type 1", jt.Contains("\"Type\":1"));
                Check("Схема: ThumbnailImageData", jt.Contains("\"ThumbnailImageData\":\"\""));
                Check("Схема: имя", jt.Contains("\"Name\":\"" + expectedName + "\""));
                Check("Схема: размер", jt.Contains("\"Width\":1920.0") && jt.Contains("\"Height\":462.0"));
            }

            if (File.Exists(settingPath))
            {
                byte[] bytes = File.ReadAllBytes(settingPath);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                Check("UTF-8 без BOM", !bom);
                string text = Encoding.UTF8.GetString(bytes);
                Check("Переводы строк CRLF", !text.Replace("\r\n", "").Contains("\n"));
                Check("Поле name", text.Contains("name:" + expectedName + "\r\n"), FirstLines(text));
                Check("Ширина 1920", text.Contains("width:1920\r\n"));
                Check("Высота 462", text.Contains("height:462\r\n"));
                Check("Статичный текст с пробелами", text.Contains("IsDefaultText@true,data@  ПРИВЕТ МИР  "));
                Check("Цвет текста подстроен под фон", text.Contains(",Foreground@#FFFFFFFF"));
                Check("Целевой цвет текста сохранён", text.Contains(",Foreground@#FFFFC44D"));
                Check("Текст с датчиком", text.Contains("data@CPUTemp") && text.Contains("Title@CPU") && text.Contains("unit@°C"));
                Check("Время", text.Contains("data@CurrentTime"));
                Check("Полоса", text.Contains("BorderLine:x@40") && text.Contains("data@CpuUsage"));
                Check("Кольцо", text.Contains("RingProgressBar:x@820") && text.Contains("data@GpuUsage"));
                Check("Картинка", text.Contains("img1.png:x@1100"));

                string fontFile = FindTestFont();
                if (!string.IsNullOrEmpty(fontFile))
                {
                    string fontDir = Path.Combine(dir, "font");
                    int fontCount = Directory.Exists(fontDir) ? Directory.GetFiles(fontDir).Length : 0;
                    Check("Шрифт скопирован", fontCount > 0, "файлов в font: " + fontCount);
                    string family = FontUtil.ReadFamilyName(fontFile);
                    Check("Шрифт указан в Setting.txt",
                        !string.IsNullOrEmpty(family) && text.Contains("FontFamily@#" + family + ","),
                        "семейство: " + family);
                }

                CheckBitmap(settingPath.Replace("Setting.txt", "back.png"), 1920, 462, "back.png");
                CheckBitmap(settingPath.Replace("Setting.txt", "demo.png"), 1920, 462, "demo.png");
            }

            string vmaxPath = Path.Combine(exportRoot, expectedName + ".vmax");
            ExportResult vmaxResult = ThemeExporter.ExportVmax(project, vmaxPath);
            Check("Экспорт .vmax без ошибок", vmaxResult.Ok, vmaxResult.Error);
            Check("Файл .vmax создан", File.Exists(vmaxPath));
            if (File.Exists(vmaxPath))
            {
                byte[] zip = File.ReadAllBytes(vmaxPath);
                Check("PKG: ZIP-сигнатура", zip.Length > 4 && zip[0] == 0x50 && zip[1] == 0x4B);
            }

            foreach (var w in result.Warnings)
                Line("[WARN] " + w);
            Line("");
        }

        private static void TestInstall(ThemeProject project, string baseDir)
        {
            Line("— Проверка установки (в тестовый Programme) —");
            string fakeRoot = Path.Combine(baseDir, "FakeNacodex");
            Directory.CreateDirectory(Path.Combine(fakeRoot, "Programme"));

            ExportResult result = ThemeExporter.Install(project, fakeRoot);
            string target = Path.Combine(fakeRoot, "Programme", ThemeExporter.SanitizeName(project.ThemeName));
            Check("Установка выполнена", result.Ok, result.Error);
            Check("Папка темы в Programme", Directory.Exists(target));
            Check("Setting.txt в Programme", File.Exists(Path.Combine(target, "Setting.txt")));
            Check("demo.png в Programme", File.Exists(Path.Combine(target, "demo.png")));
            Check("Схема в Programme",
                File.Exists(Path.Combine(target, ThemeExporter.SanitizeName(project.ThemeName) + ".json")));
            Line("");
        }

        private static void TestVideo(string baseDir)
        {
            Line("— Проверка видео-фона —");
            string ffmpeg = FfmpegUtil.FindFfmpeg();
            if (ffmpeg == null)
            {
                Line("[ПРОПУСК] ffmpeg не найден.");
                Line("");
                return;
            }
            Line("[INFO] ffmpeg: " + ffmpeg);

            string video = Path.Combine(baseDir, "test_video.mp4");
            if (!RunFfmpeg(
                "-y -f lavfi -i color=c=0x101114:s=640x360:d=1.2 -pix_fmt yuv420p \"" + video + "\"",
                video,
                30000))
            {
                _failed++;
                Line("[FAIL] Не удалось создать тестовое видео.");
                return;
            }

            var project = BuildProject(Path.Combine(baseDir, "test_image.png"));
            project.ThemeName = "Selftest Video";
            project.Background.Kind = BackgroundKind.Video;
            project.Background.VideoPath = video;

            string exportRoot = Path.Combine(baseDir, "ExportVideo");
            var result = ThemeExporter.Export(project, exportRoot);
            Check("Экспорт видео-темы", result.Ok, result.Error);
            if (result.Ok && result.ThemeDir != null)
            {
                string mp4 = Path.Combine(result.ThemeDir, "default.mp4");
                Check("default.mp4 скопирован", File.Exists(mp4));
                Check("default.mp4 — MP4-контейнер", HasMp4Header(mp4));
                Check("back.png отсутствует", !File.Exists(Path.Combine(result.ThemeDir, "back.png")));
                Check("demo.png создан", File.Exists(Path.Combine(result.ThemeDir, "demo.png")));
            }
            foreach (var w in result.Warnings)
                Line("[WARN] " + w);

            string wmv = Path.Combine(baseDir, "test_video.wmv");
            if (RunFfmpeg(
                "-y -f lavfi -i color=c=0x223344:s=640x360:d=1.2 -c:v wmv2 -q:v 5 \"" + wmv + "\"",
                wmv,
                30000))
            {
                project.ThemeName = "Selftest Transcode";
                project.Background.VideoPath = wmv;
                var transcode = ThemeExporter.Export(project, Path.Combine(baseDir, "ExportTranscode"));
                Check("Экспорт WMV-темы", transcode.Ok, transcode.Error);
                if (transcode.Ok && transcode.ThemeDir != null)
                {
                    string mp4 = Path.Combine(transcode.ThemeDir, "default.mp4");
                    Check("WMV перекодирован в default.mp4",
                        File.Exists(mp4) && new FileInfo(mp4).Length > 0);
                    Check("Перекодированный файл — MP4", HasMp4Header(mp4));
                    Check("demo.png для WMV создан", File.Exists(Path.Combine(transcode.ThemeDir, "demo.png")));
                }
                foreach (var w in transcode.Warnings)
                    Line("[WARN] " + w);
            }
            else
            {
                _failed++;
                Line("[FAIL] Не удалось создать WMV для теста транскодинга.");
            }
            Line("");
        }

        private static bool RunFfmpeg(string arguments, string expectedFile, int timeoutMs)
        {
            string ffmpeg = FfmpegUtil.FindFfmpeg();
            if (ffmpeg == null)
                return false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                        return false;
                    string err = "";
                    try { err = proc.StandardError.ReadToEnd(); } catch { }
                    bool exited = proc.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { }
                    }
                    bool ok = exited && proc.ExitCode == 0
                        && File.Exists(expectedFile) && new FileInfo(expectedFile).Length > 0;
                    if (!ok && !string.IsNullOrEmpty(err))
                    {
                        string tail = err.Length > 600 ? err.Substring(err.Length - 600) : err;
                        Line("[INFO] ffmpeg: " + tail.Trim());
                    }
                    return ok;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool HasMp4Header(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                using (var fs = File.OpenRead(path))
                {
                    var buf = new byte[12];
                    if (fs.Read(buf, 0, 12) < 12)
                        return false;
                    return buf[4] == (byte)'f' && buf[5] == (byte)'t'
                        && buf[6] == (byte)'y' && buf[7] == (byte)'p';
                }
            }
            catch
            {
                return false;
            }
        }

        private static void TestRoundTrip(ThemeProject project, string baseDir)
        {
            Line("— Проверка сохранения проекта —");
            string path = Path.Combine(baseDir, "roundtrip.vmaxproj");
            try
            {
                var serializer = new XmlSerializer(typeof(ThemeProject));
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    serializer.Serialize(fs, project);

                ThemeProject loaded;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    loaded = (ThemeProject)serializer.Deserialize(fs);

                Check("Проект читается", loaded != null);
                Check("Элементы сохраняются",
                    loaded.Elements.Count == project.Elements.Count &&
                    loaded.Elements.OfType<TextElement>().Count() == project.Elements.OfType<TextElement>().Count() &&
                    loaded.Elements.OfType<BarElement>().Count() == project.Elements.OfType<BarElement>().Count() &&
                    loaded.Elements.OfType<RingElement>().Count() == project.Elements.OfType<RingElement>().Count() &&
                    loaded.Elements.OfType<ImageElement>().Count() == project.Elements.OfType<ImageElement>().Count());

                var originalText = project.Elements.OfType<TextElement>().FirstOrDefault(t => !string.IsNullOrEmpty(t.FontFile));
                var loadedText = loaded.Elements.OfType<TextElement>().FirstOrDefault(t => !string.IsNullOrEmpty(t.FontFile));
                Check("Шрифт в проекте сохраняется",
                    originalText == null || (loadedText != null && loadedText.FontFile == originalText.FontFile));
            }
            catch (Exception ex)
            {
                Check("Проект сохраняется", false, ex.Message);
            }
            Line("");
        }

        private static void TestUiSmoke()
        {
            Line("— Смоук-тест интерфейса —");
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke_error.log");
            string contentLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke_content.log");
            try { if (File.Exists(log)) File.Delete(log); } catch { }
            try { if (File.Exists(contentLog)) File.Delete(contentLog); } catch { }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--smoke",
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        Check("Запуск окна", false, "процесс не создан");
                    }
                    else
                    {
                        bool exited = p.WaitForExit(20000);
                        if (!exited)
                        {
                            try { p.Kill(); } catch { }
                        }
                        Check("Окно создаётся и закрывается", exited && p.ExitCode == 0,
                            exited ? "код: " + p.ExitCode : "таймаут");
                    }
                }
                if (File.Exists(log))
                    Check("Без ошибок UI", false, FirstLines(File.ReadAllText(log)));
                else
                    Check("Без ошибок UI", true);
                Check("Содержимое окна создано",
                    File.Exists(contentLog) && File.ReadAllText(contentLog).Trim() == "OK",
                    File.Exists(contentLog) ? File.ReadAllText(contentLog).Trim() : "файл не создан");
            }
            catch (Exception ex)
            {
                Check("Смоук-тест UI", false, ex.Message);
            }
            Line("");
        }

        private static void CreateTestImage(string path)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 200, 255)), null, new Rect(0, 0, 96, 96));
                var ft = new FormattedText("IMG", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 30, Brushes.White, 1.0);
                dc.DrawText(ft, new Point(8, 30));
            }
            var bmp = new RenderTargetBitmap(96, 96, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(fs);
        }

        private static void CheckBitmap(string path, int width, int height, string label)
        {
            try
            {
                var decoder = BitmapDecoder.Create(new Uri(Path.GetFullPath(path)),
                    BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                BitmapFrame frame = decoder.Frames[0];
                Check(label + " " + width + "x" + height,
                    frame.PixelWidth == width && frame.PixelHeight == height,
                    frame.PixelWidth + "x" + frame.PixelHeight);
            }
            catch (Exception ex)
            {
                Check(label + " читается", false, ex.Message);
            }
        }

        private static string FirstLines(string text)
        {
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            return lines.Length > 0 ? lines[0] : "";
        }

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok)
            {
                Line("  [OK]   " + name);
            }
            else
            {
                _failed++;
                Line("  [FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
            }
        }

        private static void Line(string text)
        {
            _log.Add(text);
            try { Console.WriteLine(text); } catch { }
            try
            {
                File.AppendAllText(_liveLog, text + "\r\n", new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        private static void EnsureConsole()
        {
            bool attached = false;
            try { attached = AttachConsole(0xFFFFFFFF); } catch { }
            if (!attached)
            {
                try { AllocConsole(); } catch { }
            }
            try
            {
                var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
                Console.SetOut(stdout);
            }
            catch
            {
            }
        }
    }
}
