using System.Windows;
using R6Planner.Models;

namespace R6Planner.Views
{
    public partial class SpawnEditDialog : Window
    {
        private readonly SpawnPoint _spawn;

        public SpawnEditDialog(SpawnPoint spawn)
        {
            InitializeComponent();
            _spawn = spawn;
            TbLetter.Text = spawn.Letter;
            TbName.Text = spawn.Name;
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            var letter = TbLetter.Text?.Trim().ToUpper() ?? "";
            if (string.IsNullOrWhiteSpace(letter))
                letter = "S";
            if (letter.Length > 2)
                letter = letter.Substring(0, 2);

            var name = TbName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "Spawn";

            _spawn.Letter = letter;
            _spawn.Name = name;
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
