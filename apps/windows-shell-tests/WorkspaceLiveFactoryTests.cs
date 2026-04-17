using System.Collections.ObjectModel;

using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class WorkspaceLiveFactoryTests
{
    [Fact]
    public async Task LoadSnapshotAsync_UsesExpandedAuditWindow_AndKeepsBatchFailureLaneLocal()
    {
        var client = new StubCompanionClient((command, payload) => command switch
        {
            "print_bridge_status" => new PrintBridgeStatusDto
            {
                PrintAdapterKind = "pdf",
                AuditLogDir = "C:\\audit",
                AuditBackupDir = "C:\\audit\\backups",
                ProofOutputDir = "C:\\proofs",
                PrintOutputDir = "C:\\prints",
                SpoolOutputDir = "C:\\spool",
            },
            "template_catalog_command" => new TemplateCatalogResultDto
            {
                DefaultTemplateVersion = "missing-preview@v1",
                Templates = new()
                {
                    new TemplateCatalogEntryDto
                    {
                        Version = "missing-preview@v1",
                        LabelName = "Missing Preview Template",
                        Source = "packaged",
                    },
                },
            },
            "template_catalog_governance_command" => new TemplateCatalogGovernanceResultDto
            {
                ManifestStatus = "healthy",
                EffectiveDefaultTemplateVersion = "missing-preview@v1",
                EffectiveDefaultSource = "packaged",
            },
            "search_audit_log" => BuildAuditSearchResult(12, payload),
            "list_audit_backup_bundles" => new List<AuditBackupBundleDto>(),
            "load_batch_queue_snapshot" => throw new InvalidOperationException("snapshot corrupted"),
            "preview_template_draft" => new TemplateDraftPreviewResultDto
            {
                Svg = "<svg />",
                NormalizedJan = "4901234567894",
                TemplateVersion = "missing-preview@v1",
                LabelName = "Inline Preview",
                PageWidthMm = 50,
                PageHeightMm = 30,
                FieldCount = 5,
            },
            _ => throw new InvalidOperationException($"Unexpected companion command '{command}'."),
        });

        var snapshot = await client.LoadSnapshotAsync();

        var auditQuery = Assert.IsType<AuditSearchQueryDto>(
            client.Requests.Single((request) => request.Command == "search_audit_log").Payload);
        Assert.Equal(200, auditQuery.Limit);
        Assert.Equal(12, snapshot.AuditSearch.Entries.Count);
        Assert.Equal("snapshot corrupted", snapshot.BatchQueueSnapshot.ErrorMessage);
        Assert.Equal("fallback", snapshot.PreviewStatus);
    }

    [Fact]
    public void LoadModules_PreservesPendingProofs_AndDegradesOnlyBatchLane()
    {
        var snapshot = BuildShellWorkspaceSnapshot(
            auditEntryCount: 12,
            pendingProofCount: 12,
            batchErrorMessage: "snapshot corrupted");
        var modules = new ObservableCollection<ModuleModel>();

        WorkspaceLiveFactory.LoadModules(modules, snapshot);

        var printConsole = Assert.IsType<PrintConsoleWorkspaceModel>(
            modules.Single((module) => module.Label == "Print Console").Workspace);
        var batchJobs = Assert.IsType<BatchJobsWorkspaceModel>(
            modules.Single((module) => module.Label == "Batch Jobs").Workspace);
        var history = Assert.IsType<HistoryWorkspaceModel>(
            modules.Single((module) => module.Label == "History").Workspace);

        Assert.Equal(12, printConsole.ProofQueue.Count);
        Assert.Equal(12, history.PendingProofs.Count);
        Assert.Contains(batchJobs.ColumnRows, (row) => row.Name == "Authority" && row.Value == "apps/admin-web via apps/desktop-shell");
        Assert.Contains(batchJobs.StatusItems, (item) => item.Label == "Snapshot" && item.Value == "degraded");
        Assert.Contains("temporarily unavailable", batchJobs.MessageSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static AuditSearchResultDto BuildAuditSearchResult(int count, object? payload)
    {
        var query = Assert.IsType<AuditSearchQueryDto>(payload);
        Assert.Equal(200, query.Limit);

        var entries = Enumerable.Range(1, count)
            .Select((index) => BuildAuditEntry(index, index <= count))
            .ToList();
        return new AuditSearchResultDto { Entries = entries };
    }

    private static ShellWorkspaceSnapshot BuildShellWorkspaceSnapshot(
        int auditEntryCount,
        int pendingProofCount,
        string? batchErrorMessage = null)
    {
        var entries = Enumerable.Range(1, auditEntryCount)
            .Select((index) => BuildAuditEntry(index, index <= pendingProofCount))
            .ToList();

        return new ShellWorkspaceSnapshot(
            new PrintBridgeStatusDto
            {
                PrintAdapterKind = "pdf",
                AuditLogDir = "C:\\audit",
                AuditBackupDir = "C:\\audit\\backups",
                ProofOutputDir = "C:\\proofs",
                PrintOutputDir = "C:\\prints",
                SpoolOutputDir = "C:\\spool",
            },
            new TemplateCatalogResultDto
            {
                DefaultTemplateVersion = "basic-50x30@v1",
                Templates = new()
                {
                    new TemplateCatalogEntryDto
                    {
                        Version = "basic-50x30@v1",
                        LabelName = "Basic 50 x 30",
                        Source = "packaged",
                    },
                },
            },
            new TemplateCatalogGovernanceResultDto
            {
                ManifestStatus = "healthy",
                EffectiveDefaultTemplateVersion = "basic-50x30@v1",
                EffectiveDefaultSource = "packaged",
            },
            new AuditSearchResultDto { Entries = entries },
            Array.Empty<AuditBackupBundleDto>(),
            new BatchQueueSnapshotStateDto
            {
                ErrorMessage = batchErrorMessage,
            },
            "basic-50x30@v1",
            new TemplateDraftPreviewResultDto
            {
                Svg = "<svg />",
                NormalizedJan = "4901234567894",
                TemplateVersion = "basic-50x30@v1",
                LabelName = "Basic 50 x 30",
                PageWidthMm = 50,
                PageHeightMm = 30,
                FieldCount = 5,
            },
            null,
            "live",
            "packaged",
            "Packaged preview loaded.");
    }

    private static AuditSearchEntryDto BuildAuditEntry(int index, bool pending)
    {
        return new AuditSearchEntryDto
        {
            Dispatch = new PersistedDispatchRecordDto
            {
                Audit = new AuditEventDto
                {
                    JobId = $"JOB-{index:000}",
                    JobLineageId = $"LINEAGE-{index:000}",
                    Actor = new AuditActorDto
                    {
                        UserId = "operator",
                        DisplayName = "Operator",
                    },
                    Event = pending ? "proof_requested" : "printed",
                    OccurredAt = $"2026-04-17T08:{index:00}:00Z",
                },
                Mode = pending ? "proof" : "print",
                TemplateVersion = "basic-50x30@v1",
                MatchSubject = new DispatchMatchSubjectDto
                {
                    Sku = $"SKU-{index:000}",
                    Brand = "JAN-LAB",
                    JanNormalized = "4901234567894",
                    Qty = 24,
                },
                ArtifactMediaType = "application/pdf",
                ArtifactByteSize = 1024,
                SubmissionAdapterKind = "pdf",
                SubmissionExternalJobId = $"EXT-{index:000}",
            },
            Proof = new ProofRecordDto
            {
                ProofJobId = $"PROOF-{index:000}",
                JobLineageId = $"LINEAGE-{index:000}",
                RequestedBy = new AuditActorDto
                {
                    UserId = "operator",
                    DisplayName = "Operator",
                },
                RequestedAt = $"2026-04-17T08:{index:00}:30Z",
                Status = pending ? "pending" : "approved",
                ArtifactPath = $"C:\\proofs\\PROOF-{index:000}.pdf",
            },
        };
    }

    private sealed class StubCompanionClient(Func<string, object?, object> handler) : DesktopShellCompanionClient
    {
        public List<CompanionRequest> Requests { get; } = new();

        protected override Task<T> SendAsync<T>(string command, object? payload, CancellationToken cancellationToken)
        {
            Requests.Add(new CompanionRequest(command, payload));
            var response = handler(command, payload);
            return Task.FromResult((T)response);
        }
    }

    public sealed record CompanionRequest(string Command, object? Payload);
}
