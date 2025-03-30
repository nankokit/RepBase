using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using RepBase.Data;

namespace RepBase.ViewModels
{
    public class QueryViewModel : BaseViewModel
    {
        private readonly DatabaseManager _dbManager;
        private string _queryText;
        private DataTable _result;

        public string QueryText
        {
            get => _queryText;
            set { _queryText = value; OnPropertyChanged(nameof(QueryText)); }
        }
        public DataTable Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(nameof(Result)); }
        }

        public ICommand ExecuteQueryCommand { get; }

        public QueryViewModel(string queryText, DatabaseManager dbManager)
        {
            QueryText = queryText;
            _dbManager = dbManager;
            ExecuteQueryCommand = new RelayCommand(async () => await ExecuteQueryAsync());
        }

        private async Task ExecuteQueryAsync()
        {
            Result = await _dbManager.ExecuteQueryAsync(QueryText);
        }
    }
}