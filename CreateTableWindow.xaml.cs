using RepBase.Data;
using RepBase.ViewModels;
using System.Windows;

namespace RepBase
{
    public partial class CreateTableWindow : Window
    {
        public CreateTableWindow(DatabaseManager databaseManager)
        {
            InitializeComponent();
            DataContext = new CreateTableViewModel(databaseManager);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}