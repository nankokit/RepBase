using System.Data;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace RepBase.Services
{
    public class ExportService
    {
        public async Task ExportToExcelAsync(DataTable data, string filePath)
        {
            await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Sheet1");
                    worksheet.Cell(1, 1).InsertTable(data);
                    workbook.SaveAs(filePath);
                }
            });
        }
    }
}