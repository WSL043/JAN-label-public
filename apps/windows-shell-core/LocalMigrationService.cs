using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public sealed record LocalMigrationStatus(
    string MigrationId,
    string Status,
    string StartedAtUtc,
    string? CompletedAtUtc,
    string DetailJson);

public sealed record LegacyImportReport(
    int CatalogFilesCopied,
    int ProofArtifactsCopied,
    int PrintArtifactsCopied,
    int AuditFilesCopied,
    int BackupBundlesIndexed,
    bool BatchSnapshotCopied,
    bool LegacyContentDetected);

public sealed class LocalMigrationService : ILocalMigrationService
{
    private const string MigrationId = "legacy_runtime_import_v1";

    private readonly WindowsShellAppPaths _paths;
    private readonly LegacyRuntimePaths _legacyPaths;

    public LocalMigrationService(WindowsShellAppPaths paths, LegacyRuntimePaths legacyPaths)
    {
        _paths = paths;
        _legacyPaths = legacyPaths;
    }

    public ValueTask<LocalMigrationStatus> EnsureLegacyImportAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();

        var existing = ReadStatus(connection);
        if (existing is not null &&
            (string.Equals(existing.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(existing.Status, "skipped", StringComparison.OrdinalIgnoreCase)))
        {
            return ValueTask.FromResult(existing);
        }

        var startedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        UpsertStatus(connection, "running", startedAtUtc, null, "{\"phase\":\"starting\"}");

        try
        {
            var report = ImportLegacyContent(connection);
            var completedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            var detailJson = JsonSerializer.Serialize(report);
            var status = report.LegacyContentDetected ? "completed" : "skipped";
            UpsertStatus(connection, status, startedAtUtc, completedAtUtc, detailJson);
            return ValueTask.FromResult(new LocalMigrationStatus(MigrationId, status, startedAtUtc, completedAtUtc, detailJson));
        }
        catch (Exception ex)
        {
            var completedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            var detailJson = JsonSerializer.Serialize(new { error = ex.Message });
            UpsertStatus(connection, "failed", startedAtUtc, completedAtUtc, detailJson);
            throw;
        }
    }

    private LegacyImportReport ImportLegacyContent(SqliteConnection connection)
    {
        var catalogFilesCopied = CopyDirectoryIfMissing(_legacyPaths.TemplateOverlayDirectory, _paths.CatalogDirectory);

        var proofArtifactsCopied = 0;
        var printArtifactsCopied = 0;
        CopyArtifacts(
            _legacyPaths.ProofOutputDirectory,
            ref proofArtifactsCopied,
            ref printArtifactsCopied);
        if (!SamePath(_legacyPaths.PrintOutputDirectory, _legacyPaths.ProofOutputDirectory))
        {
            CopyArtifacts(
                _legacyPaths.PrintOutputDirectory,
                ref proofArtifactsCopied,
                ref printArtifactsCopied);
        }

        var auditImportDirectory = Path.Combine(_paths.LegacyRuntimeImportDirectory, "audit");
        var auditFilesCopied = CopyDirectoryIfMissing(_legacyPaths.AuditDirectory, auditImportDirectory);

        var batchImportDirectory = Path.Combine(_paths.LegacyRuntimeImportDirectory, "batch");
        Directory.CreateDirectory(batchImportDirectory);
        var batchSnapshotTarget = _paths.BatchSnapshotPath;
        var batchSnapshotCopied = CopyFileIfMissing(_legacyPaths.BatchQueueSnapshotPath, batchSnapshotTarget);

        var backupBundlesIndexed = IndexBackupBundles(connection, Path.Combine(auditImportDirectory, "backups"));

        return new LegacyImportReport(
            catalogFilesCopied,
            proofArtifactsCopied,
            printArtifactsCopied,
            auditFilesCopied,
            backupBundlesIndexed,
            batchSnapshotCopied,
            catalogFilesCopied > 0 ||
            proofArtifactsCopied > 0 ||
            printArtifactsCopied > 0 ||
            auditFilesCopied > 0 ||
            backupBundlesIndexed > 0 ||
            batchSnapshotCopied);
    }

    private void CopyArtifacts(
        string sourceDirectory,
        ref int proofArtifactsCopied,
        ref int printArtifactsCopied)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*.pdf", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(sourcePath);
            var destinationDirectory = fileName.EndsWith("-print.pdf", StringComparison.OrdinalIgnoreCase)
                ? _paths.PrintArtifactsDirectory
                : _paths.ProofArtifactsDirectory;
            var destinationPath = Path.Combine(destinationDirectory, fileName);
            if (!CopyFileIfMissing(sourcePath, destinationPath))
            {
                continue;
            }

            if (fileName.EndsWith("-print.pdf", StringComparison.OrdinalIgnoreCase))
            {
                printArtifactsCopied += 1;
            }
            else
            {
                proofArtifactsCopied += 1;
            }
        }
    }

    private static int CopyDirectoryIfMissing(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        if (SamePath(sourceDirectory, destinationDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(destinationDirectory);
        var copiedFiles = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            if (CopyFileIfMissing(sourcePath, destinationPath))
            {
                copiedFiles += 1;
            }
        }

        return copiedFiles;
    }

    private static bool CopyFileIfMissing(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        if (SamePath(sourcePath, destinationPath) || File.Exists(destinationPath))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
        return true;
    }

    private static int IndexBackupBundles(SqliteConnection connection, string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            return 0;
        }

        var indexed = 0;
        foreach (var filePath in Directory.EnumerateFiles(backupDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT OR IGNORE INTO backup_bundles (
                    bundle_id,
                    file_name,
                    file_path,
                    created_at_utc,
                    size_bytes,
                    source,
                    detail_json)
                VALUES (
                    $bundleId,
                    $fileName,
                    $filePath,
                    $createdAtUtc,
                    $sizeBytes,
                    $source,
                    $detailJson);
                """;
            var fileInfo = new FileInfo(filePath);
            command.Parameters.AddWithValue("$bundleId", fileInfo.Name);
            command.Parameters.AddWithValue("$fileName", fileInfo.Name);
            command.Parameters.AddWithValue("$filePath", fileInfo.FullName);
            command.Parameters.AddWithValue("$createdAtUtc", fileInfo.LastWriteTimeUtc.ToString("O"));
            command.Parameters.AddWithValue("$sizeBytes", fileInfo.Length);
            command.Parameters.AddWithValue("$source", "legacy-desktop-shell");
            command.Parameters.AddWithValue("$detailJson", "{\"importedFrom\":\"legacy-runtime\"}");
            indexed += command.ExecuteNonQuery();
        }

        return indexed;
    }

    private static LocalMigrationStatus? ReadStatus(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT migration_id, status, started_at_utc, completed_at_utc, detail_json
            FROM migration_runs
            WHERE migration_id = $migrationId;
            """;
        command.Parameters.AddWithValue("$migrationId", MigrationId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new LocalMigrationStatus(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4));
    }

    private static void UpsertStatus(
        SqliteConnection connection,
        string status,
        string startedAtUtc,
        string? completedAtUtc,
        string detailJson)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO migration_runs (
                migration_id,
                status,
                started_at_utc,
                completed_at_utc,
                detail_json)
            VALUES (
                $migrationId,
                $status,
                $startedAtUtc,
                $completedAtUtc,
                $detailJson)
            ON CONFLICT(migration_id) DO UPDATE SET
                status = excluded.status,
                started_at_utc = excluded.started_at_utc,
                completed_at_utc = excluded.completed_at_utc,
                detail_json = excluded.detail_json;
            """;
        command.Parameters.AddWithValue("$migrationId", MigrationId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$startedAtUtc", startedAtUtc);
        command.Parameters.AddWithValue("$completedAtUtc", completedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$detailJson", detailJson);
        command.ExecuteNonQuery();
    }

    private static bool SamePath(string leftPath, string rightPath)
    {
        return string.Equals(
            Path.GetFullPath(leftPath),
            Path.GetFullPath(rightPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
