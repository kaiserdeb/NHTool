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

        string owner = await ResolveOwnerAsync(connection, schemaFilter);

        // ── 1. Read all tables ───────────────────────────────────────
        var tablesSql = @"
            SELECT TABLE_NAME
            FROM ALL_TABLES
            WHERE OWNER = :owner
              AND TABLE_NAME NOT LIKE 'BIN$%'
              AND TABLE_NAME NOT LIKE 'SYS_%'
            ORDER BY TABLE_NAME";

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
        }

        if (tables.Count == 0)
            return tables;

        // ── 2. Read ALL columns + PK flags in a single round-trip ────
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
            colCmd.BindByName = true;
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
                    MaxLength = colReader.IsDBNull(4) ? null : Convert.ToInt32(colReader.GetValue(4)),
                    Precision = colReader.IsDBNull(5) ? null : Convert.ToInt32(colReader.GetValue(5)),
                    Scale = colReader.IsDBNull(6) ? null : Convert.ToInt32(colReader.GetValue(6)),
                    IsPrimaryKey = Convert.ToInt32(colReader.GetValue(7)) == 1
                });
            }
        }

        foreach (var table in tables)
        {
            if (columnsByTable.TryGetValue(table.TableName, out var cols))
                table.Columns = cols;
        }

        // ── 3. Detect identity columns (Oracle 12c+) ────────────────
        await DetectIdentityColumnsAsync(connection, owner, tables);

        return tables;
    }

    public async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(string connectionString, string? schemaFilter = null)
    {
        var fks = new List<ForeignKeyInfo>();

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        string owner = await ResolveOwnerAsync(connection, schemaFilter);

        var sql = @"
            SELECT ac.CONSTRAINT_NAME,
                   acc_fk.TABLE_NAME   AS FK_TABLE,
                   acc_fk.COLUMN_NAME  AS FK_COLUMN,
                   acc_pk.TABLE_NAME   AS PK_TABLE,
                   acc_pk.COLUMN_NAME  AS PK_COLUMN
            FROM ALL_CONSTRAINTS ac
            JOIN ALL_CONS_COLUMNS acc_fk
              ON ac.CONSTRAINT_NAME = acc_fk.CONSTRAINT_NAME
             AND ac.OWNER = acc_fk.OWNER
            JOIN ALL_CONS_COLUMNS acc_pk
              ON ac.R_CONSTRAINT_NAME = acc_pk.CONSTRAINT_NAME
             AND ac.R_OWNER = acc_pk.OWNER
             AND acc_fk.POSITION = acc_pk.POSITION
            WHERE ac.CONSTRAINT_TYPE = 'R'
              AND ac.OWNER = :owner
            ORDER BY ac.CONSTRAINT_NAME, acc_fk.POSITION";

        await using var cmd = new OracleCommand(sql, connection);
        cmd.Parameters.Add(new OracleParameter("owner", owner));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fks.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                FkTableName = reader.GetString(1),
                FkColumnName = reader.GetString(2),
                PkTableName = reader.GetString(3),
                PkColumnName = reader.GetString(4)
            });
        }

        return fks;
    }

    private static async Task<string> ResolveOwnerAsync(OracleConnection connection, string? schemaFilter)
    {
        if (!string.IsNullOrWhiteSpace(schemaFilter))
            return schemaFilter.ToUpperInvariant();

        await using var userCmd = new OracleCommand("SELECT USER FROM DUAL", connection);
        var result = await userCmd.ExecuteScalarAsync();
        var currentUser = result?.ToString();
        if (string.IsNullOrWhiteSpace(currentUser))
            throw new InvalidOperationException("Cannot determine schema owner from connected user.");

        return currentUser.ToUpperInvariant();
    }

    private static async Task DetectIdentityColumnsAsync(
        OracleConnection connection, string owner, List<TableInfo> tables)
    {
        // ALL_TAB_IDENTITY_COLS available in Oracle 12c+
        var sql = @"
            SELECT TABLE_NAME, COLUMN_NAME, SEQUENCE_NAME
            FROM ALL_TAB_IDENTITY_COLS
            WHERE OWNER = :owner";

        try
        {
            await using var cmd = new OracleCommand(sql, connection);
            cmd.Parameters.Add(new OracleParameter("owner", owner));

            var identityMap = new Dictionary<(string Table, string Column), string>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var seqName = reader.IsDBNull(2) ? null : reader.GetString(2);
                identityMap[(tableName, columnName)] = seqName ?? string.Empty;
            }

            foreach (var table in tables)
            {
                foreach (var col in table.Columns)
                {
                    if (identityMap.TryGetValue((table.TableName, col.ColumnName), out var seq))
                    {
                        col.IsIdentity = true;
                        if (!string.IsNullOrEmpty(seq))
                            col.SequenceName = seq;
                    }
                }
            }
        }
        catch (OracleException)
        {
            // ALL_TAB_IDENTITY_COLS may not exist on Oracle < 12c; silently skip
        }
    }
}
