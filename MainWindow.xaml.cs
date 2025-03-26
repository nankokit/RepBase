using RepBase.Data;
using RepBase.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RepBase
{
    public partial class MainWindow : Window
    {
        private DatabaseManager _databaseManager;
        public ObservableCollection<TableModel> TableItems { get; set; }

        private Button _lastSelectedButton;
        private string _currentTableName;


        public MainWindow()
        {
            InitializeComponent();
            _databaseManager = new DatabaseManager("Host=localhost;Port=5432;Database=repbase;Username=postgres;Password=postgres");
            TableItems = new ObservableCollection<TableModel>();
            LoadTables();
            DataContext = this;
        }

        private void LoadTables()
        {
            try
            {
                var tables = _databaseManager.GetTables();

                var tableNames = tables.Select(t => t.TableName).ToHashSet();
                TableItems.ToList().ForEach(t =>
                {
                    if (!tableNames.Contains(t.TableName))
                    {
                        TableItems.Remove(t);
                    }
                });
                foreach (var table in tables)
                {                    
                    var existingTable = TableItems.FirstOrDefault(t => t.TableName == table.TableName);
                    if (existingTable != null)
                    {
                        existingTable.Columns = table.Columns;
                        existingTable.Rows = table.Rows;
                    }
                    else
                    {
                        TableItems.Add(table);
                    }
                    Console.WriteLine($"Table: {table.TableName}");
                    Console.WriteLine($"Table: {table.TableName}");
                    Console.WriteLine("Columns:");
                    foreach (var column in table.Columns)
                    {
                        Console.WriteLine($" - {column.Name}");
                    }
                    Console.WriteLine("Rows:");
                    foreach (var row in table.Rows)
                    {
                        Console.WriteLine(" - " + string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
                    }
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
                var dataTable = _databaseManager.LoadTableData(tableName);
                DisplayTableData(dataTable);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data for table {tableName}: {ex.Message}");
            }
        }
        private void DisplayTableData(DataTable dataTable)
        {
            //Console.WriteLine($"Rows: {dataTable.Rows.Count}, Columns: {dataTable.Columns.Count}");

            foreach (DataRow row in dataTable.Rows.Cast<DataRow>()
                .Where(r => r.ItemArray.All(field => field == DBNull.Value)).ToList())
            {
                dataTable.Rows.Remove(row);
            }

            dataGrid.Columns.Clear();
            dataGrid.ItemsSource = null;

            foreach (DataColumn column in dataTable.Columns)
            {
                DataGridTextColumn textColumn = new DataGridTextColumn
                {
                    Header = column.ColumnName,
                    Binding = new Binding(column.ColumnName),
                    IsReadOnly = column.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)
                };
                dataGrid.Columns.Add(textColumn);
            }

            dataGrid.ItemsSource = dataTable.DefaultView;
        }

        public Button LastSelectedButton
        {
            get => _lastSelectedButton;
            set
            {
                if (_lastSelectedButton != null)
                {
                    _lastSelectedButton.ClearValue(Button.BackgroundProperty);
                }

                _lastSelectedButton = value;

                if (_lastSelectedButton != null)
                {
                    _lastSelectedButton.Background = new SolidColorBrush(Color.FromRgb(72, 105, 102));
                }
            }
        }
        private void TableButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                LastSelectedButton = button;
                var tableName = button.Content.ToString();
                LoadTableData(tableName);
                _currentTableName = tableName;
            }
        }

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {
            var dataTable = (dataGrid.ItemsSource as DataView)?.Table;
            if (dataTable != null)
            {
                var dataEntryWindow = new DataEntryWindow(_databaseManager, _currentTableName, (dataGrid.ItemsSource as DataView)?.Table);
                if (dataEntryWindow.ShowDialog() == true)
                {
                    RefreshDataGrid();
                }
            }
        }

        private void RefreshDataGrid()
        {
            // Получаем обновленные данные из базы данных
            var updatedDataTable = _databaseManager.LoadTableData(_currentTableName);
            dataGrid.ItemsSource = updatedDataTable.DefaultView; // Обновляем источник данных
        }
        private int GetNextId(DataTable dataTable)
        {
            if (dataTable.Rows.Count == 0)
            {
                return 1;
            }
            return dataTable.AsEnumerable().Max(row => row.Field<int>("id")) + 1;
        }


    }
}
