using NHTool.Models;

namespace NHTool.Schema;

public interface ISchemaReader
{
    Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null);
}
