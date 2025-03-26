using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RepBase.Data;
using RepBase.Models;

namespace RepBase
{
    public partial class DataEntryWindow : Window
    {
        public ObservableCollection<FieldModel> FieldValues { get; private set; }
        private DatabaseManager _databaseManager;
        private string _tableName;

        public DataEntryWindow(DatabaseManager databaseManager, string tableName, DataTable dataTable, DataRow dataRow = null)
        {
            InitializeComponent();
            FieldValues = new ObservableCollection<FieldModel>();
            _databaseManager = databaseManager;
            _tableName = tableName;

            foreach (DataColumn column in dataTable.Columns)
            {
                if (column.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fieldValue = new FieldModel
                {
                    Key = column.ColumnName,
                    Value = dataRow != null ? dataRow[column.ColumnName]?.ToString() : string.Empty
                };
                FieldValues.Add(fieldValue);
            }

            FieldsContainer.ItemsSource = FieldValues;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fieldValuesToInsert = FieldValues.Select(f => new
                {
                    Key = f.Key,
                    Value = ConvertValue(f.Key, f.Value)
                }).ToList();

                _databaseManager.InsertIntoTable(_tableName, fieldValuesToInsert);

                MessageBox.Show("Данные успешно добавлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Close();
        }

        private object ConvertValue(string key, string value)
        {
            // Здесь можно добавить логику для определения типа данных
            // Например, если у вас есть справочник типов в вашей базе данных
            // или вы можете использовать DataColumn для определения типа

            // Пример: получаем объект DataColumn из DataTable
            // Это может быть оптимизировано в зависимости от вашей структуры
            DataColumn column = null; // Получите DataColumn из вашего DataTable

            // Пример: Для демонстрации только
            if (column != null)
            {
                switch (column.DataType.Name)
                {
                    case "Int32":
                        return string.IsNullOrEmpty(value) ? (object)DBNull.Value : Convert.ToInt32(value);
                    case "DateTime":
                        return string.IsNullOrEmpty(value) ? (object)DBNull.Value : Convert.ToDateTime(value);
                    case "String":
                    default:
                        return string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
                }
            }

            return value; // По умолчанию, возвращаем значение как есть
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class FieldModel
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}