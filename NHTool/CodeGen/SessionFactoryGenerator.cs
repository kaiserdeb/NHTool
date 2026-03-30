using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class SessionFactoryGenerator
{
    private readonly DatabaseProvider _provider;
    private readonly bool _legacyStyle;

    public SessionFactoryGenerator(DatabaseProvider provider, bool legacyStyle)
    {
        _provider = provider;
        _legacyStyle = legacyStyle;
    }

    public string Generate(List<TableInfo> tables, string ns)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using NHibernate;");
        sb.AppendLine("using NHibernate.Cfg;");
        sb.AppendLine("using NHibernate.Mapping.ByCode;");
        sb.AppendLine($"using {ns}.Mappings;");
        sb.AppendLine();

        // Indentation: legacy 4+8+12, modern 0+4+8
        string i1, i2, i3;
        if (_legacyStyle)
        {
            i1 = "    ";
            i2 = "        ";
            i3 = "            ";
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
        }
        else
        {
            i1 = "";
            i2 = "    ";
            i3 = "        ";
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"{i1}public static class NHibernateHelper");
        sb.AppendLine($"{i1}{{");
        sb.AppendLine($"{i2}private static readonly object _lock = new object();");
        sb.AppendLine($"{i2}private static ISessionFactory _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine($"{i2}public static ISessionFactory BuildSessionFactory(string connectionString)");
        sb.AppendLine($"{i2}{{");
        sb.AppendLine($"{i3}if (_sessionFactory != null) return _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine($"{i3}lock (_lock)");
        sb.AppendLine($"{i3}{{");
        sb.AppendLine($"{i3}    if (_sessionFactory != null) return _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine($"{i3}    var cfg = new Configuration();");

        var driverClass = _provider switch
        {
            DatabaseProvider.Oracle => "NHibernate.Driver.OracleManagedDataClientDriver",
            DatabaseProvider.SqlServer => "NHibernate.Driver.MicrosoftDataSqlClientDriver",
            _ => "NHibernate.Driver.SqlClientDriver"
        };

        var dialect = _provider switch
        {
            DatabaseProvider.Oracle => "NHibernate.Dialect.Oracle12cDialect",
            DatabaseProvider.SqlServer => "NHibernate.Dialect.MsSql2012Dialect",
            _ => "NHibernate.Dialect.MsSql2012Dialect"
        };

        sb.AppendLine($"{i3}    cfg.DataBaseIntegration(db =>");
        sb.AppendLine($"{i3}    {{");
        sb.AppendLine($"{i3}        db.Dialect<{dialect}>();");
        sb.AppendLine($"{i3}        db.Driver<{driverClass}>();");
        sb.AppendLine($"{i3}        db.ConnectionString = connectionString;");
        sb.AppendLine($"{i3}    }});");
        sb.AppendLine();
        sb.AppendLine($"{i3}    var mapper = new ConventionModelMapper();");

        foreach (var table in tables)
        {
            var className = NamingHelper.ToClassName(table.TableName);
            sb.AppendLine($"{i3}    mapper.AddMapping<{className}Map>();");
        }

        sb.AppendLine();
        sb.AppendLine($"{i3}    cfg.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());");
        sb.AppendLine();
        sb.AppendLine($"{i3}    _sessionFactory = cfg.BuildSessionFactory();");
        sb.AppendLine($"{i3}}}");
        sb.AppendLine();
        sb.AppendLine($"{i3}return _sessionFactory;");
        sb.AppendLine($"{i2}}}");
        sb.AppendLine($"{i1}}}");

        if (_legacyStyle)
            sb.AppendLine("}");

        return sb.ToString();
    }
}
