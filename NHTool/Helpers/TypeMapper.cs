using NHTool.Models;

namespace NHTool.Helpers;

public static class TypeMapper
{
    /// <summary>
    /// Maps a database column type to a C# type string.
    /// Appends ? only for nullable value types (int -> int?).
    /// Reference types (string, byte[]) are left as-is to avoid CS8632 warnings
    /// in projects with &lt;Nullable&gt;disable&lt;/Nullable&gt;.
    /// </summary>
    public static string GetClrType(ColumnInfo column, DatabaseProvider provider)
    {
        var baseType = provider switch
        {
            DatabaseProvider.Oracle => MapOracle(column),
            DatabaseProvider.SqlServer => MapSqlServer(column),
            _ => "object"
        };

        if (column.IsNullable && !IsReferenceType(baseType))
            return baseType + "?";

        return baseType;
    }

    /// <summary>
    /// Returns the default initializer for non-nullable properties to avoid CS8618 warnings.
    /// Returns null when no initializer is needed (value types are already default-initialized).
    /// </summary>
    public static string? GetDefaultInitializer(ColumnInfo column, DatabaseProvider provider)
    {
        var baseType = provider switch
        {
            DatabaseProvider.Oracle => MapOracle(column),
            DatabaseProvider.SqlServer => MapSqlServer(column),
            _ => "object"
        };

        // Non-nullable reference types need explicit initialization
        if (!column.IsNullable && IsReferenceType(baseType))
        {
            return baseType switch
            {
                "string" => " = string.Empty;",
                "byte[]" => " = Array.Empty<byte>();",
                _ => " = default!;"
            };
        }

        return null;
    }

    private static string MapOracle(ColumnInfo col) => col.DataType.ToUpperInvariant() switch
    {
        // NUMBER without precision/scale can represent large ranges, keep it safe.
        "NUMBER" when col.Precision is null && col.Scale is null => "decimal",
        "NUMBER" when col.Scale is null or 0 && col.Precision <= 10 => "int",
        "NUMBER" when col.Scale is null or 0 && col.Precision > 10 => "long",
        "NUMBER" => "decimal",
        "FLOAT" or "BINARY_FLOAT" => "float",
        "BINARY_DOUBLE" => "double",
        "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR" or "CLOB" or "NCLOB" => "string",
        "DATE" or "TIMESTAMP" => "DateTime",
        var t when t.StartsWith("TIMESTAMP") => "DateTime",
        "BLOB" or "RAW" or "LONG RAW" => "byte[]",
        "XMLTYPE" => "string",
        _ => "object"
    };

    private static string MapSqlServer(ColumnInfo col) => col.DataType.ToLowerInvariant() switch
    {
        "int" => "int",
        "bigint" => "long",
        "smallint" => "short",
        "tinyint" => "byte",
        "bit" => "bool",
        "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
        "float" => "double",
        "real" => "float",
        "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" or "xml" => "string",
        "date" or "datetime" or "datetime2" or "smalldatetime" => "DateTime",
        "datetimeoffset" => "DateTimeOffset",
        "time" => "TimeSpan",
        "uniqueidentifier" => "Guid",
        "varbinary" or "binary" or "image" or "timestamp" or "rowversion" => "byte[]",
        _ => "object"
    };

    private static bool IsReferenceType(string clrType) => clrType switch
    {
        "string" or "byte[]" or "object" => true,
        _ => false
    };
}
