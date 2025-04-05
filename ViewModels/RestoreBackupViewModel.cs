using RepBase.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RepBase.ViewModels
{
    public class BackupInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class RestoreBackupViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private ObservableCollection<BackupInfo> _backups;
        private BackupInfo _selectedBackup;

        public ObservableCollection<BackupInfo> Backups
        {
            get => _backups;
            set { _backups = value; OnPropertyChanged(); }
        }

        public BackupInfo SelectedBackup
        {
            get => _selectedBackup;
            set { _selectedBackup = value; OnPropertyChanged(); }
        }

        public ICommand RestoreBackupCommand { get; }

        public RestoreBackupViewModel(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            Backups = new ObservableCollection<BackupInfo>();
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            LoadBackups();
        }

        private void LoadBackups()
        {
            try
            {
                if (!Directory.Exists("Backups"))
                {
                    Directory.CreateDirectory("Backups");
                }

                var backupFiles = Directory.GetFiles("Backups", "*.sql")
                    .Select(f => new BackupInfo
                    {
                        Name = Path.GetFileName(f),
                        Path = f
                    })
                    .OrderByDescending(f => f.Name)
                    .ToList();

                Backups.Clear();
                foreach (var backup in backupFiles)
                {
                    Backups.Add(backup);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки бэкапов: {ex.Message}");
            }
        }

        private void RestoreBackup(object parameter)
        {
            if (SelectedBackup == null)
            {
                MessageBox.Show("Выберите бэкап для восстановления.");
                return;
            }

            try
            {
                var sqlScript = File.ReadAllText(SelectedBackup.Path);
                _databaseManager.ExecuteNonQuery(sqlScript);
                MessageBox.Show($"Бэкап '{SelectedBackup.Name}' успешно восстановлен.");
                (parameter as Window)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка восстановления бэкапа: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}