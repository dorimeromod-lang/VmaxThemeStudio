using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VmaxThemeStudio
{
    public enum ElementKind
    {
        Text,
        Bar,
        Ring,
        Image
    }

    public enum ValueSource
    {
        Static,
        CurrentTime,
        CurrentDates,
        CpuUsage,
        GpuUsage,
        CpuTemp,
        GpuTemp,
        CpuFrequency,
        GpuFrequency,
        CpuPower,
        GpuPower,
        CpuVoltage,
        GpuVoltage,
        MemoryPercent,
        MemorySize,
        GpuMemoryLoad,
        NetUp,
        NetDown,
        Fans,
        DiskC,
        DiskD,
        DiskE,
        Volume,
        RefreshRate
    }

    public enum BackgroundKind
    {
        None,
        Image,
        Video
    }

    public enum BackgroundFit
    {
        Cover,
        Contain,
        Stretch
    }

    [XmlInclude(typeof(TextElement))]
    [XmlInclude(typeof(BarElement))]
    [XmlInclude(typeof(RingElement))]
    [XmlInclude(typeof(ImageElement))]
    public abstract class ThemeElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Z { get; set; }
        public bool Visible { get; set; }

        protected ThemeElement()
        {
            Id = Guid.NewGuid().ToString("N");
            Visible = true;
            Width = 300;
            Height = 80;
        }

        [XmlIgnore]
        public abstract ElementKind Kind { get; }

        [XmlIgnore]
        public abstract string KindName { get; }

        public abstract ThemeElement Clone();
    }

    public class TextElement : ThemeElement
    {
        public ValueSource Source { get; set; }
        public string StaticText { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public string FontFamily { get; set; }
        public string FontFile { get; set; }
        public double FontSize { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public string Color { get; set; }
        public int TextAlign { get; set; }
        public bool AutoColor { get; set; }
        public string AutoColorTarget { get; set; }

        public TextElement()
        {
            Name = "Текст";
            Source = ValueSource.Static;
            StaticText = "ТЕКСТ";
            Prefix = "";
            Suffix = "";
            FontFamily = "Segoe UI";
            FontFile = "";
            FontSize = 40;
            Color = "#FFFFFFFF";
            AutoColor = true;
            AutoColorTarget = "#FFFFFFFF";
            Width = 500;
            Height = 80;
        }

        public override ElementKind Kind { get { return ElementKind.Text; } }
        public override string KindName { get { return "Текст"; } }

        public override ThemeElement Clone()
        {
            var c = (TextElement)MemberwiseClone();
            c.Id = Guid.NewGuid().ToString("N");
            c.Name = Name + " копия";
            return c;
        }

        public string GetDisplayText(DataSimulator sim)
        {
            if (Source == ValueSource.Static)
                return StaticText ?? "";
            return (Prefix ?? "") + sim.GetFormatted(Source) + (Suffix ?? "");
        }
    }

    public class BarElement : ThemeElement
    {
        public ValueSource Source { get; set; }
        public double MaxValue { get; set; }
        public string Fill { get; set; }
        public string Track { get; set; }
        public string BorderColor { get; set; }
        public double BorderThickness { get; set; }
        public double CornerRadius { get; set; }
        public bool Animated { get; set; }

        public BarElement()
        {
            Name = "Полоса";
            Source = ValueSource.CpuUsage;
            MaxValue = 100;
            Fill = "#FF00C8FF";
            Track = "#50000000";
            BorderColor = "#00000000";
            BorderThickness = 0;
            CornerRadius = 0;
            Width = 300;
            Height = 24;
        }

        public override ElementKind Kind { get { return ElementKind.Bar; } }
        public override string KindName { get { return "Полоса"; } }

        public override ThemeElement Clone()
        {
            var c = (BarElement)MemberwiseClone();
            c.Id = Guid.NewGuid().ToString("N");
            c.Name = Name + " копия";
            return c;
        }

        public double GetFraction(DataSimulator sim)
        {
            if (Source == ValueSource.Static || Source == ValueSource.CurrentTime || Source == ValueSource.CurrentDates)
                return 0;
            double max = MaxValue <= 0 ? 100 : MaxValue;
            double v = sim.GetRaw(Source);
            if (v < 0) v = 0;
            if (v > max) v = max;
            return v / max;
        }
    }

    public class RingElement : ThemeElement
    {
        public ValueSource Source { get; set; }
        public double MaxValue { get; set; }
        public double Thickness { get; set; }
        public string Fill { get; set; }
        public string Track { get; set; }

        public RingElement()
        {
            Name = "Кольцо";
            Source = ValueSource.CpuUsage;
            MaxValue = 100;
            Thickness = 12;
            Fill = "#FF00C8FF";
            Track = "#40FFFFFF";
            Width = 140;
            Height = 140;
        }

        public override ElementKind Kind { get { return ElementKind.Ring; } }
        public override string KindName { get { return "Кольцо"; } }

        public override ThemeElement Clone()
        {
            var c = (RingElement)MemberwiseClone();
            c.Id = Guid.NewGuid().ToString("N");
            c.Name = Name + " копия";
            return c;
        }

        public double GetFraction(DataSimulator sim)
        {
            if (Source == ValueSource.Static || Source == ValueSource.CurrentTime || Source == ValueSource.CurrentDates)
                return 0;
            double max = MaxValue <= 0 ? 100 : MaxValue;
            double v = sim.GetRaw(Source);
            if (v < 0) v = 0;
            if (v > max) v = max;
            return v / max;
        }
    }

    public class ImageElement : ThemeElement
    {
        public string FilePath { get; set; }
        public bool KeepAspect { get; set; }

        public ImageElement()
        {
            Name = "Картинка";
            FilePath = "";
            KeepAspect = true;
            Width = 200;
            Height = 200;
        }

        public override ElementKind Kind { get { return ElementKind.Image; } }
        public override string KindName { get { return "Картинка"; } }

        public override ThemeElement Clone()
        {
            var c = (ImageElement)MemberwiseClone();
            c.Id = Guid.NewGuid().ToString("N");
            c.Name = Name + " копия";
            return c;
        }
    }

    public class BackgroundSettings
    {
        public BackgroundKind Kind { get; set; }
        public BackgroundFit Fit { get; set; }
        public string ImagePath { get; set; }
        public string VideoPath { get; set; }
        public string Color { get; set; }

        public BackgroundSettings()
        {
            Kind = BackgroundKind.None;
            Fit = BackgroundFit.Cover;
            Color = "#FF101114";
            ImagePath = "";
            VideoPath = "";
        }
    }

    [XmlRoot("VmaxThemeProject")]
    [XmlInclude(typeof(TextElement))]
    [XmlInclude(typeof(BarElement))]
    [XmlInclude(typeof(RingElement))]
    [XmlInclude(typeof(ImageElement))]
    public class ThemeProject
    {
        public string ThemeName { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public BackgroundSettings Background { get; set; }
        public List<ThemeElement> Elements { get; set; }

        public ThemeProject()
        {
            ThemeName = "Моя тема";
            Width = 1920;
            Height = 462;
            Background = new BackgroundSettings();
            Elements = new List<ThemeElement>();
        }
    }
}
