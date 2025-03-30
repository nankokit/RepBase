using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RepBase.Models
{
    public enum ColumnType
    {
        String,
        Integer,
        Boolean,
        DateTime,
        Decimal,
        Real,
        Json,
        CharacterVarying,
    }
    public class ColumnModel : INotifyPropertyChanged
    {
        private string _columnName;
        private ColumnType _columnType;

        public string ColumnName
        {
            get => _columnName;
            set { _columnName = value; OnPropertyChanged(); }
        }

        public ColumnType ColumnType
        {
            get => _columnType;
            set { _columnType = value; OnPropertyChanged(); }
        }

        public ColumnModel(string columnName, ColumnType columnType)
        {
            ColumnName = columnName;
            ColumnType = columnType;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}