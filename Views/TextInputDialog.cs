using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace R6Planner.Views
{
    public class TextInputDialog : Window
    {
        private readonly TextBox _tb;
        public string InputText => _tb.Text;

        public TextInputDialog()
        {
            Title             = "Add Text";
            Width             = 320;
            Height            = 140;
            WindowStyle       = WindowStyle.ToolWindow;
            ResizeMode        = ResizeMode.NoResize;
            Background        = new SolidColorBrush(Color.FromRgb(13, 17, 23));
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(12) };

            var label = new TextBlock
            {
                Text       = "Annotation text:",
                Foreground = new SolidColorBrush(Color.FromRgb(140, 148, 158)),
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 12,
                Margin     = new Thickness(0, 0, 0, 6)
            };

            _tb = new TextBox
            {
                Background   = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground   = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                BorderBrush  = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                CaretBrush   = Brushes.White,
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 13,
                Padding      = new Thickness(6),
                Margin       = new Thickness(0, 0, 0, 10)
            };
            _tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { DialogResult = true; } };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var ok = new Button
            {
                Content    = "OK",
                Width      = 70,
                Height     = 26,
                Background = new SolidColorBrush(Color.FromRgb(31, 58, 95)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                Margin     = new Thickness(0, 0, 8, 0),
                Cursor     = Cursors.Hand
            };
            ok.Click += (_, _) => DialogResult = true;

            var cancel = new Button
            {
                Content    = "Cancel",
                Width      = 70,
                Height     = 26,
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground = new SolidColorBrush(Color.FromRgb(140, 148, 158)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                Cursor     = Cursors.Hand
            };
            cancel.Click += (_, _) => DialogResult = false;

            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);

            root.Children.Add(label);
            root.Children.Add(_tb);
            root.Children.Add(btnRow);

            Content = root;
            Loaded += (_, _) => _tb.Focus();
        }
    }
}
