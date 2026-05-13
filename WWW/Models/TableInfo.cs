using System.Collections.Generic;
using System.Linq;

namespace TestDbApp.Models
{
    public class TableInfo
    {
        public string TableName { get; set; } = string.Empty;
        public List<string> ParentTables { get; set; } = new();
        public List<string> ChildTables { get; set; } = new();
        public List<ColumnSchema> Columns { get; set; } = new();

        public string ChildTablesString =>
            ChildTables.Count > 0 ? $"\u2192 {string.Join(", ", ChildTables)}" : "";
    }

    public class ColumnSchema
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
