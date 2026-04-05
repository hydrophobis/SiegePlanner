using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace R6Planner.Views
{
    public partial class OperatorIconDialog : Window
    {
        private List<string> _allOperators = new();
        private string? _selectedOperator;

        public string? SelectedOperatorIcon { get; private set; }

        public OperatorIconDialog(string? currentIcon)
        {
            InitializeComponent();
            LoadOperatorIcons();
            
            if (!string.IsNullOrEmpty(currentIcon))
            {
                _selectedOperator = currentIcon;
            }
        }

        private void LoadOperatorIcons()
        {
            var operatorsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Operators");
            
            if (!Directory.Exists(operatorsPath))
            {
                MessageBox.Show("Operators folder not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var iconFiles = Directory.GetFiles(operatorsPath, "*.png")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();

            _allOperators = iconFiles;
            DisplayOperators(iconFiles);
        }

        private void DisplayOperators(List<string> operators)
        {
            OperatorGrid.Children.Clear();

            foreach (var op in operators)
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Operators", $"{op}.png");
                
                if (!File.Exists(iconPath))
                    continue;

                var border = new Border
                {
                    Width = 64,
                    Height = 64,
                    Margin = new Thickness(4),
                    BorderThickness = new Thickness(2),
                    BorderBrush = op == _selectedOperator 
                        ? new SolidColorBrush(Color.FromRgb(88, 166, 255))
                        : new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                    Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Tag = op,
                    ToolTip = CapitalizeOperatorName(op)
                };

                var image = new Image
                {
                    Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute)),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4)
                };

                border.Child = image;
                border.MouseLeftButtonDown += OperatorIcon_Click;
                border.MouseLeftButtonUp += OperatorIcon_DoubleClick;
                border.MouseEnter += (s, e) =>
                {
                    if (border.Tag?.ToString() != _selectedOperator)
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61));
                };
                border.MouseLeave += (s, e) =>
                {
                    if (border.Tag?.ToString() != _selectedOperator)
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                };

                OperatorGrid.Children.Add(border);
            }
        }

        private string CapitalizeOperatorName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        private void OperatorIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string operatorName)
            {
                // Deselect previous
                foreach (var child in OperatorGrid.Children)
                {
                    if (child is Border b)
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                }

                // Select new
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(88, 166, 255));
                _selectedOperator = operatorName;
            }
        }

        private void OperatorIcon_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border border && border.Tag is string operatorName)
            {
                _selectedOperator = operatorName;
                SelectedOperatorIcon = operatorName;
                DialogResult = true;
                Close();
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = TbSearch.Text?.ToLower() ?? "";
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                DisplayOperators(_allOperators);
                return;
            }

            var filtered = _allOperators
                .Where(op => op.ToLower().Contains(searchText))
                .ToList();

            DisplayOperators(filtered);
        }

        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedOperator))
            {
                SelectedOperatorIcon = _selectedOperator;
                DialogResult = true;
                Close();
            }
        }

        private void NoneBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectedOperatorIcon = null;
            DialogResult = true;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
