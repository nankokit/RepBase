using RepBase.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization; // Добавляем для CultureInfo
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Npgsql;
using RepBase.Models;
using System.Collections.ObjectModel;

namespace RepBase.Services
{
    public class BackupService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly string _backupFolder = "Backups";

        public BackupService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }
        }

        public void CreateBackup(ObservableCollection<TableModel> tables)
        {
            try
            {
                var backupScript = GenerateDatabaseBackupScript(tables);
                SaveBackupScript(backupScript, "database");
                MessageBox.Show("Резервная копия базы данных успешно создана.", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания бэкапа: {ex.Message}", "Ошибка");
            }
        }

        public List<BackupInfo> GetBackups()
        {
            var backups = new List<BackupInfo>();
            var backupFiles = Directory.GetFiles(_backupFolder, "*.sql").OrderByDescending(f => f);

            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    Name = fileInfo.Name,
                    FilePath = fileInfo.FullName,
                    CreationTime = fileInfo.CreationTime
                });
            }

            return backups;
        }

        public void RestoreBackup(string backupFilePath)
        {
            try
            {
                // Читаем SQL-скрипт из файла
                string backupScript = File.ReadAllText(backupFilePath);

                using (var connection = _databaseManager.GetConnection())
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(backupScript, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Бэкап успешно восстановлен.", "Успех");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при восстановлении бэкапа: {ex.Message}");
            }
        }

        public void DeleteBackup(string backupFilePath)
        {
            try
            {
                if (File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                    MessageBox.Show("Бэкап успешно удален.", "Успех");
                }
                else
                {
                    MessageBox.Show($"Файл бэкапа не найден: {backupFilePath}", "Ошибка");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении бэкапа: {ex.Message}", "Ошибка");
            }
        }

        private string GenerateDatabaseBackupScript(ObservableCollection<TableModel> tables)
        {
            var script = new StringBuilder();
            foreach (var table in tables)
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

        public void SaveBackupScript(string script, string type)
        {
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = $"{_backupFolder}/backup_{type}_{timestamp}.sql";
            File.WriteAllText(fileName, script);
        }
    }

    public class BackupInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public DateTime CreationTime { get; set; }
    }
}