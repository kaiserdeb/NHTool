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
        var fkColumnNames = table.FkColumnNames;
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        if (_legacyStyle)
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            GenerateClassBody(sb, table, className, ns, fkColumnNames, "    ");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
            GenerateClassBody(sb, table, className, ns, fkColumnNames, "");
        }

        return sb.ToString();
    }

    private void GenerateClassBody(StringBuilder sb, TableInfo table, string className, string ns,
        HashSet<string> fkColumnNames, string indent)
    {
        var i1 = indent;           // class level
        var i2 = indent + "    ";  // member level

        sb.AppendLine($"{i1}public class {className}");
        sb.AppendLine($"{i1}{{");

        // Scalar properties (skip FK columns — they are represented by ManyToOne nav properties)
        foreach (var col in table.Columns)
        {
            if (!col.IsPrimaryKey && fkColumnNames.Contains(col.ColumnName))
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

        // ManyToOne navigation properties (one per FK)
        var manyToOneProps = GetDistinctManyToOneProperties(table);
        foreach (var (propName, referencedClassName) in manyToOneProps)
        {
            sb.AppendLine();
            sb.AppendLine($"{i2}public virtual {referencedClassName} {propName} {{ get; set; }}");
        }

        // Collection navigation properties (inverse side)
        var collectionProps = GetDistinctCollectionProperties(table);
        foreach (var (propName, fkClassName) in collectionProps)
        {
            sb.AppendLine();
            if (_legacyStyle)
                sb.AppendLine($"{i2}public virtual IList<{fkClassName}> {propName} {{ get; set; }} = new List<{fkClassName}>();");
            else
                sb.AppendLine($"{i2}public virtual IList<{fkClassName}> {propName} {{ get; set; }} = new List<{fkClassName}>();");
        }

        sb.AppendLine($"{i1}}}");
    }

    private static List<(string PropName, string ReferencedClassName)> GetDistinctManyToOneProperties(TableInfo table)
    {
        var result = new List<(string, string)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in table.ForeignKeys)
        {
            var propName = NamingHelper.ToManyToOnePropertyName(fk.FkColumnName, fk.PkTableName);
            var referencedClass = NamingHelper.ToClassName(fk.PkTableName);

            // Deduplicate: composite FKs produce multiple rows with same constraint
            if (usedNames.Add(propName))
                result.Add((propName, referencedClass));
        }

        return result;
    }

    private static List<(string PropName, string FkClassName)> GetDistinctCollectionProperties(TableInfo table)
    {
        var result = new List<(string, string)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in table.InverseForeignKeys)
        {
            var propName = NamingHelper.ToCollectionPropertyName(fk.FkTableName);
            var fkClass = NamingHelper.ToClassName(fk.FkTableName);

            if (usedNames.Add(propName))
                result.Add((propName, fkClass));
        }

        return result;
    }
}
