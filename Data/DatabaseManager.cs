﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Npgsql;
using RepBase.Models;

namespace RepBase.Data
{
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public void ExecuteNonQuery(string query)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                Console.WriteLine($"Query complited: {query}");
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error executing npsql query: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
            }

        }

        public DataTable ExecuteQuery(string query)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);
                            Console.WriteLine($"Query complited: {query}");
                            return dataTable;
                        }
                    }                    
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error executing npsql query: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
            }
            return new DataTable();
        }

        public void ExecuteNonQueryWithParams(string query, List<Npgsql.NpgsqlParameter> parameters)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
                throw;
            }
        }
        public object ExecuteScalar(string query, List<Npgsql.NpgsqlParameter> parameters)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                        return command.ExecuteScalar(); // Возвращаем одно значение
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
                throw;
            }
        }

        public List<TableModel> LoadTables()
        {
            var tables = new List<TableModel>();
            LoadTableNames(tables);
            LoadTableColumns(tables);
            foreach (var tableModel in tables)
            {
                LoadTableData(tableModel);
            }
            return tables;
        }
        public void LoadTableNames(List<TableModel> tables)
        {
            try
            {
                string query = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main'";
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var tableName = reader.GetString(0);
                            tables.Add(new TableModel { TableName = tableName, Columns = new List<ColumnModel>(), Rows = new List<RowModel>() });
                        }
                    }
                    connection.Close();
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
            }
        }

        public void LoadTableColumns(List<TableModel> tables)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    foreach (var tableModel in tables)
                    {
                        Console.WriteLine($"Loading {tableModel.TableName} columns:");
                        string queryColumns = $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{tableModel.TableName}' AND table_schema = 'main'";
                        using (var commandColumns = new NpgsqlCommand(queryColumns, connection))
                        using (var readerColumns = commandColumns.ExecuteReader())
                        {
                            while (readerColumns.Read())
                            {
                                var columnName = readerColumns.GetString(0);
                                var dataType = readerColumns.GetString(1);
                                var columnType = MapDataTypeToColumnType(dataType);
                                tableModel.Columns.Add(new ColumnModel(columnName, columnType));
                                Console.WriteLine($"Added column {columnName} with type {columnType}");
                            }

                        }
                        
                    }

                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
                throw;
            }
        }

        private ColumnType MapDataTypeToColumnType(string dataType)
        {
            dataType = dataType.ToLower();
            switch (dataType)
            {
                case "character varying":
                    return ColumnType.CharacterVarying;
                case "text":
                    return ColumnType.String;
                case "integer":
                    return ColumnType.Integer;
                case "real":
                    return ColumnType.Real;
                case "boolean":
                    return ColumnType.Boolean;
                case "timestamp without time zone":
                    return ColumnType.DateTime;
                case "numeric":
                case "decimal":
                    return ColumnType.Decimal;
                case "json":
                    return ColumnType.Json;
                default:
                    throw new ArgumentException($"Unsupported data type: {dataType}");
            }
        }

        public void LoadTableData(TableModel tableModel)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    var tableName = tableModel.TableName;
                    string queryRows = $"SELECT * FROM main.{tableName}";
                    using (var commandRows = new NpgsqlCommand(queryRows, connection))
                    using (var readerRows = commandRows.ExecuteReader())
                    {
                        while (readerRows.Read())
                        {
                            var row = new RowModel();
                            for (int i = 0; i < readerRows.FieldCount; i++)
                            {
                                row.Values[readerRows.GetName(i)] = readerRows.GetValue(i);
                            }
                            tableModel.Rows.Add(row);
                        }

                    }
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
            }
            Console.WriteLine($"Loaded {tableModel.Rows.Count} rows from {tableModel.TableName}.");
        }

        public DataTable GetTableData(string tableName)
        {
            string query = $"SELECT * FROM main.{tableName}";
            return ExecuteQuery(query);
        }
        public void CreateTable(string tableName, string tableDefinition)
        {
            string query = $"CREATE TABLE IF NOT EXISTS main.{tableName} ({tableDefinition});";
            ExecuteNonQuery(query);
        }

        public void DropTable(string tableName)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string query = $"DROP TABLE IF EXISTS main.{tableName} CASCADE;";
                    Console.WriteLine($"Executing query: {query}"); // Для отладки
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        Console.WriteLine($"DropTable: {rowsAffected} rows affected"); // Для отладки
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DropTable: {ex.Message}"); // Для отладки
                throw; // Перебрасываем исключение, чтобы увидеть его в MainViewModel
            }
        }
        public int GetNextId(string tableName)
        {
            var dataTable = GetTableData(tableName);
            if (!dataTable.Columns.Contains("id")||dataTable.Rows.Count == 0)
            {
                return 1;
            }
            return dataTable.AsEnumerable().Max(row => row.Field<int>("id")) + 1;
        }
    }
}
