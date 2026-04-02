using System.CommandLine;
using System.CommandLine.Invocation;
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

var legacyOption = new Option<bool>(
    aliases: new[] { "--use-legacy-style", "-l" },
    getDefaultValue: () => false,
    description: "Generate code compatible with .NET Framework (C# 7.3): block-scoped namespaces, no NRT, no target-typed new.");

var excludeTablesOption = new Option<string?>(
    aliases: new[] { "--exclude-tables", "-x" },
    description: "Comma-separated list of tables to exclude from scaffolding (e.g. AUDIT_LOG,TEMP_DATA).");

var dryRunOption = new Option<bool>(
    aliases: new[] { "--dry-run", "-d" },
    getDefaultValue: () => false,
    description: "Preview which files would be generated without writing them to disk.");

var forceOption = new Option<bool>(
    aliases: new[] { "--force", "-f" },
    getDefaultValue: () => false,
    description: "Overwrite existing files in the output directory without prompting.");

scaffoldCommand.AddArgument(connectionStringArg);
scaffoldCommand.AddArgument(providerArg);
scaffoldCommand.AddOption(outputOption);
scaffoldCommand.AddOption(namespaceOption);
scaffoldCommand.AddOption(schemaOption);
scaffoldCommand.AddOption(tablesOption);
scaffoldCommand.AddOption(legacyOption);
scaffoldCommand.AddOption(excludeTablesOption);
scaffoldCommand.AddOption(dryRunOption);
scaffoldCommand.AddOption(forceOption);

scaffoldCommand.SetHandler(async (InvocationContext ctx) =>
{
    try
    {
        var connStr = ctx.ParseResult.GetValueForArgument(connectionStringArg);
        var providerName = ctx.ParseResult.GetValueForArgument(providerArg);
        var output = ctx.ParseResult.GetValueForOption(outputOption)!;
        var ns = ctx.ParseResult.GetValueForOption(namespaceOption)!;
        var schema = ctx.ParseResult.GetValueForOption(schemaOption);
        var tables = ctx.ParseResult.GetValueForOption(tablesOption);
        var legacy = ctx.ParseResult.GetValueForOption(legacyOption);
        var excludeTables = ctx.ParseResult.GetValueForOption(excludeTablesOption);
        var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
        var force = ctx.ParseResult.GetValueForOption(forceOption);

        var provider = DatabaseProviderExtensions.Parse(providerName);

        var tableFilter = tables?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();

        var excludeFilter = excludeTables?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();

        var orchestrator = new ScaffoldOrchestrator();
        await orchestrator.RunAsync(connStr, provider, output, ns, schema, tableFilter, excludeFilter, legacy, dryRun, force);
    }
    catch (ArgumentException ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        ctx.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        Console.ResetColor();
        ctx.ExitCode = 2;
    }
});

rootCommand.AddCommand(scaffoldCommand);

return await rootCommand.InvokeAsync(args);
