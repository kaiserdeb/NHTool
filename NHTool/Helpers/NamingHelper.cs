using Humanizer;

namespace NHTool.Helpers;

public static class NamingHelper
{
    /// <summary>
    /// Converts a database table name (e.g. USERS, ORDER_ITEMS) to a PascalCase
    /// singular class name (e.g. User, OrderItem).
    /// </summary>
    public static string ToClassName(string tableName)
    {
        var pascal = tableName
            .ToLowerInvariant()
            .Replace("_", " ")
            .Pascalize();

        return pascal.Singularize(inputIsKnownToBePlural: false);
    }

    /// <summary>
    /// Converts a database column name (e.g. USER_NAME, ID) to a PascalCase
    /// property name (e.g. UserName, Id).
    /// </summary>
    public static string ToPropertyName(string columnName)
    {
        return columnName
            .ToLowerInvariant()
            .Replace("_", " ")
            .Pascalize();
    }

    /// <summary>
    /// Derives a ManyToOne navigation property name from a FK column name.
    /// Strips common suffixes (_ID, _KEY, _FK, _CODE) and PascalCases the rest.
    /// e.g. CREATED_BY_USER_ID -> CreatedByUser, CATEGORY_ID -> Category
    /// </summary>
    public static string ToManyToOnePropertyName(string fkColumnName, string referencedTableName)
    {
        // Try stripping _ID or _KEY suffix from FK column
        var stripped = fkColumnName;
        foreach (var suffix in new[] { "_ID", "_KEY", "_FK", "_CODE" })
        {
            if (stripped.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stripped = stripped[..^suffix.Length];
                break;
            }
        }

        // If stripping left something meaningful and not just a generic identifier,
        // use it; otherwise use the referenced table name to avoid collisions like "Id".
        if (!string.IsNullOrWhiteSpace(stripped))
        {
            var propertyName = ToPropertyName(stripped);
            if (!IsGenericIdentifierName(propertyName))
                return propertyName;
        }

        return ToClassName(referencedTableName);
    }

    /// <summary>
    /// Derives a collection navigation property name (plural) from the FK table name.
    /// e.g. ORDER_ITEMS -> OrderItems
    /// </summary>
    public static string ToCollectionPropertyName(string fkTableName)
    {
        return fkTableName
            .ToLowerInvariant()
            .Replace("_", " ")
            .Pascalize();
    }

    private static bool IsGenericIdentifierName(string propertyName)
    {
        return propertyName is "Id" or "Key" or "Fk" or "Pk" or "Code";
    }
}
