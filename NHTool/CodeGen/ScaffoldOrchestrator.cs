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
        HashSet<string>? tableFilter = null)
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

        if (tables.Count == 0)
        {
            Console.WriteLine("No tables found. Check connection string, schema, and table filters.");
            return;
        }

        Console.WriteLine($"Found {tables.Count} table(s). Generating code...");

        var entitiesDir = Path.Combine(outputDir, "Entities");
        var mappingsDir = Path.Combine(outputDir, "Mappings");

        Directory.CreateDirectory(entitiesDir);
        Directory.CreateDirectory(mappingsDir);

        var entityGen = new EntityGenerator(provider);
        var mappingGen = new MappingGenerator(provider);

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
        var sfGen = new SessionFactoryGenerator(provider);
        var helperCode = sfGen.Generate(tables, ns);
        var helperPath = Path.Combine(outputDir, "NHibernateHelper.cs");
        await File.WriteAllTextAsync(helperPath, helperCode);
        Console.WriteLine($"  -> {helperPath}");

        Console.WriteLine();
        Console.WriteLine($"Scaffold complete! {tables.Count} entities generated in '{outputDir}'.");
    }
}
