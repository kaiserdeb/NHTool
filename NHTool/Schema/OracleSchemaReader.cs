using System.Data;
using NHTool.Models;
using Oracle.ManagedDataAccess.Client;

namespace NHTool.Schema;

public class OracleSchemaReader : ISchemaReader
{
    public async Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null)
    {
        var tables = new List<TableInfo>();
        var columnsByTable = new Dictionary<string, List<ColumnInfo>>();

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        // ── Resolve owner: prefer --schema, fallback to connected user ──
        string owner;
        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            owner = schemaFilter.ToUpperInvariant();
        }
        else
        {
            await using var userCmd = new OracleCommand("SELECT USER FROM DUAL", connection);
            var result = await userCmd.ExecuteScalarAsync();
            var currentUser = result?.ToString();
            if (string.IsNullOrWhiteSpace(currentUser))
                throw new InvalidOperationException("Cannot determine schema owner from connected user.");

            owner = currentUser.ToUpperInvariant();
        }

        // ── 1. Read all tables ───────────────────────────────────────
        var tablesSql = @"
            SELECT TABLE_NAME
            FROM ALL_TABLES
            WHERE OWNER = :owner
              AND TABLE_NAME NOT LIKE 'BIN$%'
              AND TABLE_NAME NOT LIKE 'SYS_%'
            ORDER BY TABLE_NAME";

        // Scope the reader so it's closed before the next query
        {
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
        } // tablesReader is disposed here

        if (tables.Count == 0)
            return tables;

        // ── 2. Read ALL columns + PK flags in a single round-trip ────
        // Join to ALL_TABLES with the same filters (excluding BIN$%, SYS_%) to
        // avoid pulling columns for views or excluded system tables.
        var columnsSql = @"
            SELECT c.TABLE_NAME,
                   c.COLUMN_NAME,
                   c.DATA_TYPE,
                   c.NULLABLE,
                   c.DATA_LENGTH,
                   c.DATA_PRECISION,
                   c.DATA_SCALE,
                   CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
            FROM ALL_TAB_COLUMNS c
            INNER JOIN ALL_TABLES t
                ON c.OWNER = t.OWNER
               AND c.TABLE_NAME = t.TABLE_NAME
               AND t.TABLE_NAME NOT LIKE 'BIN$%'
               AND t.TABLE_NAME NOT LIKE 'SYS_%'
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
            ORDER BY c.TABLE_NAME, c.COLUMN_ID";

        {
            await using var colCmd = new OracleCommand(columnsSql, connection);
            colCmd.Parameters.Add(new OracleParameter("owner", owner));

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                var tableName = colReader.GetString(0);

                if (!columnsByTable.TryGetValue(tableName, out var columns))
                {
                    columns = new List<ColumnInfo>();
                    columnsByTable[tableName] = columns;
                }

                columns.Add(new ColumnInfo
                {
                    ColumnName = colReader.GetString(1),
                    DataType = colReader.GetString(2),
                    IsNullable = colReader.GetString(3) == "Y",
                    MaxLength = colReader.IsDBNull(4) ? null : colReader.GetInt32(4),
                    Precision = colReader.IsDBNull(5) ? null : colReader.GetInt32(5),
                    Scale = colReader.IsDBNull(6) ? null : colReader.GetInt32(6),
                    IsPrimaryKey = colReader.GetInt32(7) == 1
                });
            }
        }

        // ── 3. Assign columns to their tables ───────────────────────
        foreach (var table in tables)
        {
            if (columnsByTable.TryGetValue(table.TableName, out var cols))
                table.Columns = cols;
        }

        return tables;
    }
}
