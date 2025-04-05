using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Npgsql;
using RepBase.Data;
using RepBase.Models;
using OfficeOpenXml;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace RepBase.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private ObservableCollection<TableModel> _tableItems;
        private TableModel _selectedTable;
        private DataTable _tableData;
        private ObservableCollection<string> _scriptNames;
        private string _selectedScriptName;
        private string _currentScript;
        private Dictionary<string, string> _scripts;


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
                    LoadTableData(value.TableName);
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
        public ObservableCollection<string> ScriptNames
        {
            get => _scriptNames;
            set { _scriptNames = value; OnPropertyChanged(); }
        }

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
                Console.WriteLine($"CurrentScript updated to: {value}"); // Для отладки
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
            TableItems = new ObservableCollection<TableModel>();
            ScriptNames = new ObservableCollection<string>(); // Уже есть
            _scripts = new Dictionary<string, string>();

            InitializeScripts();

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

            LoadTables();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }
        private void CreateBackup(object parameter)
        {
            try
            {
                var backupScript = GenerateDatabaseBackupScript();
                SaveBackupScript(backupScript, "database");
                MessageBox.Show("Резервная копия базы данных успешно создана.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания бэкапа: {ex.Message}");
            }
        }

        private void ShowRestoreBackup(object parameter)
        {
            var restoreViewModel = new RestoreBackupViewModel(_databaseManager);
            var restoreWindow = new RestoreBackupWindow(restoreViewModel)
            {
                Owner = Application.Current.MainWindow
            };
            restoreWindow.ShowDialog();
            LoadTables(); // Обновляем список таблиц после восстановления
        }

        private void DeleteTable(object parameter)
        {
            if (SelectedTable == null)
            {
                MessageBox.Show("Выберите таблицу для удаления");
                return;
            }

            var result = MessageBox.Show($"Вы действительно хотите удалить таблицу '{SelectedTable.TableName}'? Это действие будет нельзя отменить.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Создаем бэкап таблицы перед удалением
                    var tableData = _databaseManager.GetTableData(SelectedTable.TableName);
                    var tableBackupScript = GenerateTableBackupScript(SelectedTable, tableData);
                    SaveBackupScript(tableBackupScript, $"table_{SelectedTable.TableName}");

                    // Удаляем таблицу
                    _databaseManager.DropTable(SelectedTable.TableName);
                    Console.WriteLine($"Table '{SelectedTable.TableName}' dropped successfully."); // Для отладки

                    // Обновляем состояние
                    TableItems.Remove(SelectedTable); // Удаляем из списка
                    SelectedTable = null;
                    TableData = null;
                    LoadTables(); // Обновляем список таблиц
                    MessageBox.Show($"Таблица '{SelectedTable.TableName}' успешно удалена.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления таблицы: {ex.Message}");
                    LoadTables(); // На случай, если таблица всё же удалилась частично
                }
            }
        }

        private void DeleteRow(object parameter)
        {
            if (SelectedTable != null && parameter is DataRowView rowView)
            {
                try
                {
                    // Создаем бэкап строки перед удалением
                    var rowBackupScript = GenerateRowBackupScript(SelectedTable, rowView.Row);
                    SaveBackupScript(rowBackupScript, $"row_{SelectedTable.TableName}_{DateTime.Now:yyyyMMddHHmmss}");

                    var whereClause = BuildWhereClause(rowView.Row);
                    _databaseManager.ExecuteNonQuery($"DELETE FROM main.{SelectedTable.TableName} WHERE {whereClause}");
                    TableData.Rows.Remove(rowView.Row);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting row: {ex.Message}");
                }
            }
        }

        private string GenerateDatabaseBackupScript()
        {
            var script = new StringBuilder();
            foreach (var table in TableItems)
            {
                var tableData = _databaseManager.GetTableData(table.TableName);
                script.AppendLine(GenerateTableBackupScript(table, tableData));
                script.AppendLine();
            }
            return script.ToString();
        }

        private string GenerateTableBackupScript(TableModel table, DataTable tableData)
        {
            var script = new StringBuilder();

            // Генерируем CREATE TABLE
            var columnDefs = table.Columns.Select(c =>
            {
                string colDef = $"{c.ColumnName} {MapColumnTypeToSqlType(c.ColumnType)}";
                if (c.ColumnName.ToLower() == "id") colDef += " PRIMARY KEY";
                return colDef;
            });
            script.AppendLine($"DROP TABLE IF EXISTS main.{table.TableName};");
            script.AppendLine($"CREATE TABLE main.{table.TableName} ({string.Join(", ", columnDefs)});");

            // Генерируем INSERT для данных
            if (tableData != null && tableData.Rows.Count > 0)
            {
                foreach (DataRow row in tableData.Rows)
                {
                    script.AppendLine(GenerateInsertStatement(table, row));
                }
            }

            return script.ToString();
        }

        private string GenerateRowBackupScript(TableModel table, DataRow row)
        {
            return GenerateInsertStatement(table, row);
        }

        private string GenerateInsertStatement(TableModel table, DataRow row)
        {
            var columns = new List<string>();
            var values = new List<string>();

            foreach (DataColumn col in row.Table.Columns)
            {
                var value = row[col.ColumnName];
                if (value != DBNull.Value)
                {
                    columns.Add(col.ColumnName);
                    var columnDef = table.Columns.FirstOrDefault(c => c.ColumnName == col.ColumnName);
                    if (columnDef != null)
                    {
                        values.Add(FormatValueForSql(value, columnDef.ColumnType));
                    }
                }
            }

            if (columns.Any())
            {
                return $"INSERT INTO main.{table.TableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});";
            }
            return "";
        }

        private string FormatValueForSql(object value, ColumnType columnType)
        {
            if (value == DBNull.Value) return "NULL";

            switch (columnType)
            {
                case ColumnType.String:
                case ColumnType.CharacterVarying:
                case ColumnType.Json:
                    return $"'{value.ToString().Replace("'", "''")}'";
                case ColumnType.Boolean:
                    return (bool)value ? "TRUE" : "FALSE";
                case ColumnType.DateTime:
                    return $"'{(DateTime)value:yyyy-MM-dd HH:mm:ss}'";
                default:
                    return value.ToString();
            }
        }

        private string MapColumnTypeToSqlType(ColumnType columnType)
        {
            switch (columnType)
            {
                case ColumnType.String:
                    return "TEXT";
                case ColumnType.CharacterVarying:
                    return "VARCHAR(255)";
                case ColumnType.Integer:
                    return "INTEGER";
                case ColumnType.Boolean:
                    return "BOOLEAN";
                case ColumnType.DateTime:
                    return "TIMESTAMP";
                case ColumnType.Decimal:
                    return "DECIMAL";
                case ColumnType.Real:
                    return "REAL";
                case ColumnType.Json:
                    return "JSON";
                default:
                    throw new ArgumentException($"Unsupported column type: {columnType}");
            }
        }

        private void SaveBackupScript(string script, string type)
        {
            if (!Directory.Exists("Backups"))
            {
                Directory.CreateDirectory("Backups");
            }

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = $"Backups/backup_{type}_{timestamp}.sql";
            File.WriteAllText(fileName, script);
        }
        private void InitializeScripts()
        {
            _scripts["новый скрипт"] = "";
            _scripts["Get All Tables"] = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main'";
            _scripts["Count Rows"] = "SELECT table_name, (SELECT count(*) FROM main.\"table_name\") as row_count FROM information_schema.tables WHERE table_schema = 'main'";
            _scripts["Get Schema Info"] = "SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema = 'main'";

            // Загрузка пользовательских скриптов из файла
            LoadScriptsFromFile();

            // Обновляем список названий
            ScriptNames.Clear();
            foreach (var scriptName in _scripts.Keys)
            {
                ScriptNames.Add(scriptName);
                Console.WriteLine($"Added script to ScriptNames: {scriptName}"); // Для отладки
            }

            // Убедимся, что UI уведомлен об изменении
            OnPropertyChanged(nameof(ScriptNames));

            // Устанавливаем начальное значение
            if (ScriptNames.Any())
            {
                SelectedScriptName = ScriptNames.First();
            }
        }
        private void LoadScriptsFromFile()
        {
            try
            {
                if (File.Exists("user_scripts.json"))
                {
                    var json = File.ReadAllText("user_scripts.json");
                    var loadedScripts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loadedScripts != null)
                    {
                        foreach (var script in loadedScripts)
                        {
                            if (!_scripts.ContainsKey(script.Key))
                            {
                                _scripts[script.Key] = script.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading scripts: {ex.Message}");
            }
        }
        private void SaveScriptsToFile()
        {
            try
            {
                var userScripts = _scripts.Where(s => !new[] { "новый скрипт", "Get All Tables", "Count Rows", "Get Schema Info" }.Contains(s.Key))
                                         .ToDictionary(s => s.Key, s => s.Value);
                var json = JsonSerializer.Serialize(userScripts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("user_scripts.json", json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving scripts: {ex.Message}");
            }
        }

        public void UpdateCurrentScript()
        {
            if (_scripts.ContainsKey(SelectedScriptName))
            {
                var newScriptContent = _scripts[SelectedScriptName];
                if (CurrentScript != newScriptContent)
                {
                    CurrentScript = newScriptContent;
                }
            }
        }

        public string GetScriptContent(string scriptName)
        {
            return _scripts.ContainsKey(scriptName) ? _scripts[scriptName] : "";
        }

        private void ExecuteScript(object parameter)
        {
            if (string.IsNullOrWhiteSpace(CurrentScript))
            {
                MessageBox.Show("Введите SQL-скрипт для выполнения.");
                return;
            }

            try
            {
                var result = _databaseManager.ExecuteQuery(CurrentScript);
                if (result != null && result.Rows.Count > 0)
                {
                    TableData = result; // Отображаем результат в DataGrid
                }
                else
                {
                    MessageBox.Show("Скрипт выполнен, но не вернул данных.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing script: {ex.Message}");
            }
        }

        private void SaveScript(object parameter)
        {
            if (string.IsNullOrWhiteSpace(CurrentScript))
            {
                MessageBox.Show("Введите SQL-скрипт для сохранения.");
                return;
            }

            var scriptName = PromptForScriptName();
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return;
            }

            if (_scripts.ContainsKey(scriptName) && scriptName != "новый скрипт")
            {
                var result = MessageBox.Show($"Скрипт с именем '{scriptName}' уже существует. Перезаписать?",
                    "Подтверждение", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _scripts[scriptName] = CurrentScript;
            if (!ScriptNames.Contains(scriptName))
            {
                ScriptNames.Add(scriptName);
            }
            SaveScriptsToFile();
            SelectedScriptName = scriptName;
            MessageBox.Show($"Скрипт '{scriptName}' успешно сохранен.");
        }

        private string PromptForScriptName()
        {
            var dialog = new Window
            {
                Title = "Введите имя скрипта",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Отмена", Width = 75 };

            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(new TextBlock { Text = "Введите имя скрипта:" });
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);
            dialog.Content = stackPanel;

            return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : null;
        }

        private void CreateTable(object parameter)
        {
            var createTableWindow = new CreateTableWindow(_databaseManager);
            createTableWindow.Owner = Application.Current.MainWindow;
            createTableWindow.ShowDialog();

            LoadTables();
        }

        private void ExportToExcel(object parameter)
        {
            if (TableData == null || SelectedTable == null)
            {
                MessageBox.Show("Не выбраны данные для экспорта.");
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{SelectedTable.TableName}_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add(SelectedTable.TableName);

                        for (int i = 0; i < TableData.Columns.Count; i++)
                        {
                            worksheet.Cells[1, i + 1].Value = TableData.Columns[i].ColumnName;
                            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                        }

                        for (int row = 0; row < TableData.Rows.Count; row++)
                        {
                            for (int col = 0; col < TableData.Columns.Count; col++)
                            {
                                var value = TableData.Rows[row][col];
                                worksheet.Cells[row + 2, col + 1].Value = value == DBNull.Value ? null : value;
                            }
                        }

                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"Data successfully exported to {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to Excel: {ex.Message}");
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
                        ExportCurrentTable();
                        break;
                    case ExportOptionsWindow.ExportType.ScriptResult:
                        ExportScriptResult();
                        break;
                    case ExportOptionsWindow.ExportType.EntireDatabase:
                        ExportEntireDatabase();
                        break;
                }
            }
        }
        private void ExportCurrentTable()
        {
            if (TableData == null || SelectedTable == null)
            {
                MessageBox.Show("Не выбраны данные для экспорта.");
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{SelectedTable.TableName}_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add(SelectedTable.TableName);
                        ExportDataTableToWorksheet(TableData, worksheet);
                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"Данные успешно экспортированы в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }

        private void ExportScriptResult()
        {
            if (string.IsNullOrWhiteSpace(CurrentScript))
            {
                MessageBox.Show("Введите SQL-скрипт для экспорта результата.");
                return;
            }

            try
            {
                var result = _databaseManager.ExecuteQuery(CurrentScript);
                if (result == null || result.Rows.Count == 0)
                {
                    MessageBox.Show("Скрипт не вернул данных для экспорта.");
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "script_result_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("Script_Result");
                        ExportDataTableToWorksheet(result, worksheet);
                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"Результат скрипта успешно экспортирован в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта результата скрипта: {ex.Message}");
            }
        }

        private void ExportEntireDatabase()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "database_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        foreach (var table in TableItems)
                        {
                            var tableData = _databaseManager.GetTableData(table.TableName);
                            if (tableData != null && tableData.Rows.Count > 0)
                            {
                                var worksheet = package.Workbook.Worksheets.Add(table.TableName);
                                ExportDataTableToWorksheet(tableData, worksheet);
                            }
                        }

                        if (!package.Workbook.Worksheets.Any())
                        {
                            MessageBox.Show("Нет данных для экспорта.");
                            return;
                        }

                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"База данных успешно экспортирована в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта базы данных: {ex.Message}");
            }
        }

        private void ExportDataTableToWorksheet(DataTable dataTable, ExcelWorksheet worksheet)
        {
            // Экспортируем заголовки
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                worksheet.Cells[1, i + 1].Value = dataTable.Columns[i].ColumnName;
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            // Экспортируем данные
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var value = dataTable.Rows[row][col];
                    worksheet.Cells[row + 2, col + 1].Value = value == DBNull.Value ? null : value;
                }
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }
        private void LoadTables()
        {
            try
            {
                var tables = _databaseManager.LoadTables();
                TableItems.Clear();
                foreach (var table in tables)
                {
                    TableItems.Add(table);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tables: {ex.Message}");
            }
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                var dataTable = _databaseManager.GetTableData(tableName);
                CleanDataTable(dataTable);
                foreach (DataColumn column in dataTable.Columns)
                {
                    column.ReadOnly = false;
                }
                TableData = dataTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data for table {tableName}: {ex.Message}");
            }
        }

        private void CleanDataTable(DataTable dataTable)
        {
            foreach (DataRow row in dataTable.Rows.Cast<DataRow>()
                .Where(r => r.ItemArray.All(field => field == DBNull.Value)).ToList())
            {
                dataTable.Rows.Remove(row);
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
            if (SelectedTable != null && TableData != null)
            {
                var newRow = TableData.NewRow();
                if (TableData.Columns.Contains("id"))
                {
                    int nextId = _databaseManager.GetNextId(SelectedTable.TableName);
                    newRow["id"] = nextId;
                }

                foreach (DataColumn column in TableData.Columns)
                {
                    if (column.ColumnName != "id")
                    {
                        newRow[column.ColumnName] = DBNull.Value;
                    }
                }
                TableData.Rows.Add(newRow);
                OnPropertyChanged(nameof(TableData));
            }
        }

        private void UpdateCell(object parameter)
        {
            if (SelectedTable != null && parameter is CellUpdateArgs args)
            {
                try
                {
                    var row = args.Row;
                    var columnName = args.ColumnName;
                    var newValueInput = args.NewValue;
                    object newValue = null;

                    var column = SelectedTable.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                    if (column == null)
                    {
                        MessageBox.Show($"Column {columnName} not found.");
                        return;
                    }

                    bool isNewRow = row.ItemArray.All(field => field == DBNull.Value);
                    if (isNewRow)
                    {
                        //MessageBox.Show("Cannot update cells in a new row until all fields are filled.");
                        return;
                    }

                    if (!ValidateAndConvertValue(newValueInput, column.ColumnType, out newValue))
                    {
                        MessageBox.Show($"Invalid value '{newValueInput}' for column '{columnName}' of type {column.ColumnType}");
                        return;
                    }

                    var query = $"UPDATE main.{SelectedTable.TableName} SET {columnName} = @newValue WHERE ";
                    var conditions = new List<string>();
                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("@newValue", newValue ?? DBNull.Value)
                    };

                    int paramCount = 0;
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        if (col.ColumnName != columnName)
                        {
                            var value = row[col.ColumnName];
                            if (value != DBNull.Value)
                            {
                                conditions.Add($"{col.ColumnName} = @p{paramCount}");
                                parameters.Add(new NpgsqlParameter($"@p{paramCount}", value));
                                paramCount++;
                            }
                        }
                    }

                    query += conditions.Any() ? string.Join(" AND ", conditions) : "1=1";
                    _databaseManager.ExecuteNonQueryWithParams(query, parameters);

                    row[columnName] = newValue ?? DBNull.Value;
                    OnPropertyChanged(nameof(TableData));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating cell: {ex.Message}");
                }
            }
        }

        private void SaveNewRow(object parameter)
        {
            if (SelectedTable != null && parameter is DataRowView rowView)
            {
                try
                {
                    var columns = new List<string>();
                    var values = new List<string>();
                    var parameters = new List<NpgsqlParameter>();
                    int paramCount = 0;

                    if (rowView.Row.Table.Columns.Contains("id"))
                    {
                        columns.Add("id");
                        values.Add("@p0");
                        parameters.Add(new NpgsqlParameter("@p0", rowView.Row["id"]));
                        paramCount++;
                    }

                    foreach (DataColumn column in rowView.Row.Table.Columns)
                    {
                        if (column.ColumnName != "id")
                        {
                            var value = rowView.Row[column.ColumnName];
                            var columnDef = SelectedTable.Columns.FirstOrDefault(c => c.ColumnName == column.ColumnName);
                            if (columnDef != null && value != DBNull.Value)
                            {
                                if (!ValidateAndConvertValue(value.ToString(), columnDef.ColumnType, out object validatedValue))
                                {
                                    MessageBox.Show($"Invalid value '{value}' for column '{column.ColumnName}' of type {columnDef.ColumnType}");
                                    return;
                                }
                                paramCount++;
                                columns.Add(column.ColumnName);
                                values.Add($"@p{paramCount}");
                                parameters.Add(new NpgsqlParameter($"@p{paramCount}", validatedValue));
                            }
                        }
                    }

                    if (columns.Any())
                    {
                        var query = $"INSERT INTO main.{SelectedTable.TableName} ({string.Join(", ", columns)}) " +
                                  $"VALUES ({string.Join(", ", values)})";
                        _databaseManager.ExecuteNonQueryWithParams(query, parameters);
                    }

                    LoadTableData(SelectedTable.TableName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving new row: {ex.Message}");
                }
            }
        }

        private string BuildWhereClause(DataRow row)
        {
            var conditions = new StringBuilder();
            bool firstCondition = true;

            foreach (DataColumn column in row.Table.Columns)
            {
                var value = row[column.ColumnName];
                if (value != DBNull.Value)
                {
                    if (!firstCondition)
                    {
                        conditions.Append(" AND ");
                    }
                    conditions.Append($"{column.ColumnName} = '{value}'");
                    firstCondition = false;
                }
            }

            return conditions.Length > 0 ? conditions.ToString() : "1=1";
        }

        private bool ValidateAndConvertValue(object input, ColumnType columnType, out object result)
        {
            result = null;

            if (input == null || (input is string str && string.IsNullOrWhiteSpace(str)))
            {
                result = DBNull.Value;
                return true;
            }

            switch (columnType)
            {
                case ColumnType.Boolean:
                    if (input is bool boolValue)
                    {
                        result = boolValue;
                        return true;
                    }
                    if (input is string strValue)
                    {
                        if (bool.TryParse(strValue, out bool parsedBool))
                        {
                            result = parsedBool;
                            return true;
                        }
                        if (strValue.ToLower() == "true" || strValue == "1") { result = true; return true; }
                        if (strValue.ToLower() == "false" || strValue == "0") { result = false; return true; }
                    }
                    return false;

                case ColumnType.String:
                case ColumnType.CharacterVarying:
                    result = input.ToString();
                    return true;

                case ColumnType.Integer:
                    if (int.TryParse(input.ToString(), out int intValue))
                    {
                        result = intValue;
                        return true;
                    }
                    return false;

                case ColumnType.DateTime:
                    if (DateTime.TryParse(input.ToString(), out DateTime dateValue))
                    {
                        result = dateValue;
                        return true;
                    }
                    return false;

                case ColumnType.Decimal:
                    if (decimal.TryParse(input.ToString(), out decimal decValue))
                    {
                        result = decValue;
                        return true;
                    }
                    return false;

                case ColumnType.Real:
                    if (float.TryParse(input.ToString(), out float floatValue))
                    {
                        result = floatValue;
                        return true;
                    }
                    return false;

                case ColumnType.Json:
                    try
                    {
                        System.Text.Json.JsonSerializer.Deserialize<object>(input.ToString());
                        result = input.ToString();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    return false;
            }
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