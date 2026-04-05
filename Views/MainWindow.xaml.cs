using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using R6Planner.ViewModels;

namespace R6Planner.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private ToggleButton[] _toolButtons = Array.Empty<ToggleButton>();

        public MainWindow()
        {
            InitializeComponent();
            TheCanvas.ViewModel = _vm;
            DataContext = _vm;

            _toolButtons = new[]
            {
                TbSelect, TbAttacker, TbDefender, TbDrone,
                TbBreach, TbObjective, TbLoS, TbLine,
                TbArrow, TbRect, TbFreehand, TbText, TbCamera, TbSpawn, TbEraser, TbGadget
            };

            LbMaps.ItemsSource       = _vm.Maps;
            LbMaps.DisplayMemberPath = "Name";

            if (_vm.Maps.Count > 0)
                LbMaps.SelectedIndex = 0;
        }

        private void LbMaps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbMaps.SelectedItem is Models.MapInfo map)
            {
                if (HasUnsavedChanges())
                {
                    var result = MessageBox.Show(
                        "Switching maps will clear your current plan. Continue?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                    {
                        LbMaps.SelectionChanged -= LbMaps_SelectionChanged;
                        LbMaps.SelectedItem = _vm.SelectedMap;
                        LbMaps.SelectionChanged += LbMaps_SelectionChanged;
                        return;
                    }
                }

                _vm.SelectedMap = map;
                TbMapTitle.Text  = " › " + map.Name.ToUpperInvariant();
                BuildFloorButtons(map);
            }
        }

        private bool HasUnsavedChanges()
        {
            return _vm.Plan.Tokens.Count > 0 ||
                   _vm.Plan.Annotations.Count > 0 ||
                   _vm.Plan.LoSLines.Count > 0;
        }

        private void BuildFloorButtons(Models.MapInfo map)
        {
            SpFloors.Children.Clear();
            for (int i = 0; i < map.Floors.Count; i++)
            {
                int idx   = i;
                var floor = map.Floors[i];
                var btn   = new RadioButton
                {
                    Content     = floor.Label,
                    GroupName   = "FloorGroup",
                    IsChecked   = idx == 0,
                    Foreground  = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                    FontFamily  = new FontFamily("Consolas"),
                    FontSize    = 12,
                    Margin      = new Thickness(10, 3, 4, 3)
                };
                btn.Checked += (_, _) => { _vm.SelectedFloor = idx; UpdateStatus(); };
                SpFloors.Children.Add(btn);
            }
            _vm.SelectedFloor = 0;
        }

        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;
            foreach (var btn in _toolButtons)
                if (btn != clicked) btn.IsChecked = false;

            if (Enum.TryParse<EditTool>(clicked.Tag?.ToString(), out var tool))
            {
                _vm.ActiveTool = tool;
                TheCanvas.Focusable = true;
                TheCanvas.Focus();
            }
            UpdateStatus();
        }

        private void ColorBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _vm.ActiveColor = hex;
                try { ActiveColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { }
            }
        }

        private void TbOperator_Changed(object sender, TextChangedEventArgs e)
            => _vm.ActiveOperator = TbOperator.Text;

        private void CbCams_Changed(object sender, RoutedEventArgs e)
            => _vm.ShowDefaultCams = CbCams.IsChecked == true;

        private void CbLoS_Changed(object sender, RoutedEventArgs e)
            => _vm.ShowLoS = CbLoS.IsChecked == true;

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter           = "R6 Plan|*.r6plan",
                DefaultExt       = ".r6plan",
                FileName         = $"{_vm.SelectedMap?.Name ?? "plan"}_{DateTime.Now:yyyyMMdd_HHmm}"
            };
            if (dlg.ShowDialog() == true)
            {
                _vm.SavePlan(dlg.FileName);
                SetStatus($"Saved → {Path.GetFileName(dlg.FileName)}");
            }
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "R6 Plan|*.r6plan" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _vm.LoadPlan(dlg.FileName);
                    var loaded = _vm.Maps.FirstOrDefault(m => m.Name == _vm.Plan.MapName);
                    if (loaded != null) LbMaps.SelectedItem = loaded;
                    SetStatus($"Loaded → {Path.GetFileName(dlg.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load plan:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear all tokens, annotations and LoS lines?",
                    "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _vm.ClearAll();
                SetStatus("Canvas cleared.");
            }
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#FFD700");
            }
            else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#E63946");
            }
            else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#2196F3");
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#4CAF50");
            }
            else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#FF9800");
            }
            else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#9C27B0");
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#00BCD4");
            }
            else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#FFFFFF");
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#FF5722");
            }
            else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.None)
            {
                SetColor("#00FF88");
            }
            else if (e.Key == Key.D1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbSelect, EditTool.Select);
            }
            else if (e.Key == Key.D2 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbLoS, EditTool.DrawLoS);
            }
            else if (e.Key == Key.D3 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbLine, EditTool.DrawLine);
            }
            else if (e.Key == Key.D4 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbArrow, EditTool.DrawArrow);
            }
            else if (e.Key == Key.D5 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbRect, EditTool.DrawRect);
            }
            else if (e.Key == Key.D6 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbFreehand, EditTool.DrawFreehand);
            }
            else if (e.Key == Key.D7 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbText, EditTool.PlaceText);
            }
            else if (e.Key == Key.D8 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ActivateTool(TbEraser, EditTool.Eraser);
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.Undo();
                SetStatus("Undone.");
                e.Handled = true;
            }
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.Redo();
                SetStatus("Redone.");
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SaveBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                foreach (var btn in _toolButtons) btn.IsChecked = false;
                var enable = _vm.ActiveTool != EditTool.EditSpawn;
                if (enable)
                {
                    TbSpawn.IsChecked = true;
                    _vm.ActiveTool = EditTool.EditSpawn;
                    SetStatus("Spawn edit mode enabled. Right-click empty space to add spawns.");
                }
                else
                {
                    TbSelect.IsChecked = true;
                    _vm.ActiveTool = EditTool.Select;
                    SetStatus("Spawn edit mode disabled.");
                }
            }
            else if (e.Key == Key.F3)
            {
                foreach (var btn in _toolButtons) btn.IsChecked = false;
                var enable = _vm.ActiveTool != EditTool.EditCamera;
                if (enable)
                {
                    TbCamera.IsChecked = true;
                    _vm.ActiveTool = EditTool.EditCamera;
                    SetStatus("Camera edit mode enabled. Right-click empty space to add cams.");
                }
                else
                {
                    TbSelect.IsChecked = true;
                    _vm.ActiveTool = EditTool.Select;
                    SetStatus("Camera edit mode disabled.");
                }
            }
            else if (e.Key == Key.Escape)
            {
                foreach (var btn in _toolButtons) btn.IsChecked = false;
                TbSelect.IsChecked = true;
                _vm.ActiveTool = EditTool.Select;
            }
            else if (e.Key == Key.Delete)
            {
                ClearBtn_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.NumPad1)
            {
                SetFloor(0);
                e.Handled = true;
            }
            else if (e.Key == Key.NumPad2)
            {
                SetFloor(1);
                e.Handled = true;
            }
            else if (e.Key == Key.NumPad3)
            {
                SetFloor(2);
                e.Handled = true;
            }
            else if (e.Key == Key.NumPad4)
            {
                SetFloor(3);
                e.Handled = true;
            }
            else if (e.Key == Key.NumPad5)
            {
                SetFloor(4);
                e.Handled = true;
            }
        }

        private void SetFloor(int floorIndex)
        {
            if (_vm.SelectedMap == null) return;
            if (floorIndex < 0 || floorIndex >= _vm.SelectedMap.Floors.Count) return;
            
            _vm.SelectedFloor = floorIndex;
            
            for (int i = 0; i < SpFloors.Children.Count; i++)
            {
                if (SpFloors.Children[i] is RadioButton rb)
                    rb.IsChecked = (i == floorIndex);
            }
            
            UpdateStatus();
        }

        private void SetColor(string hex)
        {
            _vm.ActiveColor = hex;
            try { ActiveColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
            SetStatus($"Color: {hex}");
        }

        private void ActivateTool(ToggleButton button, EditTool tool)
        {
            foreach (var btn in _toolButtons) btn.IsChecked = false;
            button.IsChecked = true;
            _vm.ActiveTool = tool;
            TheCanvas.Focusable = true;
            TheCanvas.Focus();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var tool  = _vm.ActiveTool;
            var floor = _vm.FloorLabel;
            TbStatus.Text = $"Tool: {tool}  |  Floor: {floor}  |  1-8=Tools  |  QWERT/ASDFG=Colors  |  Ctrl+Z=Undo  |  Ctrl+Y=Redo  |  Esc=Select";
        }

        private void SetStatus(string msg) => TbStatus.Text = msg;

        private void GadgetDisplay_Click(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 34, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };

            var gadgets = Enum.GetValues(typeof(Models.GadgetType)).Cast<Models.GadgetType>();
            foreach (var gadget in gadgets)
            {
                var item = new MenuItem
                {
                    Header = gadget.ToString(),
                    IsChecked = gadget == _vm.ActiveGadget,
                    Background = new SolidColorBrush(Color.FromRgb(28, 34, 48)),
                    Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                    FontFamily = new FontFamily("Consolas")
                };
                
                var currentGadget = gadget;
                item.Click += (_, _) =>
                {
                    _vm.ActiveGadget = currentGadget;
                    SetStatus($"Gadget: {currentGadget}");
                };
                
                menu.Items.Add(item);
            }

            menu.PlacementTarget = GadgetDisplay;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}
