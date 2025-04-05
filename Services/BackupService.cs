using Npgsql;
using RepBase.Data;
using RepBase.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace RepBase.Services
{
    public class BackupService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly string _backupFolder = "Backups";

        public BackupService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }
        }

        public void CreateBackup(ObservableCollection<TableModel> tables)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"backup_{timestamp}.sql";
                string backupFilePath = Path.Combine(_backupFolder, backupFileName);

                using (var connection = _databaseManager.GetConnection())
                {
                    connection.Open();
                    var backupCommand = $"pg_dump -h localhost -U postgres -F p -f \"{backupFilePath}\" repbase";
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/C {backupCommand}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Ошибка при создании бэкапа: {error}");
                    }
                }

                System.Windows.MessageBox.Show($"Бэкап успешно создан: {backupFileName}", "Успех");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании бэкапа: {ex.Message}", "Ошибка");
            }
        }

        public List<BackupInfo> GetBackups()
        {
            var backups = new List<BackupInfo>();
            var backupFiles = Directory.GetFiles(_backupFolder, "*.sql").OrderByDescending(f => f);

            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    Name = fileInfo.Name,
                    FilePath = fileInfo.FullName,
                    CreationTime = fileInfo.CreationTime
                });
            }

            return backups;
        }

        public void RestoreBackup(string backupFilePath)
        {
            try
            {
                using (var connection = _databaseManager.GetConnection())
                {
                    connection.Open();
                    var dropCommand = "DROP SCHEMA IF EXISTS main CASCADE; CREATE SCHEMA main;";
                    using (var cmd = new NpgsqlCommand(dropCommand, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    var restoreCommand = $"psql -h localhost -U postgres -d repbase -f \"{backupFilePath}\"";
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/C {restoreCommand}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Ошибка при восстановлении бэкапа: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при восстановлении бэкапа: {ex.Message}");
            }
        }
    }

    public class BackupInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public DateTime CreationTime { get; set; }
    }
}