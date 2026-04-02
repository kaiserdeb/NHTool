using NHTool.Helpers;
using NHTool.Models;
using NHTool.Schema;

namespace NHTool.CodeGen;

public class ScaffoldOrchestrator
{
    public async Task RunAsync(
        string connectionString,
        DatabaseProvider provider,
        string outputDir,
        string ns,
        string? schemaFilter,
        HashSet<string>? tableFilter = null,
        HashSet<string>? excludeFilter = null,
        bool legacyStyle = false,
        bool dryRun = false,
        bool force = false)
    {
        Console.WriteLine($"Connecting to {provider} database...");

        var reader = SchemaReaderFactory.Create(provider);
        var tables = await reader.ReadTablesAsync(connectionString, schemaFilter);

        if (tableFilter is { Count: > 0 })
        {
            var before = tables.Count;
            tables = tables.Where(t => tableFilter.Contains(t.TableName.ToUpperInvariant())).ToList();

            var notFound = tableFilter.Except(tables.Select(t => t.TableName.ToUpperInvariant())).ToList();
            if (notFound.Count > 0)
                Console.WriteLine($"Warning: tables not found in schema: {string.Join(", ", notFound)}");

            Console.WriteLine($"Filtered {before} -> {tables.Count} table(s) by --tables parameter.");
        }

        if (excludeFilter is { Count: > 0 })
        {
            var before = tables.Count;
            tables = tables.Where(t => !excludeFilter.Contains(t.TableName.ToUpperInvariant())).ToList();
            Console.WriteLine($"Excluded {before - tables.Count} table(s) by --exclude-tables parameter.");
        }

        if (tables.Count == 0)
        {
            Console.WriteLine("No tables found. Check connection string, schema, and table filters.");
            return;
        }

        // Skip tables with no columns (e.g. permission issues on catalog views)
        var skipped = tables.Where(t => t.Columns.Count == 0).Select(t => t.TableName).ToList();
        if (skipped.Count > 0)
        {
            Console.WriteLine($"Warning: skipping {skipped.Count} table(s) with no columns: {string.Join(", ", skipped)}");
            tables = tables.Where(t => t.Columns.Count > 0).ToList();
        }

        if (tables.Count == 0)
        {
            Console.WriteLine("No tables with columns found. Check permissions on catalog views.");
            return;
        }

        // ── Wire up foreign keys ────────────────────────────────────────
        var fks = await reader.ReadForeignKeysAsync(connectionString, schemaFilter);
        var tableByName = tables.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fks)
        {
            // Only wire FK if both sides are in the scaffolded set
            if (tableByName.TryGetValue(fk.FkTableName, out var fkTable)
                && tableByName.ContainsKey(fk.PkTableName))
            {
                fkTable.ForeignKeys.Add(fk);
            }

            if (tableByName.TryGetValue(fk.PkTableName, out var pkTable)
                && tableByName.ContainsKey(fk.FkTableName))
            {
                pkTable.InverseForeignKeys.Add(fk);
            }
        }

        Console.WriteLine($"Found {tables.Count} table(s). Generating code...");

        if (dryRun)
        {
            var dryRunCompositeWarnings = BuildCompositeFkWarnings(tables);
            if (dryRunCompositeWarnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: dry run detected composite foreign keys that require manual navigation mapping:");
                foreach (var warning in dryRunCompositeWarnings)
                    Console.WriteLine($"  - {warning}");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("[DRY RUN] The following files would be generated:");
            foreach (var table in tables)
            {
                var className = NamingHelper.ToClassName(table.TableName);
                Console.WriteLine($"  -> {Path.Combine(outputDir, "Entities", $"{className}.cs")}");
                Console.WriteLine($"  -> {Path.Combine(outputDir, "Mappings", $"{className}Map.cs")}");
            }
            Console.WriteLine($"  -> {Path.Combine(outputDir, "NHibernateHelper.cs")}");
            Console.WriteLine();
            Console.WriteLine($"[DRY RUN] {tables.Count} entities would be generated. No files were written.");
            return;
        }

        var compositeWarnings = BuildCompositeFkWarnings(tables);
        if (compositeWarnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: some composite foreign keys require manual navigation mapping:");
            foreach (var warning in compositeWarnings)
                Console.WriteLine($"  - {warning}");
            Console.ResetColor();
        }

        var entitiesDir = Path.Combine(outputDir, "Entities");
        var mappingsDir = Path.Combine(outputDir, "Mappings");

        if (!force && Directory.Exists(outputDir)
            && (Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length > 0))
        {
            throw new ArgumentException(
                $"Scaffolding aborted because output directory '{outputDir}' already contains .cs files and --force was not specified.",
                nameof(outputDir));
        }

        Directory.CreateDirectory(entitiesDir);
        Directory.CreateDirectory(mappingsDir);

        if (legacyStyle)
            Console.WriteLine("Using legacy style (compatible with .NET Framework / C# 7.3).");

        var entityGen = new EntityGenerator(provider, legacyStyle);
        var mappingGen = new MappingGenerator(provider, legacyStyle);

        foreach (var table in tables)
        {
            var className = NamingHelper.ToClassName(table.TableName);

            var entityCode = entityGen.Generate(table, ns);
            var entityPath = Path.Combine(entitiesDir, $"{className}.cs");
            await File.WriteAllTextAsync(entityPath, entityCode);
            Console.WriteLine($"  -> {entityPath}");

            var mappingCode = mappingGen.Generate(table, ns);
            var mappingPath = Path.Combine(mappingsDir, $"{className}Map.cs");
            await File.WriteAllTextAsync(mappingPath, mappingCode);
            Console.WriteLine($"  -> {mappingPath}");
        }

        // Generate NHibernateHelper.cs
        var sfGen = new SessionFactoryGenerator(provider, legacyStyle);
        var helperCode = sfGen.Generate(tables, ns);
        var helperPath = Path.Combine(outputDir, "NHibernateHelper.cs");
        await File.WriteAllTextAsync(helperPath, helperCode);
        Console.WriteLine($"  -> {helperPath}");

        Console.WriteLine();
        Console.WriteLine($"Scaffold complete! {tables.Count} entities generated in '{outputDir}'.");
    }

    private static List<string> BuildCompositeFkWarnings(List<TableInfo> tables)
    {
        var warnings = new List<string>();

        foreach (var table in tables)
        {
            var plan = AssociationNamingPlanner.Build(table);

            foreach (var assoc in plan.ManyToOnes.Where(a => a.IsComposite))
            {
                warnings.Add(
                    $"{table.TableName}: ManyToOne '{assoc.ConstraintName}' ({string.Join(", ", assoc.ColumnNames)}) was skipped.");
            }

            foreach (var assoc in plan.InverseCollections.Where(a => a.IsComposite))
            {
                warnings.Add(
                    $"{table.TableName}: inverse collection '{assoc.ConstraintName}' ({string.Join(", ", assoc.KeyColumnNames)}) was skipped.");
            }
        }

        return warnings;
    }
}
