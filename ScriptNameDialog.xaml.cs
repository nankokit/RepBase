using System.Windows;

namespace RepBase
{
    public partial class ScriptNameDialog : Window
    {
        public string ScriptName => ScriptNameTextBox.Text;

        public ScriptNameDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}