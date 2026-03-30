using NHTool.Models;

namespace NHTool.Schema;

public static class SchemaReaderFactory
{
    public static ISchemaReader Create(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Oracle => new OracleSchemaReader(),
        DatabaseProvider.SqlServer => new SqlServerSchemaReader(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
