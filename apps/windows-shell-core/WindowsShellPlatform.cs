using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public sealed record WindowsShellPlatformContext(
    WindowsShellAppPaths Paths,
    LegacyRuntimePaths LegacyRuntimePaths,
    LocalMigrationStatus MigrationStatus,
    ITemplateCatalogService TemplateCatalogService,
    IAuditService AuditService,
    IProofService ProofService,
    IRenderService RenderService);

public static class WindowsShellPlatform
{
    public static WindowsShellPlatformContext Initialize()
    {
        return Initialize(WindowsShellAppPaths.Resolve(), LegacyRuntimePaths.Detect());
    }

    public static WindowsShellPlatformContext Initialize(
        WindowsShellAppPaths paths,
        LegacyRuntimePaths legacyRuntimePaths)
    {
        paths.EnsureCreated();

        using (var connection = OpenConnection(paths))
        {
            connection.Open();
            LocalStateSchema.EnsureCreated(connection);
        }

        var migrationService = new LocalMigrationService(paths, legacyRuntimePaths);
        var migrationStatus = migrationService.EnsureLegacyImportAsync().GetAwaiter().GetResult();
        var templateCatalogService = new LocalTemplateCatalogService(paths);
        var auditService = new LocalAuditService(paths);
        var renderService = new LocalRenderService();
        var proofService = new LocalProofService(paths, templateCatalogService, renderService);
        return new WindowsShellPlatformContext(
            paths,
            legacyRuntimePaths,
            migrationStatus,
            templateCatalogService,
            auditService,
            proofService,
            renderService);
    }

    public static SqliteConnection OpenConnection(WindowsShellAppPaths paths)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };
        return new SqliteConnection(builder.ToString());
    }
}
