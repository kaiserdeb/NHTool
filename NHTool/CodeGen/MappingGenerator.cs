using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class MappingGenerator
{
    private readonly DatabaseProvider _provider;
    private readonly bool _legacyStyle;

    public MappingGenerator(DatabaseProvider provider, bool legacyStyle)
    {
        _provider = provider;
        _legacyStyle = legacyStyle;
    }

    public string Generate(TableInfo table, string ns)
    {
        var className = NamingHelper.ToClassName(table.TableName);
        var fkColumnNames = table.FkColumnNames;
        var sb = new StringBuilder();

        sb.AppendLine("using NHibernate.Mapping.ByCode;");
        sb.AppendLine("using NHibernate.Mapping.ByCode.Conformist;");
        sb.AppendLine($"using {ns};");
        sb.AppendLine();

        // Indentation: legacy uses 4+8+12, modern uses 0+4+8
        string i1, i2, i3;
        if (_legacyStyle)
        {
            i1 = "    ";   // class level
            i2 = "        ";  // method level
            i3 = "            "; // body level
            sb.AppendLine($"namespace {ns}.Mappings");
            sb.AppendLine("{");
        }
        else
        {
            i1 = "";
            i2 = "    ";
            i3 = "        ";
            sb.AppendLine($"namespace {ns}.Mappings;");
            sb.AppendLine();
        }

        sb.AppendLine($"{i1}public class {className}Map : ClassMapping<{className}>");
        sb.AppendLine($"{i1}{{");
        sb.AppendLine($"{i2}public {className}Map()");
        sb.AppendLine($"{i2}{{");

        // Table + Schema
        if (!string.IsNullOrEmpty(table.Schema))
            sb.AppendLine($"{i3}Schema(\"{table.Schema}\");");

        sb.AppendLine($"{i3}Table(\"{table.TableName}\");");
        sb.AppendLine();

        // Primary key(s)
        var pks = table.PrimaryKeys;
        if (pks.Count == 1)
        {
            var pk = pks[0];
            var propName = NamingHelper.ToPropertyName(pk.ColumnName);
            var clrType = TypeMapper.GetClrType(pk, _provider);
            var isNumericPk = IsNumericType(clrType);

            sb.AppendLine($"{i3}Id(x => x.{propName}, m =>");
            sb.AppendLine($"{i3}{{");
            sb.AppendLine($"{i3}    m.Column(\"{pk.ColumnName}\");");

            if (pk.IsIdentity && isNumericPk)
            {
                if (!string.IsNullOrEmpty(pk.SequenceName))
                {
                    // Oracle sequence-backed identity
                    sb.AppendLine($"{i3}    m.Generator(Generators.Sequence, g => g.Params(new {{ sequence = \"{pk.SequenceName}\" }}));");
                }
                else
                {
                    sb.AppendLine($"{i3}    m.Generator(Generators.Identity);");
                }
            }
            else if (isNumericPk)
            {
                sb.AppendLine($"{i3}    m.Generator(Generators.Native);");
            }
            else
            {
                sb.AppendLine($"{i3}    m.Generator(Generators.Assigned);");
            }

            sb.AppendLine($"{i3}}});");
        }
        else if (pks.Count > 1)
        {
            sb.AppendLine($"{i3}ComposedId(m =>");
            sb.AppendLine($"{i3}{{");
            foreach (var pk in pks)
            {
                var propName = NamingHelper.ToPropertyName(pk.ColumnName);
                sb.AppendLine($"{i3}    m.Property(x => x.{propName}, p => p.Column(\"{pk.ColumnName}\"));");
            }
            sb.AppendLine($"{i3}}});");
        }

        sb.AppendLine();

        // Regular properties (non-PK, non-FK)
        foreach (var col in table.Columns.Where(c => !c.IsPrimaryKey))
        {
            if (fkColumnNames.Contains(col.ColumnName))
                continue;

            var propName = NamingHelper.ToPropertyName(col.ColumnName);
            sb.AppendLine($"{i3}Property(x => x.{propName}, m =>");
            sb.AppendLine($"{i3}{{");
            sb.AppendLine($"{i3}    m.Column(\"{col.ColumnName}\");");

            if (!col.IsNullable)
                sb.AppendLine($"{i3}    m.NotNullable(true);");

            if (col.MaxLength.HasValue && col.MaxLength.Value > 0 && IsStringType(col))
                sb.AppendLine($"{i3}    m.Length({col.MaxLength.Value});");

            sb.AppendLine($"{i3}}});");
        }

        // ManyToOne mappings
        var usedManyToOneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in table.ForeignKeys)
        {
            var propName = NamingHelper.ToManyToOnePropertyName(fk.FkColumnName, fk.PkTableName);
            if (!usedManyToOneNames.Add(propName))
                continue;

            sb.AppendLine();
            sb.AppendLine($"{i3}ManyToOne(x => x.{propName}, m =>");
            sb.AppendLine($"{i3}{{");
            sb.AppendLine($"{i3}    m.Column(\"{fk.FkColumnName}\");");
            sb.AppendLine($"{i3}}});");
        }

        // Bag (collection) mappings for inverse side
        var usedBagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in table.InverseForeignKeys)
        {
            var propName = NamingHelper.ToCollectionPropertyName(fk.FkTableName);
            if (!usedBagNames.Add(propName))
                continue;

            var fkClassName = NamingHelper.ToClassName(fk.FkTableName);

            sb.AppendLine();
            sb.AppendLine($"{i3}Bag(x => x.{propName}, c =>");
            sb.AppendLine($"{i3}{{");
            sb.AppendLine($"{i3}    c.Key(k => k.Column(\"{fk.FkColumnName}\"));");
            sb.AppendLine($"{i3}    c.Inverse(true);");
            sb.AppendLine($"{i3}    c.Lazy(CollectionLazy.Lazy);");
            sb.AppendLine($"{i3}}}, r => r.OneToMany());");
        }

        sb.AppendLine($"{i2}}}");
        sb.AppendLine($"{i1}}}");

        if (_legacyStyle)
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
