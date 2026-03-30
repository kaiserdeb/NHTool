using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class EntityGenerator
{
    private readonly DatabaseProvider _provider;

    public EntityGenerator(DatabaseProvider provider)
    {
        _provider = provider;
    }

    public string Generate(TableInfo table, string ns)
    {
        var className = NamingHelper.ToClassName(table.TableName);
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        foreach (var col in table.Columns)
        {
            var propName = NamingHelper.ToPropertyName(col.ColumnName);
            var clrType = TypeMapper.GetClrType(col, _provider);
            var initializer = TypeMapper.GetDefaultInitializer(col, _provider);

            if (initializer != null)
                sb.AppendLine($"    public virtual {clrType} {propName} {{ get; set; }}{initializer}");
            else
                sb.AppendLine($"    public virtual {clrType} {propName} {{ get; set; }}");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}
