using RepBase.Data;
using RepBase.Models;
using RepBase.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RepBase.ViewModels;
using MvvmHelpers;
using GalaSoft.MvvmLight.Command;

namespace RepBase.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly DatabaseManager _databaseManager;
        private readonly BackupService _backupService;
        private TableViewModel _selectedTable;
        private QueryViewModel _selectedQuery;

        public ObservableCollection<TableViewModel> Tables { get; } = new ObservableCollection<TableViewModel>();
        public ObservableCollection<QueryViewModel> Queries { get; } = new ObservableCollection<QueryViewModel>();
        public TableViewModel SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != null) _selectedTable.IsSelected = false;
                _selectedTable = value;
                if (_selectedTable != null) _selectedTable.IsSelected = true;
                OnPropertyChanged(nameof(SelectedTable));
            }
        }
        public QueryViewModel SelectedQuery
        {
            get => _selectedQuery;
            set { _selectedQuery = value; OnPropertyChanged(nameof(SelectedQuery)); }
        }

        public ICommand LoadTablesCommand { get; }
        public ICommand CreateTableCommand { get; }
        public ICommand DropTableCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand EditRowCommand { get; }
        public ICommand DeleteRowCommand { get; }
        public ICommand ExecuteQueryCommand { get; }
        public ICommand CreateBackupCommand { get; }

        public MainViewModel()
        {
            _databaseManager = new DatabaseManager("Host=localhost;Port=5432;Database=repbase;Username=postgres;Password=postgres");
            _backupService = new BackupService(_databaseManager);

            LoadTablesCommand = new RelayCommand(async () => await LoadTablesAsync());
            CreateTableCommand = new RelayCommand(async () => await CreateTableAsync());
            DropTableCommand = new RelayCommand(async () => await DropTableAsync(), () => SelectedTable != null);
            AddRowCommand = new RelayCommand(async () => await AddRowAsync(), () => SelectedTable != null);
            EditRowCommand = new RelayCommand(async () => await EditRowAsync(), () => SelectedTable != null);
            DeleteRowCommand = new RelayCommand(async () => await DeleteRowAsync(), () => SelectedTable != null);
            ExecuteQueryCommand = new RelayCommand(async () => await ExecuteQueryAsync(), () => SelectedQuery != null);
            CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync(), () => SelectedTable != null);

            // Загрузка при старте
            LoadTablesAsync().GetAwaiter().GetResult();

            // Добавляем 24 предустановленных запроса (пример)
            for (int i = 1; i <= 24; i++)
            {
                Queries.Add(new QueryViewModel($"SELECT * FROM main.table{i}", _databaseManager));
            }
        }

        private async Task LoadTablesAsync()
        {
            try
            {
                var tables = await _databaseManager.LoadTablesAsync();
                var tableNames = tables.Select(t => t.TableName).ToHashSet();

                foreach (var table in Tables.ToList())
                {
                    if (!10!tableNames.Contains(table.TableName))
                    {
                        Tables.Remove(table);
                    }
                }

                foreach (var table in tables)
                {
                    var existingTable = Tables.FirstOrDefault(t => t.Table.TableName == table.TableName);
                    if (existingTable != null)
                    {
                        existingTable.UpdateTable(table);
                    }
                    else
                    {
                        Tables.Add(new TableViewModel(table, _databaseManager));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tables: {ex.Message}");
            }
        }

        private async Task CreateTableAsync()
        {
            // Здесь можно открыть диалоговое окно для создания таблицы
            var newTable = new TableModel
            {
                TableName = "NewTable",
                Columns = new List<ColumnModel> { new ColumnModel("id", ColumnType.Integer) },
                Rows = new List<RowModel>()
            };
            await _databaseManager.CreateTableAsync(newTable);
            await LoadTablesAsync();
        }

        private async Task DropTableAsync()
        {
            if (SelectedTable != null && MessageBox.Show($"Удалить таблицу {SelectedTable.Table.TableName}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _databaseManager.DropTableAsync(SelectedTable.Table.TableName);
                Tables.Remove(SelectedTable);
                SelectedTable = null;
            }
        }

        private async Task AddRowAsync()
        {
            if (SelectedTable != null)
            {
                var dataEntryWindow = new DataEntryWindow(_databaseManager, SelectedTable.Table.TableName, null);
                if (dataEntryWindow.ShowDialog() == true)
                {
                    await SelectedTable.RefreshDataAsync();
                }
            }
        }

        private async Task EditRowAsync()
        {
            if (SelectedTable != null)
            {
                // Предполагается, что у таблицы есть столбец "id"
                var selectedRow = SelectedTable.Rows.FirstOrDefault(); // Логика выбора строки
                if (selectedRow != null && selectedRow.Values.ContainsKey("id"))
                {
                    var dataEntryWindow = new DataEntryWindow(_databaseManager, SelectedTable.Table.TableName, null, selectedRow);
                    if (dataEntryWindow.ShowDialog() == true)
                    {
                        await SelectedTable.RefreshDataAsync();
                    }
                }
            }
        }

        private async Task DeleteRowAsync()
        {
            if (SelectedTable != null)
            {
                var selectedRow = SelectedTable.Rows.FirstOrDefault(); // Логика выбора строки
                if (selectedRow != null && selectedRow.Values.ContainsKey("id"))
                {
                    await _databaseManager.DeleteRowAsync(SelectedTable.Table.TableName, "id", selectedRow.Values["id"]);
                    await SelectedTable.RefreshDataAsync();
                }
            }
        }

        private async Task ExecuteQueryAsync()
        {
            if (SelectedQuery != null)
            {
                await SelectedQuery.ExecuteQueryAsync();
                // Результат можно отобразить в отдельном окне или в DataGrid
            }
        }

        private async Task CreateBackupAsync()
        {
            if (SelectedTable != null)
            {
                await _backupService.BackupTableAsync(SelectedTable.Table, $"{SelectedTable.Table.TableName}_backup.csv");
                MessageBox.Show("Бэкап успешно создан!");
            }
        }

        public void SelectTable(TableViewModel tableViewModel)
        {
            SelectedTable = tableViewModel;
        }
    }
}