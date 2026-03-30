using System.CommandLine;
using NHTool.CodeGen;
using NHTool.Models;

// ── Root command ──────────────────────────────────────────────────────
var rootCommand = new RootCommand("nh-tool - NHibernate Mapping-by-Code scaffolder from database schema");

// ── nh-scaffold command ──────────────────────────────────────────────
var scaffoldCommand = new Command("nh-scaffold", "Scaffold NHibernate entities and mappings from a database");

var connectionStringArg = new Argument<string>(
    "connection-string",
    "The database connection string");

var providerArg = new Argument<string>(
    "provider",
    "Database provider: oracle | sqlserver");

var outputOption = new Option<string>(
    aliases: new[] { "--output", "-o" },
    getDefaultValue: () => "./Generated",
    description: "Output directory for generated files");

var namespaceOption = new Option<string>(
    aliases: new[] { "--namespace", "-n" },
    getDefaultValue: () => "MyApp.Data",
    description: "Root namespace for generated classes");

var schemaOption = new Option<string?>(
    aliases: new[] { "--schema", "-s" },
    description: "Schema/owner filter (e.g. dbo, HR). Defaults to provider default.");

var tablesOption = new Option<string?>(
    aliases: new[] { "--tables", "-t" },
    description: "Comma-separated list of tables to scaffold (e.g. USERS,ORDERS,ORDER_ITEMS). If omitted, all tables are scaffolded.");

scaffoldCommand.AddArgument(connectionStringArg);
scaffoldCommand.AddArgument(providerArg);
scaffoldCommand.AddOption(outputOption);
scaffoldCommand.AddOption(namespaceOption);
scaffoldCommand.AddOption(schemaOption);
scaffoldCommand.AddOption(tablesOption);

scaffoldCommand.SetHandler(async (connStr, providerName, output, ns, schema, tables) =>
{
    try
    {
        var provider = DatabaseProviderExtensions.Parse(providerName);
        var tableFilter = tables?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();

        var orchestrator = new ScaffoldOrchestrator();
        await orchestrator.RunAsync(connStr, provider, output, ns, schema, tableFilter);
    }
    catch (ArgumentException ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        Console.ResetColor();
        Environment.ExitCode = 2;
    }
},
connectionStringArg, providerArg, outputOption, namespaceOption, schemaOption, tablesOption);

rootCommand.AddCommand(scaffoldCommand);

return await rootCommand.InvokeAsync(args);
