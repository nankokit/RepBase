using System.Windows;

namespace RepBase
{
    public partial class RestoreBackupWindow : Window
    {
        public RestoreBackupWindow(RestoreBackupViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}