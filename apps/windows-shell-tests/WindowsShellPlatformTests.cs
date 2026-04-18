using System.IO;

using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class WindowsShellPlatformTests
{
    [Fact]
    public void Initialize_CreatesLocalStateScaffold_WhenLegacyRuntimeIsMissing()
    {
        var appRoot = CreateTempPath();
        var legacyRoot = CreateTempPath();
        var context = WindowsShellPlatform.Initialize(
            WindowsShellAppPaths.FromRootDirectory(appRoot),
            LegacyRuntimePaths.FromRootDirectory(legacyRoot));

        Assert.True(Directory.Exists(context.Paths.RootDirectory));
        Assert.True(Directory.Exists(context.Paths.StateDirectory));
        Assert.True(Directory.Exists(context.Paths.CatalogDirectory));
        Assert.True(Directory.Exists(context.Paths.ProofArtifactsDirectory));
        Assert.True(Directory.Exists(context.Paths.PrintArtifactsDirectory));
        Assert.True(File.Exists(context.Paths.DatabasePath));
        Assert.Equal("skipped", context.MigrationStatus.Status);
    }

    [Fact]
    public void Initialize_ImportsLegacyCatalogAuditArtifactsAndBatchSnapshot()
    {
        var appRoot = CreateTempPath();
        var legacyRoot = CreateTempDirectory();
        var overlayDirectory = Path.Combine(legacyRoot, "template-overlays");
        var auditDirectory = Path.Combine(legacyRoot, "audit");
        var backupDirectory = Path.Combine(auditDirectory, "backups");
        var proofsDirectory = Path.Combine(legacyRoot, "proofs");

        Directory.CreateDirectory(overlayDirectory);
        Directory.CreateDirectory(backupDirectory);
        Directory.CreateDirectory(proofsDirectory);

        File.WriteAllText(
            Path.Combine(overlayDirectory, "template-manifest.json"),
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "legacy-basic@v1",
              "templates": [
                {
                  "version": "legacy-basic@v1",
                  "path": "legacy-basic@v1.json",
                  "label_name": "Legacy Basic",
                  "enabled": true,
                  "description": "Legacy overlay"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(overlayDirectory, "legacy-basic@v1.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "legacy-basic@v1",
              "label_name": "Legacy Basic",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": []
            }
            """);
        File.WriteAllText(Path.Combine(auditDirectory, "dispatch-ledger.json"), "[]");
        File.WriteAllText(Path.Combine(auditDirectory, "proof-ledger.json"), "[]");
        File.WriteAllText(Path.Combine(backupDirectory, "bundle-legacy.json"), "{}");
        File.WriteAllText(Path.Combine(proofsDirectory, "JOB-001-proof.pdf"), "%PDF-1.4\nlegacy-proof\n");
        File.WriteAllText(Path.Combine(proofsDirectory, "JOB-001-print.pdf"), "%PDF-1.4\nlegacy-print\n");
        File.WriteAllText(Path.Combine(legacyRoot, "batch-queue-snapshot.json"), "{\"snapshotId\":\"legacy\"}");

        var context = WindowsShellPlatform.Initialize(
            WindowsShellAppPaths.FromRootDirectory(appRoot),
            LegacyRuntimePaths.FromRootDirectory(legacyRoot));

        Assert.Equal("completed", context.MigrationStatus.Status);
        Assert.True(File.Exists(context.Paths.CatalogManifestPath));
        Assert.True(File.Exists(Path.Combine(context.Paths.CatalogDirectory, "legacy-basic@v1.json")));
        Assert.True(File.Exists(Path.Combine(context.Paths.ProofArtifactsDirectory, "JOB-001-proof.pdf")));
        Assert.True(File.Exists(Path.Combine(context.Paths.PrintArtifactsDirectory, "JOB-001-print.pdf")));
        Assert.True(File.Exists(Path.Combine(context.Paths.LegacyRuntimeImportDirectory, "audit", "dispatch-ledger.json")));
        Assert.True(File.Exists(Path.Combine(context.Paths.LegacyRuntimeImportDirectory, "audit", "backups", "bundle-legacy.json")));
        Assert.True(File.Exists(context.Paths.BatchSnapshotPath));
        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM backup_bundles;";
        Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
    }

    [Fact]
    public void Initialize_DoesNotOverwriteImportedState_WhenMigrationAlreadyCompleted()
    {
        var appRoot = CreateTempPath();
        var legacyRoot = CreateTempDirectory();
        var overlayDirectory = Path.Combine(legacyRoot, "template-overlays");
        Directory.CreateDirectory(overlayDirectory);

        var legacyManifestPath = Path.Combine(overlayDirectory, "template-manifest.json");
        File.WriteAllText(legacyManifestPath, "{\"default_template_version\":\"legacy-basic@v1\"}");

        var paths = WindowsShellAppPaths.FromRootDirectory(appRoot);
        var legacyPaths = LegacyRuntimePaths.FromRootDirectory(legacyRoot);

        var first = WindowsShellPlatform.Initialize(paths, legacyPaths);
        Assert.Equal("completed", first.MigrationStatus.Status);

        File.WriteAllText(paths.CatalogManifestPath, "{\"default_template_version\":\"local-edited@v2\"}");
        File.WriteAllText(legacyManifestPath, "{\"default_template_version\":\"legacy-overwrite-attempt@v9\"}");

        var second = WindowsShellPlatform.Initialize(paths, legacyPaths);

        Assert.Equal("completed", second.MigrationStatus.Status);
        Assert.Equal("{\"default_template_version\":\"local-edited@v2\"}", File.ReadAllText(paths.CatalogManifestPath));
    }

    private static string CreateTempDirectory()
    {
        var path = CreateTempPath();
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "jan-label-tests", Path.GetRandomFileName());
    }
}
