namespace JanLabel.WindowsShell.Core;

public interface ITemplateCatalogService
{
    TemplateCatalogSnapshot LoadCatalogSnapshot();

    ValueTask<TemplateCatalogDocument> LoadTemplateAsync(string templateVersion, CancellationToken cancellationToken = default);

    ValueTask<TemplateCatalogSaveResult> SaveTemplateAsync(TemplateDocument document, CancellationToken cancellationToken = default);
}

public interface IDesignerDocumentService
{
    ValueTask<DesignerDocument> LoadDocumentAsync(string templateVersion, CancellationToken cancellationToken = default);

    ValueTask<DesignerDocumentSaveResult> SaveDraftAsync(DesignerDocument document, CancellationToken cancellationToken = default);
}

public interface IRenderService
{
    ValueTask<RenderArtifact> RenderAsync(DesignerDocument document, RenderRequest request, CancellationToken cancellationToken = default);
}

public interface IBatchImportService
{
    ValueTask<BatchImportSession> ImportAsync(BatchImportRequest request, CancellationToken cancellationToken = default);

    ValueTask<BatchSubmitResult> SubmitAsync(string sessionId, CancellationToken cancellationToken = default);

    ValueTask<BatchRetryResult> RetryFailedAsync(string sessionId, CancellationToken cancellationToken = default);
}

public interface IProofService
{
    ValueTask SyncFromAuthorityAsync(IReadOnlyList<ProofLedgerRecord> records, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, ProofLedgerRecord>> LoadRecordsAsync(
        IEnumerable<string> proofJobIds,
        CancellationToken cancellationToken = default);

    ValueTask<ProofRecordSummary> CreateProofAsync(ProofRequest request, CancellationToken cancellationToken = default);

    ValueTask<ProofRecordSummary> ReviewAsync(ProofDecision decision, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    ValueTask SyncFromAuthorityAsync(
        IReadOnlyList<AuditDispatchRecord> dispatches,
        IReadOnlyList<AuditBackupBundleRecord> bundles,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AuditMirrorEntry>> LoadEntriesAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AuditBackupBundleRecord>> LoadBundlesAsync(CancellationToken cancellationToken = default);

    ValueTask<AuditSearchResult> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default);

    ValueTask<AuditExportResult> ExportAsync(AuditExportRequest request, CancellationToken cancellationToken = default);

    ValueTask<AuditRetentionResult> ApplyRetentionAsync(AuditRetentionRequest request, CancellationToken cancellationToken = default);
}

public interface IDispatchService
{
    ValueTask<DispatchResult> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default);
}

public interface IPrintAdapter
{
    string AdapterKind { get; }

    ValueTask<PrintDispatchArtifact> WritePdfAsync(PrintDispatchArtifact artifact, CancellationToken cancellationToken = default);
}

public interface ILocalMigrationService
{
    ValueTask<LocalMigrationStatus> EnsureLegacyImportAsync(CancellationToken cancellationToken = default);
}

public sealed record TemplateDocument(
    string TemplateVersion,
    string LabelName,
    string DocumentJson,
    string? Description = null);

public sealed record TemplateCatalogDocument(
    string TemplateVersion,
    string LabelName,
    string Source,
    string SourcePath,
    string DocumentJson,
    double PageWidthMm,
    double PageHeightMm,
    IReadOnlyList<TemplateCatalogDocumentField> Fields,
    string? Description = null);

public sealed record TemplateCatalogDocumentField(
    string Name,
    double Xmm,
    double Ymm,
    double FontSizeMm,
    string Template);

public sealed record TemplateCatalogSaveResult(
    string TemplateVersion,
    string ManifestPath,
    string TemplatePath,
    bool RequiresProofReview,
    IReadOnlyList<string> Warnings);

public sealed record DesignerDocument(
    string DocumentId,
    string TemplateVersion,
    string LabelName,
    string CanvasJson,
    string BindingJson,
    string? SourcePath = null);

public sealed record DesignerDocumentSaveResult(
    string DocumentId,
    string DraftPath,
    string Status,
    IReadOnlyList<string> Warnings);

public sealed record RenderRequest(
    string OutputTargets,
    string? SampleJson = null,
    string? NormalizedJan = null);

public sealed record RenderArtifact(
    string TemplateVersion,
    string Svg,
    byte[] PdfBytes,
    string OutputMediaType,
    string? NormalizedJan = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record BatchImportRequest(
    string SourcePath,
    string SourceKind,
    string? AliasMapJson = null);

public sealed record BatchImportSession(
    string SessionId,
    string SourceName,
    string Status,
    int RowCount,
    int ReadyCount,
    int WarningCount,
    IReadOnlyList<BatchImportRow> Rows);

public sealed record BatchImportRow(
    int RowIndex,
    string Sku,
    string JanRaw,
    string? JanNormalized,
    int Qty,
    string Brand,
    string TemplateVersion,
    string Status,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public sealed record BatchSubmitResult(
    string SessionId,
    string Status,
    int SubmittedCount,
    IReadOnlyList<string> Errors);

public sealed record BatchRetryResult(
    string SessionId,
    string Status,
    int RetriedCount,
    IReadOnlyList<string> Errors);

public sealed record ProofRequest(
    string TemplateVersion,
    string SubjectSku,
    string Brand,
    string JanNormalized,
    int Qty,
    string RequestedBy,
    string SampleJson = "{}",
    string JobLineageId = "",
    string? ParentJobId = null,
    string? RequestedAtUtc = null,
    string? Notes = null);

public sealed record ProofDecision(
    string ProofJobId,
    string Decision,
    string Actor,
    string OccurredAtUtc,
    string? Notes = null);

public sealed record ProofRecordSummary(
    string ProofJobId,
    string JobLineageId,
    string Status,
    string ArtifactPath,
    string SubjectSku,
    string TemplateVersion);

public sealed record ProofLedgerRecord(
    string ProofJobId,
    string JobLineageId,
    string Status,
    string ArtifactPath,
    string SubjectSku,
    string TemplateVersion,
    string RequestedBy,
    string RequestedAtUtc,
    string? Notes = null,
    ProofReviewSnapshot? Review = null);

public sealed record ProofReviewSnapshot(
    string Status,
    string Actor,
    string OccurredAtUtc,
    string? Notes = null);

public sealed record AuditQuery(
    string? SearchText = null,
    string? Lane = null,
    string? Status = null,
    int Limit = 200);

public sealed record AuditEventSummary(
    string AuditEventId,
    string Lane,
    string SubjectKey,
    string EventKind,
    string OccurredAtUtc,
    string DetailJson);

public sealed record AuditDispatchRecord(
    string DispatchJobId,
    string JobLineageId,
    string Lane,
    string EventKind,
    string OccurredAtUtc,
    string Actor,
    string TemplateVersion,
    string SubjectSku,
    string Brand,
    string JanNormalized,
    int Qty,
    string ArtifactPath,
    string ArtifactMediaType,
    long ArtifactByteSize,
    string AdapterKind,
    string ExternalJobId,
    string? Reason = null,
    string? ParentJobId = null);

public sealed record AuditBackupBundleRecord(
    string BundleId,
    string FileName,
    string FilePath,
    string CreatedAtUtc,
    long SizeBytes,
    string Source);

public sealed record AuditMirrorEntry(
    AuditDispatchRecord Dispatch,
    ProofLedgerRecord? Proof = null);

public sealed record AuditSearchResult(
    IReadOnlyList<AuditEventSummary> Events,
    int TotalCount);

public sealed record AuditExportRequest(
    string ExportName,
    AuditQuery Query);

public sealed record AuditExportResult(
    string ExportPath,
    int EventCount);

public sealed record AuditRetentionRequest(
    bool DryRun,
    string Actor);

public sealed record AuditRetentionResult(
    string Status,
    int RemovedEventCount,
    string? BackupBundlePath = null);

public sealed record DispatchRequest(
    string TemplateVersion,
    string SubjectSku,
    string Brand,
    string JanNormalized,
    int Qty,
    string ProofJobId,
    string JobLineageId,
    string RequestedBy);

public sealed record DispatchResult(
    string DispatchJobId,
    string Status,
    string ArtifactPath,
    IReadOnlyList<string> Warnings);

public sealed record PrintDispatchArtifact(
    string JobId,
    string TargetPath,
    byte[] PdfBytes,
    string MediaType = "application/pdf");
