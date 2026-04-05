using System.Windows;
using System.Windows.Controls;
using R6Planner.Models;

namespace R6Planner.Views
{
    public partial class CameraEditDialog : Window
    {
        public DefaultCamera Camera { get; private set; }

        public CameraEditDialog(DefaultCamera cam)
        {
            InitializeComponent();
            Camera = cam;
            TbName.Text = cam.Name;
            TbAngle.Text = cam.AngleDeg.ToString();
            TbFov.Text = cam.FovHalfDeg.ToString();
            TbRange.Text = cam.RangeNorm.ToString();
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TbAngle.Text, out var angle) &&
                double.TryParse(TbFov.Text, out var fov) &&
                double.TryParse(TbRange.Text, out var range))
            {
                Camera.Name = TbName.Text;
                Camera.AngleDeg = angle;
                Camera.FovHalfDeg = fov;
                Camera.RangeNorm = range;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Invalid numeric values.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}