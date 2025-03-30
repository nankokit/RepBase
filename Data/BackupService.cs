using System.Data;
using System.IO;
using System.Threading.Tasks;
using RepBase.Data;
using RepBase.Models;

namespace RepBase.Services
{
    public class BackupService
    {
        private readonly DatabaseManager _dbManager;

        public BackupService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public async Task BackupTableAsync(TableModel table, string filePath)
        {
            var dataTable = await _dbManager.ExecuteQueryAsync($"SELECT * FROM main.{table.TableName}");
            using (var writer = new StreamWriter(filePath))
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    await writer.WriteLineAsync(string.Join(",", row.ItemArray));
                }
            }
        }
    }
}