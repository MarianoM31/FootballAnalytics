using System.Text.RegularExpressions;

namespace FootballAnalytics.DatabaseMigrator;

public sealed class MigrationCatalog
{
    private static readonly Regex MigrationFilePattern = new(
        "^(?<id>\\d{3,})_(?<name>.+)\\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly string directory;

    public MigrationCatalog(string directory) => this.directory = directory;

    public IReadOnlyList<MigrationFile> Discover()
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Migration directory not found: {directory}");
        }

        var migrations = Directory.EnumerateFiles(directory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(path => CreateMigration(Path.GetFileName(path), path))
            .OrderBy(migration => migration.Id, StringComparer.Ordinal)
            .ToList();

        var duplicateId = migrations.GroupBy(migration => migration.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Duplicate migration id '{duplicateId.Key}'.");
        }

        return migrations;
    }

    private static MigrationFile CreateMigration(string fileName, string path)
    {
        var match = MigrationFilePattern.Match(fileName);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Migration '{fileName}' must use 001_descriptive_name.sql.");
        }

        return new MigrationFile(match.Groups["id"].Value, fileName, path);
    }
}
