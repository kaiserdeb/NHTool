using System.Text;
using NHTool.Helpers;
using NHTool.Models;

namespace NHTool.CodeGen;

public class SessionFactoryGenerator
{
    private readonly DatabaseProvider _provider;

    public SessionFactoryGenerator(DatabaseProvider provider)
    {
        _provider = provider;
    }

    public string Generate(List<TableInfo> tables, string ns)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using NHibernate;");
        sb.AppendLine("using NHibernate.Cfg;");
        sb.AppendLine("using NHibernate.Mapping.ByCode;");
        sb.AppendLine($"using {ns}.Mappings;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("public static class NHibernateHelper");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly object _lock = new();");
        sb.AppendLine("    private static ISessionFactory? _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine("    public static ISessionFactory BuildSessionFactory(string connectionString)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_sessionFactory != null) return _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine("        lock (_lock)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_sessionFactory != null) return _sessionFactory;");
        sb.AppendLine();
        sb.AppendLine("            var cfg = new Configuration();");

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

        sb.AppendLine($"            cfg.DataBaseIntegration(db =>");
        sb.AppendLine("            {");
        sb.AppendLine($"                db.Dialect<{dialect}>();");
        sb.AppendLine($"                db.Driver<{driverClass}>();");
        sb.AppendLine("                db.ConnectionString = connectionString;");
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("            var mapper = new ConventionModelMapper();");

        foreach (var table in tables)
        {
            var className = NamingHelper.ToClassName(table.TableName);
            sb.AppendLine($"            mapper.AddMapping<{className}Map>();");
        }

        sb.AppendLine();
        sb.AppendLine("            cfg.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());");
        sb.AppendLine();
        sb.AppendLine("            _sessionFactory = cfg.BuildSessionFactory();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return _sessionFactory;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
