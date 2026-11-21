using System;
using System.Collections.Generic;
using System.Globalization;

namespace VmaxThemeStudio
{
    public static class DataCatalog
    {
        public static readonly ValueSource[] AllSources = new[]
        {
            ValueSource.Static,
            ValueSource.CurrentTime,
            ValueSource.CurrentDates,
            ValueSource.CpuUsage,
            ValueSource.GpuUsage,
            ValueSource.CpuTemp,
            ValueSource.GpuTemp,
            ValueSource.CpuFrequency,
            ValueSource.GpuFrequency,
            ValueSource.CpuPower,
            ValueSource.GpuPower,
            ValueSource.CpuVoltage,
            ValueSource.GpuVoltage,
            ValueSource.MemoryPercent,
            ValueSource.MemorySize,
            ValueSource.GpuMemoryLoad,
            ValueSource.NetUp,
            ValueSource.NetDown,
            ValueSource.Fans,
            ValueSource.DiskC,
            ValueSource.DiskD,
            ValueSource.DiskE,
            ValueSource.Volume,
            ValueSource.RefreshRate
        };

        private static readonly Dictionary<ValueSource, string> _labels =
            new Dictionary<ValueSource, string>
        {
            { ValueSource.Static, "Статичный текст" },
            { ValueSource.CurrentTime, "Время" },
            { ValueSource.CurrentDates, "Дата" },
            { ValueSource.CpuUsage, "CPU: загрузка" },
            { ValueSource.GpuUsage, "GPU: загрузка" },
            { ValueSource.CpuTemp, "CPU: температура" },
            { ValueSource.GpuTemp, "GPU: температура" },
            { ValueSource.CpuFrequency, "CPU: частота" },
            { ValueSource.GpuFrequency, "GPU: частота" },
            { ValueSource.CpuPower, "CPU: мощность" },
            { ValueSource.GpuPower, "GPU: мощность" },
            { ValueSource.CpuVoltage, "CPU: напряжение" },
            { ValueSource.GpuVoltage, "GPU: напряжение" },
            { ValueSource.MemoryPercent, "RAM: загрузка" },
            { ValueSource.MemorySize, "RAM: объём" },
            { ValueSource.GpuMemoryLoad, "GPU: память" },
            { ValueSource.NetUp, "Сеть: отдача" },
            { ValueSource.NetDown, "Сеть: приём" },
            { ValueSource.Fans, "Вентиляторы" },
            { ValueSource.DiskC, "Диск C:" },
            { ValueSource.DiskD, "Диск D:" },
            { ValueSource.DiskE, "Диск E:" },
            { ValueSource.Volume, "Громкость" },
            { ValueSource.RefreshRate, "Герцовка экрана" }
        };

        private static readonly Dictionary<ValueSource, string> _nacodexNames =
            new Dictionary<ValueSource, string>
        {
            { ValueSource.Static, "" },
            { ValueSource.CurrentTime, "CurrentTime" },
            { ValueSource.CurrentDates, "CurrentDates" },
            { ValueSource.CpuUsage, "CpuUsage" },
            { ValueSource.GpuUsage, "GpuUsage" },
            { ValueSource.CpuTemp, "CPUTemp" },
            { ValueSource.GpuTemp, "GPUTemp" },
            { ValueSource.CpuFrequency, "CpuFrequency" },
            { ValueSource.GpuFrequency, "GpuFrequency" },
            { ValueSource.CpuPower, "CpuTEC" },
            { ValueSource.GpuPower, "GpuTEC" },
            { ValueSource.CpuVoltage, "CpuVoltage" },
            { ValueSource.GpuVoltage, "GpuVoltage" },
            { ValueSource.MemoryPercent, "MemoryUseInt" },
            { ValueSource.MemorySize, "MemoryUsageSize" },
            { ValueSource.GpuMemoryLoad, "GPUMemoryLoad" },
            { ValueSource.NetUp, "UpNetSpeed" },
            { ValueSource.NetDown, "DownNetSpeed" },
            { ValueSource.Fans, "Fans" },
            { ValueSource.DiskC, "DiskCUsed" },
            { ValueSource.DiskD, "DiskDUsed" },
            { ValueSource.DiskE, "DiskEUsed" },
            { ValueSource.Volume, "Volume" },
            { ValueSource.RefreshRate, "RefreshRate" }
        };

        private static readonly Dictionary<ValueSource, string> _units =
            new Dictionary<ValueSource, string>
        {
            { ValueSource.CpuUsage, "%" },
            { ValueSource.GpuUsage, "%" },
            { ValueSource.CpuTemp, "°C" },
            { ValueSource.GpuTemp, "°C" },
            { ValueSource.CpuFrequency, "MHz" },
            { ValueSource.GpuFrequency, "MHz" },
            { ValueSource.CpuPower, "W" },
            { ValueSource.GpuPower, "W" },
            { ValueSource.CpuVoltage, "V" },
            { ValueSource.GpuVoltage, "V" },
            { ValueSource.MemoryPercent, "%" },
            { ValueSource.MemorySize, "GB" },
            { ValueSource.GpuMemoryLoad, "%" },
            { ValueSource.NetUp, "MB/s" },
            { ValueSource.NetDown, "MB/s" },
            { ValueSource.Fans, "RPM" },
            { ValueSource.DiskC, "%" },
            { ValueSource.DiskD, "%" },
            { ValueSource.DiskE, "%" },
            { ValueSource.Volume, "%" },
            { ValueSource.RefreshRate, "Hz" }
        };

        public static string Label(ValueSource s)
        {
            string v;
            return _labels.TryGetValue(s, out v) ? v : s.ToString();
        }

        public static string NacodexName(ValueSource s)
        {
            string v;
            return _nacodexNames.TryGetValue(s, out v) ? v : "";
        }

        public static string DefaultUnit(ValueSource s)
        {
            string v;
            return _units.TryGetValue(s, out v) ? v : "";
        }

        public static bool IsNumeric(ValueSource s)
        {
            return s != ValueSource.Static
                && s != ValueSource.CurrentTime
                && s != ValueSource.CurrentDates;
        }
    }

    public class DataSimulator
    {
        private static readonly Random Rng = new Random();
        private readonly Dictionary<ValueSource, double> _values = new Dictionary<ValueSource, double>();
        private readonly Dictionary<ValueSource, double> _targets = new Dictionary<ValueSource, double>();

        private static readonly Dictionary<ValueSource, double[]> Ranges =
            new Dictionary<ValueSource, double[]>
        {
            { ValueSource.CpuUsage, new[] { 8.0, 92.0 } },
            { ValueSource.GpuUsage, new[] { 5.0, 99.0 } },
            { ValueSource.CpuTemp, new[] { 38.0, 82.0 } },
            { ValueSource.GpuTemp, new[] { 35.0, 78.0 } },
            { ValueSource.CpuFrequency, new[] { 2400.0, 5200.0 } },
            { ValueSource.GpuFrequency, new[] { 900.0, 2600.0 } },
            { ValueSource.CpuPower, new[] { 35.0, 180.0 } },
            { ValueSource.GpuPower, new[] { 40.0, 260.0 } },
            { ValueSource.CpuVoltage, new[] { 0.85, 1.30 } },
            { ValueSource.GpuVoltage, new[] { 0.80, 1.15 } },
            { ValueSource.MemoryPercent, new[] { 35.0, 88.0 } },
            { ValueSource.MemorySize, new[] { 6.0, 14.5 } },
            { ValueSource.GpuMemoryLoad, new[] { 20.0, 90.0 } },
            { ValueSource.NetUp, new[] { 0.0, 6.0 } },
            { ValueSource.NetDown, new[] { 0.2, 24.0 } },
            { ValueSource.Fans, new[] { 850.0, 2100.0 } },
            { ValueSource.DiskC, new[] { 45.0, 92.0 } },
            { ValueSource.DiskD, new[] { 30.0, 80.0 } },
            { ValueSource.DiskE, new[] { 10.0, 70.0 } },
            { ValueSource.Volume, new[] { 25.0, 95.0 } },
            { ValueSource.RefreshRate, new[] { 60.0, 165.0 } }
        };

        public DataSimulator()
        {
            foreach (var pair in Ranges)
            {
                double[] r = pair.Value;
                double start = r[0] + (r[1] - r[0]) * 0.45;
                _values[pair.Key] = start;
                _targets[pair.Key] = RandomInRange(r);
            }
        }

        private static double RandomInRange(double[] r)
        {
            return r[0] + Rng.NextDouble() * (r[1] - r[0]);
        }

        public void Tick()
        {
            foreach (var pair in Ranges)
            {
                double v = _values[pair.Key];
                double t = _targets[pair.Key];
                v += (t - v) * 0.16;
                double[] r = pair.Value;
                if (Math.Abs(v - t) < Math.Max(0.4, (r[1] - r[0]) * 0.025))
                {
                    _targets[pair.Key] = RandomInRange(r);
                }
                _values[pair.Key] = v;
            }
        }

        public double GetRaw(ValueSource s)
        {
            double v;
            return _values.TryGetValue(s, out v) ? v : 0;
        }

        public string GetFormatted(ValueSource s)
        {
            switch (s)
            {
                case ValueSource.CurrentTime:
                    return DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                case ValueSource.CurrentDates:
                    return DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                case ValueSource.Static:
                    return "";
                case ValueSource.CpuVoltage:
                case ValueSource.GpuVoltage:
                    return GetRaw(s).ToString("0.00", CultureInfo.InvariantCulture);
                case ValueSource.MemorySize:
                    return GetRaw(s).ToString("0.0", CultureInfo.InvariantCulture);
                case ValueSource.NetUp:
                case ValueSource.NetDown:
                    return GetRaw(s).ToString("0.0", CultureInfo.InvariantCulture);
                default:
                    return Math.Round(GetRaw(s)).ToString("0", CultureInfo.InvariantCulture);
            }
        }
    }
}
