namespace NHTool.Models;

public class TableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    public List<ForeignKeyInfo> InverseForeignKeys { get; set; } = new();

    public List<ColumnInfo> PrimaryKeys =>
        Columns.Where(c => c.IsPrimaryKey).ToList();

    public HashSet<string> FkColumnNames =>
        ForeignKeys.Select(fk => fk.FkColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
