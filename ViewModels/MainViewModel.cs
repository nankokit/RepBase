using RepBase.Data;
using RepBase.Models;
using RepBase.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RepBase.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private readonly TableService _tableService;
        private readonly ScriptService _scriptService;
        private readonly BackupService _backupService;
        private readonly ExportService _exportService;

        private ObservableCollection<TableModel> _tableItems;
        private TableModel _selectedTable;
        private DataTable _tableData;
        private string _selectedScriptName;
        private string _currentScript;

        public ObservableCollection<TableModel> TableItems
        {
            get => _tableItems;
            set { _tableItems = value; OnPropertyChanged(); }
        }

        public TableModel SelectedTable
        {
            get => _selectedTable;
            set
            {
                _selectedTable = value;
                OnPropertyChanged();
                if (value != null)
                {
                    TableData = _tableService.LoadTableData(value.TableName);
                }
                else
                {
                    TableData = null;
                }
            }
        }

        public DataTable TableData
        {
            get => _tableData;
            set { _tableData = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ScriptNames => _scriptService.ScriptNames;

        public string SelectedScriptName
        {
            get => _selectedScriptName;
            set
            {
                _selectedScriptName = value;
                OnPropertyChanged();
                UpdateCurrentScript();
            }
        }

        public string CurrentScript
        {
            get => _currentScript;
            set
            {
                _currentScript = value;
                Console.WriteLine($"CurrentScript updated to: {value}");
                OnPropertyChanged();
            }
        }

        public ICommand SelectTableCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand DeleteRowCommand { get; }
        public ICommand UpdateCellCommand { get; }
        public ICommand SaveNewRowCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand CreateTableCommand { get; }
        public ICommand DeleteTableCommand { get; }
        public ICommand ExecuteScriptCommand { get; }
        public ICommand SaveScriptCommand { get; }
        public ICommand ShowExportOptionsCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand ShowRestoreBackupCommand { get; }

        public MainViewModel()
        {
            _databaseManager = new DatabaseManager("Host=localhost;Port=5432;Database=repbase;Username=postgres;Password=postgres");
            _backupService = new BackupService(_databaseManager);
            _tableService = new TableService(_databaseManager, _backupService);
            _scriptService = new ScriptService(_databaseManager);
            _exportService = new ExportService(_databaseManager);

            TableItems = _tableService.LoadTables();

            SelectTableCommand = new RelayCommand(SelectTable);
            AddRowCommand = new RelayCommand(AddRow);
            DeleteRowCommand = new RelayCommand(DeleteRow);
            UpdateCellCommand = new RelayCommand(UpdateCell);
            SaveNewRowCommand = new RelayCommand(SaveNewRow);
            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            CreateTableCommand = new RelayCommand(CreateTable);
            DeleteTableCommand = new RelayCommand(DeleteTable);
            ExecuteScriptCommand = new RelayCommand(ExecuteScript);
            SaveScriptCommand = new RelayCommand(SaveScript);
            ShowExportOptionsCommand = new RelayCommand(ShowExportOptions);
            CreateBackupCommand = new RelayCommand(CreateBackup);
            ShowRestoreBackupCommand = new RelayCommand(ShowRestoreBackup);

            if (ScriptNames.Contains("новый скрипт"))
            {
                SelectedScriptName = "новый скрипт";
            }
            else if (ScriptNames.Any())
            {
                SelectedScriptName = ScriptNames.First();
            }
        }

        private void SelectTable(object parameter)
        {
            if (parameter is TableModel table)
            {
                SelectedTable = table;
            }
        }

        private void AddRow(object parameter)
        {
            _tableService.AddRow(TableData, SelectedTable);
            OnPropertyChanged(nameof(TableData));
        }

        private void DeleteRow(object parameter)
        {

            if (parameter is DataRowView rowView)
            {
                _tableService.DeleteRow(SelectedTable, rowView);
                TableData.Rows.Remove(rowView.Row);
            }
        }

        private void UpdateCell(object parameter)
        {
            _tableService.UpdateCell(SelectedTable, parameter as CellUpdateArgs);
            OnPropertyChanged(nameof(TableData));
        }

        private void SaveNewRow(object parameter)
        {
            if (parameter is DataRowView rowView)
            {
                _tableService.SaveNewRow(SelectedTable, rowView);
                TableData = _tableService.LoadTableData(SelectedTable.TableName);
            }
        }

        private void ExportToExcel(object parameter)
        {
            _exportService.ExportCurrentTable(SelectedTable, TableData);
        }

        private void CreateTable(object parameter)
        {
            var createTableWindow = new CreateTableWindow(_databaseManager)
            {
                Owner = Application.Current.MainWindow
            };
            createTableWindow.ShowDialog();
            TableItems = _tableService.LoadTables();
        }

        private void DeleteTable(object parameter)
        {
            _tableService.DeleteTable(SelectedTable, TableItems);
            SelectedTable = null;
            TableData = null;
            TableItems = _tableService.LoadTables();
        }

        private void ExecuteScript(object parameter)
        {
            var result = _scriptService.ExecuteScript(CurrentScript);
            if (result != null)
            {
                TableData = result;
            }
        }

        private void SaveScript(object parameter)
        {
            var scriptName = PromptForScriptName();
            if (!string.IsNullOrWhiteSpace(scriptName))
            {
                _scriptService.SaveScript(CurrentScript, scriptName);
                SelectedScriptName = scriptName;
            }
        }

        private void ShowExportOptions(object parameter)
        {
            var exportOptionsWindow = new ExportOptionsWindow
            {
                Owner = Application.Current.MainWindow
            };

            if (exportOptionsWindow.ShowDialog() == true)
            {
                switch (exportOptionsWindow.SelectedExportType)
                {
                    case ExportOptionsWindow.ExportType.CurrentTable:
                        _exportService.ExportCurrentTable(SelectedTable, TableData);
                        break;
                    case ExportOptionsWindow.ExportType.ScriptResult:
                        _exportService.ExportScriptResult(CurrentScript);
                        break;
                    case ExportOptionsWindow.ExportType.EntireDatabase:
                        _exportService.ExportEntireDatabase(TableItems);
                        break;
                }
            }
        }

        private void CreateBackup(object parameter)
        {
            _backupService.CreateBackup(TableItems);
        }

        private void ShowRestoreBackup(object parameter)
        {
            var restoreViewModel = new RestoreBackupViewModel(_databaseManager);
            var restoreWindow = new RestoreBackupWindow(restoreViewModel)
            {
                Owner = Application.Current.MainWindow
            };
            restoreWindow.ShowDialog();
            TableItems = _tableService.LoadTables();
        }

        public void UpdateCurrentScript()
        {
            CurrentScript = _scriptService.GetScriptContent(SelectedScriptName);
        }

        public string GetScriptContent(string scriptName)
        {
            return _scriptService.GetScriptContent(scriptName);
        }

        private string PromptForScriptName()
        {
            var dialog = new ScriptNameDialog();
            bool? result = dialog.ShowDialog();
            return result == true && !string.IsNullOrWhiteSpace(dialog.ScriptName) ? dialog.ScriptName : null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CellUpdateArgs
    {
        public DataRow Row { get; set; }
        public string ColumnName { get; set; }
        public object NewValue { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}