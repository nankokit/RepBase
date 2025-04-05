using RepBase.Data;
using RepBase.Models;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using Npgsql;
using RepBase.ViewModels;
using System.Collections.Generic;
using System.Text;
using System.Globalization; // Добавляем для CultureInfo

namespace RepBase.Services
{
    public class TableService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly BackupService _backupService;

        public TableService(DatabaseManager databaseManager, BackupService backupService)
        {
            _databaseManager = databaseManager;
            _backupService = backupService;
        }

        public ObservableCollection<TableModel> LoadTables()
        {
            var tableItems = new ObservableCollection<TableModel>();
            try
            {
                var tables = _databaseManager.LoadTables();
                foreach (var table in tables)
                {
                    tableItems.Add(table);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tables: {ex.Message}");
            }
            return tableItems;
        }

        public DataTable LoadTableData(string tableName)
        {
            try
            {
                var dataTable = _databaseManager.GetTableData(tableName);
                CleanDataTable(dataTable);
                foreach (DataColumn column in dataTable.Columns)
                {
                    column.ReadOnly = false;
                }
                return dataTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data for table {tableName}: {ex.Message}");
                return null;
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

        public void AddRow(DataTable tableData, TableModel selectedTable)
        {
            if (selectedTable != null && tableData != null)
            {
                var newRow = tableData.NewRow();
                if (tableData.Columns.Contains("id"))
                {
                    int nextId = _databaseManager.GetNextId(selectedTable.TableName);
                    newRow["id"] = nextId;
                }

                foreach (DataColumn column in tableData.Columns)
                {
                    if (column.ColumnName != "id")
                    {
                        newRow[column.ColumnName] = DBNull.Value;
                    }
                }
                tableData.Rows.Add(newRow);
            }
        }

        public void UpdateCell(TableModel selectedTable, CellUpdateArgs args)
        {
            if (selectedTable == null || args == null) return;

            try
            {
                var row = args.Row;
                var columnName = args.ColumnName;
                var newValueInput = args.NewValue;
                object newValue = null;

                var column = selectedTable.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                if (column == null)
                {
                    MessageBox.Show($"Column {columnName} not found.");
                    return;
                }

                bool isNewRow = row.ItemArray.All(field => field == DBNull.Value);
                if (isNewRow)
                {
                    return;
                }

                if (!ValidateAndConvertValue(newValueInput, column.ColumnType, out newValue))
                {
                    MessageBox.Show($"Invalid value '{newValueInput}' for column '{columnName}' of type {column.ColumnType}");
                    return;
                }

                var query = $"UPDATE main.{selectedTable.TableName} SET {columnName} = @newValue WHERE ";
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating cell: {ex.Message}");
            }
        }

        public void SaveNewRow(TableModel selectedTable, DataRowView rowView)
        {
            if (selectedTable == null || rowView == null) return;

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
                        var columnDef = selectedTable.Columns.FirstOrDefault(c => c.ColumnName == column.ColumnName);
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
                    var query = $"INSERT INTO main.{selectedTable.TableName} ({string.Join(", ", columns)}) " +
                              $"VALUES ({string.Join(", ", values)})";
                    _databaseManager.ExecuteNonQueryWithParams(query, parameters);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving new row: {ex.Message}");
            }
        }

        public void DeleteRow(TableModel selectedTable, DataRowView rowView)
        {
            if (selectedTable == null || rowView == null) return;

            try
            {
                // Создаем бэкап строки перед удалением
                var rowBackupScript = GenerateRowBackupScript(selectedTable, rowView.Row);
                _backupService.SaveBackupScript(rowBackupScript, $"row_{selectedTable.TableName}_{DateTime.Now:yyyyMMddHHmmss}");

                var whereClause = BuildWhereClause(rowView.Row);
                _databaseManager.ExecuteNonQuery($"DELETE FROM main.{selectedTable.TableName} WHERE {whereClause}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting row: {ex.Message}");
            }
        }

        public void DeleteTable(TableModel selectedTable, ObservableCollection<TableModel> tableItems)
        {
            if (selectedTable == null)
            {
                MessageBox.Show("Выберите таблицу для удаления");
                return;
            }

            var result = MessageBox.Show($"Вы действительно хотите удалить таблицу '{selectedTable.TableName}'? Это действие будет нельзя отменить.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Создаем бэкап таблицы перед удалением
                    var tableData = _databaseManager.GetTableData(selectedTable.TableName);
                    var tableBackupScript = GenerateTableBackupScript(selectedTable, tableData);
                    _backupService.SaveBackupScript(tableBackupScript, $"table_{selectedTable.TableName}");

                    _databaseManager.DropTable(selectedTable.TableName);
                    tableItems.Remove(selectedTable);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления таблицы: {ex.Message}");
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
                        string inputStr = input.ToString();
                        // Проверяем, является ли строка валидным JSON
                        System.Text.Json.JsonSerializer.Deserialize<object>(inputStr);
                        result = inputStr;
                        return true;
                    }
                    catch
                    {
                        // Если строка не является валидным JSON, пытаемся преобразовать ее
                        string inputStr = input.ToString();
                        try
                        {
                            // Если это просто строка, оборачиваем ее в кавычки
                            if (!inputStr.StartsWith("{") && !inputStr.StartsWith("["))
                            {
                                inputStr = $"\"{inputStr}\"";
                                System.Text.Json.JsonSerializer.Deserialize<object>(inputStr); // Проверяем, что теперь это валидный JSON
                                result = inputStr;
                                return true;
                            }
                            // Если преобразовать не удалось, возвращаем false
                            return false;
                        }
                        catch
                        {
                            MessageBox.Show($"Value '{inputStr}' is not a valid JSON format.");
                            return false;
                        }
                    }

                default:
                    return false;
            }
        }

        private string GenerateRowBackupScript(TableModel table, DataRow row)
        {
            return GenerateInsertStatement(table, row);
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
                case ColumnType.Decimal:
                case ColumnType.Real:
                    // Используем InvariantCulture для записи чисел с точкой
                    return Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
                case ColumnType.Integer:
                    return value.ToString();
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
    }
}