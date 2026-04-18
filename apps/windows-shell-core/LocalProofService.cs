using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public sealed class LocalProofService : IProofService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WindowsShellAppPaths _paths;
    private readonly ITemplateCatalogService _templateCatalogService;
    private readonly IRenderService _renderService;

    public LocalProofService(
        WindowsShellAppPaths paths,
        ITemplateCatalogService templateCatalogService,
        IRenderService renderService)
    {
        _paths = paths;
        _templateCatalogService = templateCatalogService;
        _renderService = renderService;
    }

    public ValueTask SyncFromAuthorityAsync(IReadOnlyList<ProofLedgerRecord> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (records.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeRecord(record);
            var existing = TryLoadRecord(transaction, normalized.ProofJobId);
            var merged = MergeAuthorityRecord(existing, normalized);
            UpsertRecord(transaction, merged);
            if (ShouldSupersedePriorLineageProofs(existing, normalized, merged))
            {
                SupersedePriorLineageProofs(transaction, merged);
            }
        }

        transaction.Commit();
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyDictionary<string, ProofLedgerRecord>> LoadRecordsAsync(
        IEnumerable<string> proofJobIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ids = proofJobIds
            .Where(static (value) => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ids.Length == 0)
        {
            return ValueTask.FromResult<IReadOnlyDictionary<string, ProofLedgerRecord>>(
                new Dictionary<string, ProofLedgerRecord>(StringComparer.OrdinalIgnoreCase));
        }

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();

        var results = new Dictionary<string, ProofLedgerRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var proofJobId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = TryLoadRecord(connection, proofJobId);
            if (record is not null)
            {
                results[record.ProofJobId] = record;
            }
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, ProofLedgerRecord>>(results);
    }

    public async ValueTask<ProofRecordSummary> CreateProofAsync(ProofRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRequest = NormalizeRequest(request);
        var proofJobId = BuildProofJobId();
        var requestedAtUtc = normalizedRequest.RequestedAtUtc
            ?? DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var bindingJson = BuildProofSampleJson(normalizedRequest, proofJobId);
        var template = await _templateCatalogService.LoadTemplateAsync(normalizedRequest.TemplateVersion, cancellationToken);
        var renderRequest = new RenderRequest("PDF", bindingJson, normalizedRequest.JanNormalized);
        var document = new DesignerDocument(
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            template.TemplateVersion,
            template.LabelName,
            template.DocumentJson,
            bindingJson,
            template.SourcePath);
        var render = await _renderService.RenderAsync(document, renderRequest, cancellationToken);
        if (render.PdfBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Native proof creation for '{normalizedRequest.TemplateVersion}' did not produce a PDF artifact.");
        }

        Directory.CreateDirectory(_paths.ProofArtifactsDirectory);
        var artifactPath = Path.Combine(_paths.ProofArtifactsDirectory, $"{proofJobId}-proof.pdf");
        WriteArtifactAtomically(artifactPath, render.PdfBytes);

        try
        {
            using var connection = WindowsShellPlatform.OpenConnection(_paths);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var record = new ProofLedgerRecord(
                proofJobId,
                normalizedRequest.JobLineageId,
                "pending",
                artifactPath,
                normalizedRequest.SubjectSku,
                normalizedRequest.TemplateVersion,
                normalizedRequest.RequestedBy,
                requestedAtUtc,
                normalizedRequest.Notes ?? "Created from the Windows-native shell local proof path.");

            UpsertRecord(transaction, record);
            SupersedePriorLineageProofs(transaction, record);

            var dispatch = new AuditDispatchRecord(
                proofJobId,
                normalizedRequest.JobLineageId,
                "proof",
                "proof_pending",
                requestedAtUtc,
                normalizedRequest.RequestedBy,
                normalizedRequest.TemplateVersion,
                normalizedRequest.SubjectSku,
                normalizedRequest.Brand,
                render.NormalizedJan ?? normalizedRequest.JanNormalized,
                normalizedRequest.Qty,
                artifactPath,
                "application/pdf",
                render.PdfBytes.LongLength,
                "pdf-proof",
                proofJobId,
                normalizedRequest.Notes ?? "Created from the Windows-native shell local proof path.",
                normalizedRequest.ParentJobId);
            UpsertDispatchRecord(transaction, dispatch);
            UpsertDispatchAuditEvent(transaction, dispatch);

            transaction.Commit();
            return ToSummary(record);
        }
        catch
        {
            TryDeleteArtifact(artifactPath);
            throw;
        }
    }

    public ValueTask<ProofRecordSummary> ReviewAsync(ProofDecision decision, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedDecision = NormalizeDecision(decision);

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var existing = TryLoadRecord(transaction, normalizedDecision.ProofJobId)
            ?? throw new InvalidOperationException(
                $"Proof job '{normalizedDecision.ProofJobId}' was not found in the local proof ledger.");

        if (string.Equals(existing.Status, "superseded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Proof job '{normalizedDecision.ProofJobId}' is already superseded and cannot be reviewed.");
        }

        var updated = existing with
        {
            Status = normalizedDecision.Decision,
            Review = new ProofReviewSnapshot(
                normalizedDecision.Decision,
                normalizedDecision.Actor,
                normalizedDecision.OccurredAtUtc,
                normalizedDecision.Notes),
        };

        UpsertRecord(transaction, updated);
        InsertAuditEvent(transaction, updated);
        transaction.Commit();

        return ValueTask.FromResult(ToSummary(updated));
    }

    private static ProofRequest NormalizeRequest(ProofRequest request)
    {
        if (request.Qty <= 0)
        {
            throw new InvalidOperationException("Proof request quantity must be greater than zero.");
        }

        var jobLineageId = NormalizeOptionalText(request.JobLineageId);
        if (string.IsNullOrWhiteSpace(jobLineageId))
        {
            throw new InvalidOperationException("Proof request requires a job lineage id.");
        }

        return request with
        {
            TemplateVersion = NormalizeRequired(request.TemplateVersion, "template version"),
            SubjectSku = NormalizeRequired(request.SubjectSku, "subject sku"),
            Brand = NormalizeRequired(request.Brand, "brand"),
            JanNormalized = NormalizeRequired(request.JanNormalized, "normalized JAN"),
            RequestedBy = NormalizeActor(request.RequestedBy),
            SampleJson = NormalizeSampleJson(request.SampleJson),
            JobLineageId = jobLineageId,
            ParentJobId = NormalizeOptionalText(request.ParentJobId),
            RequestedAtUtc = string.IsNullOrWhiteSpace(request.RequestedAtUtc)
                ? null
                : NormalizeTimestamp(request.RequestedAtUtc),
            Notes = NormalizeOptionalText(request.Notes),
        };
    }

    private static ProofDecision NormalizeDecision(ProofDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.ProofJobId))
        {
            throw new InvalidOperationException("Proof decision requires a proof job id.");
        }

        var normalizedStatus = NormalizeStatus(decision.Decision);
        if (!string.Equals(normalizedStatus, "approved", StringComparison.Ordinal) &&
            !string.Equals(normalizedStatus, "rejected", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported proof decision '{decision.Decision}'. Only approved or rejected are allowed.");
        }

        return decision with
        {
            Decision = normalizedStatus,
            Actor = NormalizeActor(decision.Actor),
            OccurredAtUtc = NormalizeTimestamp(decision.OccurredAtUtc),
            Notes = NormalizeOptionalText(decision.Notes),
        };
    }

    private static string NormalizeRequired(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Proof request is missing {label}.");
        }

        return value.Trim();
    }

    private static string NormalizeSampleJson(string? sampleJson)
    {
        if (string.IsNullOrWhiteSpace(sampleJson))
        {
            return "{}";
        }

        try
        {
            var parsed = JsonNode.Parse(sampleJson);
            if (parsed is null)
            {
                return "{}";
            }

            if (parsed is not JsonObject)
            {
                throw new InvalidOperationException("Proof sample JSON must be a JSON object.");
            }

            return parsed.ToJsonString();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Proof sample JSON could not be parsed: {ex.Message}", ex);
        }
    }

    private static ProofLedgerRecord NormalizeRecord(ProofLedgerRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ProofJobId))
        {
            throw new InvalidOperationException("Proof ledger sync requires a proof job id.");
        }

        if (string.IsNullOrWhiteSpace(record.JobLineageId))
        {
            throw new InvalidOperationException($"Proof job '{record.ProofJobId}' is missing job lineage id.");
        }

        return record with
        {
            Status = NormalizeStatus(record.Status),
            RequestedBy = NormalizeActor(record.RequestedBy),
            RequestedAtUtc = NormalizeTimestamp(record.RequestedAtUtc),
            Notes = NormalizeOptionalText(record.Notes),
            Review = NormalizeReview(record.Review),
        };
    }

    private static ProofReviewSnapshot? NormalizeReview(ProofReviewSnapshot? review)
    {
        if (review is null)
        {
            return null;
        }

        return review with
        {
            Status = NormalizeStatus(review.Status),
            Actor = NormalizeActor(review.Actor),
            OccurredAtUtc = NormalizeTimestamp(review.OccurredAtUtc),
            Notes = NormalizeOptionalText(review.Notes),
        };
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pending" or "approved" or "rejected" or "superseded" => normalized,
            _ => throw new InvalidOperationException($"Unsupported proof status '{status}'."),
        };
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor)
            ? "windows-shell"
            : actor.Trim();
    }

    private static string NormalizeTimestamp(string timestamp)
    {
        if (!DateTimeOffset.TryParse(timestamp, out var parsed))
        {
            throw new InvalidOperationException($"Timestamp '{timestamp}' could not be parsed as UTC proof state.");
        }

        return parsed.ToUniversalTime().ToString("O");
    }

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static ProofLedgerRecord MergeAuthorityRecord(ProofLedgerRecord? existing, ProofLedgerRecord incoming)
    {
        if (existing is null)
        {
            return incoming;
        }

        if (ShouldPreserveExisting(existing, incoming))
        {
            return existing with
            {
                ArtifactPath = incoming.ArtifactPath,
                SubjectSku = incoming.SubjectSku,
                TemplateVersion = incoming.TemplateVersion,
            };
        }

        return incoming;
    }

    private static bool ShouldPreserveExisting(ProofLedgerRecord existing, ProofLedgerRecord incoming)
    {
        if (IsTerminal(existing.Status) && string.Equals(incoming.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsTerminal(existing.Status) || !IsTerminal(incoming.Status))
        {
            return false;
        }

        return CompareRecordTimestamps(existing, incoming) >= 0;
    }

    private static bool ShouldSupersedePriorLineageProofs(
        ProofLedgerRecord? existing,
        ProofLedgerRecord incoming,
        ProofLedgerRecord merged)
    {
        return existing is null &&
            string.Equals(incoming.ProofJobId, merged.ProofJobId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(merged.Status, "pending", StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareRecordTimestamps(ProofLedgerRecord left, ProofLedgerRecord right)
    {
        var leftTimestamp = left.Review?.OccurredAtUtc ?? left.RequestedAtUtc;
        var rightTimestamp = right.Review?.OccurredAtUtc ?? right.RequestedAtUtc;
        return DateTimeOffset.Parse(leftTimestamp).CompareTo(DateTimeOffset.Parse(rightTimestamp));
    }

    private static bool IsTerminal(string status)
    {
        return string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "superseded", StringComparison.OrdinalIgnoreCase);
    }

    private static ProofRecordSummary ToSummary(ProofLedgerRecord record)
    {
        return new ProofRecordSummary(
            record.ProofJobId,
            record.JobLineageId,
            record.Status,
            record.ArtifactPath,
            record.SubjectSku,
            record.TemplateVersion);
    }

    private static string BuildProofJobId()
    {
        return $"PROOF-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string BuildProofSampleJson(ProofRequest request, string proofJobId)
    {
        var bindings = DesignerDraftBindings.CreateBaseBindings(request.TemplateVersion);

        var parsed = JsonNode.Parse(request.SampleJson) as JsonObject;
        if (parsed is not null)
        {
            foreach (var property in parsed)
            {
                if (property.Value is null)
                {
                    continue;
                }

                if (property.Value is JsonValue jsonValue)
                {
                    if (jsonValue.TryGetValue<string>(out var stringValue))
                    {
                        bindings[property.Key] = stringValue ?? string.Empty;
                        continue;
                    }

                    if (jsonValue.TryGetValue<bool>(out var boolValue))
                    {
                        bindings[property.Key] = boolValue ? "true" : "false";
                        continue;
                    }

                    if (jsonValue.TryGetValue<long>(out var longValue))
                    {
                        bindings[property.Key] = longValue.ToString(CultureInfo.InvariantCulture);
                        continue;
                    }

                    if (jsonValue.TryGetValue<double>(out var doubleValue))
                    {
                        bindings[property.Key] = doubleValue.ToString(CultureInfo.InvariantCulture);
                        continue;
                    }
                }

                bindings[property.Key] = property.Value.ToJsonString();
            }
        }

        bindings["proof_mode"] = "native-local-proof";
        bindings["status"] = "Pending native proof review";
        bindings["job"] = proofJobId;
        bindings["job_id"] = proofJobId;
        bindings["brand"] = request.Brand;
        bindings["sku"] = request.SubjectSku;
        bindings["jan"] = request.JanNormalized;
        bindings["qty"] = request.Qty.ToString(CultureInfo.InvariantCulture);

        return JsonSerializer.Serialize(bindings, JsonOptions);
    }

    private static void WriteArtifactAtomically(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(tempPath, bytes);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, path);
    }

    private static void TryDeleteArtifact(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void UpsertRecord(SqliteTransaction transaction, ProofLedgerRecord record)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO proof_records (
                proof_job_id,
                job_lineage_id,
                subject_sku,
                template_version,
                artifact_path,
                status,
                requested_at_utc,
                reviewed_at_utc,
                review_actor,
                notes_json)
            VALUES (
                $proofJobId,
                $jobLineageId,
                $subjectSku,
                $templateVersion,
                $artifactPath,
                $status,
                $requestedAtUtc,
                $reviewedAtUtc,
                $reviewActor,
                $notesJson)
            ON CONFLICT(proof_job_id) DO UPDATE SET
                job_lineage_id = excluded.job_lineage_id,
                subject_sku = excluded.subject_sku,
                template_version = excluded.template_version,
                artifact_path = excluded.artifact_path,
                status = excluded.status,
                requested_at_utc = excluded.requested_at_utc,
                reviewed_at_utc = excluded.reviewed_at_utc,
                review_actor = excluded.review_actor,
                notes_json = excluded.notes_json;
            """;
        command.Parameters.AddWithValue("$proofJobId", record.ProofJobId);
        command.Parameters.AddWithValue("$jobLineageId", record.JobLineageId);
        command.Parameters.AddWithValue("$subjectSku", record.SubjectSku);
        command.Parameters.AddWithValue("$templateVersion", record.TemplateVersion);
        command.Parameters.AddWithValue("$artifactPath", record.ArtifactPath);
        command.Parameters.AddWithValue("$status", record.Status);
        command.Parameters.AddWithValue("$requestedAtUtc", record.RequestedAtUtc);
        command.Parameters.AddWithValue("$reviewedAtUtc", record.Review?.OccurredAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$reviewActor", record.Review?.Actor ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$notesJson", SerializeMetadata(record));
        command.ExecuteNonQuery();
    }

    private static void UpsertDispatchRecord(SqliteTransaction transaction, AuditDispatchRecord dispatch)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO dispatch_records (
                dispatch_job_id,
                job_lineage_id,
                template_version,
                subject_sku,
                artifact_path,
                status,
                submitted_at_utc,
                completed_at_utc,
                adapter_kind,
                detail_json)
            VALUES (
                $dispatchJobId,
                $jobLineageId,
                $templateVersion,
                $subjectSku,
                $artifactPath,
                $status,
                $submittedAtUtc,
                $completedAtUtc,
                $adapterKind,
                $detailJson)
            ON CONFLICT(dispatch_job_id) DO UPDATE SET
                job_lineage_id = excluded.job_lineage_id,
                template_version = excluded.template_version,
                subject_sku = excluded.subject_sku,
                artifact_path = excluded.artifact_path,
                status = excluded.status,
                submitted_at_utc = excluded.submitted_at_utc,
                completed_at_utc = excluded.completed_at_utc,
                adapter_kind = excluded.adapter_kind,
                detail_json = excluded.detail_json;
            """;
        command.Parameters.AddWithValue("$dispatchJobId", dispatch.DispatchJobId);
        command.Parameters.AddWithValue("$jobLineageId", dispatch.JobLineageId);
        command.Parameters.AddWithValue("$templateVersion", dispatch.TemplateVersion);
        command.Parameters.AddWithValue("$subjectSku", dispatch.SubjectSku);
        command.Parameters.AddWithValue("$artifactPath", dispatch.ArtifactPath);
        command.Parameters.AddWithValue("$status", dispatch.EventKind);
        command.Parameters.AddWithValue("$submittedAtUtc", dispatch.OccurredAtUtc);
        command.Parameters.AddWithValue("$completedAtUtc", dispatch.OccurredAtUtc);
        command.Parameters.AddWithValue("$adapterKind", dispatch.AdapterKind);
        command.Parameters.AddWithValue("$detailJson", JsonSerializer.Serialize(dispatch, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void UpsertDispatchAuditEvent(SqliteTransaction transaction, AuditDispatchRecord dispatch)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_events (
                audit_event_id,
                lane,
                subject_key,
                event_kind,
                occurred_at_utc,
                actor,
                detail_json)
            VALUES (
                $auditEventId,
                $lane,
                $subjectKey,
                $eventKind,
                $occurredAtUtc,
                $actor,
                $detailJson)
            ON CONFLICT(audit_event_id) DO UPDATE SET
                lane = excluded.lane,
                subject_key = excluded.subject_key,
                event_kind = excluded.event_kind,
                occurred_at_utc = excluded.occurred_at_utc,
                actor = excluded.actor,
                detail_json = excluded.detail_json;
            """;
        command.Parameters.AddWithValue("$auditEventId", $"dispatch-{dispatch.DispatchJobId}-{dispatch.EventKind}-{dispatch.OccurredAtUtc}");
        command.Parameters.AddWithValue("$lane", dispatch.Lane);
        command.Parameters.AddWithValue("$subjectKey", dispatch.SubjectSku);
        command.Parameters.AddWithValue("$eventKind", dispatch.EventKind);
        command.Parameters.AddWithValue("$occurredAtUtc", dispatch.OccurredAtUtc);
        command.Parameters.AddWithValue("$actor", dispatch.Actor);
        command.Parameters.AddWithValue("$detailJson", JsonSerializer.Serialize(dispatch, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void InsertAuditEvent(SqliteTransaction transaction, ProofLedgerRecord record)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_events (
                audit_event_id,
                lane,
                subject_key,
                event_kind,
                occurred_at_utc,
                actor,
                detail_json)
            VALUES (
                $auditEventId,
                $lane,
                $subjectKey,
                $eventKind,
                $occurredAtUtc,
                $actor,
                $detailJson);
            """;

        var review = record.Review ?? throw new InvalidOperationException("Proof audit event requires a review decision.");
        command.Parameters.AddWithValue("$auditEventId", $"proof-review-{record.ProofJobId}-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("$lane", "proof");
        command.Parameters.AddWithValue("$subjectKey", record.ProofJobId);
        command.Parameters.AddWithValue("$eventKind", $"proof_{review.Status}");
        command.Parameters.AddWithValue("$occurredAtUtc", review.OccurredAtUtc);
        command.Parameters.AddWithValue("$actor", review.Actor);
        command.Parameters.AddWithValue(
            "$detailJson",
            JsonSerializer.Serialize(
                new
                {
                    record.ProofJobId,
                    record.JobLineageId,
                    record.SubjectSku,
                    record.TemplateVersion,
                    record.Status,
                    record.ArtifactPath,
                    review.Notes,
                    source = "windows-shell-local-proof-service",
                },
                JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void SupersedePriorLineageProofs(SqliteTransaction transaction, ProofLedgerRecord record)
    {
        var related = LoadRecordsForLineage(transaction, record.JobLineageId);
        foreach (var existing in related)
        {
            if (string.Equals(existing.ProofJobId, record.ProofJobId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(existing.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var superseded = existing with
            {
                Status = "superseded",
                Review = new ProofReviewSnapshot(
                    "superseded",
                    record.RequestedBy,
                    record.RequestedAtUtc,
                    $"superseded by newer proof {record.ProofJobId}"),
            };
            UpsertRecord(transaction, superseded);
        }
    }

    private static IReadOnlyList<ProofLedgerRecord> LoadRecordsForLineage(SqliteTransaction transaction, string jobLineageId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT proof_job_id,
                   job_lineage_id,
                   subject_sku,
                   template_version,
                   artifact_path,
                   status,
                   requested_at_utc,
                   reviewed_at_utc,
                   review_actor,
                   notes_json
            FROM proof_records
            WHERE job_lineage_id = $jobLineageId;
            """;
        command.Parameters.AddWithValue("$jobLineageId", jobLineageId);

        using var reader = command.ExecuteReader();
        var records = new List<ProofLedgerRecord>();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    private static ProofLedgerRecord? TryLoadRecord(SqliteTransaction transaction, string proofJobId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        ConfigureLoadRecordCommand(command, proofJobId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    private static ProofLedgerRecord? TryLoadRecord(SqliteConnection connection, string proofJobId)
    {
        using var command = connection.CreateCommand();
        ConfigureLoadRecordCommand(command, proofJobId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    private static void ConfigureLoadRecordCommand(SqliteCommand command, string proofJobId)
    {
        command.CommandText =
            """
            SELECT proof_job_id,
                   job_lineage_id,
                   subject_sku,
                   template_version,
                   artifact_path,
                   status,
                   requested_at_utc,
                   reviewed_at_utc,
                   review_actor,
                   notes_json
            FROM proof_records
            WHERE proof_job_id = $proofJobId;
            """;
        command.Parameters.AddWithValue("$proofJobId", proofJobId);
    }

    private static ProofLedgerRecord ReadRecord(SqliteDataReader reader)
    {
        var metadata = DeserializeMetadata(reader.IsDBNull(9) ? "{}" : reader.GetString(9));
        var reviewedAtUtc = reader.IsDBNull(7) ? null : reader.GetString(7);
        var reviewActor = reader.IsDBNull(8) ? null : reader.GetString(8);
        var review = metadata.Review;
        if (review is null && !string.IsNullOrWhiteSpace(reviewedAtUtc) && !string.IsNullOrWhiteSpace(reviewActor))
        {
            review = new ProofReviewSnapshot(
                reader.GetString(5),
                reviewActor,
                reviewedAtUtc,
                null);
        }

        return new ProofLedgerRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(5),
            reader.GetString(4),
            reader.GetString(2),
            reader.GetString(3),
            metadata.RequestedBy ?? "windows-shell",
            reader.GetString(6),
            metadata.RequestNotes,
            review);
    }

    private static string SerializeMetadata(ProofLedgerRecord record)
    {
        return JsonSerializer.Serialize(
            new ProofLedgerMetadata(record.RequestedBy, record.Notes, record.Review),
            JsonOptions);
    }

    private static ProofLedgerMetadata DeserializeMetadata(string notesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ProofLedgerMetadata>(notesJson, JsonOptions)
                ?? new ProofLedgerMetadata(null, null, null);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Local proof ledger metadata could not be parsed: {ex.Message}",
                ex);
        }
    }

    private sealed record ProofLedgerMetadata(
        string? RequestedBy,
        string? RequestNotes,
        ProofReviewSnapshot? Review);
}
