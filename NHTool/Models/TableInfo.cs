namespace NHTool.Models;

public class TableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();

    public List<ColumnInfo> PrimaryKeys =>
        Columns.Where(c => c.IsPrimaryKey).ToList();
}
