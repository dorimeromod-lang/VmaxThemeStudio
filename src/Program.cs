using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

[assembly: System.Reflection.AssemblyTitle("VMAX Theme Studio")]
[assembly: System.Reflection.AssemblyProduct("VMAX Theme Studio")]
[assembly: System.Reflection.AssemblyCompany("")]
[assembly: System.Reflection.AssemblyVersion("0.7.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.7.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.7")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace VmaxThemeStudio
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (SelfTest.IsRequested(args))
                return SelfTest.Run(args);

            if (HasArg(args, "--smoke"))
                return RunSmokeTest();

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ShowError(e.ExceptionObject as Exception);
            };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                ShowError(e.Exception);
                e.Handled = true;
            };

            try
            {
                var window = new MainWindow();
                app.MainWindow = window;
                return app.Run(window);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return 1;
            }
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

        private static int RunSmokeTest()
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                var window = new MainWindow();
                app.MainWindow = window;
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke_content.log"),
                        window.Content != null ? "OK" : "NO_CONTENT");
                }
                catch
                {
                }
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    window.Close();
                };
                timer.Start();
                window.Show();
                return app.Run();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke_error.log"),
                        ex.ToString());
                }
                catch
                {
                }
                return 1;
            }
        }

        private static void ShowError(Exception ex)
        {
            string message = ex == null
                ? "Неизвестная ошибка."
                : ex.GetType().Name + ": " + ex.Message;
            try
            {
                MessageBox.Show(
                    message + (ex == null ? "" : "\n\n" + ex.StackTrace),
                    "VMAX Theme Studio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
            }
        }
    }
}
