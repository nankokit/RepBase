// MainWindow.xaml.cs
using RepBase.ViewModels;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace RepBase
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as MainViewModel;
                var row = e.Row.Item as DataRowView;
                var column = e.Column as DataGridColumn;

                if (viewModel != null && row != null && column != null)
                {
                    var columnName = column.Header.ToString();
                    object newValue = null;

                    if (e.Column is DataGridCheckBoxColumn)
                    {
                        newValue = (e.EditingElement as CheckBox)?.IsChecked;
                    }
                    else
                    {
                        newValue = (e.EditingElement as TextBox)?.Text;
                    }

                    var oldValue = row[columnName];
                    if (!Equals(newValue, oldValue))
                    {
                        var args = new CellUpdateArgs
                        {
                            Row = row.Row,
                            ColumnName = columnName,
                            NewValue = newValue
                        };
                        viewModel.UpdateCellCommand.Execute(args);
                    }
                }
            }
        }
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = e.PropertyName; 
        }
    }
}