namespace NHTool.Models;

public enum DatabaseProvider
{
    Oracle,
    SqlServer
}

public static class DatabaseProviderExtensions
{
    public static DatabaseProvider Parse(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "oracle" => DatabaseProvider.Oracle,
            "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
            _ => throw new ArgumentException(
                $"Unsupported provider '{providerName}'. Use 'oracle' or 'sqlserver'.")
        };
    }
}
