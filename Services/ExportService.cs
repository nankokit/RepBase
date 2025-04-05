using OfficeOpenXml;
using System;
using System.Data;
using System.Windows;
using RepBase.Models;
using RepBase.Data;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace RepBase.Services
{
    public class ExportService
    {
        private readonly DatabaseManager _databaseManager;

        public ExportService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        public void ExportCurrentTable(TableModel selectedTable, DataTable tableData)
        {
            if (tableData == null || selectedTable == null)
            {
                MessageBox.Show("Не выбраны данные для экспорта.");
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{selectedTable.TableName}_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add(selectedTable.TableName);
                        ExportDataTableToWorksheet(tableData, worksheet);
                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"Данные успешно экспортированы в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }

        public void ExportScriptResult(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                MessageBox.Show("Введите SQL-скрипт для экспорта результата.");
                return;
            }

            try
            {
                var result = _databaseManager.ExecuteQuery(script);
                if (result == null || result.Rows.Count == 0)
                {
                    MessageBox.Show("Скрипт не вернул данных для экспорта.");
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "script_result_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("Script_Result");
                        ExportDataTableToWorksheet(result, worksheet);
                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"Результат скрипта успешно экспортирован в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта результата скрипта: {ex.Message}");
            }
        }

        public void ExportEntireDatabase(ObservableCollection<TableModel> tableItems)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "database_export",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        foreach (var table in tableItems)
                        {
                            var tableData = _databaseManager.GetTableData(table.TableName);
                            if (tableData != null && tableData.Rows.Count > 0)
                            {
                                var worksheet = package.Workbook.Worksheets.Add(table.TableName);
                                ExportDataTableToWorksheet(tableData, worksheet);
                            }
                        }

                        if (!package.Workbook.Worksheets.Any())
                        {
                            MessageBox.Show("Нет данных для экспорта.");
                            return;
                        }

                        File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
                        MessageBox.Show($"База данных успешно экспортирована в {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта базы данных: {ex.Message}");
            }
        }

        private void ExportDataTableToWorksheet(DataTable dataTable, ExcelWorksheet worksheet)
        {
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                worksheet.Cells[1, i + 1].Value = dataTable.Columns[i].ColumnName;
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var value = dataTable.Rows[row][col];
                    worksheet.Cells[row + 2, col + 1].Value = value == DBNull.Value ? null : value;
                }
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }
    }
}