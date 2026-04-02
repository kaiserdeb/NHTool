namespace NHTool.Models;

public class ForeignKeyInfo
{
    public string ConstraintName { get; set; } = string.Empty;
    public string FkTableName { get; set; } = string.Empty;
    public string FkColumnName { get; set; } = string.Empty;
    public string PkTableName { get; set; } = string.Empty;
    public string PkColumnName { get; set; } = string.Empty;
}
