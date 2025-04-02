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

namespace RepBase.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private ObservableCollection<TableModel> _tableItems;
        private TableModel _selectedTable;
        private DataTable _tableData;

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

        public ICommand SelectTableCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand DeleteRowCommand { get; }
        public ICommand UpdateCellCommand { get; }
        public ICommand SaveNewRowCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand CreateTableCommand { get; } // Новая команда
        public ICommand DeleteTableCommand { get; } // Новая команда

        public MainViewModel()
        {
            _databaseManager = new DatabaseManager("Host=localhost;Port=5432;Database=repbase;Username=postgres;Password=postgres");
            TableItems = new ObservableCollection<TableModel>();

            SelectTableCommand = new RelayCommand(SelectTable);
            AddRowCommand = new RelayCommand(AddRow);
            DeleteRowCommand = new RelayCommand(DeleteRow);
            UpdateCellCommand = new RelayCommand(UpdateCell);
            SaveNewRowCommand = new RelayCommand(SaveNewRow);
            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            CreateTableCommand = new RelayCommand(CreateTable); // Инициализация
            DeleteTableCommand = new RelayCommand(DeleteTable); // Инициализация

            LoadTables();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        private void CreateTable(object parameter)
        {
            var createTableWindow = new CreateTableWindow(_databaseManager);
            createTableWindow.Owner = Application.Current.MainWindow;
            createTableWindow.ShowDialog();

            // После закрытия окна обновляем список таблиц
            LoadTables();
        }

        private void DeleteTable(object parameter)
        {
            if (SelectedTable == null)
            {
                MessageBox.Show("Please select a table to delete.");
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete the table '{SelectedTable.TableName}'? This action cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _databaseManager.DropTable(SelectedTable.TableName);
                    MessageBox.Show($"Table '{SelectedTable.TableName}' deleted successfully.");
                    SelectedTable = null; // Очищаем выбранную таблицу
                    LoadTables(); // Обновляем список таблиц
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting table: {ex.Message}");
                }
            }
        }

        private void ExportToExcel(object parameter)
        {
            if (TableData == null || SelectedTable == null)
            {
                MessageBox.Show("No table selected or data to export.");
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

        private void DeleteRow(object parameter)
        {
            if (SelectedTable != null && parameter is DataRowView rowView)
            {
                try
                {
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