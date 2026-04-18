using System.IO;
using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class LocalAuditServiceTests
{
    [Fact]
    public async Task SyncFromAuthorityAsync_PersistsDispatchAndBundleRows()
    {
        var context = InitializePlatform();
        var service = context.AuditService;

        await service.SyncFromAuthorityAsync(
            new[]
            {
                SampleDispatch("DISPATCH-001", "proof", "proof_pending", "2026-04-19T09:00:00Z"),
            },
            new[]
            {
                new AuditBackupBundleRecord(
                    "bundle-001",
                    "bundle-001.json",
                    @"C:\audit\bundle-001.json",
                    "2026-04-19T08:30:00Z",
                    2048,
                    "desktop-shell-companion"),
            });

        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();

        using var dispatchCommand = connection.CreateCommand();
        dispatchCommand.CommandText = "SELECT COUNT(*) FROM dispatch_records;";
        Assert.Equal(1L, (long)dispatchCommand.ExecuteScalar()!);

        using var auditCommand = connection.CreateCommand();
        auditCommand.CommandText = "SELECT COUNT(*) FROM audit_events;";
        Assert.Equal(1L, (long)auditCommand.ExecuteScalar()!);

        using var bundleCommand = connection.CreateCommand();
        bundleCommand.CommandText = "SELECT COUNT(*) FROM backup_bundles;";
        Assert.Equal(1L, (long)bundleCommand.ExecuteScalar()!);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySearchTextAndLane()
    {
        var context = InitializePlatform();
        var service = context.AuditService;

        await service.SyncFromAuthorityAsync(
            new[]
            {
                SampleDispatch("DISPATCH-PROOF-001", "proof", "proof_pending", "2026-04-19T09:00:00Z", sku: "200-145-3"),
                SampleDispatch("DISPATCH-PRINT-001", "print", "printed", "2026-04-19T10:00:00Z", sku: "900-555-1"),
            },
            Array.Empty<AuditBackupBundleRecord>());

        var result = await service.SearchAsync(new AuditQuery("200-145-3", "proof", null, 20));

        var single = Assert.Single(result.Events);
        Assert.Equal("proof", single.Lane);
        Assert.Contains("200-145-3", single.DetailJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WritesJsonFromLocalAuditLedger()
    {
        var context = InitializePlatform();
        await context.AuditService.SyncFromAuthorityAsync(
            new[]
            {
                SampleDispatch("DISPATCH-EXPORT-001", "proof", "proof_pending", "2026-04-19T09:00:00Z"),
            },
            Array.Empty<AuditBackupBundleRecord>());

        await context.ProofService.SyncFromAuthorityAsync(
            new[]
            {
                new ProofLedgerRecord(
                    "PROOF-EXPORT-001",
                    "lineage-export-001",
                    "pending",
                    @"C:\proofs\PROOF-EXPORT-001.pdf",
                    "200-145-3",
                    "basic-50x30@v2",
                    "proof.user",
                    "2026-04-19T09:05:00Z"),
            });
        await context.ProofService.ReviewAsync(
            new ProofDecision(
                "PROOF-EXPORT-001",
                "approved",
                "ops.manager",
                "2026-04-19T09:10:00Z",
                "approved locally"));

        var export = await context.AuditService.ExportAsync(
            new AuditExportRequest(
                "audit-export-test",
                new AuditQuery()));

        Assert.True(File.Exists(export.ExportPath));
        Assert.True(export.EventCount >= 2);

        var json = File.ReadAllText(export.ExportPath);
        Assert.Contains("\"events\"", json, StringComparison.Ordinal);
        Assert.Contains("proof_approved", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DISPATCH-EXPORT-001", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadEntriesAsync_JoinsLocalProofByDispatchJobIdAndHonorsStatusFilter()
    {
        var context = InitializePlatform();
        await context.AuditService.SyncFromAuthorityAsync(
            new[]
            {
                SampleDispatch("JOB-PROOF-001", "proof", "proof_pending", "2026-04-19T09:00:00Z"),
                SampleDispatch("JOB-PRINT-001", "print", "printed", "2026-04-19T08:00:00Z", sku: "900-555-1"),
            },
            Array.Empty<AuditBackupBundleRecord>());

        await context.ProofService.SyncFromAuthorityAsync(
            new[]
            {
                new ProofLedgerRecord(
                    "JOB-PROOF-001",
                    "lineage-JOB-PROOF-001",
                    "pending",
                    @"C:\proofs\JOB-PROOF-001.pdf",
                    "200-145-3",
                    "basic-50x30@v2",
                    "proof.user",
                    "2026-04-19T09:05:00Z"),
            });
        await context.ProofService.ReviewAsync(
            new ProofDecision(
                "JOB-PROOF-001",
                "approved",
                "ops.manager",
                "2026-04-19T09:10:00Z",
                "approved locally"));

        var result = await context.AuditService.LoadEntriesAsync(
            new AuditQuery(Lane: "proof", Status: "approved", Limit: 20));

        var single = Assert.Single(result);
        Assert.Equal("JOB-PROOF-001", single.Dispatch.DispatchJobId);
        var proof = Assert.IsType<ProofLedgerRecord>(single.Proof);
        Assert.Equal("approved", proof.Status);
        Assert.Equal("ops.manager", proof.Review?.Actor);
    }

    [Fact]
    public async Task LoadBundlesAsync_ReturnsNewestFirst()
    {
        var context = InitializePlatform();
        await context.AuditService.SyncFromAuthorityAsync(
            Array.Empty<AuditDispatchRecord>(),
            new[]
            {
                new AuditBackupBundleRecord(
                    "bundle-older",
                    "bundle-older.json",
                    @"C:\audit\bundle-older.json",
                    "2026-04-19T08:30:00Z",
                    2048,
                    "desktop-shell-companion"),
                new AuditBackupBundleRecord(
                    "bundle-newer",
                    "bundle-newer.json",
                    @"C:\audit\bundle-newer.json",
                    "2026-04-19T09:30:00Z",
                    4096,
                    "desktop-shell-companion"),
            });

        var bundles = await context.AuditService.LoadBundlesAsync();

        Assert.Collection(
            bundles,
            bundle => Assert.Equal("bundle-newer", bundle.BundleId),
            bundle => Assert.Equal("bundle-older", bundle.BundleId));
    }

    private static AuditDispatchRecord SampleDispatch(
        string dispatchJobId,
        string lane,
        string eventKind,
        string occurredAtUtc,
        string sku = "200-145-3")
    {
        return new AuditDispatchRecord(
            dispatchJobId,
            $"lineage-{dispatchJobId}",
            lane,
            eventKind,
            occurredAtUtc,
            "ops.user",
            "basic-50x30@v2",
            sku,
            "JAN-LAB",
            "4901234567894",
            24,
            $@"C:\artifacts\{dispatchJobId}.pdf",
            "application/pdf",
            1024,
            "pdf-proof",
            $"external-{dispatchJobId}");
    }

    private static WindowsShellPlatformContext InitializePlatform()
    {
        return WindowsShellPlatform.Initialize(
            WindowsShellAppPaths.FromRootDirectory(CreateTempPath()),
            LegacyRuntimePaths.FromRootDirectory(CreateTempPath()));
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "jan-label-tests", Path.GetRandomFileName());
    }
}
