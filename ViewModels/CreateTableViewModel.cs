using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using RepBase.Data;
using RepBase.Models;

namespace RepBase.ViewModels
{
    public class CreateTableViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private string _tableName;
        private ObservableCollection<ColumnModel> _columns;

        public string TableName
        {
            get => _tableName;
            set { _tableName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ColumnModel> Columns
        {
            get => _columns;
            set { _columns = value; OnPropertyChanged(); }
        }

        public Array ColumnTypes => Enum.GetValues(typeof(ColumnType));

        public ICommand AddColumnCommand { get; }
        public ICommand RemoveColumnCommand { get; }
        public ICommand CreateTableCommand { get; }

        public CreateTableViewModel(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            Columns = new ObservableCollection<ColumnModel>();
            AddColumnCommand = new RelayCommand(AddColumn);
            RemoveColumnCommand = new RelayCommand(RemoveColumn);
            CreateTableCommand = new RelayCommand(CreateTable);
        }

        private void AddColumn(object parameter)
        {
            Columns.Add(new ColumnModel("NewColumn", ColumnType.String));
        }

        private void RemoveColumn(object parameter)
        {
            if (parameter is ColumnModel column)
            {
                Columns.Remove(column);
            }
        }

        private void CreateTable(object parameter)
        {
            if (string.IsNullOrWhiteSpace(TableName))
            {
                MessageBox.Show("Table name cannot be empty.");
                return;
            }

            if (!Columns.Any())
            {
                MessageBox.Show("At least one column must be defined.");
                return;
            }

            // Проверяем, что имена колонок уникальны
            var columnNames = Columns.Select(c => c.ColumnName).ToList();
            if (columnNames.Distinct().Count() != columnNames.Count)
            {
                MessageBox.Show("Column names must be unique.");
                return;
            }

            // Добавляем колон, если его нет
            if (!Columns.Any(c => c.ColumnName.ToLower() == "id"))
            {
                Columns.Insert(0, new ColumnModel("id", ColumnType.Integer) { ColumnName = "id" });
            }

            try
            {
                // Формируем определение таблицы
                var columnDefinitions = new List<string>();
                foreach (var column in Columns)
                {
                    string columnDef = $"{column.ColumnName} {MapColumnTypeToSqlType(column.ColumnType)}";
                    if (column.ColumnName.ToLower() == "id")
                    {
                        columnDef += " PRIMARY KEY";
                    }
                    columnDefinitions.Add(columnDef);
                }

                string tableDefinition = string.Join(", ", columnDefinitions);
                _databaseManager.CreateTable(TableName, tableDefinition);

                MessageBox.Show($"Table '{TableName}' created successfully.");
                (parameter as Window)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating table: {ex.Message}");
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}