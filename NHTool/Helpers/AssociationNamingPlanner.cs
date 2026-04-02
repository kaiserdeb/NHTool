using NHTool.Models;

namespace NHTool.Helpers;

public sealed record ManyToOneAssociationPlan(
    string ConstraintName,
    string PropertyName,
    string ReferencedClassName,
    IReadOnlyList<string> ColumnNames,
    bool IsComposite);

public sealed record InverseCollectionAssociationPlan(
    string ConstraintName,
    string PropertyName,
    string ItemClassName,
    IReadOnlyList<string> KeyColumnNames,
    bool IsComposite);

public sealed record TableAssociationPlan(
    IReadOnlyList<ManyToOneAssociationPlan> ManyToOnes,
    IReadOnlyList<InverseCollectionAssociationPlan> InverseCollections);

public static class AssociationNamingPlanner
{
    public static TableAssociationPlan Build(TableInfo table)
    {
        var fkColumnsCoveredByManyToOne = GetSingleColumnForeignKeyColumns(table.ForeignKeys);
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in table.Columns)
        {
            // Reserve scalar names that remain in the entity.
            // Only skip single-column FK scalars that are replaced by ManyToOne navigation.
            if (!col.IsPrimaryKey && fkColumnsCoveredByManyToOne.Contains(col.ColumnName))
                continue;

            reservedNames.Add(NamingHelper.ToPropertyName(col.ColumnName));
        }

        var manyToOnes = BuildManyToOnePlans(table.ForeignKeys, reservedNames);

        foreach (var assoc in manyToOnes)
            reservedNames.Add(assoc.PropertyName);

        var inverseCollections = BuildInverseCollectionPlans(table.InverseForeignKeys, reservedNames);

        return new TableAssociationPlan(manyToOnes, inverseCollections);
    }

    private static List<ManyToOneAssociationPlan> BuildManyToOnePlans(
        IReadOnlyCollection<ForeignKeyInfo> foreignKeys,
        HashSet<string> reservedNames)
    {
        var plans = new List<ManyToOneAssociationPlan>();
        var usedNames = new HashSet<string>(reservedNames, StringComparer.OrdinalIgnoreCase);

        foreach (var fkGroup in GroupByConstraint(foreignKeys)
                     .OrderBy(g => g[0].PkTableName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g[0].ConstraintName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g[0].FkColumnName, StringComparer.OrdinalIgnoreCase))
        {
            var first = fkGroup[0];
            var referencedClass = NamingHelper.ToClassName(first.PkTableName);
            var preferredName = NamingHelper.ToManyToOnePropertyName(first.FkColumnName, first.PkTableName);
            var propertyName = MakeUniqueName(preferredName, referencedClass, usedNames);

            var columnNames = fkGroup
                .Select(fk => fk.FkColumnName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            plans.Add(new ManyToOneAssociationPlan(
                first.ConstraintName,
                propertyName,
                referencedClass,
                columnNames,
                columnNames.Count > 1));

            usedNames.Add(propertyName);
        }

        return plans;
    }

    private static List<InverseCollectionAssociationPlan> BuildInverseCollectionPlans(
        IReadOnlyCollection<ForeignKeyInfo> inverseForeignKeys,
        HashSet<string> reservedNames)
    {
        var plans = new List<InverseCollectionAssociationPlan>();
        var usedNames = new HashSet<string>(reservedNames, StringComparer.OrdinalIgnoreCase);

        foreach (var fkGroup in GroupByConstraint(inverseForeignKeys)
                     .OrderBy(g => g[0].FkTableName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g[0].ConstraintName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g[0].FkColumnName, StringComparer.OrdinalIgnoreCase))
        {
            var first = fkGroup[0];
            var itemClassName = NamingHelper.ToClassName(first.FkTableName);
            var preferredName = NamingHelper.ToCollectionPropertyName(first.FkTableName);
            var fallbackName = $"{preferredName}By{NamingHelper.ToPropertyName(first.FkColumnName)}";
            var propertyName = MakeUniqueName(preferredName, fallbackName, usedNames);

            var keyColumns = fkGroup
                .Select(fk => fk.FkColumnName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            plans.Add(new InverseCollectionAssociationPlan(
                first.ConstraintName,
                propertyName,
                itemClassName,
                keyColumns,
                keyColumns.Count > 1));

            usedNames.Add(propertyName);
        }

        return plans;
    }

    private static string MakeUniqueName(string preferredName, string fallbackStem, HashSet<string> usedNames)
    {
        var candidate = string.IsNullOrWhiteSpace(preferredName) ? fallbackStem : preferredName;
        if (!usedNames.Contains(candidate))
            return candidate;

        if (!string.IsNullOrWhiteSpace(fallbackStem) && !usedNames.Contains(fallbackStem))
            return fallbackStem;

        var stem = string.IsNullOrWhiteSpace(fallbackStem) ? candidate : fallbackStem;
        var suffix = 2;
        while (usedNames.Contains($"{stem}{suffix}"))
            suffix++;

        return $"{stem}{suffix}";
    }

    private static IEnumerable<List<ForeignKeyInfo>> GroupByConstraint(IReadOnlyCollection<ForeignKeyInfo> foreignKeys)
    {
        return foreignKeys
            .GroupBy(fk => string.IsNullOrWhiteSpace(fk.ConstraintName)
                ? $"{fk.FkTableName}|{fk.PkTableName}|{fk.FkColumnName}"
                : fk.ConstraintName,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.ToList());
    }

    private static HashSet<string> GetSingleColumnForeignKeyColumns(IReadOnlyCollection<ForeignKeyInfo> foreignKeys)
    {
        return GroupByConstraint(foreignKeys)
            .Select(g => g.Select(fk => fk.FkColumnName).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            .Where(cols => cols.Count == 1)
            .SelectMany(cols => cols)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
