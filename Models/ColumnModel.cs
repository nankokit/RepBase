using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ColumnModel
    {
        public string ColumnName { get; set; }
        public ColumnType ColumnType { get; set; }

        public ColumnModel(string columnName, ColumnType columnType)
        {
            ColumnName = columnName;
            ColumnType = columnType;
        }
    }
}