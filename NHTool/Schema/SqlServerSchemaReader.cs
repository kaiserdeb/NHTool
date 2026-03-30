using NHTool.Models;
using MsSqlConnection = global::Microsoft.Data.SqlClient.SqlConnection;
using MsSqlCommand = global::Microsoft.Data.SqlClient.SqlCommand;

namespace NHTool.Schema;

public class SqlServerSchemaReader : ISchemaReader
{
    public async Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null)
    {
        var tables = new List<TableInfo>();

        await using var connection = new MsSqlConnection(connectionString);
        await connection.OpenAsync();

        string schema = schemaFilter ?? "dbo";

        // Read tables
        var tablesSql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";

        await using var tablesCmd = new MsSqlCommand(tablesSql, connection);
        tablesCmd.Parameters.AddWithValue("@schema", schema);

        await using var tablesReader = await tablesCmd.ExecuteReaderAsync();
        while (await tablesReader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Schema = tablesReader.GetString(0),
                TableName = tablesReader.GetString(1)
            });
        }

        // Read columns for each table
        var columnsSql = @"
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME, ku.TABLE_NAME, ku.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                  ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                 AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.COLUMN_NAME = c.COLUMN_NAME
                AND pk.TABLE_NAME = c.TABLE_NAME
                AND pk.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION";

        foreach (var table in tables)
        {
            await using var colCmd = new MsSqlCommand(columnsSql, connection);
            colCmd.Parameters.AddWithValue("@schema", schema);
            colCmd.Parameters.AddWithValue("@tableName", table.TableName);

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                table.Columns.Add(new ColumnInfo
                {
                    ColumnName = colReader.GetString(0),
                    DataType = colReader.GetString(1),
                    IsNullable = colReader.GetString(2) == "YES",
                    MaxLength = colReader.IsDBNull(3) ? null : colReader.GetInt32(3),
                    Precision = colReader.IsDBNull(4) ? null : Convert.ToInt32(colReader.GetByte(4)),
                    Scale = colReader.IsDBNull(5) ? null : colReader.GetInt32(5),
                    IsPrimaryKey = colReader.GetInt32(6) == 1
                });
            }
        }

        return tables;
    }
}
