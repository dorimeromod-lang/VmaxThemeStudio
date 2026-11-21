using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VmaxThemeStudio
{
    public class MainWindow : Window
    {
        private ThemeProject _project;
        private readonly DataSimulator _sim = new DataSimulator();
        private readonly DispatcherTimer _timer;

        private Grid _stage;
        private Image _imageBg;
        private VideoBackgroundElement _videoBg;
        private Canvas _elementsCanvas;
        private Canvas _overlayCanvas;
        private ScrollViewer _scroller;
        private ScaleTransform _stageScale;
        private double _fitScale = 1.0;
        private double _zoom = 1.0;
        private double _stageScaleValue = 1.0;

        private readonly Dictionary<string, FrameworkElement> _visuals =
            new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, TextBox> _propBoxes =
            new Dictionary<string, TextBox>();

        private ThemeElement _selected;
        private Rectangle _selRect;
        private FrameworkElement _interactionCacheTarget;
        private bool _interactionActive;
        private bool _windowActive = true;
        private readonly Dictionary<string, Rectangle> _handles = new Dictionary<string, Rectangle>();
        private readonly List<Line> _guides = new List<Line>();
        private readonly List<Line> _guidePool = new List<Line>();
        private int _guideIndex;
        private double[] _dragTargetsX;
        private double[] _dragTargetsY;
        private double _overlayScale = -1;

        private bool _suppress;
        private bool _updatingList;
        private bool _dragging;
        private bool _resizing;
        private string _resizeCorner;
        private Point _dragStart;
        private double _origX;
        private double _origY;
        private double _origW;
        private double _origH;

        private ListBox _elementList;
        private StackPanel _propsHost;
        private TextBlock _statusData;
        private TextBlock _zoomLabel;
        private Slider _zoomSlider;

        private Style _buttonStyle;
        private Style _accentButtonStyle;
        private Style _dangerButtonStyle;

        private string _projectPath;
        private bool _dirty;
        private string _configPath;
        private string _nacodexRoot;
        private readonly string _fontStorageDir;

        private class ElementListItem
        {
            public ThemeElement Element { get; set; }

            public override string ToString()
            {
                return (Element.Visible ? "" : "◌ ") + Element.Name + "   ·   " + Element.KindName;
            }
        }

        private class SourceItem
        {
            public ValueSource Value { get; set; }
            public string Label { get; set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private sealed class FlatCombo : UserControl
        {
            private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(255, 176, 32));
            private static readonly Brush NormalText = new SolidColorBrush(Color.FromRgb(200, 203, 210));
            private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(120, 124, 132));
            private static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(26, 27, 29));
            private static readonly Brush BorderBrushValue = new SolidColorBrush(Color.FromRgb(57, 60, 66));

            public static readonly DependencyProperty SelectedItemProperty =
                DependencyProperty.Register("SelectedItem", typeof(object), typeof(FlatCombo),
                    new PropertyMetadata(null, OnSelectedItemChanged));

            public ObservableCollection<object> Items { get; } = new ObservableCollection<object>();

            public event EventHandler SelectionChanged;

            private readonly TextBlock _text;
            private readonly StackPanel _rows;
            private readonly ScrollViewer _scroll;
            private readonly Popup _popup;

            public FlatCombo()
            {
                var border = new Border
                {
                    Background = Surface,
                    BorderBrush = BorderBrushValue,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _text = new TextBlock
                {
                    Foreground = Muted,
                    Text = "—",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(7, 0, 4, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = 12
                };
                var arrow = new TextBlock
                {
                    Text = "▾",
                    Foreground = Muted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 7, 0),
                    FontSize = 11
                };
                grid.Children.Add(_text);
                Grid.SetColumn(arrow, 1);
                grid.Children.Add(arrow);
                border.Child = grid;
                border.PreviewMouseLeftButtonDown += (s, e) => { Toggle(); e.Handled = true; };
                border.MouseEnter += (s, e) => border.BorderBrush = Accent;
                border.MouseLeave += (s, e) => border.BorderBrush = BorderBrushValue;
                Content = border;

                _rows = new StackPanel();
                _scroll = new ScrollViewer
                {
                    Content = _rows,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 300
                };
                var popupBorder = new Border
                {
                    Background = Surface,
                    BorderBrush = BorderBrushValue,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2),
                    Child = _scroll
                };
                _popup = new Popup
                {
                    Child = popupBorder,
                    PlacementTarget = this,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = true,
                    Focusable = false
                };

                Items.CollectionChanged += (s, e) =>
                {
                    RebuildRows();
                    if (SelectedItem != null && !Items.Contains(SelectedItem))
                        SelectedItem = null;
                };
                Loaded += (s, e) =>
                {
                    UpdateDisplay();
                    var window = Window.GetWindow(this);
                    if (window != null)
                        window.PreviewMouseDown += WindowPreviewMouseDown;
                };
                Unloaded += (s, e) =>
                {
                    var window = Window.GetWindow(this);
                    if (window != null)
                        window.PreviewMouseDown -= WindowPreviewMouseDown;
                };
            }

            public object SelectedItem
            {
                get { return GetValue(SelectedItemProperty); }
                set { SetValue(SelectedItemProperty, value); }
            }

            public int SelectedIndex
            {
                get { return Items.IndexOf(SelectedItem); }
                set { SelectedItem = value >= 0 && value < Items.Count ? Items[value] : null; }
            }

            private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var combo = (FlatCombo)d;
                combo.UpdateDisplay();
                combo.RebuildRows();
                combo.SelectionChanged?.Invoke(combo, EventArgs.Empty);
            }

            private void UpdateDisplay()
            {
                if (_text == null)
                    return;
                _text.Text = SelectedItem == null ? "—" : Display(SelectedItem);
                _text.Foreground = SelectedItem == null ? Muted : Accent;
            }

            private void WindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                if (!_popup.IsOpen)
                    return;
                var source = e.OriginalSource as DependencyObject;
                if (source != null && (IsDescendantOf(source, this) || IsDescendantOf(source, _popup.Child)))
                    return;
                _popup.IsOpen = false;
            }

            private static bool IsDescendantOf(DependencyObject node, DependencyObject target)
            {
                while (node != null)
                {
                    if (ReferenceEquals(node, target))
                        return true;
                    node = VisualTreeHelper.GetParent(node);
                }
                return false;
            }

            private void Toggle()
            {
                if (!IsEnabled)
                    return;
                if (_popup.IsOpen)
                {
                    _popup.IsOpen = false;
                    return;
                }
                RebuildRows();
                _popup.Width = Math.Max(ActualWidth, 160);
                _popup.IsOpen = true;
                int index = Items.IndexOf(SelectedItem);
                if (index >= 0 && index < _rows.Children.Count)
                {
                    var rowElement = _rows.Children[index] as FrameworkElement;
                    if (rowElement != null)
                        rowElement.BringIntoView();
                }
            }

            private void RebuildRows()
            {
                if (_rows == null)
                    return;
                _rows.Children.Clear();
                foreach (var item in Items)
                {
                    bool selected = ReferenceEquals(item, SelectedItem);
                    var row = new Border
                    {
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(7, 5, 7, 5),
                        Cursor = Cursors.Hand
                    };
                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var bar = new Border
                    {
                        Background = selected ? Accent : Brushes.Transparent,
                        Margin = new Thickness(0, 1, 5, 1)
                    };
                    var label = new TextBlock
                    {
                        Text = Display(item),
                        Foreground = selected ? Accent : NormalText,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontSize = 12
                    };
                    rowGrid.Children.Add(bar);
                    Grid.SetColumn(label, 1);
                    rowGrid.Children.Add(label);
                    row.Child = rowGrid;
                    var captured = item;
                    row.MouseLeftButtonDown += (s, e) =>
                    {
                        SelectedItem = captured;
                        _popup.IsOpen = false;
                        e.Handled = true;
                    };
                    row.MouseEnter += (s, e) =>
                    {
                        row.BorderBrush = Accent;
                        if (!ReferenceEquals(captured, SelectedItem)) label.Foreground = Brushes.White;
                    };
                    row.MouseLeave += (s, e) =>
                    {
                        row.BorderBrush = Brushes.Transparent;
                        if (!ReferenceEquals(captured, SelectedItem)) label.Foreground = NormalText;
                    };
                    _rows.Children.Add(row);
                }
            }

            private static string Display(object item)
            {
                var ci = item as ComboBoxItem;
                if (ci != null && ci.Content != null)
                    return ci.Content.ToString();
                return item == null ? "" : item.ToString();
            }
        }

        public MainWindow()
        {
            _fontStorageDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VmaxThemeStudio", "Fonts");

            Title = "VMAX Theme Studio";
            Width = 1520;
            Height = 900;
            MinWidth = 1100;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(27, 28, 31));
            Foreground = new SolidColorBrush(Color.FromRgb(230, 231, 234));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            CreateStyles();
            LoadConfig();
            BuildUi();
            NewProject(false);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += TimerTick;
            _timer.Start();

            SizeChanged += delegate { ApplyZoom(); };
            Activated += delegate
            {
                _windowActive = true;
                if (!_interactionActive)
                {
                    _timer.Start();
                    _videoBg.Resume();
                }
            };
            Deactivated += delegate
            {
                _windowActive = false;
                _timer.Stop();
                _videoBg.Pause();
            };
            StateChanged += delegate
            {
                if (WindowState == WindowState.Minimized)
                {
                    _timer.Stop();
                    _videoBg.Pause();
                }
                else if (IsActive && !_interactionActive)
                {
                    _timer.Start();
                    _videoBg.Resume();
                }
            };
            Closing += OnWindowClosing;
            PreviewKeyDown += OnWindowKeyDown;
        }

        private void CreateStyles()
        {
            _buttonStyle = CreateButtonStyle(
                "#FF2E3035", "#FF3A3D43", "#FF25272B", "#FF3A3D43", "#FFFFFFFF");
            _accentButtonStyle = CreateButtonStyle(
                "#FF2F6BFF", "#FF3D7EFF", "#FF2456D6", "#002F6BFF", "#FFFFFFFF");
            _dangerButtonStyle = CreateButtonStyle(
                "#FF4A2A31", "#FF5D333C", "#FF3C2127", "#FF6A3A44", "#FFFFB4C0");
        }

        private static Style CreateButtonStyle(string bg, string hover, string pressed, string border, string fg)
        {
            string xaml =
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
                "<Setter Property='Background' Value='" + bg + "'/>" +
                "<Setter Property='Foreground' Value='" + fg + "'/>" +
                "<Setter Property='BorderBrush' Value='" + border + "'/>" +
                "<Setter Property='BorderThickness' Value='1'/>" +
                "<Setter Property='Padding' Value='10,5,10,5'/>" +
                "<Setter Property='FontSize' Value='12'/>" +
                "<Setter Property='Cursor' Value='Hand'/>" +
                "<Setter Property='SnapsToDevicePixels' Value='True'/>" +
                "<Setter Property='Template'>" +
                "<Setter.Value>" +
                "<ControlTemplate TargetType='Button'>" +
                "<Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' " +
                "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='4' Padding='{TemplateBinding Padding}' " +
                "SnapsToDevicePixels='True'>" +
                "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
                "</Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='" + hover + "'/></Trigger>" +
                "<Trigger Property='IsPressed' Value='True'><Setter Property='Background' Value='" + pressed + "'/></Trigger>" +
                "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>" +
                "</ControlTemplate.Triggers>" +
                "</ControlTemplate>" +
                "</Setter.Value>" +
                "</Setter>" +
                "</Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        private void BuildUi()
        {
            var root = new DockPanel { LastChildFill = true };

            var toolbarPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var toolbar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 37, 41)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 54, 59)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 7, 8, 7),
                Child = toolbarPanel
            };
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);

            AddToolbarButton(toolbarPanel, "Новая тема", delegate { NewProject(true); }, false);
            AddToolbarButton(toolbarPanel, "Открыть", delegate { OpenProject(); }, false);
            AddToolbarButton(toolbarPanel, "Сохранить", delegate { SaveProject(); }, false);

            AddToolbarSeparator(toolbarPanel);
            AddToolbarButton(toolbarPanel, "＋ Текст", delegate { AddStaticText(); }, false);
            AddToolbarButton(toolbarPanel, "＋ Значение", delegate { AddValueText(); }, false);
            AddToolbarButton(toolbarPanel, "＋ Полоса", delegate { AddBar(); }, false);
            AddToolbarButton(toolbarPanel, "＋ Кольцо", delegate { AddRing(); }, false);
            AddToolbarButton(toolbarPanel, "＋ Картинка", delegate { AddImage(); }, false);
            AddToolbarButton(toolbarPanel, "Фон…", delegate { ChooseBackgroundFile(); }, false);

            AddToolbarSeparator(toolbarPanel);
            AddToolbarButton(toolbarPanel, "Экспорт…", delegate { ExportTheme(); }, false);
            AddToolbarButton(toolbarPanel, "Установить в NACODEX", delegate { InstallToNacodex(); }, true);

            var status = new Border
            {
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(32, 34, 37)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 54, 59)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10, 0, 10, 0)
            };
            DockPanel.SetDock(status, Dock.Bottom);
            var statusDock = new DockPanel();
            status.Child = statusDock;

            var zoomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(zoomPanel, Dock.Right);
            zoomPanel.Children.Add(new TextBlock
            {
                Text = "Масштаб",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            _zoomSlider = new Slider
            {
                Minimum = 25,
                Maximum = 200,
                Value = 100,
                Width = 130,
                IsSnapToTickEnabled = true,
                TickFrequency = 5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _zoomSlider.ValueChanged += delegate
            {
                _zoom = _zoomSlider.Value / 100.0;
                ApplyZoom();
            };
            zoomPanel.Children.Add(_zoomSlider);
            _zoomLabel = new TextBlock
            {
                Text = "100%",
                Width = 46,
                TextAlignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                VerticalAlignment = VerticalAlignment.Center
            };
            zoomPanel.Children.Add(_zoomLabel);
            statusDock.Children.Add(zoomPanel);

            _statusData = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            statusDock.Children.Add(_statusData);
            root.Children.Add(status);

            var left = new Border
            {
                Width = 250,
                Background = new SolidColorBrush(Color.FromRgb(34, 35, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 54, 59)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            DockPanel.SetDock(left, Dock.Left);
            var leftGrid = new Grid { Margin = new Thickness(10) };
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var leftHeader = new TextBlock
            {
                Text = "ЭЛЕМЕНТЫ",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(138, 143, 152)),
                Margin = new Thickness(2, 2, 0, 8)
            };
            Grid.SetRow(leftHeader, 0);
            leftGrid.Children.Add(leftHeader);

            _elementList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 27, 29)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 54, 59)),
                BorderThickness = new Thickness(1),
                FontSize = 12
            };
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));

            var itemHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            itemHover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(38, 40, 44))));
            itemHover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemStyle.Triggers.Add(itemHover);

            var itemSelected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            itemSelected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(38, 40, 44))));
            itemSelected.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 176, 32))));
            itemStyle.Triggers.Add(itemSelected);

            var listTemplate = new ControlTemplate(typeof(ListBoxItem));
            var listBorder = new FrameworkElementFactory(typeof(Border), "Bd");
            listBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            listBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            listBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            listBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var listPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            listPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            listPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
                new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            listBorder.AppendChild(listPresenter);
            listTemplate.VisualTree = listBorder;
            var listTemplateHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            listTemplateHover.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(38, 40, 44)), "Bd"));
            listTemplateHover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            listTemplate.Triggers.Add(listTemplateHover);
            var listTemplateSelected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            listTemplateSelected.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(38, 40, 44)), "Bd"));
            listTemplateSelected.Setters.Add(new Setter(Control.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(255, 176, 32))));
            listTemplate.Triggers.Add(listTemplateSelected);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, listTemplate));

            _elementList.ItemContainerStyle = itemStyle;

            var listHighlight = new SolidColorBrush(Color.FromRgb(38, 40, 44));
            var listHighlightText = new SolidColorBrush(Color.FromRgb(255, 176, 32));
            _elementList.Resources.Add(SystemColors.HighlightBrushKey, listHighlight);
            _elementList.Resources.Add(SystemColors.HighlightTextBrushKey, listHighlightText);
            _elementList.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey, listHighlight);
            _elementList.Resources.Add(SystemColors.InactiveSelectionHighlightTextBrushKey, listHighlightText);
            _elementList.SelectionChanged += ElementListSelectionChanged;
            Grid.SetRow(_elementList, 1);
            leftGrid.Children.Add(_elementList);

            var listButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            AddSmallButton(listButtons, "Выше", delegate { MoveSelectedZ(1); });
            AddSmallButton(listButtons, "Ниже", delegate { MoveSelectedZ(-1); });
            AddSmallButton(listButtons, "На фон", delegate { SendSelectedToBack(); });
            AddSmallButton(listButtons, "Вперёд", delegate { SendSelectedToFront(); });
            AddSmallButton(listButtons, "Удалить", delegate { RemoveSelected(); }, true);
            Grid.SetRow(listButtons, 2);
            leftGrid.Children.Add(listButtons);
            left.Child = leftGrid;
            root.Children.Add(left);

            var right = new Border
            {
                Width = 335,
                Background = new SolidColorBrush(Color.FromRgb(34, 35, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 54, 59)),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            DockPanel.SetDock(right, Dock.Right);
            var rightDock = new DockPanel();
            var rightHeader = new TextBlock
            {
                Text = "СВОЙСТВА",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(138, 143, 152)),
                Margin = new Thickness(14, 12, 0, 6)
            };
            DockPanel.SetDock(rightHeader, Dock.Top);
            rightDock.Children.Add(rightHeader);
            var propsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _propsHost = new StackPanel { Margin = new Thickness(14, 4, 14, 20) };
            propsScroll.Content = _propsHost;
            rightDock.Children.Add(propsScroll);
            right.Child = rightDock;
            root.Children.Add(right);

            var center = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 21, 23)),
                Padding = new Thickness(10)
            };
            _scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent
            };
            _scroller.SizeChanged += delegate { ApplyZoom(); };
            center.Child = _scroller;
            root.Children.Add(center);

            _stageScale = new ScaleTransform(1, 1);
            _stage = new Grid
            {
                Width = 1920,
                Height = 462,
                Background = new SolidColorBrush(Color.FromRgb(16, 17, 20)),
                ClipToBounds = true,
                LayoutTransform = _stageScale,
                SnapsToDevicePixels = true
            };

            _imageBg = new Image { Stretch = Stretch.UniformToFill, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            _videoBg = new VideoBackgroundElement { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            _elementsCanvas = new Canvas { Background = Brushes.Transparent };
            _overlayCanvas = new Canvas();
            _elementsCanvas.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == _elementsCanvas)
                    SelectElement(null);
            };

            _stage.Children.Add(_imageBg);
            _stage.Children.Add(_videoBg);
            _stage.Children.Add(_elementsCanvas);
            _stage.Children.Add(_overlayCanvas);

            _selRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(18, 255, 176, 32)),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 176, 32)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _overlayCanvas.Children.Add(_selRect);
            CreateHandle("nw", Cursors.SizeNWSE);
            CreateHandle("ne", Cursors.SizeNESW);
            CreateHandle("sw", Cursors.SizeNESW);
            CreateHandle("se", Cursors.SizeNWSE);

            _scroller.Content = _stage;
            Content = root;
        }

        private Button AddToolbarButton(Panel host, string text, RoutedEventHandler click, bool accent)
        {
            var b = new Button
            {
                Content = text,
                Style = accent ? _accentButtonStyle : _buttonStyle,
                Margin = new Thickness(0, 0, 6, 0),
                FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal
            };
            b.Click += click;
            host.Children.Add(b);
            return b;
        }

        private void AddToolbarSeparator(Panel host)
        {
            host.Children.Add(new Border
            {
                Width = 1,
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(60, 63, 68)),
                Margin = new Thickness(6, 2, 12, 2)
            });
        }

        private Button AddSmallButton(Panel host, string text, RoutedEventHandler click, bool danger = false)
        {
            var b = new Button
            {
                Content = text,
                Style = danger ? _dangerButtonStyle : _buttonStyle,
                Margin = new Thickness(0, 0, 5, 5),
                Padding = new Thickness(7, 3, 7, 3),
                FontSize = 11
            };
            b.Click += click;
            host.Children.Add(b);
            return b;
        }

        private void CreateHandle(string corner, Cursor cursor)
        {
            var h = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(255, 176, 32)),
                Stroke = new SolidColorBrush(Color.FromRgb(20, 21, 23)),
                StrokeThickness = 1,
                Width = 10,
                Height = 10,
                Visibility = Visibility.Collapsed,
                Cursor = cursor,
                Tag = corner
            };
            h.MouseLeftButtonDown += HandleMouseDown;
            h.MouseMove += HandleMouseMove;
            h.MouseLeftButtonUp += HandleMouseUp;
            h.LostMouseCapture += InteractionLostMouseCapture;
            _overlayCanvas.Children.Add(h);
            _handles[corner] = h;
        }

        // ---------- project lifecycle ----------

        private void NewProject(bool confirm)
        {
            if (confirm)
            {
                if (_project != null && _project.Elements.Count > 0)
                {
                    if (MessageBox.Show(this, "Создать новую тему? Несохранённые изменения будут потеряны.",
                        "VMAX Theme Studio", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
            }

            _project = new ThemeProject();
            _projectPath = null;
            _dirty = false;
            _selected = null;
            ApplyProjectToStage();
            BuildProperties();
            UpdateTitle();
        }

        private void ApplyProjectToStage()
        {
            _stage.Width = _project.Width;
            _stage.Height = _project.Height;
            _elementsCanvas.Width = _project.Width;
            _elementsCanvas.Height = _project.Height;
            _overlayCanvas.Width = _project.Width;
            _overlayCanvas.Height = _project.Height;
            _stage.Background = ColorUtil.Brush(_project.Background.Color);

            _elementsCanvas.Children.Clear();
            _visuals.Clear();
            foreach (var el in _project.Elements.OrderBy(e => e.Z))
                CreateVisual(el);

            ApplyBackground();
            RefreshElementList();
            UpdateSelectionOverlay();
            ApplyZoom();
            RefreshDataVisuals();
        }

        private void ApplyBackground()
        {
            var bg = _project.Background;
            _imageBg.Source = null;
            _imageBg.Visibility = Visibility.Collapsed;
            _videoBg.Visibility = Visibility.Collapsed;

            if (bg.Kind == BackgroundKind.Image && !string.IsNullOrWhiteSpace(bg.ImagePath) && File.Exists(bg.ImagePath))
            {
                var bmp = ImageUtil.Load(bg.ImagePath);
                if (bmp != null)
                {
                    _imageBg.Source = bmp;
                    _imageBg.Stretch = bg.Fit == BackgroundFit.Cover
                        ? Stretch.UniformToFill
                        : (bg.Fit == BackgroundFit.Contain ? Stretch.Uniform : Stretch.Fill);
                    _imageBg.Visibility = Visibility.Visible;
                }
            }

            if (bg.Kind == BackgroundKind.Video && !string.IsNullOrWhiteSpace(bg.VideoPath) && File.Exists(bg.VideoPath))
            {
                _videoBg.FallbackColor = ColorUtil.Parse(bg.Color);
                _videoBg.Visibility = Visibility.Visible;
                _videoBg.SetVideo(bg.VideoPath, bg.Fit);
            }
            else
            {
                _videoBg.SetVideo(null, bg.Fit);
            }
            _stage.Background = ColorUtil.Brush(bg.Color);
            ApplyAutomaticTextColors();
        }

        private Color GetBackgroundColorForAutomaticText()
        {
            var bg = _project.Background;
            if (bg.Kind == BackgroundKind.Image && !string.IsNullOrWhiteSpace(bg.ImagePath) && File.Exists(bg.ImagePath))
            {
                var bmp = ImageUtil.Load(bg.ImagePath);
                if (bmp != null)
                    return ImageUtil.GetAverageColor(bmp);
            }
            return ColorUtil.Parse(bg.Color, Color.FromRgb(0, 0, 0));
        }

        private void ApplyAutomaticTextColor(TextElement text)
        {
            if (text == null)
                return;
            ApplyAutomaticTextColor(text, GetBackgroundColorForAutomaticText());
        }

        private void ApplyAutomaticTextColor(TextElement text, Color background)
        {
            if (text == null)
                return;
            Color target = ColorUtil.Parse(text.AutoColorTarget, Colors.White);
            text.Color = ColorUtil.ToHex(ColorUtil.GetReadableColor(background, target));
            RefreshVisual(text);
        }

        private void ApplyAutomaticTextColors()
        {
            if (_project == null)
                return;
            Color background = GetBackgroundColorForAutomaticText();
            foreach (var text in _project.Elements.OfType<TextElement>())
            {
                if (text.AutoColor)
                    ApplyAutomaticTextColor(text, background);
            }
        }

        private void UpdateTitle()
        {
            string name = _project == null ? "" : _project.ThemeName;
            Title = "VMAX Theme Studio — " + (string.IsNullOrWhiteSpace(name) ? "без названия" : name) + (_dirty ? " *" : "");
        }

        // ---------- toolbar actions ----------

        private void AddStaticText()
        {
            var el = new TextElement
            {
                Name = UniqueName("Текст"),
                StaticText = "НОВЫЙ ТЕКСТ",
                FontSize = 48,
                Width = 600,
                Height = 100
            };
            AddElement(el);
        }

        private void AddValueText()
        {
            var el = new TextElement
            {
                Name = UniqueName("Показатель"),
                Source = ValueSource.CpuUsage,
                Prefix = "",
                Suffix = "%",
                FontSize = 64,
                Width = 420,
                Height = 110
            };
            AddElement(el);
        }

        private void AddBar()
        {
            var el = new BarElement
            {
                Name = UniqueName("Полоса"),
                Source = ValueSource.CpuUsage,
                Width = 420,
                Height = 28,
                CornerRadius = 4
            };
            AddElement(el);
        }

        private void AddRing()
        {
            var el = new RingElement
            {
                Name = UniqueName("Кольцо"),
                Source = ValueSource.CpuUsage,
                Width = 160,
                Height = 160,
                Thickness = 14
            };
            AddElement(el);
        }

        private void AddImage()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Выберите картинку",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*"
            };
            if (ofd.ShowDialog(this) != true)
                return;
            var size = ImageUtil.GetPixelSize(ofd.FileName);
            if (size.Width <= 0 || size.Height <= 0)
            {
                MessageBox.Show(this, "Не удалось прочитать изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            double scale = Math.Min(1.0, 400.0 / Math.Max(size.Width, size.Height));
            var el = new ImageElement
            {
                Name = UniqueName("Картинка"),
                FilePath = ofd.FileName,
                Width = Math.Max(1, size.Width * scale),
                Height = Math.Max(1, size.Height * scale)
            };
            AddElement(el);
        }

        private void AddElement(ThemeElement el)
        {
            double maxZ = _project.Elements.Count == 0 ? 0 : _project.Elements.Max(e => e.Z);
            el.Z = maxZ + 1;
            int cascade = _project.Elements.Count % 8;
            el.X += 30 * cascade;
            el.Y += 30 * cascade;
            _project.Elements.Add(el);
            CreateVisual(el);
            if (el is TextElement)
                ApplyAutomaticTextColors();
            _dirty = true;
            RefreshElementList();
            SelectElement(el);
        }

        private string UniqueName(string baseName)
        {
            var existing = new HashSet<string>(_project.Elements.Select(e => e.Name));
            if (!existing.Contains(baseName))
                return baseName;
            int i = 2;
            while (existing.Contains(baseName + " " + i))
                i++;
            return baseName + " " + i;
        }

        private void ChooseBackgroundFile()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Выберите фон",
                Filter = "Медиа|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.wmv;*.avi;*.mov|Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Видео|*.mp4;*.wmv;*.avi;*.mov|Все файлы|*.*"
            };
            if (ofd.ShowDialog(this) != true)
                return;
            string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
            bool isVideo = ext == ".mp4" || ext == ".wmv" || ext == ".avi" || ext == ".mov" || ext == ".mkv";
            if (isVideo)
            {
                _project.Background.Kind = BackgroundKind.Video;
                _project.Background.VideoPath = ofd.FileName;
            }
            else
            {
                _project.Background.Kind = BackgroundKind.Image;
                _project.Background.ImagePath = ofd.FileName;
            }
            _dirty = true;
            ApplyBackground();
            SelectElement(null);
            BuildProperties();
        }

        // ---------- visuals ----------

        private FrameworkElement CreateVisual(ThemeElement el)
        {
            FrameworkElement fe;
            if (el is TextElement)
            {
                fe = new TextBlock
                {
                    Background = Brushes.Transparent,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.None,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0)
                };
            }
            else if (el is BarElement)
            {
                fe = new BarVisual((BarElement)el, _sim);
            }
            else if (el is RingElement)
            {
                fe = new RingVisual((RingElement)el, _sim);
            }
            else
            {
                var border = new Border
                {
                    Background = Brushes.Transparent,
                    Child = new Image { Stretch = Stretch.Fill }
                };
                fe = border;
            }

            fe.Tag = el;
            fe.Cursor = Cursors.SizeAll;
            fe.MouseLeftButtonDown += ElementMouseDown;
            fe.MouseMove += ElementMouseMove;
            fe.MouseLeftButtonUp += ElementMouseUp;
            fe.LostMouseCapture += InteractionLostMouseCapture;
            _elementsCanvas.Children.Add(fe);
            _visuals[el.Id] = fe;
            RefreshVisual(el);
            return fe;
        }

        private void RefreshVisual(ThemeElement el)
        {
            FrameworkElement fe;
            if (el == null || !_visuals.TryGetValue(el.Id, out fe))
                return;
            Canvas.SetLeft(fe, el.X);
            Canvas.SetTop(fe, el.Y);
            fe.Width = Math.Max(1, el.Width);
            fe.Height = Math.Max(1, el.Height);
            fe.Visibility = el.Visible ? Visibility.Visible : Visibility.Collapsed;
            Panel.SetZIndex(fe, (int)Math.Round(el.Z));

            var text = el as TextElement;
            if (text != null && fe is TextBlock)
            {
                var tb = (TextBlock)fe;
                tb.Text = text.GetDisplayText(_sim);
                tb.FontFamily = FontUtil.MakeFontFamily(text.FontFamily, text.FontFile);
                tb.FontSize = Math.Max(1, text.FontSize);
                tb.FontWeight = text.Bold ? FontWeights.Bold : FontWeights.Normal;
                tb.FontStyle = text.Italic ? FontStyles.Italic : FontStyles.Normal;
                tb.TextDecorations = text.Underline ? TextDecorations.Underline : null;
                tb.Foreground = ColorUtil.Brush(text.Color);
                tb.TextAlignment = text.TextAlign == 1
                    ? TextAlignment.Center
                    : (text.TextAlign == 2 ? TextAlignment.Right : TextAlignment.Left);
            }
            else if (el is BarElement || el is RingElement)
            {
                fe.InvalidateVisual();
            }
            else if (el is ImageElement && fe is Border)
            {
                var image = ((Border)fe).Child as Image;
                var img = (ImageElement)el;
                if (image != null && !Equals(image.Tag, img.FilePath))
                {
                    image.Tag = img.FilePath;
                    image.Source = string.IsNullOrWhiteSpace(img.FilePath) ? null : ImageUtil.Load(img.FilePath);
                }
            }
        }

        private void RefreshVisualPosition(ThemeElement el)
        {
            FrameworkElement fe;
            if (el == null || !_visuals.TryGetValue(el.Id, out fe))
                return;
            Canvas.SetLeft(fe, el.X);
            Canvas.SetTop(fe, el.Y);
        }

        private void RefreshVisualBounds(ThemeElement el)
        {
            FrameworkElement fe;
            if (el == null || !_visuals.TryGetValue(el.Id, out fe))
                return;
            Canvas.SetLeft(fe, el.X);
            Canvas.SetTop(fe, el.Y);
            fe.Width = Math.Max(1, el.Width);
            fe.Height = Math.Max(1, el.Height);
        }

        private void RefreshAllVisuals()
        {
            foreach (var el in _project.Elements)
                RefreshVisual(el);
        }

        // ---------- live data ----------

        private void TimerTick(object sender, EventArgs e)
        {
            _sim.Tick();
            RefreshDataVisuals();
        }

        private void RefreshDataVisuals()
        {
            foreach (var el in _project.Elements)
            {
                if (!el.Visible)
                    continue;
                FrameworkElement fe;
                if (!_visuals.TryGetValue(el.Id, out fe))
                    continue;
                var text = el as TextElement;
                if (text != null && fe is TextBlock)
                {
                    var tb = (TextBlock)fe;
                    string value = text.GetDisplayText(_sim);
                    if (tb.Text != value)
                        tb.Text = value;
                }
                else if (el is BarElement || el is RingElement)
                {
                    fe.InvalidateVisual();
                }
            }
            if (_statusData != null)
            {
                string status = string.Format(CultureInfo.InvariantCulture,
                    "CPU {0}%   GPU {1}%   CPU {2}°C   GPU {3}°C   RAM {4}%   {5}",
                    _sim.GetFormatted(ValueSource.CpuUsage),
                    _sim.GetFormatted(ValueSource.GpuUsage),
                    _sim.GetFormatted(ValueSource.CpuTemp),
                    _sim.GetFormatted(ValueSource.GpuTemp),
                    _sim.GetFormatted(ValueSource.MemoryPercent),
                    DateTime.Now.ToString("HH:mm:ss"));
                if (_statusData.Text != status)
                    _statusData.Text = status;
            }
        }

        // ---------- selection ----------

        private void SelectElement(ThemeElement el)
        {
            _selected = el;
            ClearGuides();
            _updatingList = true;
            try
            {
                if (_elementList != null)
                {
                    _elementList.SelectedItem = null;
                    if (el != null)
                    {
                        foreach (var item in _elementList.Items)
                        {
                            var li = item as ElementListItem;
                            if (li != null && li.Element == el)
                            {
                                _elementList.SelectedItem = li;
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                _updatingList = false;
            }
            BuildProperties();
            UpdateSelectionOverlay();
        }

        private void ElementListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingList)
                return;
            var item = _elementList.SelectedItem as ElementListItem;
            if (item != null)
                SelectElement(item.Element);
        }

        private void UpdateSelectionOverlay()
        {
            if (_selRect == null)
                return;
            if (_selected == null || !_selected.Visible || !_visuals.ContainsKey(_selected.Id))
            {
                _selRect.Visibility = Visibility.Collapsed;
                foreach (var h in _handles.Values)
                    h.Visibility = Visibility.Collapsed;
                return;
            }

            var el = _selected;
            double scale = _stageScaleValue <= 0 ? 1 : _stageScaleValue;
            if (Math.Abs(scale - _overlayScale) > 0.0001)
            {
                _overlayScale = scale;
                _selRect.StrokeThickness = 2.0 / scale;
                foreach (var h in _handles.Values)
                    h.StrokeThickness = 1.0 / Math.Max(0.1, scale);
            }
            _selRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selRect, el.X);
            Canvas.SetTop(_selRect, el.Y);
            _selRect.Width = Math.Max(1, el.Width);
            _selRect.Height = Math.Max(1, el.Height);

            double hs = 10.0 / scale;
            PlaceHandle("nw", el.X, el.Y, hs);
            PlaceHandle("ne", el.X + el.Width, el.Y, hs);
            PlaceHandle("sw", el.X, el.Y + el.Height, hs);
            PlaceHandle("se", el.X + el.Width, el.Y + el.Height, hs);
        }

        private void PlaceHandle(string corner, double x, double y, double size)
        {
            var h = _handles[corner];
            h.Visibility = Visibility.Visible;
            h.Width = size;
            h.Height = size;
            Canvas.SetLeft(h, x - size / 2.0);
            Canvas.SetTop(h, y - size / 2.0);
        }

        private void ClearGuides()
        {
            foreach (var g in _guides)
                g.Visibility = Visibility.Collapsed;
            _guides.Clear();
            _guideIndex = 0;
        }

        private void AddGuide(double x1, double y1, double x2, double y2, double scale)
        {
            Line line;
            if (_guideIndex < _guidePool.Count)
            {
                line = _guidePool[_guideIndex];
            }
            else
            {
                line = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 176, 32)),
                    Opacity = 0.9,
                    StrokeDashArray = new DoubleCollection { 5, 4 },
                    IsHitTestVisible = false
                };
                _overlayCanvas.Children.Add(line);
                _guidePool.Add(line);
            }
            _guideIndex++;
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.StrokeThickness = 1.0 / Math.Max(0.1, scale);
            line.Visibility = Visibility.Visible;
            _guides.Add(line);
        }

        private double[] TargetsX(ThemeElement self)
        {
            var list = new List<double> { 0, _project.Width / 2.0, _project.Width };
            foreach (var e in _project.Elements)
            {
                if (e == self || !e.Visible)
                    continue;
                list.Add(e.X);
                list.Add(e.X + e.Width / 2.0);
                list.Add(e.X + e.Width);
            }
            return list.Distinct().ToArray();
        }

        private double[] TargetsY(ThemeElement self)
        {
            var list = new List<double> { 0, _project.Height / 2.0, _project.Height };
            foreach (var e in _project.Elements)
            {
                if (e == self || !e.Visible)
                    continue;
                list.Add(e.Y);
                list.Add(e.Y + e.Height / 2.0);
                list.Add(e.Y + e.Height);
            }
            return list.Distinct().ToArray();
        }

        private static double SnapDelta(double[] moving, double[] targets, double threshold, out double guide)
        {
            guide = double.NaN;
            double best = threshold;
            double delta = 0;
            foreach (var v in moving)
            {
                foreach (var t in targets)
                {
                    double d = Math.Abs(t - v);
                    if (d <= best)
                    {
                        best = d;
                        delta = t - v;
                        guide = t;
                    }
                }
            }
            return double.IsNaN(guide) ? 0 : delta;
        }

        private static double SnapEdge(double value, double[] targets, double threshold, out double guide)
        {
            guide = double.NaN;
            double best = threshold;
            foreach (var t in targets)
            {
                double d = Math.Abs(t - value);
                if (d <= best)
                {
                    best = d;
                    guide = t;
                }
            }
            return double.IsNaN(guide) ? value : guide;
        }

        // ---------- element mouse interaction ----------

        private void BeginInteraction(bool cacheVisual)
        {
            if (_interactionActive)
                return;
            _interactionActive = true;
            _timer.Stop();
            _videoBg.Pause();
            FrameworkElement fe;
            if (cacheVisual && _selected != null && _visuals.TryGetValue(_selected.Id, out fe))
            {
                _interactionCacheTarget = fe;
                fe.CacheMode = new BitmapCache
                {
                    RenderAtScale = Math.Min(2.0, Math.Max(1.0, _stageScaleValue))
                };
            }
        }

        private void EndInteraction()
        {
            if (!_interactionActive)
                return;
            _interactionActive = false;
            if (_interactionCacheTarget != null)
            {
                _interactionCacheTarget.CacheMode = null;
                _interactionCacheTarget.InvalidateVisual();
                _interactionCacheTarget = null;
            }
            if (_windowActive)
            {
                _videoBg.Resume();
                _timer.Start();
            }
            RefreshDataVisuals();
        }

        private void InteractionLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                _dragTargetsX = null;
                _dragTargetsY = null;
                ClearGuides();
            }
            if (_resizing)
            {
                _resizing = false;
                _dragTargetsX = null;
                _dragTargetsY = null;
                ClearGuides();
            }
            EndInteraction();
        }

        private void ElementMouseDown(object sender, MouseButtonEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe == null)
                return;
            var el = fe.Tag as ThemeElement;
            if (el == null)
                return;
            SelectElement(el);
            BeginInteraction(true);
            ClearGuides();
            _dragging = true;
            _dragTargetsX = TargetsX(el);
            _dragTargetsY = TargetsY(el);
            _dragStart = e.GetPosition(_elementsCanvas);
            _origX = el.X;
            _origY = el.Y;
            fe.CaptureMouse();
            e.Handled = true;
        }

        private void ElementMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _selected == null)
                return;
            var fe = sender as FrameworkElement;
            if (fe == null || !fe.IsMouseCaptured)
                return;
            var p = e.GetPosition(_elementsCanvas);
            double nx = Math.Round(_origX + (p.X - _dragStart.X));
            double ny = Math.Round(_origY + (p.Y - _dragStart.Y));
            double scale = Math.Max(0.1, _stageScaleValue);
            double threshold = 6.0 / scale;

            double gx, gy;
            double dx = SnapDelta(
                new[] { nx, nx + _selected.Width / 2.0, nx + _selected.Width },
                _dragTargetsX, threshold, out gx);
            double dy = SnapDelta(
                new[] { ny, ny + _selected.Height / 2.0, ny + _selected.Height },
                _dragTargetsY, threshold, out gy);
            nx = Math.Round(nx + dx);
            ny = Math.Round(ny + dy);

            _selected.X = nx;
            _selected.Y = ny;
            _dirty = true;
            ClearGuides();
            if (!double.IsNaN(gx))
                AddGuide(gx, 0, gx, _project.Height, scale);
            if (!double.IsNaN(gy))
                AddGuide(0, gy, _project.Width, gy, scale);
            RefreshVisualPosition(_selected);
            UpdateSelectionOverlay();
            SetPropBox("X", nx);
            SetPropBox("Y", ny);
            e.Handled = true;
        }

        private void ElementMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging)
                return;
            _dragging = false;
            _dragTargetsX = null;
            _dragTargetsY = null;
            ClearGuides();
            EndInteraction();
            var fe = sender as FrameworkElement;
            if (fe != null)
                fe.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            var h = sender as Rectangle;
            if (h == null || _selected == null)
                return;
            ClearGuides();
            BeginInteraction(false);
            _resizing = true;
            _resizeCorner = h.Tag as string;
            _dragTargetsX = TargetsX(_selected);
            _dragTargetsY = TargetsY(_selected);
            _dragStart = e.GetPosition(_elementsCanvas);
            _origX = _selected.X;
            _origY = _selected.Y;
            _origW = _selected.Width;
            _origH = _selected.Height;
            h.CaptureMouse();
            e.Handled = true;
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizing || _selected == null)
                return;
            var h = sender as Rectangle;
            if (h == null || !h.IsMouseCaptured)
                return;
            var p = e.GetPosition(_elementsCanvas);
            double dx = p.X - _dragStart.X;
            double dy = p.Y - _dragStart.Y;

            double nx = _origX;
            double ny = _origY;
            double nw = _origW;
            double nh = _origH;

            switch (_resizeCorner)
            {
                case "se":
                    nw = _origW + dx;
                    nh = _origH + dy;
                    break;
                case "ne":
                    nw = _origW + dx;
                    nh = _origH - dy;
                    ny = _origY + dy;
                    break;
                case "sw":
                    nw = _origW - dx;
                    nh = _origH + dy;
                    nx = _origX + dx;
                    break;
                case "nw":
                    nw = _origW - dx;
                    nh = _origH - dy;
                    nx = _origX + dx;
                    ny = _origY + dy;
                    break;
            }

            if (nw < 16) nw = 16;
            if (nh < 16) nh = 16;

            if (_selected is RingElement)
            {
                double size = Math.Max(nw, nh);
                switch (_resizeCorner)
                {
                    case "se":
                        nw = nh = size;
                        break;
                    case "ne":
                        nw = nh = size;
                        ny = _origY + _origH - size;
                        break;
                    case "sw":
                        nw = nh = size;
                        nx = _origX + _origW - size;
                        break;
                    case "nw":
                        nw = nh = size;
                        nx = _origX + _origW - size;
                        ny = _origY + _origH - size;
                        break;
                }
            }
            else if (_selected is ImageElement && ((ImageElement)_selected).KeepAspect && _origH > 0)
            {
                double aspect = _origW / _origH;
                double sizeW = nw;
                double newH = sizeW / aspect;
                switch (_resizeCorner)
                {
                    case "se":
                        nh = newH;
                        break;
                    case "ne":
                        nh = newH;
                        ny = _origY + _origH - nh;
                        break;
                    case "sw":
                        nh = newH;
                        break;
                    case "nw":
                        nh = newH;
                        ny = _origY + _origH - nh;
                        break;
                }
                if (nh < 16)
                {
                    nh = 16;
                    nw = nh * aspect;
                }
            }

            double scale = Math.Max(0.1, _stageScaleValue);
            double threshold = 6.0 / scale;
            double left = nx;
            double right = nx + nw;
            double top = ny;
            double bottom = ny + nh;
            double gx1 = double.NaN, gx2 = double.NaN;
            double gy1 = double.NaN, gy2 = double.NaN;
            double[] tx = _dragTargetsX;
            double[] ty = _dragTargetsY;

            if (_resizeCorner.Contains("w"))
                left = SnapEdge(left, tx, threshold, out gx1);
            if (_resizeCorner.Contains("e"))
                right = SnapEdge(right, tx, threshold, out gx2);
            if (_resizeCorner.Contains("n"))
                top = SnapEdge(top, ty, threshold, out gy1);
            if (_resizeCorner.Contains("s"))
                bottom = SnapEdge(bottom, ty, threshold, out gy2);

            if (right - left < 16)
            {
                if (_resizeCorner.Contains("w"))
                    left = right - 16;
                else
                    right = left + 16;
            }
            if (bottom - top < 16)
            {
                if (_resizeCorner.Contains("n"))
                    top = bottom - 16;
                else
                    bottom = top + 16;
            }

            nx = left;
            ny = top;
            nw = right - left;
            nh = bottom - top;

            _selected.X = Math.Round(nx);
            _selected.Y = Math.Round(ny);
            _selected.Width = Math.Round(nw);
            _selected.Height = Math.Round(nh);
            _dirty = true;
            ClearGuides();
            if (!double.IsNaN(gx1)) AddGuide(gx1, 0, gx1, _project.Height, scale);
            if (!double.IsNaN(gx2)) AddGuide(gx2, 0, gx2, _project.Height, scale);
            if (!double.IsNaN(gy1)) AddGuide(0, gy1, _project.Width, gy1, scale);
            if (!double.IsNaN(gy2)) AddGuide(0, gy2, _project.Width, gy2, scale);
            RefreshVisualBounds(_selected);
            UpdateSelectionOverlay();
            SetPropBox("X", _selected.X);
            SetPropBox("Y", _selected.Y);
            SetPropBox("W", _selected.Width);
            SetPropBox("H", _selected.Height);
            e.Handled = true;
        }

        private void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_resizing)
                return;
            _resizing = false;
            _dragTargetsX = null;
            _dragTargetsY = null;
            ClearGuides();
            EndInteraction();
            var h = sender as Rectangle;
            if (h != null)
                h.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void SetPropBox(string key, double value)
        {
            TextBox tb;
            if (!_propBoxes.TryGetValue(key, out tb))
                return;
            string text = value.ToString("0.##", CultureInfo.InvariantCulture);
            if (tb.Text == text)
                return;
            _suppress = true;
            tb.Text = text;
            _suppress = false;
        }

        // ---------- keyboard ----------

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox)
            {
                if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    SaveProject();
                    e.Handled = true;
                }
                else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    OpenProject();
                    e.Handled = true;
                }
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.S) { SaveProject(); e.Handled = true; return; }
                if (e.Key == Key.O) { OpenProject(); e.Handled = true; return; }
                if (e.Key == Key.N) { NewProject(true); e.Handled = true; return; }
                if (e.Key == Key.D && _selected != null)
                {
                    var clone = _selected.Clone();
                    clone.Z = _project.Elements.Max(x => x.Z) + 1;
                    _project.Elements.Add(clone);
                    CreateVisual(clone);
                    _dirty = true;
                    RefreshElementList();
                    SelectElement(clone);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Delete)
            {
                RemoveSelected();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                SelectElement(null);
                e.Handled = true;
                return;
            }

            if (_selected == null)
                return;
            double step = Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1;
            double dx = 0;
            double dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            if (dx != 0 || dy != 0)
            {
                _selected.X += dx;
                _selected.Y += dy;
                _dirty = true;
                RefreshVisual(_selected);
                UpdateSelectionOverlay();
                SetPropBox("X", _selected.X);
                SetPropBox("Y", _selected.Y);
                e.Handled = true;
            }
        }

        // ---------- z order / removal ----------

        private void NormalizeZ()
        {
            var ordered = _project.Elements.OrderBy(e => e.Z).ToList();
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].Z = i + 1;
            foreach (var el in _project.Elements)
            {
                FrameworkElement fe;
                if (_visuals.TryGetValue(el.Id, out fe))
                    Panel.SetZIndex(fe, (int)Math.Round(el.Z));
            }
            RefreshElementList();
            UpdateSelectionOverlay();
        }

        private void MoveSelectedZ(int direction)
        {
            if (_selected == null)
                return;
            var ordered = _project.Elements.OrderBy(e => e.Z).ThenBy(e => _project.Elements.IndexOf(e)).ToList();
            int index = ordered.IndexOf(_selected);
            int target = index + direction;
            if (target < 0 || target >= ordered.Count)
                return;
            double tmp = ordered[index].Z;
            ordered[index].Z = ordered[target].Z;
            ordered[target].Z = tmp;
            _dirty = true;
            NormalizeZ();
        }

        private void SendSelectedToBack()
        {
            if (_selected == null)
                return;
            _selected.Z = _project.Elements.Min(e => e.Z) - 1;
            _dirty = true;
            NormalizeZ();
        }

        private void SendSelectedToFront()
        {
            if (_selected == null)
                return;
            _selected.Z = _project.Elements.Max(e => e.Z) + 1;
            _dirty = true;
            NormalizeZ();
        }

        private void RemoveSelected()
        {
            if (_selected == null)
                return;
            var el = _selected;
            _project.Elements.Remove(el);
            FrameworkElement fe;
            if (_visuals.TryGetValue(el.Id, out fe))
            {
                _elementsCanvas.Children.Remove(fe);
                _visuals.Remove(el.Id);
            }
            _selected = null;
            _dirty = true;
            RefreshElementList();
            BuildProperties();
            UpdateSelectionOverlay();
            NormalizeZ();
        }

        private void RefreshElementList()
        {
            if (_elementList == null)
                return;
            _updatingList = true;
            try
            {
                _elementList.Items.Clear();
                foreach (var el in _project.Elements.OrderBy(e => e.Z))
                    _elementList.Items.Add(new ElementListItem { Element = el });
                if (_selected != null)
                {
                    foreach (var item in _elementList.Items)
                    {
                        var li = item as ElementListItem;
                        if (li != null && li.Element == _selected)
                        {
                            _elementList.SelectedItem = li;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _updatingList = false;
            }
        }

        // ---------- zoom ----------

        private void ApplyZoom()
        {
            if (_stage == null || _scroller == null || _project == null)
                return;
            double aw = _scroller.ActualWidth - 24;
            double ah = _scroller.ActualHeight - 24;
            _fitScale = 1.0;
            if (aw > 50 && ah > 50 && _project.Width > 0 && _project.Height > 0)
                _fitScale = Math.Min(aw / _project.Width, ah / _project.Height);
            if (_fitScale <= 0)
                _fitScale = 1.0;
            _stageScaleValue = Math.Max(0.05, _fitScale * _zoom);
            _stageScale.ScaleX = _stageScaleValue;
            _stageScale.ScaleY = _stageScaleValue;
            if (_zoomLabel != null)
                _zoomLabel.Text = ((int)Math.Round(_zoom * 100)).ToString(CultureInfo.InvariantCulture) + "%";
            UpdateSelectionOverlay();
        }

        // ---------- properties UI ----------

        private TextBox MakeTextBox(string text)
        {
            return new TextBox
            {
                Text = text ?? "",
                Background = new SolidColorBrush(Color.FromRgb(24, 25, 28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(57, 60, 66)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                CaretBrush = Brushes.White,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private FlatCombo MakeCombo()
        {
            return new FlatCombo();
        }

        private void AddRow(Panel host, string label, UIElement control)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(158, 163, 172)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(control, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(control);
            host.Children.Add(grid);
        }

        private void AddSection(Panel host, string title)
        {
            host.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(52, 54, 59)),
                Margin = new Thickness(0, 10, 0, 10)
            });
            host.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(138, 143, 152)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        private TextBox AddNumber(Panel host, string label, double value, Action<double> setter, string key)
        {
            var tb = MakeTextBox(value.ToString("0.##", CultureInfo.InvariantCulture));
            tb.TextChanged += delegate
            {
                if (_suppress)
                    return;
                double v;
                string text = tb.Text.Replace(',', '.');
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    setter(v);
                    AfterElementChange();
                }
            };
            AddRow(host, label, tb);
            if (!string.IsNullOrEmpty(key))
                _propBoxes[key] = tb;
            return tb;
        }

        private TextBox AddString(Panel host, string label, string value, Action<string> setter)
        {
            var tb = MakeTextBox(value);
            tb.TextChanged += delegate
            {
                if (_suppress)
                    return;
                setter(tb.Text);
                AfterElementChange();
            };
            AddRow(host, label, tb);
            return tb;
        }

        private CheckBox AddCheck(Panel host, string label, bool value, Action<bool> setter)
        {
            var cb = new CheckBox
            {
                IsChecked = value,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.Checked += delegate { if (!_suppress) { setter(true); AfterElementChange(); } };
            cb.Unchecked += delegate { if (!_suppress) { setter(false); AfterElementChange(); } };
            AddRow(host, label, cb);
            return cb;
        }

        private void AddColorRow(Panel host, string label, string value, Action<string> setter)
        {
            var tb = MakeTextBox(value);
            var swatch = new Button
            {
                Width = 32,
                Height = 24,
                Margin = new Thickness(6, 0, 0, 0),
                Background = ColorUtil.Brush(value),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 73, 80)),
                BorderThickness = new Thickness(1),
                Style = _buttonStyle,
                Padding = new Thickness(0),
                ToolTip = "Выбрать цвет"
            };
            swatch.Click += delegate
            {
                var dlg = new ColorPickerWindow(ColorUtil.Parse(tb.Text)) { Owner = this };
                if (dlg.ShowDialog() == true)
                    tb.Text = ColorUtil.ToHex(dlg.SelectedColor);
            };
            tb.TextChanged += delegate
            {
                if (_suppress)
                    return;
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(tb.Text.Trim());
                    swatch.Background = ColorUtil.Brush(c);
                    setter(ColorUtil.ToHex(c));
                    AfterElementChange();
                }
                catch
                {
                }
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(tb);
            Grid.SetColumn(swatch, 1);
            grid.Children.Add(swatch);
            AddRow(host, label, grid);
        }

        private void AddSourceRow(Panel host, ValueSource current, Action<ValueSource> setter)
        {
            var cb = MakeCombo();
            foreach (var s in DataCatalog.AllSources)
                cb.Items.Add(new SourceItem { Value = s, Label = DataCatalog.Label(s) });
            foreach (var item in cb.Items)
            {
                var si = item as SourceItem;
                if (si != null && si.Value == current)
                {
                    cb.SelectedItem = si;
                    break;
                }
            }
            cb.SelectionChanged += delegate
            {
                if (_suppress)
                    return;
                var si = cb.SelectedItem as SourceItem;
                if (si != null)
                {
                    setter(si.Value);
                    AfterElementChange();
                }
            };
            AddRow(host, "Данные", cb);
        }

        private void AddFontRow(Panel host, TextElement t)
        {
            var cb = MakeCombo();
            var choices = new List<FontChoice>(FontUtil.GetSystemFonts());
            if (!string.IsNullOrWhiteSpace(t.FontFile))
            {
                bool exists = choices.Any(c => string.Equals(c.File, t.FontFile, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                    choices.Insert(0, new FontChoice { Display = t.FontFamily + " (свой)", Family = t.FontFamily, File = t.FontFile });
            }
            foreach (var c in choices)
                cb.Items.Add(c);
            FontChoice selected = null;
            foreach (var item in cb.Items)
            {
                var fc = item as FontChoice;
                if (fc != null && string.Equals(fc.Family, t.FontFamily, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(fc.File ?? "", t.FontFile ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    selected = fc;
                    break;
                }
            }
            if (selected == null)
            {
                foreach (var item in cb.Items)
                {
                    var fc = item as FontChoice;
                    if (fc != null && string.Equals(fc.Family, t.FontFamily, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = fc;
                        break;
                    }
                }
            }
            cb.SelectedItem = selected;
            cb.SelectionChanged += delegate
            {
                if (_suppress)
                    return;
                var fc = cb.SelectedItem as FontChoice;
                if (fc != null)
                {
                    t.FontFamily = fc.Family;
                    t.FontFile = fc.File;
                    AfterElementChange();
                }
            };

            var import = new Button
            {
                Content = "…",
                Width = 30,
                Margin = new Thickness(5, 0, 0, 0),
                Style = _buttonStyle,
                Padding = new Thickness(0),
                ToolTip = "Загрузить свой шрифт (TTF/OTF)"
            };
            import.Click += delegate
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Выберите файл шрифта",
                    Filter = "Шрифты|*.ttf;*.otf;*.ttc|Все файлы|*.*"
                };
                if (ofd.ShowDialog(this) != true)
                    return;
                string family = FontUtil.ReadFamilyName(ofd.FileName);
                if (string.IsNullOrWhiteSpace(family))
                    family = Path.GetFileNameWithoutExtension(ofd.FileName);
                string stored = FontUtil.ImportFont(ofd.FileName, _fontStorageDir);
                if (stored == null)
                {
                    MessageBox.Show(this, "Не удалось загрузить шрифт.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                t.FontFamily = family;
                t.FontFile = stored;
                _dirty = true;
                AfterElementChange();
                BuildProperties();
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(cb);
            Grid.SetColumn(import, 1);
            grid.Children.Add(import);
            AddRow(host, "Шрифт", grid);
        }

        private void AfterElementChange()
        {
            if (_suppress)
                return;
            _dirty = true;
            if (_selected != null)
            {
                RefreshVisual(_selected);
                UpdateSelectionOverlay();
            }
            UpdateTitle();
        }

        private void BuildProperties()
        {
            if (_propsHost == null)
                return;
            _suppress = true;
            _propBoxes.Clear();
            _propsHost.Children.Clear();
            try
            {
                if (_selected == null)
                    BuildProjectProperties();
                else
                    BuildElementProperties(_selected);
            }
            finally
            {
                _suppress = false;
            }
            UpdateTitle();
        }

        private void BuildProjectProperties()
        {
            _propsHost.Children.Add(new TextBlock
            {
                Text = "Тема",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var nameBox = MakeTextBox(_project.ThemeName);
            nameBox.TextChanged += delegate
            {
                if (_suppress)
                    return;
                _project.ThemeName = nameBox.Text;
                _dirty = true;
                UpdateTitle();
            };
            AddRow(_propsHost, "Название", nameBox);

            AddNumber(_propsHost, "Ширина", _project.Width, delegate(double v)
            {
                _project.Width = Math.Max(100, v);
                ApplyProjectToStage();
            }, null);
            AddNumber(_propsHost, "Высота", _project.Height, delegate(double v)
            {
                _project.Height = Math.Max(100, v);
                ApplyProjectToStage();
            }, null);

            AddSection(_propsHost, "ФОН");
            var typeCombo = MakeCombo();
            typeCombo.Items.Add(new ComboBoxItem { Content = "Без фона", Tag = BackgroundKind.None });
            typeCombo.Items.Add(new ComboBoxItem { Content = "Картинка", Tag = BackgroundKind.Image });
            typeCombo.Items.Add(new ComboBoxItem { Content = "Видео", Tag = BackgroundKind.Video });
            foreach (var item in typeCombo.Items)
            {
                var ci = item as ComboBoxItem;
                if (ci != null && (BackgroundKind)ci.Tag == _project.Background.Kind)
                {
                    typeCombo.SelectedItem = ci;
                    break;
                }
            }
            typeCombo.SelectionChanged += delegate
            {
                if (_suppress)
                    return;
                var ci = typeCombo.SelectedItem as ComboBoxItem;
                if (ci == null)
                    return;
                _project.Background.Kind = (BackgroundKind)ci.Tag;
                _dirty = true;
                ApplyBackground();
                Dispatcher.BeginInvoke(new Action(BuildProperties));
            };
            AddRow(_propsHost, "Тип", typeCombo);

            if (_project.Background.Kind != BackgroundKind.None)
            {
                string path = _project.Background.Kind == BackgroundKind.Video
                    ? _project.Background.VideoPath
                    : _project.Background.ImagePath;
                var pathText = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(path) ? "Файл не выбран" : Path.GetFileName(path),
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 203, 210)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = path
                };
                var choose = new Button
                {
                    Content = "Выбрать…",
                    Style = _buttonStyle,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(6, 0, 0, 0)
                };
                choose.Click += delegate { ChooseBackgroundFile(); };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.Children.Add(pathText);
                Grid.SetColumn(choose, 1);
                grid.Children.Add(choose);
                AddRow(_propsHost, "Файл", grid);

                var fitCombo = MakeCombo();
                fitCombo.Items.Add(new ComboBoxItem { Content = "Заполнить (обрезка)", Tag = BackgroundFit.Cover });
                fitCombo.Items.Add(new ComboBoxItem { Content = "Вписать", Tag = BackgroundFit.Contain });
                fitCombo.Items.Add(new ComboBoxItem { Content = "Растянуть", Tag = BackgroundFit.Stretch });
                foreach (var item in fitCombo.Items)
                {
                    var ci = item as ComboBoxItem;
                    if (ci != null && (BackgroundFit)ci.Tag == _project.Background.Fit)
                    {
                        fitCombo.SelectedItem = ci;
                        break;
                    }
                }
                fitCombo.SelectionChanged += delegate
                {
                    if (_suppress)
                        return;
                    var ci = fitCombo.SelectedItem as ComboBoxItem;
                    if (ci == null)
                        return;
                    _project.Background.Fit = (BackgroundFit)ci.Tag;
                    _dirty = true;
                    ApplyBackground();
                };
                AddRow(_propsHost, "Режим", fitCombo);
            }

            AddColorRow(_propsHost, "Цвет фона", _project.Background.Color, delegate(string c)
            {
                _project.Background.Color = c;
                ApplyBackground();
            });

            _propsHost.Children.Add(new TextBlock
            {
                Text = "Для видео лучше MP4 (H.264): при экспорте оно станет default.mp4. Картинка сохраняется как back.png нужного размера.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });

            AddSection(_propsHost, "NACODEX");
            var rootText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_nacodexRoot) ? "Не найден" : _nacodexRoot,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 203, 210)),
                FontSize = 11,
                ToolTip = _nacodexRoot
            };
            _propsHost.Children.Add(rootText);
            var changeRoot = new Button
            {
                Content = "Изменить папку…",
                Style = _buttonStyle,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0)
            };
            changeRoot.Click += delegate { ChooseNacodexRoot(); BuildProperties(); };
            _propsHost.Children.Add(changeRoot);

            AddSection(_propsHost, "ДЕЙСТВИЯ");
            var exportBtn = new Button
            {
                Content = "Экспорт темы…",
                Style = _buttonStyle,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 6)
            };
            exportBtn.Click += delegate { ExportTheme(); };
            _propsHost.Children.Add(exportBtn);

            var installBtn = new Button
            {
                Content = "Установить в NACODEX",
                Style = _accentButtonStyle,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 7, 10, 7)
            };
            installBtn.Click += delegate { InstallToNacodex(); };
            _propsHost.Children.Add(installBtn);

            _propsHost.Children.Add(new TextBlock
            {
                Text = "Совет: элементы можно перетаскивать мышью, а угловые маркеры менять их размер.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                FontSize = 11,
                Margin = new Thickness(0, 12, 0, 0)
            });
        }

        private void BuildElementProperties(ThemeElement el)
        {
            _propsHost.Children.Add(new TextBlock
            {
                Text = el.KindName,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            });

            AddString(_propsHost, "Имя", el.Name, delegate(string v)
            {
                el.Name = v;
                RefreshElementList();
            });
            AddNumber(_propsHost, "X", el.X, delegate(double v) { el.X = v; }, "X");
            AddNumber(_propsHost, "Y", el.Y, delegate(double v) { el.Y = v; }, "Y");

            if (el is RingElement)
            {
                AddNumber(_propsHost, "Диаметр", el.Width, delegate(double v)
                {
                    el.Width = Math.Max(16, v);
                    el.Height = el.Width;
                }, "W");
            }
            else
            {
                AddNumber(_propsHost, "Ширина", el.Width, delegate(double v) { el.Width = Math.Max(1, v); }, "W");
                AddNumber(_propsHost, "Высота", el.Height, delegate(double v) { el.Height = Math.Max(1, v); }, "H");
            }

            AddNumber(_propsHost, "Z-порядок", el.Z, delegate(double v)
            {
                el.Z = v;
                RefreshVisual(el);
                RefreshElementList();
            }, "Z");
            AddCheck(_propsHost, "Видимость", el.Visible, delegate(bool v)
            {
                el.Visible = v;
                RefreshElementList();
            });

            var text = el as TextElement;
            if (text != null)
            {
                AddSection(_propsHost, "ТЕКСТ");
                AddSourceRow(_propsHost, text.Source, delegate(ValueSource v) { text.Source = v; });
                AddString(_propsHost, "Статичный", text.StaticText, delegate(string v) { text.StaticText = v; });
                AddString(_propsHost, "Префикс", text.Prefix, delegate(string v) { text.Prefix = v; });
                AddString(_propsHost, "Суффикс", text.Suffix, delegate(string v) { text.Suffix = v; });

                AddFontRow(_propsHost, text);
                AddNumber(_propsHost, "Размер", text.FontSize, delegate(double v) { text.FontSize = Math.Max(1, v); }, null);

                var stylePanel = new StackPanel { Orientation = Orientation.Horizontal };
                AddInlineCheck(stylePanel, "Ж", text.Bold, delegate(bool v) { text.Bold = v; });
                AddInlineCheck(stylePanel, "К", text.Italic, delegate(bool v) { text.Italic = v; });
                AddInlineCheck(stylePanel, "Ч", text.Underline, delegate(bool v) { text.Underline = v; });
                stylePanel.Children.Add(new TextBlock
                {
                    Text = "только предпросмотр",
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                    FontSize = 10,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                AddRow(_propsHost, "Стиль", stylePanel);

                var autoColorCheck = AddCheck(_propsHost, "Авт. цвет", text.AutoColor, delegate(bool v)
                {
                    text.AutoColor = v;
                    if (v)
                        ApplyAutomaticTextColor(text);
                });
                AddColorRow(_propsHost, "Цвет", text.Color, delegate(string c)
                {
                    text.Color = c;
                    text.AutoColor = false;
                    if (autoColorCheck != null)
                        autoColorCheck.IsChecked = false;
                });
                AddColorRow(_propsHost, "Целевой цвет", text.AutoColorTarget ?? text.Color, delegate(string c)
                {
                    text.AutoColorTarget = c;
                    if (text.AutoColor)
                        ApplyAutomaticTextColor(text);
                });

                var autoColorPanel = new StackPanel { Orientation = Orientation.Horizontal };
                AddSmallButton(autoColorPanel, "Подстроить под фон", delegate
                {
                    text.AutoColor = true;
                    if (autoColorCheck != null && autoColorCheck.IsChecked != true)
                        autoColorCheck.IsChecked = true;
                    else
                        ApplyAutomaticTextColor(text);
                    AfterElementChange();
                });
                AddRow(_propsHost, "Подстройка", autoColorPanel);

                var align = MakeCombo();
                align.Items.Add(new ComboBoxItem { Content = "Слева", Tag = 0 });
                align.Items.Add(new ComboBoxItem { Content = "По центру", Tag = 1 });
                align.Items.Add(new ComboBoxItem { Content = "Справа", Tag = 2 });
                foreach (var item in align.Items)
                {
                    var ci = item as ComboBoxItem;
                    if (ci != null && (int)ci.Tag == text.TextAlign)
                    {
                        align.SelectedItem = ci;
                        break;
                    }
                }
                align.SelectionChanged += delegate
                {
                    if (_suppress)
                        return;
                    var ci = align.SelectedItem as ComboBoxItem;
                    if (ci != null)
                    {
                        text.TextAlign = (int)ci.Tag;
                        AfterElementChange();
                    }
                };
                AddRow(_propsHost, "Выравнивание", align);

                _propsHost.Children.Add(new TextBlock
                {
                    Text = "Если выбрано «Статичный текст», используется поле «Статичный». Для датчиков NACODEX сам подставит значение, а префикс и суффикс добавятся вокруг.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            var bar = el as BarElement;
            if (bar != null)
            {
                AddSection(_propsHost, "ПОЛОСА");
                AddSourceRow(_propsHost, bar.Source, delegate(ValueSource v) { bar.Source = v; });
                AddNumber(_propsHost, "Максимум", bar.MaxValue, delegate(double v) { bar.MaxValue = Math.Max(1, v); }, null);
                AddColorRow(_propsHost, "Заполнение", bar.Fill, delegate(string c) { bar.Fill = c; });
                AddColorRow(_propsHost, "Фон", bar.Track, delegate(string c) { bar.Track = c; });
                AddColorRow(_propsHost, "Рамка", bar.BorderColor, delegate(string c) { bar.BorderColor = c; });
                AddNumber(_propsHost, "Толщина рамки", bar.BorderThickness, delegate(double v) { bar.BorderThickness = Math.Max(0, v); }, null);
                AddNumber(_propsHost, "Скругление", bar.CornerRadius, delegate(double v) { bar.CornerRadius = Math.Max(0, v); }, null);
                AddCheck(_propsHost, "Анимация", bar.Animated, delegate(bool v) { bar.Animated = v; });

                _propsHost.Children.Add(new TextBlock
                {
                    Text = "Ориентация полосы определяется её размерами: шире — заполняется слева направо, выше — снизу вверх.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 155, 165)),
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            var ring = el as RingElement;
            if (ring != null)
            {
                AddSection(_propsHost, "КОЛЬЦО");
                AddSourceRow(_propsHost, ring.Source, delegate(ValueSource v) { ring.Source = v; });
                AddNumber(_propsHost, "Максимум", ring.MaxValue, delegate(double v) { ring.MaxValue = Math.Max(1, v); }, null);
                AddNumber(_propsHost, "Толщина", ring.Thickness, delegate(double v) { ring.Thickness = Math.Max(1, v); }, null);
                AddColorRow(_propsHost, "Прогресс", ring.Fill, delegate(string c) { ring.Fill = c; });
                AddColorRow(_propsHost, "Фон", ring.Track, delegate(string c) { ring.Track = c; });
            }

            var img = el as ImageElement;
            if (img != null)
            {
                AddSection(_propsHost, "КАРТИНКА");
                var pathText = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(img.FilePath) ? "Файл не выбран" : Path.GetFileName(img.FilePath),
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 203, 210)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = img.FilePath
                };
                _propsHost.Children.Add(pathText);
                var choose = new Button
                {
                    Content = "Выбрать картинку…",
                    Style = _buttonStyle,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 6, 0, 6)
                };
                choose.Click += delegate
                {
                    var ofd = new OpenFileDialog
                    {
                        Title = "Выберите картинку",
                        Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*"
                    };
                    if (ofd.ShowDialog(this) == true)
                    {
                        img.FilePath = ofd.FileName;
                        _dirty = true;
                        AfterElementChange();
                        BuildProperties();
                    }
                };
                _propsHost.Children.Add(choose);

                var byFile = new Button
                {
                    Content = "Размер как у файла",
                    Style = _buttonStyle,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                byFile.Click += delegate
                {
                    var size = ImageUtil.GetPixelSize(img.FilePath);
                    if (size.Width > 0 && size.Height > 0)
                    {
                        img.Width = size.Width;
                        img.Height = size.Height;
                        _dirty = true;
                        AfterElementChange();
                        BuildProperties();
                    }
                };
                _propsHost.Children.Add(byFile);

                AddCheck(_propsHost, "Пропорции", img.KeepAspect, delegate(bool v) { img.KeepAspect = v; });
            }

            AddSection(_propsHost, "");
            var remove = new Button
            {
                Content = "Удалить элемент",
                Style = _dangerButtonStyle,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            remove.Click += delegate { RemoveSelected(); };
            _propsHost.Children.Add(remove);
        }

        private void AddInlineCheck(Panel host, string label, bool value, Action<bool> setter)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = value,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.Checked += delegate { if (!_suppress) { setter(true); AfterElementChange(); } };
            cb.Unchecked += delegate { if (!_suppress) { setter(false); AfterElementChange(); } };
            host.Children.Add(cb);
        }

        // ---------- save / open / export ----------

        private bool SaveProject()
        {
            if (_project == null)
                return false;
            string path = _projectPath;
            if (string.IsNullOrEmpty(path))
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Сохранить проект",
                    Filter = "VMAX проект|*.vmaxproj|Все файлы|*.*",
                    FileName = ThemeExporter.SanitizeName(_project.ThemeName) + ".vmaxproj"
                };
                if (sfd.ShowDialog(this) != true)
                    return false;
                path = sfd.FileName;
            }
            try
            {
                var serializer = new XmlSerializer(typeof(ThemeProject));
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    serializer.Serialize(fs, _project);
                _projectPath = path;
                _dirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить проект:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void OpenProject()
        {
            if (!ConfirmDiscardChanges())
                return;
            var ofd = new OpenFileDialog
            {
                Title = "Открыть проект",
                Filter = "VMAX проект|*.vmaxproj|Все файлы|*.*"
            };
            if (ofd.ShowDialog(this) != true)
                return;
            try
            {
                var serializer = new XmlSerializer(typeof(ThemeProject));
                ThemeProject loaded;
                using (var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read))
                    loaded = (ThemeProject)serializer.Deserialize(fs);
                if (loaded.Elements == null)
                    loaded.Elements = new List<ThemeElement>();
                if (loaded.Background == null)
                    loaded.Background = new BackgroundSettings();
                if (loaded.Width <= 0) loaded.Width = 1920;
                if (loaded.Height <= 0) loaded.Height = 462;
                _project = loaded;
                _projectPath = ofd.FileName;
                _selected = null;
                _dirty = false;
                ApplyProjectToStage();
                BuildProperties();
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть проект:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ConfirmDiscardChanges()
        {
            if (!_dirty)
                return true;
            var result = MessageBox.Show(this, "Сохранить изменения перед продолжением?",
                "VMAX Theme Studio", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
                return false;
            if (result == MessageBoxResult.Yes)
                return SaveProject();
            return true;
        }

        private bool ValidateProject()
        {
            if (_project == null)
                return false;
            if (string.IsNullOrWhiteSpace(_project.ThemeName))
            {
                MessageBox.Show(this, "Введите название темы.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (_project.Elements.Count(e => e.Visible) == 0 && _project.Background.Kind == BackgroundKind.None)
            {
                if (MessageBox.Show(this, "Тема пустая (нет видимых элементов и фона). Всё равно продолжить?",
                    "Проверка", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return false;
            }
            return true;
        }

        private void ExportTheme()
        {
            if (!ValidateProject())
                return;
            var dlg = new WinForms.SaveFileDialog
            {
                Title = "Сохранить пакет темы",
                Filter = "Пакет темы VMAX (*.vmax)|*.vmax",
                DefaultExt = "vmax",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = ThemeExporter.SanitizeName(_project.ThemeName) + ".vmax"
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK)
                return;
            var result = ThemeExporter.ExportVmax(_project, dlg.FileName);
            ShowExportResult(result, false);
        }

        private void ShowExportResult(ExportResult result, bool install)
        {
            if (!result.Ok)
            {
                MessageBox.Show(this, "Ошибка:\n" + result.Error, "VMAX Theme Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string savedPath = string.IsNullOrEmpty(result.PackagePath) ? result.ThemeDir : result.PackagePath;
            string message = (install ? "Тема установлена:\n" : "Пакет темы сохранён:\n") + savedPath;
            if (result.Warnings.Count > 0)
            {
                var warnings = result.Warnings.Distinct().Take(8).ToArray();
                message += "\n\nПредупреждения:\n• " + string.Join("\n• ", warnings);
            }
            if (install)
            {
                if (MessageBox.Show(this, message + "\n\nЗапустить NACODEX сейчас?", "Готово",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    StartNacodex();
            }
            else
            {
                if (MessageBox.Show(this, message + "\n\nОткрыть папку с файлом?", "Экспорт завершён",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    OpenInExplorer(Path.GetDirectoryName(savedPath));
            }
        }

        private void InstallToNacodex()
        {
            if (!ValidateProject())
                return;
            string root = GetNacodexRoot();
            if (string.IsNullOrWhiteSpace(root))
                return;

            var processes = Process.GetProcessesByName("NACODEX");
            if (processes.Length > 0)
            {
                if (MessageBox.Show(this, "NACODEX запущен. Закрыть его, чтобы установить тему?",
                    "Установка", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                foreach (var p in processes)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                    catch
                    {
                    }
                }
            }

            string target = Path.Combine(root, "Programme", ThemeExporter.SanitizeName(_project.ThemeName));
            if (Directory.Exists(target))
            {
                if (MessageBox.Show(this, "Тема с таким названием уже существует. Заменить её?",
                    "Установка", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            var result = ThemeExporter.Install(_project, root);
            ShowExportResult(result, true);
        }

        private string GetNacodexRoot()
        {
            if (IsNacodexRoot(_nacodexRoot))
                return _nacodexRoot;
            _nacodexRoot = ThemeExporter.FindNacodexRoot();
            if (IsNacodexRoot(_nacodexRoot))
            {
                SaveConfig();
                return _nacodexRoot;
            }
            return ChooseNacodexRoot();
        }

        private string ChooseNacodexRoot()
        {
            var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Выберите папку NACODEX (в ней должна быть папка Programme)",
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK)
                return null;
            if (!IsNacodexRoot(dlg.SelectedPath))
            {
                MessageBox.Show(this, "В выбранной папке нет подпапки Programme.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            _nacodexRoot = dlg.SelectedPath;
            SaveConfig();
            return _nacodexRoot;
        }

        private static bool IsNacodexRoot(string root)
        {
            return !string.IsNullOrWhiteSpace(root) && Directory.Exists(Path.Combine(root, "Programme"));
        }

        private void StartNacodex()
        {
            string root = GetNacodexRoot();
            if (string.IsNullOrWhiteSpace(root))
                return;
            string exe = Path.Combine(root, "bin", "Release", "NACODEX.exe");
            if (File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось запустить NACODEX:\n" + ex.Message, "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(this, "NACODEX.exe не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenInExplorer(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true });
            }
            catch
            {
            }
        }

        // ---------- config / closing ----------

        private void LoadConfig()
        {
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VmaxThemeStudio", "settings.ini");
            try
            {
                if (File.Exists(_configPath))
                {
                    foreach (var line in File.ReadAllLines(_configPath))
                    {
                        int idx = line.IndexOf('=');
                        if (idx <= 0)
                            continue;
                        string key = line.Substring(0, idx).Trim();
                        string value = line.Substring(idx + 1).Trim();
                        if (string.Equals(key, "NacodexPath", StringComparison.OrdinalIgnoreCase))
                            _nacodexRoot = value;
                    }
                }
            }
            catch
            {
            }
            if (!IsNacodexRoot(_nacodexRoot))
                _nacodexRoot = ThemeExporter.FindNacodexRoot();
        }

        private void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_configPath, "NacodexPath=" + (_nacodexRoot ?? "") + "\r\n");
            }
            catch
            {
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ConfirmDiscardChanges())
                e.Cancel = true;
            else
            {
                try { _timer.Stop(); } catch { }
                try { _videoBg.SetVideo(null, BackgroundFit.Cover); } catch { }
            }
        }
    }
}
