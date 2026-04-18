using System.IO;
using JanLabel.WindowsShell.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class LocalProofServiceTests
{
    [Fact]
    public async Task SyncFromAuthorityAsync_PreservesLocalTerminalDecisionAgainstStalePendingSnapshot()
    {
        var context = InitializePlatform();
        var service = context.ProofService;
        var authorityRecord = SampleProof("PROOF-LOCAL-001", "lineage-001", "pending");

        await service.SyncFromAuthorityAsync(new[] { authorityRecord });
        await service.ReviewAsync(
            new ProofDecision(
                authorityRecord.ProofJobId,
                "approved",
                "ops.manager",
                "2026-04-19T10:00:00Z",
                "approved locally"));

        await service.SyncFromAuthorityAsync(new[] { authorityRecord });
        var records = await service.LoadRecordsAsync(new[] { authorityRecord.ProofJobId });

        var record = Assert.Single(records.Values);
        Assert.Equal("approved", record.Status);
        Assert.Equal("approved", record.Review?.Status);
        Assert.Equal("ops.manager", record.Review?.Actor);
    }

    [Fact]
    public async Task SyncFromAuthorityAsync_SupersedesEarlierPendingOrApprovedProofOnSameLineage()
    {
        var context = InitializePlatform();
        var service = context.ProofService;
        var first = SampleProof("PROOF-LINEAGE-001", "shared-lineage", "pending");
        var second = SampleProof("PROOF-LINEAGE-002", "shared-lineage", "pending", requestedAtUtc: "2026-04-19T11:00:00Z");

        await service.SyncFromAuthorityAsync(new[] { first });
        await service.ReviewAsync(
            new ProofDecision(
                first.ProofJobId,
                "approved",
                "ops.manager",
                "2026-04-19T10:30:00Z",
                "approved before replacement"));

        await service.SyncFromAuthorityAsync(new[] { second });
        var records = await service.LoadRecordsAsync(new[] { first.ProofJobId, second.ProofJobId });

        Assert.Equal("superseded", records[first.ProofJobId].Status);
        Assert.Equal("pending", records[second.ProofJobId].Status);
        var reviewNotes = records[first.ProofJobId].Review?.Notes;
        Assert.NotNull(reviewNotes);
        Assert.Contains("superseded by newer proof", reviewNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewAsync_RejectsSupersededProof()
    {
        var context = InitializePlatform();
        var service = context.ProofService;
        var first = SampleProof("PROOF-SUPERSEDED-001", "superseded-lineage", "pending");
        var second = SampleProof("PROOF-SUPERSEDED-002", "superseded-lineage", "pending", requestedAtUtc: "2026-04-19T11:00:00Z");

        await service.SyncFromAuthorityAsync(new[] { first });
        await service.SyncFromAuthorityAsync(new[] { second });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.ReviewAsync(
                new ProofDecision(
                    first.ProofJobId,
                    "rejected",
                    "ops.manager",
                    "2026-04-19T11:05:00Z",
                    "too late")));

        Assert.Contains("superseded", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewAsync_PersistsAuditEventForLocalProofDecision()
    {
        var context = InitializePlatform();
        var service = context.ProofService;
        var authorityRecord = SampleProof("PROOF-AUDIT-001", "audit-lineage-001", "pending");

        await service.SyncFromAuthorityAsync(new[] { authorityRecord });
        await service.ReviewAsync(
            new ProofDecision(
                authorityRecord.ProofJobId,
                "rejected",
                "ops.qa",
                "2026-04-19T12:00:00Z",
                "subject mismatch"));

        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();

        using var proofCommand = connection.CreateCommand();
        proofCommand.CommandText =
            """
            SELECT status, reviewed_at_utc, review_actor
            FROM proof_records
            WHERE proof_job_id = $proofJobId;
            """;
        proofCommand.Parameters.AddWithValue("$proofJobId", authorityRecord.ProofJobId);
        using var proofReader = proofCommand.ExecuteReader();
        Assert.True(proofReader.Read());
        Assert.Equal("rejected", proofReader.GetString(0));
        Assert.Equal("2026-04-19T12:00:00.0000000+00:00", proofReader.GetString(1));
        Assert.Equal("ops.qa", proofReader.GetString(2));

        using var auditCommand = connection.CreateCommand();
        auditCommand.CommandText =
            """
            SELECT event_kind, actor
            FROM audit_events
            WHERE subject_key = $subjectKey;
            """;
        auditCommand.Parameters.AddWithValue("$subjectKey", authorityRecord.ProofJobId);
        using var auditReader = auditCommand.ExecuteReader();
        Assert.True(auditReader.Read());
        Assert.Equal("proof_rejected", auditReader.GetString(0));
        Assert.Equal("ops.qa", auditReader.GetString(1));
    }

    [Fact]
    public async Task CreateProofAsync_PersistsArtifactProofLedgerAndMatchingDispatchMirror()
    {
        var context = InitializePlatform();
        await SaveTemplateAsync(context, "native-proof@v1");

        var created = await context.ProofService.CreateProofAsync(
            new ProofRequest(
                "native-proof@v1",
                "200-145-3",
                "JAN-LAB",
                "4901234567894",
                24,
                "ops.creator",
                "{\"status\":\"Queued for native proof\",\"qty\":\"24 PCS\"}",
                "lineage-create-001",
                "JOB-ROOT-001",
                "2026-04-19T13:00:00Z",
                "Created for local proof create test."));

        Assert.Equal("pending", created.Status);
        Assert.Equal("200-145-3", created.SubjectSku);
        Assert.Equal("native-proof@v1", created.TemplateVersion);
        Assert.True(File.Exists(created.ArtifactPath));
        Assert.Contains("-proof.pdf", created.ArtifactPath, StringComparison.OrdinalIgnoreCase);

        var mirroredEntries = await context.AuditService.LoadEntriesAsync(new AuditQuery(Lane: "proof", Limit: 20));
        var mirrored = Assert.Single(mirroredEntries);
        Assert.Equal(created.ProofJobId, mirrored.Dispatch.DispatchJobId);
        Assert.Equal("lineage-create-001", mirrored.Dispatch.JobLineageId);
        Assert.Equal("JOB-ROOT-001", mirrored.Dispatch.ParentJobId);
        Assert.Equal("JAN-LAB", mirrored.Dispatch.Brand);
        Assert.Equal("4901234567894", mirrored.Dispatch.JanNormalized);
        Assert.Equal(24, mirrored.Dispatch.Qty);
        Assert.Equal("pending", mirrored.Proof?.Status);
        Assert.Equal(created.ArtifactPath, mirrored.Proof?.ArtifactPath);

        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();

        using var auditCommand = connection.CreateCommand();
        auditCommand.CommandText =
            """
            SELECT event_kind, actor
            FROM audit_events
            WHERE subject_key = $subjectKey;
            """;
        auditCommand.Parameters.AddWithValue("$subjectKey", "200-145-3");
        using var auditReader = auditCommand.ExecuteReader();
        Assert.True(auditReader.Read());
        Assert.Equal("proof_pending", auditReader.GetString(0));
        Assert.Equal("ops.creator", auditReader.GetString(1));
    }

    private static WindowsShellPlatformContext InitializePlatform()
    {
        return WindowsShellPlatform.Initialize(
            WindowsShellAppPaths.FromRootDirectory(CreateTempPath()),
            LegacyRuntimePaths.FromRootDirectory(CreateTempPath()));
    }

    private static ProofLedgerRecord SampleProof(
        string proofJobId,
        string lineageId,
        string status,
        string requestedAtUtc = "2026-04-19T09:00:00Z")
    {
        return new ProofLedgerRecord(
            proofJobId,
            lineageId,
            status,
            $@"C:\proofs\{proofJobId}.pdf",
            "200-145-3",
            "basic-50x30@v2",
            "proof.user",
            requestedAtUtc,
            "pending import");
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "jan-label-tests", Path.GetRandomFileName());
    }

    private static async Task SaveTemplateAsync(WindowsShellPlatformContext context, string templateVersion)
    {
        var documentJson =
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "__TEMPLATE_VERSION__",
              "label_name": "Native Proof",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": [
                { "name": "brand", "x_mm": 3, "y_mm": 4, "font_size_mm": 3, "template": "{brand}" },
                { "name": "sku", "x_mm": 3, "y_mm": 10, "font_size_mm": 3, "template": "{sku}" },
                { "name": "barcode", "x_mm": 3, "y_mm": 16, "font_size_mm": 3, "template": "{jan}" },
                { "name": "qty", "x_mm": 3, "y_mm": 24, "font_size_mm": 3, "template": "{qty}" }
              ]
            }
            """
            .Replace("__TEMPLATE_VERSION__", templateVersion, StringComparison.Ordinal);

        await context.TemplateCatalogService.SaveTemplateAsync(
            new TemplateDocument(
                templateVersion,
                "Native Proof",
                documentJson,
                "Local proof create fixture"));
    }
}
