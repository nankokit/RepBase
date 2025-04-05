using RepBase.Data;
using RepBase.Services;
using RepBase.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RepBase
{
    public class RestoreBackupViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseManager _databaseManager;
        private readonly BackupService _backupService;
        private ObservableCollection<BackupInfo> _backups;
        private BackupInfo _selectedBackup;

        public ObservableCollection<BackupInfo> Backups
        {
            get => _backups;
            set { _backups = value; OnPropertyChanged(nameof(Backups)); }
        }

        public BackupInfo SelectedBackup
        {
            get => _selectedBackup;
            set { _selectedBackup = value; OnPropertyChanged(nameof(SelectedBackup)); }
        }

        public ICommand RestoreBackupCommand { get; }

        public RestoreBackupViewModel(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            _backupService = new BackupService(_databaseManager);
            Backups = new ObservableCollection<BackupInfo>(_backupService.GetBackups());

            RestoreBackupCommand = new RelayCommand(RestoreBackup, CanRestoreBackup);
        }

        private bool CanRestoreBackup(object parameter)
        {
            return SelectedBackup != null;
        }

        private void RestoreBackup(object parameter)
        {
            if (SelectedBackup == null)
            {
                MessageBox.Show("Пожалуйста, выберите бэкап для восстановления.");
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите восстановить бэкап '{SelectedBackup.Name}'? Все текущие данные будут потеряны.",
                "Подтверждение восстановления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _backupService.RestoreBackup(SelectedBackup.FilePath);
                    MessageBox.Show("Бэкап успешно восстановлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive)?.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при восстановлении бэкапа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}