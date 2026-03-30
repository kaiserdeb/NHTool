using System.Data;
using NHTool.Models;
using Oracle.ManagedDataAccess.Client;

namespace NHTool.Schema;

public class OracleSchemaReader : ISchemaReader
{
    public async Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null)
    {
        var tables = new List<TableInfo>();

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        string owner = schemaFilter ?? connection.Database?.ToUpperInvariant()
            ?? throw new InvalidOperationException("Cannot determine schema owner.");

        // Read tables
        var tablesSql = @"
            SELECT TABLE_NAME
            FROM ALL_TABLES
            WHERE OWNER = :owner
              AND TABLE_NAME NOT LIKE 'BIN$%'
              AND TABLE_NAME NOT LIKE 'SYS_%'
            ORDER BY TABLE_NAME";

        await using var tablesCmd = new OracleCommand(tablesSql, connection);
        tablesCmd.Parameters.Add(new OracleParameter("owner", owner));

        await using var tablesReader = await tablesCmd.ExecuteReaderAsync();
        while (await tablesReader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Schema = owner,
                TableName = tablesReader.GetString(0)
            });
        }

        // Read columns for each table
        var columnsSql = @"
            SELECT c.COLUMN_NAME,
                   c.DATA_TYPE,
                   c.NULLABLE,
                   c.DATA_LENGTH,
                   c.DATA_PRECISION,
                   c.DATA_SCALE,
                   CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
            FROM ALL_TAB_COLUMNS c
            LEFT JOIN (
                SELECT acc.COLUMN_NAME, acc.TABLE_NAME
                FROM ALL_CONS_COLUMNS acc
                JOIN ALL_CONSTRAINTS ac
                  ON acc.CONSTRAINT_NAME = ac.CONSTRAINT_NAME
                 AND acc.OWNER = ac.OWNER
                WHERE ac.CONSTRAINT_TYPE = 'P'
                  AND ac.OWNER = :owner
            ) pk ON pk.COLUMN_NAME = c.COLUMN_NAME AND pk.TABLE_NAME = c.TABLE_NAME
            WHERE c.OWNER = :owner
              AND c.TABLE_NAME = :tableName
            ORDER BY c.COLUMN_ID";

        foreach (var table in tables)
        {
            await using var colCmd = new OracleCommand(columnsSql, connection);
            colCmd.Parameters.Add(new OracleParameter("owner", owner));
            colCmd.Parameters.Add(new OracleParameter("tableName", table.TableName));

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                table.Columns.Add(new ColumnInfo
                {
                    ColumnName = colReader.GetString(0),
                    DataType = colReader.GetString(1),
                    IsNullable = colReader.GetString(2) == "Y",
                    MaxLength = colReader.IsDBNull(3) ? null : colReader.GetInt32(3),
                    Precision = colReader.IsDBNull(4) ? null : colReader.GetInt32(4),
                    Scale = colReader.IsDBNull(5) ? null : colReader.GetInt32(5),
                    IsPrimaryKey = colReader.GetInt32(6) == 1
                });
            }
        }

        return tables;
    }
}
