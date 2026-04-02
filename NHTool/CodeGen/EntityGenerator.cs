using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class EntityGenerator
{
    private readonly DatabaseProvider _provider;
    private readonly bool _legacyStyle;

    public EntityGenerator(DatabaseProvider provider, bool legacyStyle)
    {
        _provider = provider;
        _legacyStyle = legacyStyle;
    }

    public string Generate(TableInfo table, string ns)
    {
        var className = NamingHelper.ToClassName(table.TableName);
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        if (_legacyStyle)
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            GenerateClassBody(sb, table, className, "    ");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
            GenerateClassBody(sb, table, className, "");
        }

        return sb.ToString();
    }

    private void GenerateClassBody(StringBuilder sb, TableInfo table, string className, string indent)
    {
        var i1 = indent;           // class level
        var i2 = indent + "    ";  // member level

        var associationPlan = AssociationNamingPlanner.Build(table);
        var primaryKeyColumnNames = table.PrimaryKeys
            .Select(pk => pk.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fkColumnsCoveredByManyToOne = associationPlan.ManyToOnes
            .Where(a => !a.IsComposite)
            .SelectMany(a => a.ColumnNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        sb.AppendLine($"{i1}public class {className}");
        sb.AppendLine($"{i1}{{");

        // Scalar properties (skip FK columns — they are represented by ManyToOne nav properties)
        foreach (var col in table.Columns)
        {
            if (!col.IsPrimaryKey && fkColumnsCoveredByManyToOne.Contains(col.ColumnName))
                continue;

            var propName = NamingHelper.ToPropertyName(col.ColumnName);
            var clrType = TypeMapper.GetClrType(col, _provider);

            if (_legacyStyle)
            {
                sb.AppendLine($"{i2}public virtual {clrType} {propName} {{ get; set; }}");
            }
            else
            {
                var initializer = TypeMapper.GetDefaultInitializer(col, _provider);
                if (initializer != null)
                    sb.AppendLine($"{i2}public virtual {clrType} {propName} {{ get; set; }}{initializer}");
                else
                    sb.AppendLine($"{i2}public virtual {clrType} {propName} {{ get; set; }}");
            }
        }

        // ManyToOne navigation properties
        foreach (var assoc in associationPlan.ManyToOnes)
        {
            if (assoc.IsComposite)
                continue;

            var fkColumnName = assoc.ColumnNames[0];
            if (primaryKeyColumnNames.Contains(fkColumnName))
                continue;

            sb.AppendLine();
            if (_legacyStyle)
                sb.AppendLine($"{i2}public virtual {assoc.ReferencedClassName} {assoc.PropertyName} {{ get; set; }}");
            else
                sb.AppendLine($"{i2}public virtual {assoc.ReferencedClassName} {assoc.PropertyName} {{ get; set; }} = default!;");
        }

        // Collection navigation properties (inverse side)
        foreach (var assoc in associationPlan.InverseCollections)
        {
            if (assoc.IsComposite)
                continue;

            sb.AppendLine();
            sb.AppendLine($"{i2}public virtual IList<{assoc.ItemClassName}> {assoc.PropertyName} {{ get; set; }} = new List<{assoc.ItemClassName}>();");
        }

        sb.AppendLine($"{i1}}}");
    }
}
