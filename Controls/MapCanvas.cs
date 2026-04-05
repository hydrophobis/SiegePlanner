using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using R6Planner.Data;
using R6Planner.Models;
using R6Planner.ViewModels;

namespace R6Planner.Controls
{
    public class MapCanvas : Canvas
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(MapCanvas),
                new PropertyMetadata(null, OnVmChanged));

        public MainViewModel? ViewModel
        {
            get => (MainViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private static void OnVmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (MapCanvas)d;
            if (e.OldValue is MainViewModel old)
            {
                old.PropertyChanged -= c.Vm_PropertyChanged;
                old.VisibleTokens.CollectionChanged      -= c.Collection_Changed;
                old.VisibleAnnotations.CollectionChanged -= c.Collection_Changed;
                old.VisibleLoSLines.CollectionChanged    -= c.Collection_Changed;
                old.VisibleDefaultCams.CollectionChanged -= c.Collection_Changed;
                old.VisibleSpawnPoints.CollectionChanged -= c.Collection_Changed;
            }
            if (e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += c.Vm_PropertyChanged;
                vm.VisibleTokens.CollectionChanged      += c.Collection_Changed;
                vm.VisibleAnnotations.CollectionChanged += c.Collection_Changed;
                vm.VisibleLoSLines.CollectionChanged    += c.Collection_Changed;
                vm.VisibleDefaultCams.CollectionChanged += c.Collection_Changed;
                vm.VisibleSpawnPoints.CollectionChanged += c.Collection_Changed;
                c.Redraw();
            }
        }

        private void Vm_PropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedMap)
                or nameof(MainViewModel.SelectedFloor)
                or nameof(MainViewModel.ActiveTool))
            {
                Redraw();
            }
        }

        private void Collection_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => Redraw();

        private Image _mapImage = new() { Stretch = Stretch.Uniform };
        private double _mapW = 1, _mapH = 1;
        private Rect _mapRect = new(0, 0, 1, 1);
        private double _zoom = 1.0;
        private Point _pan = new(0, 0);
        private bool _isPanning;
        private Point _panStart;

        private bool _isDrawing;
        private bool _isErasing;
        private Point _drawStart;
        private Point _lastPoint;
        private readonly List<Point> _freehandPoints = new();
        private Line? _ghostLine;
        private Rectangle? _ghostRect;

        private UIElement? _dragging;
        private string? _draggingId;
        private Point _dragOffset;
        private Point _dragInitialTokenPos;
        private Point _dragInitialMousePos;

        private string? _selectedId;

        public MapCanvas()
        {
            Background     = Brushes.Transparent;
            ClipToBounds   = true;
            Focusable      = true;

            Children.Add(_mapImage);
            SetLeft(_mapImage, 0);
            SetTop(_mapImage, 0);

            SizeChanged += (_, _) => Redraw();
        }

        private Point ToNorm(Point canvas)
        {
            if (_mapRect.Width <= 0 || _mapRect.Height <= 0)
                return new(canvas.X / Math.Max(ActualWidth, 1), canvas.Y / Math.Max(ActualHeight, 1));

            var x = (canvas.X - _mapRect.X) / _mapRect.Width;
            var y = (canvas.Y - _mapRect.Y) / _mapRect.Height;
            return new(Math.Clamp(x, 0.0, 1.0), Math.Clamp(y, 0.0, 1.0));
        }

        private Point ToCanvas(double normX, double normY)
        {
            if (_mapRect.Width <= 0 || _mapRect.Height <= 0)
                return new(normX * ActualWidth, normY * ActualHeight);

            return new(_mapRect.X + normX * _mapRect.Width,
                       _mapRect.Y + normY * _mapRect.Height);
        }

        private Point ToCanvas(Point norm) => ToCanvas(norm.X, norm.Y);

        private void Redraw()
        {
            var vm = ViewModel;

            for (int i = Children.Count - 1; i >= 1; i--)
                Children.RemoveAt(i);

            if (vm == null) return;

            var floor = vm.SelectedMap?.Floors.ElementAtOrDefault(vm.SelectedFloor);
            if (floor != null)
            {
                var path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "Maps", floor.ImagePath);
                if (System.IO.File.Exists(path))
                {
                    var bmp = new BitmapImage(new Uri(path, UriKind.Absolute));
                    _mapW = bmp.PixelWidth;
                    _mapH = bmp.PixelHeight;
                    _mapImage.Source = bmp;
                    var baseRect = CalculateImageRect();
                    _mapRect = new Rect(
                        baseRect.X + _pan.X,
                        baseRect.Y + _pan.Y,
                        baseRect.Width * _zoom,
                        baseRect.Height * _zoom);
                    _mapImage.Width  = _mapRect.Width;
                    _mapImage.Height = _mapRect.Height;
                    SetLeft(_mapImage, _mapRect.X);
                    SetTop(_mapImage,  _mapRect.Y);
                }
                else
                {
                    _mapImage.Source = null;
                    _mapRect = new Rect(0, 0, ActualWidth, ActualHeight);
                    _mapImage.Width  = ActualWidth;
                    _mapImage.Height = ActualHeight;
                    SetLeft(_mapImage, 0);
                    SetTop(_mapImage,  0);
                    DrawPlaceholderGrid();
                }
            }
            else
            {
                _mapImage.Source = null;
                _mapRect = new Rect(0, 0, ActualWidth, ActualHeight);
                _mapImage.Width  = ActualWidth;
                _mapImage.Height = ActualHeight;
                SetLeft(_mapImage, 0);
                SetTop(_mapImage,  0);
            }

            foreach (var los in vm.VisibleLoSLines)
                DrawLoSLine(los);

            foreach (var ann in vm.VisibleAnnotations)
                DrawAnnotation(ann);

            foreach (var cam in vm.VisibleDefaultCams)
                DrawDefaultCamera(cam);

            foreach (var spawn in vm.VisibleSpawnPoints)
                DrawSpawnPoint(spawn);

            foreach (var tok in vm.VisibleTokens)
                DrawToken(tok);
        }

        private Rect CalculateImageRect()
        {
            if (_mapW <= 0 || _mapH <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
                return new Rect(0, 0, ActualWidth, ActualHeight);

            var imageRatio = _mapW / _mapH;
            var canvasRatio = ActualWidth / ActualHeight;

            if (canvasRatio > imageRatio)
            {
                var height = ActualHeight;
                var width = height * imageRatio;
                return new Rect((ActualWidth - width) / 2, 0, width, height);
            }
            else
            {
                var width = ActualWidth;
                var height = width / imageRatio;
                return new Rect(0, (ActualHeight - height) / 2, width, height);
            }
        }

        private void SetZoom(double zoom, Point anchor)
        {
            const double minZoom = 0.1;
            const double maxZoom = 10.0;

            var newZoom = Math.Clamp(zoom, minZoom, maxZoom);
            if (Math.Abs(newZoom - _zoom) < 1e-6) return;

            if (_mapRect.Width > 0 && _mapRect.Height > 0)
            {
                var imagePt = new Point(
                    (anchor.X - _mapRect.X) / _mapRect.Width,
                    (anchor.Y - _mapRect.Y) / _mapRect.Height);
                imagePt = new Point(Math.Clamp(imagePt.X, 0.0, 1.0), Math.Clamp(imagePt.Y, 0.0, 1.0));

                var baseRect = CalculateImageRect();
                var newWidth = baseRect.Width * newZoom;
                var newHeight = baseRect.Height * newZoom;

                _pan = new Point(
                    anchor.X - baseRect.X - imagePt.X * newWidth,
                    anchor.Y - baseRect.Y - imagePt.Y * newHeight);
            }

            _zoom = newZoom;
            Redraw();
        }

        private void DrawPlaceholderGrid()
        {
            var rect = new Rectangle
            {
                Width           = ActualWidth,
                Height          = ActualHeight,
                Fill            = new SolidColorBrush(Color.FromRgb(20, 25, 35)),
                StrokeThickness = 1,
                Stroke          = new SolidColorBrush(Color.FromRgb(50, 60, 80))
            };
            Children.Add(rect);
            SetLeft(rect, 0); SetTop(rect, 0);

            var tb = new TextBlock
            {
                Text       = "Place map image in Assets/Maps/\n(see MapDatabase.cs)",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 160)),
                FontSize   = 16,
                TextAlignment = TextAlignment.Center
            };
            Children.Add(tb);
            SetLeft(tb, ActualWidth / 2 - 150);
            SetTop(tb,  ActualHeight / 2 - 20);
        }

        private static Brush ParseBrush(string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return Brushes.White; }
        }

        private void DrawLoSLine(LoSLine los)
        {
            var p1 = ToCanvas(los.StartNorm);
            var p2 = ToCanvas(los.EndNorm);
            bool isCurrentFloor = los.FloorIndex == ViewModel?.SelectedFloor;
            double opacity = isCurrentFloor ? 0.85 : 0.4;
            var line = new Line
            {
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                Stroke          = ParseBrush(los.Color),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection(new[] { 6.0, 3.0 }),
                Opacity         = opacity,
                Tag             = los.Id
            };
            Children.Add(line);
        }

        private void DrawAnnotation(PlanAnnotation ann)
        {
            if (ann.Points.Count == 0) return;
            var brush = ParseBrush(ann.Color);
            bool isCurrentFloor = ann.FloorIndex == ViewModel?.SelectedFloor;
            double opacity = isCurrentFloor ? 1.0 : 0.4;

            switch (ann.Type)
            {
                case AnnotationType.Line:
                case AnnotationType.Arrow:
                {
                    if (ann.Points.Count < 2) break;
                    var p1 = ToCanvas(ann.Points[0]);
                    var p2 = ToCanvas(ann.Points[^1]);
                    var line = new Line
                    {
                        X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                        Stroke          = brush,
                        StrokeThickness = ann.StrokeThickness,
                        Tag             = ann.Id,
                        Opacity         = opacity
                    };
                    if (ann.Type == AnnotationType.Arrow)
                    {
                        var ag = new ArrowGeometry(p1, p2, 12);
                        var path = new System.Windows.Shapes.Path
                        {
                            Data   = ag.Geometry,
                            Fill   = brush,
                            Tag    = ann.Id,
                            Opacity = opacity
                        };
                        Children.Add(path);
                    }
                    Children.Add(line);
                    break;
                }

                case AnnotationType.Rectangle:
                {
                    if (ann.Points.Count < 2) break;
                    var p1 = ToCanvas(ann.Points[0]);
                    var p2 = ToCanvas(ann.Points[^1]);
                    var rect = new Rectangle
                    {
                        Width           = Math.Abs(p2.X - p1.X),
                        Height          = Math.Abs(p2.Y - p1.Y),
                        Stroke          = brush,
                        StrokeThickness = ann.StrokeThickness,
                        Fill            = new SolidColorBrush(
                            Color.FromArgb(30,
                                ((SolidColorBrush)brush).Color.R,
                                ((SolidColorBrush)brush).Color.G,
                                ((SolidColorBrush)brush).Color.B)),
                        Tag = ann.Id,
                        Opacity = opacity
                    };
                    Children.Add(rect);
                    SetLeft(rect, Math.Min(p1.X, p2.X));
                    SetTop(rect,  Math.Min(p1.Y, p2.Y));
                    break;
                }

                case AnnotationType.FreehandLine:
                {
                    var poly = new Polyline
                    {
                        Stroke          = brush,
                        StrokeThickness = ann.StrokeThickness,
                        StrokeLineJoin  = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap   = PenLineCap.Round,
                        Tag             = ann.Id,
                        Opacity         = opacity
                    };
                    foreach (var pt in ann.Points)
                        poly.Points.Add(ToCanvas(pt));
                    Children.Add(poly);
                    break;
                }

                case AnnotationType.Text:
                {
                    var p = ToCanvas(ann.Points[0]);
                    var tb = new Border
                    {
                        Background      = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                        BorderBrush     = brush,
                        BorderThickness = new Thickness(1),
                        Padding         = new Thickness(4, 2, 4, 2),
                        Tag             = ann.Id,
                        Opacity         = opacity,
                        Child = new TextBlock
                        {
                            Text       = ann.Text,
                            Foreground = brush,
                            FontSize   = 12,
                            FontFamily = new FontFamily("Consolas")
                        }
                    };
                    Children.Add(tb);
                    SetLeft(tb, p.X); SetTop(tb, p.Y);
                    break;
                }
            }
        }

        private void DrawDefaultCamera(DefaultCamera cam)
        {
            var cp = ToCanvas(cam.NormX, cam.NormY);

            bool isCurrentFloor = cam.FloorIndex == ViewModel?.SelectedFloor;
            double size = isCurrentFloor ? 10 : 6;
            double opacity = isCurrentFloor ? 1.0 : 0.4;

            var dot = new Ellipse
            {
                Width  = size, Height = size,
                Fill   = new SolidColorBrush(Color.FromRgb(0, 190, 255)),
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                ToolTip = cam.Name,
                Tag = "cam_" + cam.Name,
                Cursor = Cursors.Hand,
                Opacity = opacity
            };
            dot.MouseRightButtonDown += Camera_RightClick;
            if (ViewModel?.ActiveTool == EditTool.EditCamera)
                dot.MouseLeftButtonDown += Camera_LeftClick;
            Children.Add(dot);
            SetLeft(dot, cp.X - size / 2); SetTop(dot, cp.Y - size / 2);

            var cameraText = new TextBlock
            {
                Text = cam.Name,
                Foreground = Brushes.White,
                FontSize = isCurrentFloor ? 10 : 8,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Opacity = opacity
            };

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2, 4, 2),
                Child = cameraText,
                Opacity = opacity,
                IsHitTestVisible = false
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var labelWidth = label.DesiredSize.Width;
            Children.Add(label);
            SetLeft(label, cp.X - labelWidth / 2);
            SetTop(label, cp.Y + size / 2 + 2);
        }

        private void DrawSpawnPoint(SpawnPoint spawn)
        {
            var p = ToCanvas(spawn.NormX, spawn.NormY);
            double size = 26;
            double opacity = 1.0;
            var fill = new SolidColorBrush(Color.FromRgb(255, 224, 50));
            var stroke = Brushes.Black;

            var circle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1.8,
                Opacity = opacity,
                IsHitTestVisible = false
            };

            var letter = new TextBlock
            {
                Text = spawn.Letter,
                Foreground = Brushes.Black,
                FontSize = 14,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Opacity = opacity
            };

            var marker = new Grid
            {
                Width = size,
                Height = size,
                Tag = spawn.Id,
                Opacity = opacity,
                ToolTip = spawn.Name
            };
            marker.Children.Add(circle);
            marker.Children.Add(letter);

            var labelText = new TextBlock
            {
                Text = spawn.Name,
                Foreground = Brushes.White,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 2, 4, 2),
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false,
                Opacity = opacity
            };

            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Tag = spawn.Id,
                Opacity = opacity
            };
            container.Children.Add(marker);
            container.Children.Add(labelText);

            bool isCurrentFloor = spawn.FloorIndex == ViewModel?.SelectedFloor;
            if (isCurrentFloor && ViewModel?.ActiveTool == EditTool.EditSpawn)
            {
                container.Cursor = Cursors.SizeAll;
                container.MouseLeftButtonDown += Spawn_MouseDown;
                container.MouseRightButtonDown += Spawn_RightClick;
            }
            else if (isCurrentFloor)
            {
                container.MouseRightButtonDown += Spawn_RightClick;
            }

            Children.Add(container);
            SetLeft(container, p.X - size / 2);
            SetTop(container, p.Y - size / 2);
        }

        private void DrawToken(PlanToken tok)
        {
            var p = ToCanvas(tok.NormX, tok.NormY);
            var brush = ParseBrush(tok.Color);

            bool isCurrentFloor = tok.FloorIndex == ViewModel?.SelectedFloor;
            double scale = isCurrentFloor ? 1.0 : 0.6;
            double opacity = isCurrentFloor ? 1.0 : 0.4;

            string tooltipText = tok.Type == TokenType.Gadget && tok.GadgetType.HasValue
                ? tok.GadgetType.Value.ToString()
                : $"{tok.Type}: {tok.OperatorName}";

            FrameworkElement shape = tok.Type switch
            {
                TokenType.Attacker  => MakeCircle(22 * scale, brush, tok),
                TokenType.Defender  => MakeSquare(22 * scale, brush, tok),
                TokenType.Drone     => MakeDiamond(18 * scale, brush, tok),
                TokenType.Breach    => MakeBreachIcon(brush, tok),
                TokenType.Objective => MakeStar(brush, tok),
                TokenType.Gadget    => MakeGadgetShape(tok.GadgetType ?? Models.GadgetType.MuteJammer, 20 * scale, brush, tok),
                _ => MakeCircle(22 * scale, brush, tok)
            };

            shape.Tag = tok.Id;

            var tooltip = new ToolTip
            {
                Content = tooltipText
            };
            
            try
            {
                var style = Application.Current.FindResource("InstantTooltip") as Style;
                if (style != null)
                    tooltip.Style = style;
            }
            catch
            {
            }
            
            ToolTipService.SetInitialShowDelay(tooltip, 0);
            ToolTipService.SetShowDuration(tooltip, 30000);

            var container = new Grid { Tag = tok.Id, Opacity = opacity, ToolTip = tooltip };
            container.Children.Add(shape);

            // Add operator icon or label
            if (!string.IsNullOrEmpty(tok.OperatorIcon) && 
                (tok.Type == TokenType.Attacker || tok.Type == TokenType.Defender))
            {
                var iconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Operators", $"{tok.OperatorIcon}.png");
                
                if (System.IO.File.Exists(iconPath))
                {
                    var iconImage = new Image
                    {
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute)),
                        Width = 20 * scale,
                        Height = 20 * scale,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Opacity = opacity
                    };
                    
                    // Add a subtle background to make icons more visible
                    var iconBg = new Border
                    {
                        Width = 20 * scale,
                        Height = 20 * scale,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        CornerRadius = new CornerRadius(2),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Child = iconImage
                    };
                    container.Children.Add(iconBg);
                }
                else
                {
                    // Fallback to label if icon not found
                    var label = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(tok.Label) ? GetTypeGlyph(tok) : tok.Label,
                        Foreground = Brushes.White,
                        FontSize = 9 * scale,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                        Opacity = opacity
                    };
                    container.Children.Add(label);
                }
            }
            else
            {
                var label = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(tok.Label) ? GetTypeGlyph(tok) : tok.Label,
                    Foreground = Brushes.White,
                    FontSize = 9 * scale,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                    Opacity = opacity
                };
                container.Children.Add(label);
            }

            container.Tag = tok.Id;

            if (isCurrentFloor)
            {
                container.MouseLeftButtonDown += Token_MouseDown;
                container.MouseRightButtonDown += Token_RightClick;
            }

            Children.Add(container);
            SetLeft(container, p.X - 11 * scale);
            SetTop(container,  p.Y - 11 * scale);
        }

        private static string GetTypeGlyph(PlanToken tok)
        {
            if (tok.Type == TokenType.Gadget && tok.GadgetType.HasValue)
            {
                return tok.GadgetType.Value switch
                {
                    // Defender gadgets
                    Models.GadgetType.MuteJammer => "M",
                    Models.GadgetType.KapkanTrap => "K",
                    Models.GadgetType.BanditBattery => "⚡",
                    Models.GadgetType.KaidClaw => "⚡",
                    Models.GadgetType.JagerADS => "J",
                    Models.GadgetType.WamaiMagnet => "W",
                    Models.GadgetType.ValkyrieCam => "👁",
                    Models.GadgetType.MaestroEvil => "📹",
                    Models.GadgetType.SmokeBomb => "💨",
                    Models.GadgetType.CastleDoor => "🚪",
                    Models.GadgetType.PulseSensor => "💓",
                    Models.GadgetType.DocStim => "+",
                    Models.GadgetType.RookArmor => "🛡",
                    Models.GadgetType.FrostTrap => "❄",
                    Models.GadgetType.EchoYokai => "E",
                    Models.GadgetType.CaveiraInterrogation => "?",
                    Models.GadgetType.MiraWindow => "⬜",
                    Models.GadgetType.LesionMine => "L",
                    Models.GadgetType.ElaGrzmot => "⚡",
                    Models.GadgetType.VigilERC => "V",
                    Models.GadgetType.AlibiPrisma => "A",
                    Models.GadgetType.ClashShield => "🛡",
                    Models.GadgetType.KaidRtila => "⚡",
                    Models.GadgetType.MozzieHack => "🔧",
                    Models.GadgetType.WardenGlasses => "👓",
                    Models.GadgetType.GoyoVolcan => "🔥",
                    Models.GadgetType.AruniGate => "⚡",
                    Models.GadgetType.ThunderbirdKona => "+",
                    Models.GadgetType.TachankaShumikha => "🔥",
                    Models.GadgetType.AzamiKiba => "▲",
                    Models.GadgetType.SolisSpec => "👓",
                    Models.GadgetType.FenrirMine => "F",
                    Models.GadgetType.TubaraoZoto => "⬜",
                    Models.GadgetType.ThornMine => "🌹",
                    Models.GadgetType.MelusiBanshee => "M",
                    Models.GadgetType.OryxDash => "O",
                    Models.GadgetType.SkoposShell => "📷",
                    Models.GadgetType.DenariConnector => "⚡",
                    
                    // Attacker gadgets
                    Models.GadgetType.ThermiteCharge => "T",
                    Models.GadgetType.HibanaXKairos => "H",
                    Models.GadgetType.AceSelma => "A",
                    Models.GadgetType.AshBreachRound => "💥",
                    Models.GadgetType.ZofiaConcussion => "Z",
                    Models.GadgetType.BuckSkeleton => "B",
                    Models.GadgetType.SledgeHammer => "🔨",
                    Models.GadgetType.ThatcherEMP => "⚡",
                    Models.GadgetType.TwitchDrone => "🔧",
                    Models.GadgetType.MontagneShield => "🛡",
                    Models.GadgetType.GlazScope => "🔭",
                    Models.GadgetType.FuzeCluster => "💣",
                    Models.GadgetType.BlitzFlash => "⚡",
                    Models.GadgetType.IQScanner => "📡",
                    Models.GadgetType.CapitaoArrow => "➤",
                    Models.GadgetType.BlackbeardShield => "🛡",
                    Models.GadgetType.JackalFootprint => "👣",
                    Models.GadgetType.YingCandela => "💡",
                    Models.GadgetType.ZofiaKS79 => "Z",
                    Models.GadgetType.DokkaebiLogic => "📱",
                    Models.GadgetType.LionEE => "🔊",
                    Models.GadgetType.FinkaSurge => "+",
                    Models.GadgetType.MaverickTorch => "🔥",
                    Models.GadgetType.NomadAirjab => "💨",
                    Models.GadgetType.KaliLance => "➤",
                    Models.GadgetType.AmauruSupressa => "A",
                    Models.GadgetType.IanaGemini => "I",
                    Models.GadgetType.ZeroCamera => "📷",
                    Models.GadgetType.FloresRCE => "🔧",
                    Models.GadgetType.OsaShield => "🛡",
                    Models.GadgetType.SensOrb => "👁",
                    Models.GadgetType.GrimSkyline => "G",
                    Models.GadgetType.BravaKludge => "🔧",
                    Models.GadgetType.RamBU => "R",
                    Models.GadgetType.DeimosDeathmark => "D",
                    Models.GadgetType.GridlockTrax => "G",
                    Models.GadgetType.NokkHEL => "N",
                    Models.GadgetType.RauoraBulletproofPanel => "🛡",
                    Models.GadgetType.SnakeSoliton => "📡",
                    
                    // Universal
                    Models.GadgetType.Claymore => "C",
                    Models.GadgetType.BarbedWire => "⚠",
                    Models.GadgetType.DeployableShield => "🛡",
                    Models.GadgetType.BulletproofCamera => "📷",
                    Models.GadgetType.ImpactGrenade => "💥",
                    Models.GadgetType.NitroCell => "💣",
                    Models.GadgetType.ProximityAlarm => "🔔",
                    Models.GadgetType.ObservationBlocker => "⬛",
                    
                    _ => "G"
                };
            }

            return tok.Type switch
            {
                TokenType.Attacker  => "A",
                TokenType.Defender  => "D",
                TokenType.Drone     => "U",
                TokenType.Breach    => "B",
                TokenType.Objective => "★",
                _ => "?"
            };
        }

        private FrameworkElement MakeGadgetShape(Models.GadgetType gadget, double size, Brush fill, PlanToken tok)
        {
            string tooltip = gadget.ToString();
            
            return gadget switch
            {
                // Defender gadgets - distinctive shapes
                Models.GadgetType.MuteJammer => MakeHexagon(size, fill, tooltip),
                Models.GadgetType.KapkanTrap => MakeTriangle(size, fill, tooltip),
                Models.GadgetType.BanditBattery => MakeSmallRect(size, fill, tooltip),
                Models.GadgetType.KaidClaw => MakeOctagon(size, fill, tooltip),
                Models.GadgetType.JagerADS => MakePentagon(size, fill, tooltip),
                Models.GadgetType.WamaiMagnet => MakePentagon(size, fill, tooltip),
                Models.GadgetType.ValkyrieCam => MakeSmallCircle(size * 0.8, fill, tooltip),
                Models.GadgetType.MaestroEvil => MakeSmallCircle(size * 0.9, fill, tooltip),
                Models.GadgetType.SmokeBomb => MakeHexagon(size * 0.9, fill, tooltip),
                Models.GadgetType.CastleDoor => MakeSmallRect(size * 1.3, fill, tooltip),
                Models.GadgetType.PulseSensor => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.DocStim => MakeCross(size, fill, tooltip),
                Models.GadgetType.RookArmor => MakeSquare(size, fill, tok),
                Models.GadgetType.FrostTrap => MakeTriangle(size, fill, tooltip),
                Models.GadgetType.EchoYokai => MakeDiamondShape(size, fill, tooltip),
                Models.GadgetType.CaveiraInterrogation => MakeOctagon(size * 0.9, fill, tooltip),
                Models.GadgetType.MiraWindow => MakeSmallRect(size * 1.4, fill, tooltip),
                Models.GadgetType.LesionMine => MakeHexagon(size * 0.85, fill, tooltip),
                Models.GadgetType.ElaGrzmot => MakeDiamondShape(size * 0.9, fill, tooltip),
                Models.GadgetType.VigilERC => MakeOctagon(size, fill, tooltip),
                Models.GadgetType.AlibiPrisma => MakePentagon(size * 0.9, fill, tooltip),
                Models.GadgetType.ClashShield => MakeSmallRect(size * 1.2, fill, tooltip),
                Models.GadgetType.KaidRtila => MakeOctagon(size * 0.95, fill, tooltip),
                Models.GadgetType.MozzieHack => MakeDiamondShape(size * 0.85, fill, tooltip),
                Models.GadgetType.WardenGlasses => MakeRoundedRect(size * 0.9, fill, tooltip),
                Models.GadgetType.GoyoVolcan => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.AruniGate => MakeSmallRect(size * 1.5, fill, tooltip),
                Models.GadgetType.ThunderbirdKona => MakeCross(size * 0.9, fill, tooltip),
                Models.GadgetType.TachankaShumikha => MakeHexagon(size, fill, tooltip),
                Models.GadgetType.AzamiKiba => MakeTriangle(size * 0.95, fill, tooltip),
                Models.GadgetType.SolisSpec => MakeOctagon(size * 0.85, fill, tooltip),
                Models.GadgetType.FenrirMine => MakeHexagon(size * 0.9, fill, tooltip),
                Models.GadgetType.TubaraoZoto => MakeSmallRect(size * 1.35, fill, tooltip),
                Models.GadgetType.ThornMine => MakeTriangle(size * 0.9, fill, tooltip),
                Models.GadgetType.MelusiBanshee => MakePentagon(size * 0.9, fill, tooltip),
                Models.GadgetType.OryxDash => MakeDiamondShape(size * 0.9, fill, tooltip),
                Models.GadgetType.SkoposShell => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.DenariConnector => MakeSmallRect(size * 1.2, fill, tooltip),
                
                // Attacker gadgets - distinctive shapes
                Models.GadgetType.ThermiteCharge => MakeRoundedRect(size, fill, tooltip),
                Models.GadgetType.HibanaXKairos => MakeDiamondShape(size, fill, tooltip),
                Models.GadgetType.AceSelma => MakeRoundedRect(size * 0.95, fill, tooltip),
                Models.GadgetType.AshBreachRound => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.ZofiaConcussion => MakeSmallCircle(size * 0.8, fill, tooltip),
                Models.GadgetType.BuckSkeleton => MakeSquare(size * 0.9, fill, tok),
                Models.GadgetType.SledgeHammer => MakeSquare(size, fill, tok),
                Models.GadgetType.ThatcherEMP => MakeHexagon(size * 0.9, fill, tooltip),
                Models.GadgetType.TwitchDrone => MakeDiamondShape(size * 0.85, fill, tooltip),
                Models.GadgetType.MontagneShield => MakeSmallRect(size * 1.3, fill, tooltip),
                Models.GadgetType.GlazScope => MakeSmallCircle(size * 0.75, fill, tooltip),
                Models.GadgetType.FuzeCluster => MakeHexagon(size, fill, tooltip),
                Models.GadgetType.BlitzFlash => MakeSmallCircle(size * 0.9, fill, tooltip),
                Models.GadgetType.IQScanner => MakeRoundedRect(size * 0.85, fill, tooltip),
                Models.GadgetType.CapitaoArrow => MakeTriangle(size * 0.9, fill, tooltip),
                Models.GadgetType.BlackbeardShield => MakeSmallRect(size * 1.2, fill, tooltip),
                Models.GadgetType.JackalFootprint => MakeOctagon(size * 0.85, fill, tooltip),
                Models.GadgetType.YingCandela => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.ZofiaKS79 => MakeSmallCircle(size * 0.8, fill, tooltip),
                Models.GadgetType.DokkaebiLogic => MakeRoundedRect(size * 0.9, fill, tooltip),
                Models.GadgetType.LionEE => MakeDiamondShape(size * 0.9, fill, tooltip),
                Models.GadgetType.FinkaSurge => MakeCross(size * 0.85, fill, tooltip),
                Models.GadgetType.MaverickTorch => MakeSmallCircle(size * 0.75, fill, tooltip),
                Models.GadgetType.NomadAirjab => MakeSmallCircle(size * 0.9, fill, tooltip),
                Models.GadgetType.KaliLance => MakeTriangle(size, fill, tooltip),
                Models.GadgetType.AmauruSupressa => MakeSquare(size * 0.9, fill, tok),
                Models.GadgetType.IanaGemini => MakePentagon(size * 0.9, fill, tooltip),
                Models.GadgetType.ZeroCamera => MakeSmallCircle(size * 0.8, fill, tooltip),
                Models.GadgetType.FloresRCE => MakeDiamondShape(size * 0.85, fill, tooltip),
                Models.GadgetType.OsaShield => MakeSmallRect(size * 1.3, fill, tooltip),
                Models.GadgetType.SensOrb => MakeSmallCircle(size * 0.9, fill, tooltip),
                Models.GadgetType.GrimSkyline => MakeHexagon(size * 0.85, fill, tooltip),
                Models.GadgetType.BravaKludge => MakeDiamondShape(size * 0.8, fill, tooltip),
                Models.GadgetType.RamBU => MakeSquare(size, fill, tok),
                Models.GadgetType.DeimosDeathmark => MakeOctagon(size * 0.9, fill, tooltip),
                Models.GadgetType.GridlockTrax => MakeHexagon(size * 0.9, fill, tooltip),
                Models.GadgetType.NokkHEL => MakeDiamondShape(size * 0.85, fill, tooltip),
                Models.GadgetType.RauoraBulletproofPanel => MakeSmallRect(size * 1.4, fill, tooltip),
                Models.GadgetType.SnakeSoliton => MakeOctagon(size * 0.9, fill, tooltip),
                
                // Universal
                Models.GadgetType.Claymore => MakeTriangle(size, fill, tooltip),
                Models.GadgetType.BarbedWire => MakeHexagon(size, fill, tooltip),
                Models.GadgetType.DeployableShield => MakeSmallRect(size * 1.2, fill, tooltip),
                Models.GadgetType.BulletproofCamera => MakeSmallCircle(size * 0.8, fill, tooltip),
                Models.GadgetType.ImpactGrenade => MakeSmallCircle(size * 0.85, fill, tooltip),
                Models.GadgetType.NitroCell => MakeRoundedRect(size * 0.9, fill, tooltip),
                Models.GadgetType.ProximityAlarm => MakeSmallCircle(size * 0.75, fill, tooltip),
                Models.GadgetType.ObservationBlocker => MakeHexagon(size * 0.9, fill, tooltip),
                
                _ => MakeHexagon(size, fill, tooltip)
            };
        }

        private static Ellipse MakeCircle(double size, Brush fill, PlanToken tok) => new()
        {
            Width = size, Height = size, Fill = fill,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            ToolTip = $"{tok.Type}: {tok.OperatorName}"
        };

        private static Ellipse MakeSmallCircle(double size, Brush fill, string tooltip) => new()
        {
            Width = size, Height = size, Fill = fill,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            ToolTip = tooltip
        };

        private static Rectangle MakeSquare(double size, Brush fill, PlanToken tok) => new()
        {
            Width = size, Height = size, Fill = fill,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            ToolTip = $"{tok.Type}: {tok.OperatorName}"
        };

        private static Rectangle MakeSmallRect(double size, Brush fill, string tooltip) => new()
        {
            Width = size, Height = size * 0.6, Fill = fill,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            ToolTip = tooltip
        };

        private static Rectangle MakeRoundedRect(double size, Brush fill, string tooltip) => new()
        {
            Width = size, Height = size, Fill = fill,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            RadiusX = 3, RadiusY = 3,
            ToolTip = tooltip
        };

        private FrameworkElement MakeDiamond(double size, Brush fill, PlanToken tok)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            fig.StartPoint = new Point(size / 2, 0);
            fig.Segments.Add(new LineSegment(new Point(size, size / 2), true));
            fig.Segments.Add(new LineSegment(new Point(size / 2, size), true));
            fig.Segments.Add(new LineSegment(new Point(0, size / 2), true));
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = $"{tok.Type}: {tok.OperatorName}"
            };
        }

        private FrameworkElement MakeDiamondShape(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            fig.StartPoint = new Point(size / 2, 0);
            fig.Segments.Add(new LineSegment(new Point(size, size / 2), true));
            fig.Segments.Add(new LineSegment(new Point(size / 2, size), true));
            fig.Segments.Add(new LineSegment(new Point(0, size / 2), true));
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakeHexagon(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            double w = size;
            double h = size;
            fig.StartPoint = new Point(w * 0.5, 0);
            fig.Segments.Add(new LineSegment(new Point(w, h * 0.25), true));
            fig.Segments.Add(new LineSegment(new Point(w, h * 0.75), true));
            fig.Segments.Add(new LineSegment(new Point(w * 0.5, h), true));
            fig.Segments.Add(new LineSegment(new Point(0, h * 0.75), true));
            fig.Segments.Add(new LineSegment(new Point(0, h * 0.25), true));
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakeTriangle(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            fig.StartPoint = new Point(size / 2, 0);
            fig.Segments.Add(new LineSegment(new Point(size, size), true));
            fig.Segments.Add(new LineSegment(new Point(0, size), true));
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakePentagon(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            double cx = size / 2;
            double cy = size / 2;
            double r = size / 2;
            for (int i = 0; i < 5; i++)
            {
                double angle = (Math.PI * 2 * i / 5) - Math.PI / 2;
                double x = cx + r * Math.Cos(angle);
                double y = cy + r * Math.Sin(angle);
                if (i == 0)
                    fig.StartPoint = new Point(x, y);
                else
                    fig.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakeOctagon(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };
            double cx = size / 2;
            double cy = size / 2;
            double r = size / 2;
            for (int i = 0; i < 8; i++)
            {
                double angle = (Math.PI * 2 * i / 8) - Math.PI / 2;
                double x = cx + r * Math.Cos(angle);
                double y = cy + r * Math.Sin(angle);
                if (i == 0)
                    fig.StartPoint = new Point(x, y);
                else
                    fig.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            geo.Figures.Add(fig);
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakeCross(double size, Brush fill, string tooltip)
        {
            var geo = new PathGeometry();
            double thickness = size * 0.3;
            double halfSize = size / 2;
            double halfThick = thickness / 2;
            
            // Vertical bar
            var fig1 = new PathFigure { IsClosed = true };
            fig1.StartPoint = new Point(halfSize - halfThick, 0);
            fig1.Segments.Add(new LineSegment(new Point(halfSize + halfThick, 0), true));
            fig1.Segments.Add(new LineSegment(new Point(halfSize + halfThick, size), true));
            fig1.Segments.Add(new LineSegment(new Point(halfSize - halfThick, size), true));
            geo.Figures.Add(fig1);
            
            // Horizontal bar
            var fig2 = new PathFigure { IsClosed = true };
            fig2.StartPoint = new Point(0, halfSize - halfThick);
            fig2.Segments.Add(new LineSegment(new Point(size, halfSize - halfThick), true));
            fig2.Segments.Add(new LineSegment(new Point(size, halfSize + halfThick), true));
            fig2.Segments.Add(new LineSegment(new Point(0, halfSize + halfThick), true));
            geo.Figures.Add(fig2);
            
            return new System.Windows.Shapes.Path
            {
                Data = geo, Fill = fill,
                Stroke = Brushes.White, StrokeThickness = 1.5,
                ToolTip = tooltip
            };
        }

        private FrameworkElement MakeBreachIcon(Brush fill, PlanToken tok)
        {
            var rect = new Rectangle
            {
                Width = 22, Height = 22, Fill = fill,
                Stroke = Brushes.OrangeRed, StrokeThickness = 2,
                RadiusX = 3, RadiusY = 3,
                ToolTip = $"Breach: {tok.Label}"
            };
            return rect;
        }

        private FrameworkElement MakeStar(Brush fill, PlanToken tok)
        {
            var star = new TextBlock
            {
                Text = "★", FontSize = 20,
                Foreground = fill,
                ToolTip = $"Objective: {tok.Label}"
            };
            return star;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (ViewModel == null) return;

            var pos    = e.GetPosition(this);
            var normPt = ToNorm(pos);
            var tool   = ViewModel.ActiveTool;

            _selectedId = null;

            if (tool == EditTool.Select) { return; }

            if (tool == EditTool.Eraser)
            {
                _isErasing = true;
                EraseAtPoint(normPt);
                CaptureMouse();
                return;
            }

            if (tool is EditTool.PlaceAttacker or EditTool.PlaceDefender or
                EditTool.PlaceDrone or EditTool.PlaceBreach or EditTool.PlaceObjective or EditTool.PlaceGadget)
            {
                ViewModel.PlaceToken(normPt.X, normPt.Y);
                return;
            }

            if (tool is EditTool.DrawLoS or EditTool.DrawLine or EditTool.DrawArrow or EditTool.DrawRect or EditTool.DrawFreehand)
            {
                _isDrawing  = true;
                _drawStart  = normPt;
                _lastPoint  = normPt;
                _freehandPoints.Clear();
                _freehandPoints.Add(normPt);
                CaptureMouse();

                if (tool is EditTool.DrawLoS or EditTool.DrawLine or EditTool.DrawArrow)
                {
                    var cp = ToCanvas(normPt);
                    _ghostLine = new Line
                    {
                        X1 = cp.X, Y1 = cp.Y, X2 = cp.X, Y2 = cp.Y,
                        Stroke = ParseBrush(tool == EditTool.DrawLoS ? "#00FF88" : ViewModel.ActiveColor),
                        StrokeThickness = 2,
                        StrokeDashArray = tool == EditTool.DrawLoS ? new DoubleCollection(new[] { 6.0, 3.0 }) : null!,
                        IsHitTestVisible = false
                    };
                    Children.Add(_ghostLine);
                }
                
                if (tool == EditTool.DrawRect)
                {
                    _ghostRect = new Rectangle
                    {
                        Stroke = ParseBrush(ViewModel.ActiveColor),
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 100, 100, 100)),
                        IsHitTestVisible = false,
                        Tag = "ghost-rect"
                    };
                    Children.Add(_ghostRect);
                    SetLeft(_ghostRect, 0);
                    SetTop(_ghostRect, 0);
                }
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _panStart = e.GetPosition(this);
                CaptureMouse();
                this.Focus();
                e.Handled = true;
                return;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (ViewModel == null) return;

            var pos    = e.GetPosition(this);
            var normPt = ToNorm(pos);
            var tool   = ViewModel.ActiveTool;

            if (tool is EditTool.PlaceAttacker or EditTool.PlaceDefender or
                EditTool.PlaceDrone or EditTool.PlaceBreach or EditTool.PlaceObjective or EditTool.PlaceGadget)
            {
                ViewModel.PlaceToken(normPt.X, normPt.Y);
                return;
            }

            if (tool == EditTool.EditCamera)
            {
                if (ViewModel.SelectedMap == null) return;

                var cam = new DefaultCamera
                {
                    Name = $"New Cam {ViewModel.SelectedMap.DefaultCameras.Count + 1}",
                    FloorIndex = ViewModel.SelectedFloor,
                    NormX = normPt.X,
                    NormY = normPt.Y,
                    AngleDeg = 0,
                    FovHalfDeg = 35,
                    RangeNorm = 0.12
                };

                var dlg = new Views.CameraEditDialog(cam) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true)
                {
                    ViewModel.AddCamera(cam);
                }
                return;
            }

            if (tool == EditTool.EditSpawn)
            {
                if (ViewModel.SelectedMap == null) return;

                var spawn = new SpawnPoint
                {
                    Letter = "S",
                    Name = $"Spawn {ViewModel.SelectedMap.SpawnPoints.Count + 1}",
                    FloorIndex = ViewModel.SelectedFloor,
                    NormX = normPt.X,
                    NormY = normPt.Y
                };

                var dlg = new Views.SpawnEditDialog(spawn) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true)
                {
                    ViewModel.AddSpawnPoint(spawn);
                }
                return;
            }

            if (tool == EditTool.PlaceText)
            {
                var dlg = new Views.TextInputDialog { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
                {
                    ViewModel.AddAnnotation(new PlanAnnotation
                    {
                        Type   = AnnotationType.Text,
                        Color  = ViewModel.ActiveColor,
                        Text   = dlg.InputText,
                        Points = new() { normPt },
                        FloorIndex = ViewModel.SelectedFloor
                    });
                }
                return;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_isPanning && e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            if (ViewModel?.ActiveTool == EditTool.PlaceGadget && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var gadgets = Enum.GetValues(typeof(Models.GadgetType)).Cast<Models.GadgetType>().ToArray();
                int currentIndex = Array.IndexOf(gadgets, ViewModel.ActiveGadget);
                int newIndex = e.Delta > 0 
                    ? (currentIndex + 1) % gadgets.Length 
                    : (currentIndex - 1 + gadgets.Length) % gadgets.Length;
                ViewModel.ActiveGadget = gadgets[newIndex];
                e.Handled = true;
                return;
            }

            var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            SetZoom(_zoom * factor, e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isPanning)
            {
                var panPos = e.GetPosition(this);
                _pan = new Point(_pan.X + (panPos.X - _panStart.X), _pan.Y + (panPos.Y - _panStart.Y));
                _panStart = panPos;
                Redraw();
                e.Handled = true;
                return;
            }

            base.OnMouseMove(e);

            if (_isErasing && ViewModel != null)
            {
                var pos = e.GetPosition(this);
                var normPt = ToNorm(pos);
                EraseAtPoint(normPt);
                return;
            }

            if (!_isDrawing || ViewModel == null) return;

            var pos2    = e.GetPosition(this);
            var normPt2 = ToNorm(pos2);
            var tool   = ViewModel.ActiveTool;

            if (_ghostLine != null)
            {
                var cp = ToCanvas(normPt2);
                _ghostLine.X2 = cp.X;
                _ghostLine.Y2 = cp.Y;
            }
            
            if (_ghostRect != null && ViewModel != null)
            {
                var p1 = ToCanvas(_drawStart);
                var p2 = ToCanvas(normPt2);
                double x = Math.Min(p1.X, p2.X);
                double y = Math.Min(p1.Y, p2.Y);
                double width = Math.Abs(p2.X - p1.X);
                double height = Math.Abs(p2.Y - p1.Y);
                
                _ghostRect.Width = width;
                _ghostRect.Height = height;
                SetLeft(_ghostRect, x);
                SetTop(_ghostRect, y);
            }

            if (tool == EditTool.DrawFreehand)
            {
                _freehandPoints.Add(normPt2);
                if (Children.OfType<Polyline>().FirstOrDefault(p => p.Tag?.ToString() == "ghost") is { } ghost)
                    ghost.Points.Add(ToCanvas(normPt2));
                else
                {
                    var poly = new Polyline
                    {
                        Stroke = ParseBrush(ViewModel.ActiveColor),
                        StrokeThickness = 2,
                        Tag = "ghost",
                        IsHitTestVisible = false
                    };
                    foreach (var pt in _freehandPoints)
                        poly.Points.Add(ToCanvas(pt));
                    Children.Add(poly);
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();

            if (_isErasing)
            {
                _isErasing = false;
                return;
            }

            if (!_isDrawing || ViewModel == null) { _isDrawing = false; return; }
            _isDrawing = false;

            var pos    = e.GetPosition(this);
            var normPt = ToNorm(pos);
            var tool   = ViewModel.ActiveTool;

            if (_ghostLine != null) { Children.Remove(_ghostLine); _ghostLine = null; }
            if (_ghostRect != null) { Children.Remove(_ghostRect); _ghostRect = null; }
            var ghostPoly = Children.OfType<Polyline>().FirstOrDefault(p => p.Tag?.ToString() == "ghost");
            if (ghostPoly != null) Children.Remove(ghostPoly);

            if (tool == EditTool.DrawLoS)
            {
                ViewModel.AddLoSLine(_drawStart, normPt);
                return;
            }

            var annType = tool switch
            {
                EditTool.DrawLine     => AnnotationType.Line,
                EditTool.DrawArrow    => AnnotationType.Arrow,
                EditTool.DrawRect     => AnnotationType.Rectangle,
                EditTool.DrawFreehand => AnnotationType.FreehandLine,
                _ => (AnnotationType?)null
            };
            if (annType == null) return;

            var points = annType == AnnotationType.FreehandLine
                ? _freehandPoints.ToList()
                : new List<Point> { _drawStart, normPt };

            ViewModel.AddAnnotation(new PlanAnnotation
            {
                Type   = annType.Value,
                Color  = ViewModel.ActiveColor,
                Points = points,
                StrokeThickness = 2,
                FloorIndex = ViewModel.SelectedFloor
            });
        }

        private void EraseAtPoint(Point normPt)
        {
            if (ViewModel == null) return;

            const double eraseRadius = 0.005;

            var tokensToDelete = ViewModel.VisibleTokens
                .Where(t => Distance(normPt, new Point(t.NormX, t.NormY)) < eraseRadius)
                .Select(t => t.Id)
                .ToList();

            foreach (var id in tokensToDelete)
                ViewModel.DeleteToken(id);

            var annotationsToDelete = ViewModel.VisibleAnnotations
                .Where(a => a.Points.Any(p => Distance(normPt, p) < eraseRadius))
                .Select(a => a.Id)
                .ToList();

            foreach (var id in annotationsToDelete)
                ViewModel.DeleteAnnotation(id);

            var losLinesToDelete = ViewModel.VisibleLoSLines
                .Where(l => Distance(normPt, l.StartNorm) < eraseRadius || 
                           Distance(normPt, l.EndNorm) < eraseRadius ||
                           DistanceToLineSegment(normPt, l.StartNorm, l.EndNorm) < eraseRadius)
                .Select(l => l.Id)
                .ToList();

            foreach (var id in losLinesToDelete)
            {
                var line = ViewModel.Plan.LoSLines.FirstOrDefault(l => l.Id == id);
                if (line != null)
                {
                    ViewModel.PushUndo();
                    ViewModel.Plan.LoSLines.Remove(line);
                    ViewModel.RefreshCanvas();
                }
            }
        }

        private static double Distance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double DistanceToLineSegment(Point p, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
                return Distance(p, lineStart);

            double t = Math.Max(0, Math.Min(1, ((p.X - lineStart.X) * dx + (p.Y - lineStart.Y) * dy) / lengthSquared));
            Point projection = new Point(lineStart.X + t * dx, lineStart.Y + t * dy);
            return Distance(p, projection);
        }

        private void Token_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;

            _draggingId = el.Tag?.ToString();
            _dragging   = el;
            _dragInitialMousePos = e.GetPosition(this);
            
            // Store initial token position (normalized)
            var token = ViewModel?.Plan.Tokens.FirstOrDefault(t => t.Id == _draggingId);
            if (token != null)
                _dragInitialTokenPos = new Point(token.NormX, token.NormY);
            
            el.CaptureMouse();
            el.MouseMove          += Token_Drag;
            el.MouseLeftButtonUp  += Token_DragEnd;
            e.Handled = true;
        }

        private void Token_Drag(object sender, MouseEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;

            var token = ViewModel?.Plan.Tokens.FirstOrDefault(t => t.Id == _draggingId);
            if (token == null) return;

            var currentMousePos = e.GetPosition(this);
            var initialMouseNorm = ToNorm(_dragInitialMousePos);
            var currentMouseNorm = ToNorm(currentMousePos);

            double newNormX = _dragInitialTokenPos.X + (currentMouseNorm.X - initialMouseNorm.X);
            double newNormY = _dragInitialTokenPos.Y + (currentMouseNorm.Y - initialMouseNorm.Y);

            bool isCurrentFloor = token.FloorIndex == ViewModel?.SelectedFloor;
            double scale = isCurrentFloor ? 1.0 : 0.6;
            double half = 11 * scale;   // matches DrawToken's SetLeft/SetTop math

            var newCanvasPos = ToCanvas(newNormX, newNormY);
            SetLeft(_dragging, newCanvasPos.X - half);
            SetTop(_dragging,  newCanvasPos.Y - half);
        }

        private void Token_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;

            var currentMousePos = e.GetPosition(this);
            var initialMouseNorm = ToNorm(_dragInitialMousePos);
            var currentMouseNorm = ToNorm(currentMousePos);
            
            double normDeltaX = currentMouseNorm.X - initialMouseNorm.X;
            double normDeltaY = currentMouseNorm.Y - initialMouseNorm.Y;
            
            double finalNormX = _dragInitialTokenPos.X + normDeltaX;
            double finalNormY = _dragInitialTokenPos.Y + normDeltaY;
            
            // Push undo ONCE for the entire drag operation
            ViewModel?.PushUndo();
            // Then update model with final position
            ViewModel?.MoveToken(_draggingId, finalNormX, finalNormY);

            _dragging.ReleaseMouseCapture();
            _dragging.MouseMove         -= Token_Drag;
            _dragging.MouseLeftButtonUp -= Token_DragEnd;
            _dragging   = null;
            _draggingId = null;
            e.Handled   = true;
        }

        private void Camera_Drag(object sender, MouseEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;
            var pos  = e.GetPosition(this);
            SetLeft(_dragging, pos.X - _dragOffset.X);
            SetTop(_dragging,  pos.Y - _dragOffset.Y);
        }

        private void Camera_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;

            var pos = e.GetPosition(this);
            var norm = ToNorm(new Point(pos.X, pos.Y));
            ViewModel?.PushUndo();
            ViewModel?.MoveCamera(_draggingId, norm.X, norm.Y);

            _dragging.ReleaseMouseCapture();
            _dragging.MouseMove         -= Camera_Drag;
            _dragging.MouseLeftButtonUp -= Camera_DragEnd;
            _dragging   = null;
            _draggingId = null;
            e.Handled   = true;
        }

        private void Camera_LeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Ellipse el) return;
            var tag = el.Tag?.ToString();
            if (tag == null || !tag.StartsWith("cam_")) return;

            _draggingId = tag.Substring(4); // camera name
            _dragging   = el;
            _dragOffset = e.GetPosition(el);
            el.CaptureMouse();
            el.MouseMove          += Camera_Drag;
            el.MouseLeftButtonUp  += Camera_DragEnd;
            e.Handled = true;
        }

        private void Camera_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Ellipse el) return;
            var tag = el.Tag?.ToString();
            if (tag == null || !tag.StartsWith("cam_")) return;

            var camName = tag.Substring(4);
            var cam = ViewModel?.SelectedMap?.DefaultCameras.FirstOrDefault(c => c.Name == camName);
            if (cam == null) return;

            var dlg = new Views.CameraEditDialog(cam) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                MapDatabase.SaveMapOverrides();
                Redraw(); // Refresh to show updated camera
            }
            e.Handled = true;
        }

        private void Spawn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;

            _draggingId = el.Tag?.ToString();
            _dragging = el;
            _dragOffset = e.GetPosition(el);
            el.CaptureMouse();
            el.MouseMove          += Spawn_Drag;
            el.MouseLeftButtonUp  += Spawn_DragEnd;
            e.Handled = true;
        }

        private void Spawn_Drag(object sender, MouseEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;
            var pos  = e.GetPosition(this);
            SetLeft(_dragging, pos.X - _dragOffset.X);
            SetTop(_dragging,  pos.Y - _dragOffset.Y);
        }

        private void Spawn_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (_dragging == null || _draggingId == null) return;

            var pos = e.GetPosition(this);
            var norm = ToNorm(new Point(pos.X, pos.Y));
            ViewModel?.PushUndo();
            ViewModel?.MoveSpawnPoint(_draggingId, norm.X, norm.Y);

            _dragging.ReleaseMouseCapture();
            _dragging.MouseMove         -= Spawn_Drag;
            _dragging.MouseLeftButtonUp -= Spawn_DragEnd;
            _dragging   = null;
            _draggingId = null;
            e.Handled   = true;
        }

        private void Spawn_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;
            var id = el.Tag?.ToString();
            if (id == null) return;

            var spawn = ViewModel?.SelectedMap?.SpawnPoints.FirstOrDefault(s => s.Id == id);
            if (spawn == null) return;

            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "✎ Edit" };
            edit.Click += (_, _) =>
            {
                var dlg = new Views.SpawnEditDialog(spawn) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true)
                {
                    ViewModel?.UpdateSpawnPoint(spawn.Id, spawn.Letter, spawn.Name);
                }
            };
            var del = new MenuItem { Header = "🗑 Delete" };
            del.Click += (_, _) =>
            {
                ViewModel?.DeleteSpawnPoint(id);
            };
            menu.Items.Add(edit);
            menu.Items.Add(del);
            el.ContextMenu = menu;
            e.Handled = true;
        }

        private void Token_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;
            var id = el.Tag?.ToString();
            if (id == null) return;

            var token = ViewModel?.Plan.Tokens.FirstOrDefault(t => t.Id == id);
            if (token == null) return;

            var menu = new ContextMenu();
            
            // Add "Change Icon" option for Attacker/Defender tokens
            if (token.Type == TokenType.Attacker || token.Type == TokenType.Defender)
            {
                var changeIcon = new MenuItem { Header = "🎭 Change Icon" };
                changeIcon.Click += (_, _) =>
                {
                    var dlg = new Views.OperatorIconDialog(token.OperatorIcon) 
                    { 
                        Owner = Window.GetWindow(this) 
                    };
                    
                    if (dlg.ShowDialog() == true)
                    {
                        ViewModel?.PushUndo();
                        token.OperatorIcon = dlg.SelectedOperatorIcon;
                        ViewModel?.RefreshCanvas();
                    }
                };
                menu.Items.Add(changeIcon);
            }
            
            var del = new MenuItem { Header = "🗑 Delete" };
            del.Click += (_, _) => ViewModel?.DeleteToken(id);
            menu.Items.Add(del);
            
            el.ContextMenu = menu;
            menu.PlacementTarget = el;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
        }
    }

    internal class ArrowGeometry
    {
        public Geometry Geometry { get; }

        public ArrowGeometry(Point from, Point to, double size)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) { Geometry = Geometry.Empty; return; }

            var ux = dx / len;
            var uy = dy / len;
            var p1 = new Point(to.X - ux * size - uy * size * 0.4,
                               to.Y - uy * size + ux * size * 0.4);
            var p2 = new Point(to.X - ux * size + uy * size * 0.4,
                               to.Y - uy * size - ux * size * 0.4);

            var geo = new StreamGeometry();
            using var ctx = geo.Open();
            ctx.BeginFigure(to, true, true);
            ctx.LineTo(p1, true, false);
            ctx.LineTo(p2, true, false);
            geo.Freeze();
            Geometry = geo;
        }
    }
}
