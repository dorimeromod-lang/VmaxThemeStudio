using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VmaxThemeStudio
{
    public class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; }

        private readonly Slider _r;
        private readonly Slider _g;
        private readonly Slider _b;
        private readonly Slider _a;
        private readonly TextBlock _rValue;
        private readonly TextBlock _gValue;
        private readonly TextBlock _bValue;
        private readonly TextBlock _aValue;
        private readonly TextBox _hex;
        private readonly Border _preview;
        private bool _updating;

        public ColorPickerWindow(Color initial)
        {
            SelectedColor = initial;
            Title = "Выбор цвета";
            Width = 380;
            Height = 500;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 31, 34));
            Foreground = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var root = new StackPanel { Margin = new Thickness(16) };
            Content = root;

            root.Children.Add(new TextBlock
            {
                Text = "Цвет элемента",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _preview = new Border
            {
                Height = 80,
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 63, 68)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14)
            };
            root.Children.Add(_preview);

            _r = AddSlider(root, "R", out _rValue);
            _g = AddSlider(root, "G", out _gValue);
            _b = AddSlider(root, "B", out _bValue);
            _a = AddSlider(root, "A", out _aValue);

            var hexRow = new Grid { Margin = new Thickness(0, 8, 0, 12) };
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hexRow.Children.Add(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center });
            _hex = MakeTextBox();
            Grid.SetColumn(_hex, 1);
            hexRow.Children.Add(_hex);
            root.Children.Add(hexRow);

            var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            AddPreset(presets, Color.FromRgb(255, 255, 255));
            AddPreset(presets, Color.FromRgb(0, 200, 255));
            AddPreset(presets, Color.FromRgb(47, 107, 255));
            AddPreset(presets, Color.FromRgb(255, 70, 120));
            AddPreset(presets, Color.FromRgb(255, 140, 60));
            AddPreset(presets, Color.FromRgb(80, 220, 120));
            AddPreset(presets, Color.FromRgb(170, 170, 175));
            AddPreset(presets, Color.FromRgb(15, 15, 18));
            root.Children.Add(presets);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = MakeButton("Отмена");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { DialogResult = false; };
            var ok = MakeButton("Готово");
            ok.Background = new SolidColorBrush(Color.FromRgb(47, 107, 255));
            ok.Click += delegate
            {
                SelectedColor = CurrentColor();
                DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            _r.ValueChanged += SliderChanged;
            _g.ValueChanged += SliderChanged;
            _b.ValueChanged += SliderChanged;
            _a.ValueChanged += SliderChanged;
            _hex.TextChanged += HexChanged;
            _hex.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    ApplyHex();
                    e.Handled = true;
                }
            };

            SetFromColor(initial);
        }

        private Slider AddSlider(Panel host, string label, out TextBlock valueText)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var slider = new Slider { Minimum = 0, Maximum = 255, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);
            var tb = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 165, 175))
            };
            Grid.SetColumn(tb, 2);
            grid.Children.Add(tb);
            host.Children.Add(grid);
            valueText = tb;
            return slider;
        }

        private static TextBox MakeTextBox()
        {
            return new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 25, 28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 63, 68)),
                Padding = new Thickness(6, 4, 6, 4),
                CaretBrush = Brushes.White,
                FontFamily = new FontFamily("Consolas")
            };
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(16, 6, 16, 6),
                Background = new SolidColorBrush(Color.FromRgb(46, 48, 53)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(62, 65, 71)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        private void AddPreset(Panel host, Color color)
        {
            var button = new Button
            {
                Width = 32,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 73, 80)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = ColorUtil.ToHex(color)
            };
            button.Click += delegate { SetFromColor(color); };
            host.Children.Add(button);
        }

        private Color CurrentColor()
        {
            return Color.FromArgb((byte)_a.Value, (byte)_r.Value, (byte)_g.Value, (byte)_b.Value);
        }

        private void SetFromColor(Color c)
        {
            _updating = true;
            _r.Value = c.R;
            _g.Value = c.G;
            _b.Value = c.B;
            _a.Value = c.A;
            _updating = false;
            RefreshPreview();
        }

        private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating)
                return;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var c = CurrentColor();
            _preview.Background = new SolidColorBrush(c);
            _rValue.Text = c.R.ToString(CultureInfo.InvariantCulture);
            _gValue.Text = c.G.ToString(CultureInfo.InvariantCulture);
            _bValue.Text = c.B.ToString(CultureInfo.InvariantCulture);
            _aValue.Text = c.A.ToString(CultureInfo.InvariantCulture);
            _updating = true;
            _hex.Text = ColorUtil.ToHex(c);
            _updating = false;
        }

        private void HexChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating)
                return;
            ApplyHex();
        }

        private void ApplyHex()
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(_hex.Text.Trim());
                SetFromColor(c);
            }
            catch
            {
            }
        }
    }
}
