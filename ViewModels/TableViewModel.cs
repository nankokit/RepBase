using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using RepBase.Data;
using RepBase.Models;

namespace RepBase.ViewModels
{
    public class TableViewModel : BaseViewModel
    {
        private readonly DatabaseManager _dbManager;
        public TableModel Table { get; }
        public ObservableCollection<RowModel> Rows => new ObservableCollection<RowModel>(Table.Rows);

        public ICommand AddRowCommand { get; }
        public ICommand DeleteRowCommand { get; }

        public TableViewModel(TableModel table, DatabaseManager dbManager)
        {
            Table = table;
            _dbManager = dbManager;
            AddRowCommand = new RelayCommand(async () => await AddRowAsync());
            DeleteRowCommand = new RelayCommand(async () => await DeleteRowAsync());
        }

        private async Task AddRowAsync()
        {
            var newRow = new RowModel();
            foreach (var column in Table.Columns)
            {
                newRow.Values[column.ColumnName] = GetDefaultValue(column.ColumnType); // Логика ввода значений
            }
            await _dbManager.InsertRowAsync(Table.TableName, newRow);
            Table.Rows.Add(newRow);
            OnPropertyChanged(nameof(Rows));
        }

        private async Task DeleteRowAsync()
        {
            if (Table.Rows.Any())
            {
                var rowToDelete = Table.Rows.Last();
                await _dbManager.DeleteRowAsync(Table.TableName, "id", rowToDelete.Values["id"]);
                Table.Rows.Remove(rowToDelete);
                OnPropertyChanged(nameof(Rows));
            }
        }

        private object GetDefaultValue(ColumnType type)
        {
            switch (type)
            {
                case ColumnType.Integer: return 0;
                case ColumnType.String: return "";
                case ColumnType.Boolean: return false;
                default: return null;
            }
        }
    }
}