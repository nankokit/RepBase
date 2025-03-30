using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepBase.Models
{
    public class RowModel
    {
        public Dictionary<string, object> Values { get; set; }
        public RowModel()
        {
            Values = new Dictionary<string, object>();
        }
    }
}