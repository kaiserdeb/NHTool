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
        // Lowercase first, then pascalize, then singularize
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
}
