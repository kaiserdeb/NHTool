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

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Concurrent;");
        sb.AppendLine("using System.Threading;");
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
        sb.AppendLine($"{i2}private static readonly ConcurrentDictionary<string, Lazy<ISessionFactory>> _sessionFactories =");
        sb.AppendLine($"{i2}    new ConcurrentDictionary<string, Lazy<ISessionFactory>>(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine($"{i2}public static ISessionFactory BuildSessionFactory(string connectionString)");
        sb.AppendLine($"{i2}{{");
        sb.AppendLine($"{i3}if (string.IsNullOrWhiteSpace(connectionString))");
        sb.AppendLine($"{i3}    throw new ArgumentException(\"Connection string cannot be null or empty.\", nameof(connectionString));");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"{i3}var lazyFactory = _sessionFactories.GetOrAdd(connectionString, cs =>");
        sb.AppendLine($"{i3}{{");
        sb.AppendLine($"{i3}    return new Lazy<ISessionFactory>(() =>");
        sb.AppendLine($"{i3}    {{");
        sb.AppendLine($"{i3}        var cfg = new Configuration();");

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

        sb.AppendLine($"{i3}        cfg.DataBaseIntegration(db =>");
        sb.AppendLine($"{i3}        {{");
        sb.AppendLine($"{i3}            db.Dialect<{dialect}>();");
        sb.AppendLine($"{i3}            db.Driver<{driverClass}>();");
        sb.AppendLine($"{i3}            db.ConnectionString = cs;");
        sb.AppendLine($"{i3}        }});");
        sb.AppendLine();
        sb.AppendLine($"{i3}        var mapper = new ConventionModelMapper();");

        foreach (var table in tables)
        {
            var className = NamingHelper.ToClassName(table.TableName);
            sb.AppendLine($"{i3}        mapper.AddMapping<{className}Map>();");
        }

        sb.AppendLine();
        sb.AppendLine($"{i3}        cfg.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());");
        sb.AppendLine();
        sb.AppendLine($"{i3}        return cfg.BuildSessionFactory();");
        sb.AppendLine($"{i3}    }}, LazyThreadSafetyMode.ExecutionAndPublication);");
        sb.AppendLine($"{i3}}});");
        sb.AppendLine();
        sb.AppendLine($"{i3}try");
        sb.AppendLine($"{i3}{{");
        sb.AppendLine($"{i3}    return lazyFactory.Value;");
        sb.AppendLine($"{i3}}}");
        sb.AppendLine($"{i3}catch");
        sb.AppendLine($"{i3}{{");
        sb.AppendLine($"{i3}    _sessionFactories.TryRemove(connectionString, out _);");
        sb.AppendLine($"{i3}    throw;");
        sb.AppendLine($"{i3}}}");
        sb.AppendLine($"{i2}}}");
        sb.AppendLine();
        sb.AppendLine($"{i2}public static bool DisposeSessionFactory(string connectionString)");
        sb.AppendLine($"{i2}{{");
        sb.AppendLine($"{i3}if (string.IsNullOrWhiteSpace(connectionString))");
        sb.AppendLine($"{i3}    return false;");
        sb.AppendLine();
        sb.AppendLine($"{i3}if (!_sessionFactories.TryRemove(connectionString, out var lazyFactory))");
        sb.AppendLine($"{i3}    return false;");
        sb.AppendLine();
        sb.AppendLine($"{i3}if (lazyFactory.IsValueCreated)");
        sb.AppendLine($"{i3}    lazyFactory.Value.Dispose();");
        sb.AppendLine();
        sb.AppendLine($"{i3}return true;");
        sb.AppendLine($"{i2}}}");
        sb.AppendLine();
        sb.AppendLine($"{i2}public static void DisposeAllSessionFactories()");
        sb.AppendLine($"{i2}{{");
        sb.AppendLine($"{i3}foreach (var pair in _sessionFactories.ToArray())");
        sb.AppendLine($"{i3}{{");
        sb.AppendLine($"{i3}    if (_sessionFactories.TryRemove(pair.Key, out var lazyFactory) && lazyFactory.IsValueCreated)");
        sb.AppendLine($"{i3}        lazyFactory.Value.Dispose();");
        sb.AppendLine($"{i3}}}");
        sb.AppendLine($"{i2}}}");
        sb.AppendLine($"{i1}}}");

        if (_legacyStyle)
            sb.AppendLine("}");

        return sb.ToString();
    }
}
