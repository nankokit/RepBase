using RepBase.ViewModels;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace RepBase
{
    public partial class MainWindow : Window
    {
        private bool _isUpdatingScriptText = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            var viewModel = DataContext as MainViewModel;
            if (viewModel != null)
            {
                Console.WriteLine($"ScriptNames count after initialization: {viewModel.ScriptNames.Count}");
                foreach (var script in viewModel.ScriptNames)
                {
                    Console.WriteLine($"Script: {script}");
                }
            }
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

        private void ScriptsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            if (viewModel != null && scriptsComboBox.SelectedItem != null)
            {
                _isUpdatingScriptText = true;
                viewModel.UpdateCurrentScript();
                _isUpdatingScriptText = false;
            }
        }

        private void ScriptTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingScriptText) return;

            var viewModel = DataContext as MainViewModel;
            if (viewModel != null)
            {
                var currentScriptContent = viewModel.GetScriptContent(viewModel.SelectedScriptName);
                if (scriptTextBox.Text != currentScriptContent && viewModel.SelectedScriptName != "новый скрипт")
                {
                    viewModel.SelectedScriptName = "новый скрипт";
                }
            }
        }
    }
}