using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class MappingGenerator
{
    private readonly DatabaseProvider _provider;

    public MappingGenerator(DatabaseProvider provider)
    {
        _provider = provider;
    }

    public string Generate(TableInfo table, string ns)
    {
        var className = NamingHelper.ToClassName(table.TableName);
        var sb = new StringBuilder();

        sb.AppendLine("using NHibernate.Mapping.ByCode;");
        sb.AppendLine("using NHibernate.Mapping.ByCode.Conformist;");
        sb.AppendLine($"using {ns};");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns}.Mappings;");
        sb.AppendLine();
        sb.AppendLine($"public class {className}Map : ClassMapping<{className}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {className}Map()");
        sb.AppendLine("    {");

        // Table + Schema
        if (!string.IsNullOrEmpty(table.Schema))
            sb.AppendLine($"        Schema(\"{table.Schema}\");");

        sb.AppendLine($"        Table(\"{table.TableName}\");");
        sb.AppendLine();

        // Primary key(s)
        var pks = table.PrimaryKeys;
        if (pks.Count == 1)
        {
            var pk = pks[0];
            var propName = NamingHelper.ToPropertyName(pk.ColumnName);
            var clrType = TypeMapper.GetClrType(pk, _provider);
            var isNumericPk = IsNumericType(clrType);

            sb.AppendLine($"        Id(x => x.{propName}, m =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            m.Column(\"{pk.ColumnName}\");");
            sb.AppendLine(isNumericPk
                ? "            m.Generator(Generators.Native);"
                : "            m.Generator(Generators.Assigned);");
            sb.AppendLine("        });");
        }
        else if (pks.Count > 1)
        {
            sb.AppendLine("        ComposedId(m =>");
            sb.AppendLine("        {");
            foreach (var pk in pks)
            {
                var propName = NamingHelper.ToPropertyName(pk.ColumnName);
                sb.AppendLine($"            m.Property(x => x.{propName}, p => p.Column(\"{pk.ColumnName}\"));");
            }
            sb.AppendLine("        });");
        }

        sb.AppendLine();

        // Regular properties (non-PK)
        foreach (var col in table.Columns.Where(c => !c.IsPrimaryKey))
        {
            var propName = NamingHelper.ToPropertyName(col.ColumnName);
            sb.AppendLine($"        Property(x => x.{propName}, m =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            m.Column(\"{col.ColumnName}\");");

            if (!col.IsNullable)
                sb.AppendLine("            m.NotNullable(true);");

            if (col.MaxLength.HasValue && col.MaxLength.Value > 0 && IsStringType(col))
                sb.AppendLine($"            m.Length({col.MaxLength.Value});");

            sb.AppendLine("        });");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool IsStringType(ColumnInfo col)
    {
        var dt = col.DataType.ToUpperInvariant();
        return dt.Contains("VARCHAR") || dt.Contains("CHAR") || dt.Contains("TEXT")
            || dt.Contains("NCHAR") || dt.Contains("NTEXT");
    }

    private static bool IsNumericType(string clrType)
    {
        return clrType is "int" or "int?" or "long" or "long?" or "short" or "short?"
            or "byte" or "byte?" or "decimal" or "decimal?";
    }
}
