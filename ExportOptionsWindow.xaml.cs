using System.Windows;

namespace RepBase
{
    public partial class ExportOptionsWindow : Window
    {
        public enum ExportType
        {
            CurrentTable,
            ScriptResult,
            EntireDatabase
        }

        public ExportType SelectedExportType { get; private set; }

        public ExportOptionsWindow()
        {
            InitializeComponent();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (exportTableRadio.IsChecked == true)
            {
                SelectedExportType = ExportType.CurrentTable;
            }
            else if (exportScriptRadio.IsChecked == true)
            {
                SelectedExportType = ExportType.ScriptResult;
            }
            else if (exportAllRadio.IsChecked == true)
            {
                SelectedExportType = ExportType.EntireDatabase;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}