using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace JanLabel.WindowsShell;

public class DesktopShellCompanionClient : IAsyncDisposable
{
    private const string CompanionArgument = "--native-shell-companion";
    private const int LiveAuditSearchLimit = 200;
    private const string PrintBridgeStatusCommand = "print_bridge_status";
    private const string SearchAuditLogCommand = "search_audit_log";
    private const string ExportAuditLedgerCommand = "export_audit_ledger";
    private const string ListAuditBackupBundlesCommand = "list_audit_backup_bundles";
    private const string LoadBatchQueueSnapshotCommand = "load_batch_queue_snapshot";
    private const string ApproveProofCommand = "approve_proof";
    private const string RejectProofCommand = "reject_proof";
    private const string PreviewTemplateDraftCommand = "preview_template_draft";
    private const string TemplateCatalogCommand = "template_catalog_command";
    private const string TemplateCatalogGovernanceCommand = "template_catalog_governance_command";
    private const string DesktopShellBinaryEnvVar = "JAN_LABEL_DESKTOP_SHELL_BINARY";

    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private string? _cachedPreviewTemplateVersion;
    private string? _cachedPreviewSource;
    private TemplateDraftPreviewResultDto? _cachedPreviewResult;
    private string? _cachedPreviewError;
    private bool _isDisposed;

    public async Task<ShellWorkspaceSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bridgeStatus = await FetchPrintBridgeStatusAsync(cancellationToken);
        var templateCatalog = await FetchTemplateCatalogAsync(cancellationToken);
        var templateGovernance = await FetchTemplateCatalogGovernanceAsync(cancellationToken);
        var auditSearch = await SearchAuditLogAsync(
            new AuditSearchQueryDto { Limit = LiveAuditSearchLimit },
            cancellationToken);
        var auditBackupBundles = await LoadOptionalAsync(
            ListAuditBackupBundlesAsync,
            static _ => Array.Empty<AuditBackupBundleDto>(),
            cancellationToken);
        var batchQueueSnapshot = await LoadOptionalAsync(
            LoadBatchQueueSnapshotAsync,
            static error => new BatchQueueSnapshotStateDto
            {
                ErrorMessage = error.Message,
            },
            cancellationToken);

        var previewTemplateVersion = string.IsNullOrWhiteSpace(templateCatalog.DefaultTemplateVersion)
            ? templateCatalog.Templates.FirstOrDefault()?.Version ?? "inline-preview@v1"
            : templateCatalog.DefaultTemplateVersion;
        var previewResolution = ResolveTemplateSource(previewTemplateVersion, templateGovernance);
        TemplateDraftPreviewResultDto? preview = null;
        string? previewError = null;
        string previewStatus;
        var previewSource = DescribePreviewSource(previewResolution.SourceKind);
        string previewMessage;

        try
        {
            if (string.Equals(_cachedPreviewTemplateVersion, previewTemplateVersion, StringComparison.Ordinal) &&
                string.Equals(_cachedPreviewSource, previewResolution.SourceText, StringComparison.Ordinal))
            {
                preview = _cachedPreviewResult;
                previewError = _cachedPreviewError;
                previewStatus = DeterminePreviewStatus(previewResolution.SourceKind, preview, fromCache: true);
                previewMessage = BuildPreviewMessage(previewStatus, previewResolution.SourceKind, previewTemplateVersion, previewError);
            }
            else
            {
                preview = await PreviewTemplateDraftAsync(
                    new TemplateDraftPreviewRequestDto
                    {
                        TemplateSource = previewResolution.SourceText,
                        Sample = new TemplateDraftPreviewSampleDto
                        {
                            JobId = "WIN-SHELL-PREVIEW-001",
                            Sku = "200-145-3",
                            Brand = "JAN-LAB",
                            Jan = "4901234567894",
                            Qty = 24,
                        },
                    },
                    cancellationToken);
                _cachedPreviewTemplateVersion = previewTemplateVersion;
                _cachedPreviewSource = previewResolution.SourceText;
                _cachedPreviewResult = preview;
                _cachedPreviewError = null;
                previewStatus = DeterminePreviewStatus(previewResolution.SourceKind, preview, fromCache: false);
                previewMessage = BuildPreviewMessage(previewStatus, previewResolution.SourceKind, previewTemplateVersion, null);
            }
        }
        catch (Exception ex)
        {
            previewError = ex.Message;
            previewStatus = "degraded";
            previewMessage = BuildPreviewMessage(previewStatus, previewResolution.SourceKind, previewTemplateVersion, previewError);
            _cachedPreviewTemplateVersion = previewTemplateVersion;
            _cachedPreviewSource = null;
            _cachedPreviewResult = null;
            _cachedPreviewError = previewError;
        }

        return new ShellWorkspaceSnapshot(
            bridgeStatus,
            templateCatalog,
            templateGovernance,
            auditSearch,
            auditBackupBundles,
            batchQueueSnapshot,
            previewTemplateVersion,
            preview,
            previewError,
            previewStatus,
            previewSource,
            previewMessage);
    }

    private static async Task<T> LoadOptionalAsync<T>(
        Func<CancellationToken, Task<T>> loader,
        Func<Exception, T> fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return await loader(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return fallback(ex);
        }
    }

    public Task<PrintBridgeStatusDto> FetchPrintBridgeStatusAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<PrintBridgeStatusDto>(PrintBridgeStatusCommand, null, cancellationToken);
    }

    public Task<AuditSearchResultDto> SearchAuditLogAsync(
        AuditSearchQueryDto? query,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<AuditSearchResultDto>(SearchAuditLogCommand, query, cancellationToken);
    }

    public Task<AuditExportResultDto> ExportAuditLedgerAsync(
        AuditExportRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<AuditExportResultDto>(ExportAuditLedgerCommand, request, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditBackupBundleDto>> ListAuditBackupBundlesAsync(
        CancellationToken cancellationToken = default)
    {
        var bundles = await SendAsync<List<AuditBackupBundleDto>>(
            ListAuditBackupBundlesCommand,
            null,
            cancellationToken);
        return bundles;
    }

    public Task<BatchQueueSnapshotStateDto> LoadBatchQueueSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BatchQueueSnapshotStateDto>(LoadBatchQueueSnapshotCommand, null, cancellationToken);
    }

    public Task<ProofRecordDto> ApproveProofAsync(
        ProofReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProofRecordDto>(ApproveProofCommand, request, cancellationToken);
    }

    public Task<ProofRecordDto> RejectProofAsync(
        ProofReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProofRecordDto>(RejectProofCommand, request, cancellationToken);
    }

    public Task<TemplateDraftPreviewResultDto> PreviewTemplateDraftAsync(
        TemplateDraftPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<TemplateDraftPreviewResultDto>(PreviewTemplateDraftCommand, request, cancellationToken);
    }

    public Task<TemplateCatalogResultDto> FetchTemplateCatalogAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<TemplateCatalogResultDto>(TemplateCatalogCommand, null, cancellationToken);
    }

    public Task<TemplateCatalogGovernanceResultDto> FetchTemplateCatalogGovernanceAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<TemplateCatalogGovernanceResultDto>(
            TemplateCatalogGovernanceCommand,
            null,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _disposeCts.Cancel();
        StopCompanion();

        try
        {
            if (await _requestLock.WaitAsync(TimeSpan.FromSeconds(2)))
            {
                _requestLock.Release();
            }
        }
        catch
        {
        }
        finally
        {
            _disposeCts.Dispose();
        }
    }

    protected virtual async Task<T> SendAsync<T>(string command, object? payload, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var effectiveCancellationToken = linkedCts.Token;
        await _requestLock.WaitAsync(effectiveCancellationToken);
        try
        {
            return await SendWithRestartAsync<T>(command, payload, restartAllowed: true, effectiveCancellationToken);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task<T> SendWithRestartAsync<T>(
        string command,
        object? payload,
        bool restartAllowed,
        CancellationToken cancellationToken)
    {
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureCompanionStarted();
            return await SendCoreAsync<T>(command, payload, cancellationToken);
        }
        catch (Exception ex) when (restartAllowed && ex is IOException or InvalidOperationException)
        {
            StopCompanion();
            EnsureCompanionStarted();
            return await SendCoreAsync<T>(command, payload, cancellationToken);
        }
    }

    private async Task<T> SendCoreAsync<T>(string command, object? payload, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_stdin is null || _stdout is null || _process is null)
        {
            throw new InvalidOperationException("desktop-shell companion process is not available.");
        }

        var request = new CompanionRequestEnvelope
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Command = command,
            Payload = payload,
        };
        var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
        await _stdin.WriteLineAsync(requestJson.AsMemory(), cancellationToken);
        await _stdin.FlushAsync(cancellationToken);

        var responseJson = await _stdout.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new IOException("desktop-shell companion closed the response stream.");
        }

        var response = JsonSerializer.Deserialize<CompanionResponseEnvelope<T>>(responseJson, _jsonOptions);
        if (response is null)
        {
            throw new IOException("desktop-shell companion returned an unreadable response.");
        }

        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new IOException(
                $"desktop-shell companion response id mismatch for {command}: expected {request.RequestId}, got {response.RequestId}.");
        }

        if (!response.Ok)
        {
            var code = response.Error?.Code ?? "command_failed";
            var message = response.Error?.Message ?? "desktop-shell companion command failed.";
            throw new DesktopShellCompanionException(code, message);
        }

        if (response.Result is null)
        {
            throw new IOException($"desktop-shell companion returned no result for {command}.");
        }

        return response.Result;
    }

    private void EnsureCompanionStarted()
    {
        ThrowIfDisposed();
        if (_process is { HasExited: false } && _stdin is not null && _stdout is not null)
        {
            return;
        }

        StopCompanion();

        var binaryPath = ResolveCompanionPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = CompanionArgument,
            WorkingDirectory = Path.GetDirectoryName(binaryPath) ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to launch desktop-shell companion at {binaryPath}.");
        _process.ErrorDataReceived += static (_, _) => { };
        _process.BeginErrorReadLine();
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    private void StopCompanion()
    {
        try
        {
            _stdin?.Dispose();
        }
        catch
        {
        }

        try
        {
            _stdout?.Dispose();
        }
        catch
        {
        }

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
            catch
            {
            }
            finally
            {
                _process.Dispose();
            }
        }

        _process = null;
        _stdin = null;
        _stdout = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DesktopShellCompanionClient));
        }
    }

    private static string ResolveCompanionPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(DesktopShellBinaryEnvVar);
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (var candidate in EnumerateCompanionCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"desktop-shell companion binary was not found. Set {DesktopShellBinaryEnvVar} or build apps/desktop-shell/src-tauri/target/release/desktop-shell.exe.");
    }

    private static IEnumerable<string> EnumerateCompanionCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;

        yield return Path.Combine(currentDirectory, "apps", "desktop-shell", "src-tauri", "target", "release", "desktop-shell.exe");
        yield return Path.Combine(currentDirectory, "apps", "desktop-shell", "src-tauri", "target", "debug", "desktop-shell.exe");
        yield return Path.Combine(baseDirectory, "desktop-shell.exe");
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "desktop-shell", "src-tauri", "target", "release", "desktop-shell.exe"));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "desktop-shell", "src-tauri", "target", "debug", "desktop-shell.exe"));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "desktop-shell", "src-tauri", "target", "release", "desktop-shell.exe"));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "desktop-shell", "src-tauri", "target", "debug", "desktop-shell.exe"));
    }

    private static PreviewTemplateSourceResolution ResolveTemplateSource(
        string templateVersion,
        TemplateCatalogGovernanceResultDto governance)
    {
        var localEntry = governance.LocalEntries.FirstOrDefault(
            (entry) => string.Equals(entry.Version, templateVersion, StringComparison.OrdinalIgnoreCase) && entry.FileExists);
        if (localEntry is not null && File.Exists(localEntry.ResolvedPath))
        {
            return new PreviewTemplateSourceResolution(
                File.ReadAllText(localEntry.ResolvedPath),
                PreviewSourceKind.LocalOverlay);
        }

        foreach (var candidate in EnumeratePackagedTemplateCandidates(templateVersion))
        {
            if (File.Exists(candidate))
            {
                return new PreviewTemplateSourceResolution(
                    File.ReadAllText(candidate),
                    PreviewSourceKind.PackagedTemplate);
            }
        }

        return new PreviewTemplateSourceResolution(InlinePreviewTemplate, PreviewSourceKind.InlineFallback);
    }

    private static string DeterminePreviewStatus(
        PreviewSourceKind sourceKind,
        TemplateDraftPreviewResultDto? preview,
        bool fromCache)
    {
        if (preview is null)
        {
            return "degraded";
        }

        if (sourceKind == PreviewSourceKind.InlineFallback)
        {
            return "fallback";
        }

        return fromCache ? "cached" : "live";
    }

    private static string DescribePreviewSource(PreviewSourceKind sourceKind)
    {
        return sourceKind switch
        {
            PreviewSourceKind.LocalOverlay => "saved local overlay",
            PreviewSourceKind.PackagedTemplate => "packaged template",
            PreviewSourceKind.InlineFallback => "inline fallback template",
            _ => "preview source",
        };
    }

    private static string BuildPreviewMessage(
        string previewStatus,
        PreviewSourceKind sourceKind,
        string templateVersion,
        string? previewError)
    {
        if (!string.IsNullOrWhiteSpace(previewError))
        {
            return previewError;
        }

        return previewStatus switch
        {
            "live" => $"SVG preview came from desktop-shell companion using the {DescribePreviewSource(sourceKind)}.",
            "cached" => $"Reusing the last desktop-shell preview from the {DescribePreviewSource(sourceKind)}.",
            "fallback" => $"Inline fallback preview is active because no saved or packaged source was available for {templateVersion}.",
            _ => $"Preview is degraded while resolving {templateVersion}.",
        };
    }

    private static IEnumerable<string> EnumeratePackagedTemplateCandidates(string templateVersion)
    {
        var fileName = $"{templateVersion}.json";
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;

        yield return Path.Combine(currentDirectory, "packages", "templates", fileName);
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "packages", "templates", fileName));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "packages", "templates", fileName));
    }

    private const string InlinePreviewTemplate = """
        {
          "schema_version": "template-spec-v1",
          "template_version": "inline-preview@v1",
          "label_name": "inline-preview",
          "page": { "width_mm": 50, "height_mm": 30 },
          "fields": [
            { "name": "brand", "x_mm": 3, "y_mm": 4, "font_size_mm": 3, "template": "{brand}" },
            { "name": "sku", "x_mm": 3, "y_mm": 10, "font_size_mm": 4, "template": "{sku}" },
            { "name": "jan", "x_mm": 3, "y_mm": 16, "font_size_mm": 3, "template": "{jan}" }
          ]
        }
        """;
}

public sealed class DesktopShellCompanionException : Exception
{
    public DesktopShellCompanionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed record ShellWorkspaceSnapshot(
    PrintBridgeStatusDto BridgeStatus,
    TemplateCatalogResultDto TemplateCatalog,
    TemplateCatalogGovernanceResultDto TemplateGovernance,
    AuditSearchResultDto AuditSearch,
    IReadOnlyList<AuditBackupBundleDto> AuditBackupBundles,
    BatchQueueSnapshotStateDto BatchQueueSnapshot,
    string PreviewTemplateVersion,
    TemplateDraftPreviewResultDto? Preview,
    string? PreviewError,
    string PreviewStatus,
    string PreviewSource,
    string PreviewMessage);

internal enum PreviewSourceKind
{
    LocalOverlay,
    PackagedTemplate,
    InlineFallback,
}

internal sealed record PreviewTemplateSourceResolution(string SourceText, PreviewSourceKind SourceKind);

public sealed class CompanionRequestEnvelope
{
    public string RequestId { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public object? Payload { get; set; }
}

public sealed class CompanionResponseEnvelope<T>
{
    public string RequestId { get; set; } = string.Empty;

    public bool Ok { get; set; }

    public T? Result { get; set; }

    public CompanionErrorEnvelope? Error { get; set; }
}

public sealed class CompanionErrorEnvelope
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class PrintBridgeStatusDto
{
    public List<string> AvailableAdapters { get; set; } = new();
    public string ResolvedZintPath { get; set; } = string.Empty;
    public string ProofOutputDir { get; set; } = string.Empty;
    public string PrintOutputDir { get; set; } = string.Empty;
    public string SpoolOutputDir { get; set; } = string.Empty;
    public string AuditLogDir { get; set; } = string.Empty;
    public string AuditBackupDir { get; set; } = string.Empty;
    public string PrintAdapterKind { get; set; } = string.Empty;
    public string WindowsPrinterName { get; set; } = string.Empty;
    public bool AllowWithoutProofEnabled { get; set; }
    public List<BridgeWarningDto> WarningDetails { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class BridgeWarningDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class AuditSearchQueryDto
{
    public string? SearchText { get; set; }
    public int? Limit { get; set; }
}

public sealed class AuditSearchResultDto
{
    public List<AuditSearchEntryDto> Entries { get; set; } = new();
}

public sealed class AuditSearchEntryDto
{
    public PersistedDispatchRecordDto Dispatch { get; set; } = new();
    public ProofRecordDto? Proof { get; set; }
}

public sealed class PersistedDispatchRecordDto
{
    public AuditEventDto Audit { get; set; } = new();
    public string Mode { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public DispatchMatchSubjectDto MatchSubject { get; set; } = new();
    public string ArtifactMediaType { get; set; } = string.Empty;
    public long ArtifactByteSize { get; set; }
    public string SubmissionAdapterKind { get; set; } = string.Empty;
    public string SubmissionExternalJobId { get; set; } = string.Empty;
}

public sealed class AuditEventDto
{
    public string JobId { get; set; } = string.Empty;
    public string JobLineageId { get; set; } = string.Empty;
    public string? ParentJobId { get; set; }
    public AuditActorDto Actor { get; set; } = new();
    public string Event { get; set; } = string.Empty;
    public string OccurredAt { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class AuditActorDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class DispatchMatchSubjectDto
{
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string JanNormalized { get; set; } = string.Empty;
    public int Qty { get; set; }
}

public sealed class ProofRecordDto
{
    public string ProofJobId { get; set; } = string.Empty;
    public string JobLineageId { get; set; } = string.Empty;
    public AuditActorDto RequestedBy { get; set; } = new();
    public string RequestedAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public ProofDecisionDto? Decision { get; set; }
}

public sealed class ProofDecisionDto
{
    public string Status { get; set; } = string.Empty;
    public AuditActorDto Actor { get; set; } = new();
    public string OccurredAt { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class AuditExportRequestDto
{
    public string? Scope { get; set; }
}

public sealed class AuditExportResultDto
{
    public string Scope { get; set; } = string.Empty;
    public int DispatchCount { get; set; }
    public int ProofCount { get; set; }
    public AuditLedgerSnapshotDto Snapshot { get; set; } = new();
}

public sealed class AuditLedgerSnapshotDto
{
    public List<PersistedDispatchRecordDto> Dispatches { get; set; } = new();
    public List<ProofRecordDto> Proofs { get; set; } = new();
}

public sealed class AuditBackupBundleDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string CreatedAtUtc { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public sealed class BatchQueueSnapshotStateDto
{
    public string FilePath { get; set; } = string.Empty;
    public bool Present { get; set; }
    public BatchQueueSnapshotDto? Snapshot { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class BatchQueueSnapshotDto
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public string CapturedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public string? SourceKind { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string SubmitPhase { get; set; } = string.Empty;
    public string SubmitMessage { get; set; } = string.Empty;
    public List<BatchQueueRowDto> QueueRows { get; set; } = new();
}

public sealed class BatchQueueRowDto
{
    public int RowIndex { get; set; }
    public BatchQueueDraftDto Draft { get; set; } = new();
    public string SubmissionStatus { get; set; } = string.Empty;
    public string? RetryLineageJobId { get; set; }
    public string? DispatchError { get; set; }
    public BatchQueueDispatchResultDto? DispatchResult { get; set; }
}

public sealed class BatchQueueDraftDto
{
    public string JobId { get; set; } = string.Empty;
    public string ParentSku { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public BatchQueueJanDto Jan { get; set; } = new();
    public int Qty { get; set; }
    public string Brand { get; set; } = string.Empty;
    public BatchQueueTemplateRefDto Template { get; set; } = new();
    public BatchQueueExecutionDto? Execution { get; set; }
    public BatchQueuePrinterProfileDto PrinterProfile { get; set; } = new();
    public string Actor { get; set; } = string.Empty;
    public string RequestedAt { get; set; } = string.Empty;
}

public sealed class BatchQueueJanDto
{
    public string Raw { get; set; } = string.Empty;
    public string Normalized { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class BatchQueueTemplateRefDto
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public sealed class BatchQueuePrinterProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Adapter { get; set; } = string.Empty;
    public string PaperSize { get; set; } = string.Empty;
    public int Dpi { get; set; }
    public string ScalePolicy { get; set; } = string.Empty;
}

public sealed class BatchQueueExecutionDto
{
    public string Mode { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public string? Notes { get; set; }
    public string? ExpiresAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedAt { get; set; }
    public string? SourceProofJobId { get; set; }
    public bool AllowWithoutProof { get; set; }
}

public sealed class BatchQueueDispatchResultDto
{
    public string Mode { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public BatchQueueArtifactDto Artifact { get; set; } = new();
    public BatchQueueSubmissionDto Submission { get; set; } = new();
    public BatchQueueAuditDto Audit { get; set; } = new();
}

public sealed class BatchQueueArtifactDto
{
    public string MediaType { get; set; } = string.Empty;
    public int ByteSize { get; set; }
}

public sealed class BatchQueueSubmissionDto
{
    public string AdapterKind { get; set; } = string.Empty;
    public string ExternalJobId { get; set; } = string.Empty;
}

public sealed class BatchQueueAuditDto
{
    public string Event { get; set; } = string.Empty;
    public string OccurredAt { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobLineageId { get; set; } = string.Empty;
    public string? ParentJobId { get; set; }
    public string? Reason { get; set; }
}

public sealed class ProofReviewRequestDto
{
    public string ProofJobId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string DecidedAt { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class TemplateDraftPreviewRequestDto
{
    public string TemplateSource { get; set; } = string.Empty;
    public TemplateDraftPreviewSampleDto Sample { get; set; } = new();
}

public sealed class TemplateDraftPreviewSampleDto
{
    public string JobId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Jan { get; set; } = string.Empty;
    public int Qty { get; set; }
}

public sealed class TemplateDraftPreviewResultDto
{
    public string Svg { get; set; } = string.Empty;
    public string NormalizedJan { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public string LabelName { get; set; } = string.Empty;
    public double PageWidthMm { get; set; }
    public double PageHeightMm { get; set; }
    public int FieldCount { get; set; }
}

public sealed class TemplateCatalogResultDto
{
    public string DefaultTemplateVersion { get; set; } = string.Empty;
    public List<TemplateCatalogEntryDto> Templates { get; set; } = new();
}

public sealed class TemplateCatalogEntryDto
{
    public string Version { get; set; } = string.Empty;
    public string LabelName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Source { get; set; }
}

public sealed class TemplateCatalogGovernanceResultDto
{
    public string ManifestStatus { get; set; } = string.Empty;
    public string OverlayDirectoryPath { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public bool ManifestExists { get; set; }
    public string EffectiveDefaultTemplateVersion { get; set; } = string.Empty;
    public string EffectiveDefaultSource { get; set; } = string.Empty;
    public int LocalEntryCount { get; set; }
    public int OverlayJsonFileCount { get; set; }
    public List<TemplateCatalogGovernanceEntryDto> LocalEntries { get; set; } = new();
    public List<TemplateCatalogGovernanceIssueDto> Issues { get; set; } = new();
    public List<string> BackupGuidance { get; set; } = new();
    public List<string> RepairGuidance { get; set; } = new();
    public List<string> SingleWriterGuidance { get; set; } = new();
}

public sealed class TemplateCatalogGovernanceEntryDto
{
    public string Version { get; set; } = string.Empty;
    public string LabelName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ResolvedPath { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool FileExists { get; set; }
}

public sealed class TemplateCatalogGovernanceIssueDto
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
