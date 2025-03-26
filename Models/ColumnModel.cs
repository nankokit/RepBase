using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepBase.Models
{
    public class ColumnModel
    {
        public string Name { get; set; }
        public string DataType { get; set; }

        public ColumnModel(string name, string dataType)
        {
            Name = name;
            DataType = dataType;
        }
    }
}
