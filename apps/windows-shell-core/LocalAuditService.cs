using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public sealed class LocalAuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly WindowsShellAppPaths _paths;

    public LocalAuditService(WindowsShellAppPaths paths)
    {
        _paths = paths;
    }

    public ValueTask SyncFromAuthorityAsync(
        IReadOnlyList<AuditDispatchRecord> dispatches,
        IReadOnlyList<AuditBackupBundleRecord> bundles,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var dispatch in dispatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeDispatch(dispatch);
            UpsertDispatchRecord(transaction, normalized);
            UpsertAuditEvent(transaction, normalized);
        }

        foreach (var bundle in bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertBundle(transaction, NormalizeBundle(bundle));
        }

        transaction.Commit();
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<AuditMirrorEntry>> LoadEntriesAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedQuery = NormalizeQuery(query);

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        var proofsByDispatchJobId = LoadProofRecords(connection, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT detail_json
            FROM dispatch_records
            ORDER BY submitted_at_utc DESC, dispatch_job_id DESC;
            """;

        using var reader = command.ExecuteReader();
        var entries = new List<AuditMirrorEntry>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatch = DeserializeDispatch(reader.GetString(0));
            proofsByDispatchJobId.TryGetValue(dispatch.DispatchJobId, out var proof);

            var entry = new AuditMirrorEntry(dispatch, proof);
            if (!MatchesEntry(entry, normalizedQuery))
            {
                continue;
            }

            entries.Add(entry);
            if (entries.Count >= normalizedQuery.Limit)
            {
                break;
            }
        }

        return ValueTask.FromResult<IReadOnlyList<AuditMirrorEntry>>(entries);
    }

    public ValueTask<IReadOnlyList<AuditBackupBundleRecord>> LoadBundlesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT detail_json
            FROM backup_bundles
            ORDER BY created_at_utc DESC, bundle_id DESC;
            """;

        using var reader = command.ExecuteReader();
        var bundles = new List<AuditBackupBundleRecord>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            bundles.Add(DeserializeBundle(reader.GetString(0)));
        }

        return ValueTask.FromResult<IReadOnlyList<AuditBackupBundleRecord>>(bundles);
    }

    public ValueTask<AuditSearchResult> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedQuery = NormalizeQuery(query);

        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT audit_event_id,
                   lane,
                   subject_key,
                   event_kind,
                   occurred_at_utc,
                   actor,
                   detail_json
            FROM audit_events
            WHERE ($lane IS NULL OR lane = $lane)
              AND (
                    $searchText IS NULL OR
                    LOWER(subject_key) LIKE $searchPattern OR
                    LOWER(event_kind) LIKE $searchPattern OR
                    LOWER(actor) LIKE $searchPattern OR
                    LOWER(detail_json) LIKE $searchPattern
                  )
            ORDER BY occurred_at_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$lane", normalizedQuery.Lane ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$searchText", normalizedQuery.SearchText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$searchPattern",
            normalizedQuery.SearchText is null ? (object)DBNull.Value : $"%{normalizedQuery.SearchText}%");
        command.Parameters.AddWithValue("$limit", normalizedQuery.Limit);

        using var reader = command.ExecuteReader();
        var events = new List<AuditEventSummary>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var summary = new AuditEventSummary(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));

            if (!MatchesStatus(summary, normalizedQuery.Status))
            {
                continue;
            }

            events.Add(summary);
        }

        return ValueTask.FromResult(new AuditSearchResult(events, events.Count));
    }

    public async ValueTask<AuditExportResult> ExportAsync(AuditExportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var searchResult = await SearchAsync(request.Query, cancellationToken);
        var exportName = string.IsNullOrWhiteSpace(request.ExportName)
            ? $"audit-export-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json"
            : SanitizeExportName(request.ExportName);
        var exportPath = Path.Combine(_paths.ExportsDirectory, exportName);

        var payload = new
        {
            exportedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            query = request.Query,
            events = searchResult.Events,
        };
        File.WriteAllText(exportPath, JsonSerializer.Serialize(payload, JsonOptions));

        return new AuditExportResult(exportPath, searchResult.TotalCount);
    }

    public ValueTask<AuditRetentionResult> ApplyRetentionAsync(AuditRetentionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Native audit retention is not implemented yet.");
    }

    private static AuditDispatchRecord NormalizeDispatch(AuditDispatchRecord dispatch)
    {
        if (string.IsNullOrWhiteSpace(dispatch.DispatchJobId))
        {
            throw new InvalidOperationException("Audit dispatch sync requires a dispatch job id.");
        }

        if (string.IsNullOrWhiteSpace(dispatch.JobLineageId))
        {
            throw new InvalidOperationException($"Audit dispatch '{dispatch.DispatchJobId}' is missing lineage.");
        }

        return dispatch with
        {
            Lane = NormalizeRequired(dispatch.Lane, "lane", dispatch.DispatchJobId),
            EventKind = NormalizeRequired(dispatch.EventKind, "event kind", dispatch.DispatchJobId),
            OccurredAtUtc = NormalizeTimestamp(dispatch.OccurredAtUtc),
            Actor = NormalizeActor(dispatch.Actor),
            TemplateVersion = NormalizeRequired(dispatch.TemplateVersion, "template version", dispatch.DispatchJobId),
            SubjectSku = NormalizeRequired(dispatch.SubjectSku, "subject sku", dispatch.DispatchJobId),
            Brand = NormalizeRequired(dispatch.Brand, "brand", dispatch.DispatchJobId),
            JanNormalized = NormalizeRequired(dispatch.JanNormalized, "normalized JAN", dispatch.DispatchJobId),
            ArtifactPath = string.IsNullOrWhiteSpace(dispatch.ArtifactPath) ? $"dispatch://{dispatch.DispatchJobId}" : dispatch.ArtifactPath.Trim(),
            ArtifactMediaType = string.IsNullOrWhiteSpace(dispatch.ArtifactMediaType) ? "application/octet-stream" : dispatch.ArtifactMediaType.Trim(),
            AdapterKind = string.IsNullOrWhiteSpace(dispatch.AdapterKind) ? "unknown" : dispatch.AdapterKind.Trim(),
            ExternalJobId = string.IsNullOrWhiteSpace(dispatch.ExternalJobId) ? dispatch.DispatchJobId : dispatch.ExternalJobId.Trim(),
            Reason = NormalizeOptional(dispatch.Reason),
            ParentJobId = NormalizeOptional(dispatch.ParentJobId),
        };
    }

    private static AuditBackupBundleRecord NormalizeBundle(AuditBackupBundleRecord bundle)
    {
        if (string.IsNullOrWhiteSpace(bundle.BundleId))
        {
            throw new InvalidOperationException("Audit bundle sync requires a bundle id.");
        }

        return bundle with
        {
            FileName = NormalizeRequired(bundle.FileName, "file name", bundle.BundleId),
            FilePath = NormalizeRequired(bundle.FilePath, "file path", bundle.BundleId),
            CreatedAtUtc = NormalizeTimestamp(bundle.CreatedAtUtc),
            Source = NormalizeRequired(bundle.Source, "source", bundle.BundleId),
        };
    }

    private static AuditQuery NormalizeQuery(AuditQuery query)
    {
        var limit = query.Limit <= 0 ? 200 : Math.Min(query.Limit, 500);
        return query with
        {
            SearchText = NormalizeOptional(query.SearchText)?.ToLowerInvariant(),
            Lane = NormalizeOptional(query.Lane)?.ToLowerInvariant(),
            Status = NormalizeOptional(query.Status)?.ToLowerInvariant(),
            Limit = limit,
        };
    }

    private static Dictionary<string, ProofLedgerRecord> LoadProofRecords(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
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
            FROM proof_records;
            """;

        using var reader = command.ExecuteReader();
        var records = new Dictionary<string, ProofLedgerRecord>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = ReadProofRecord(reader);
            records[record.ProofJobId] = record;
        }

        return records;
    }

    private static string NormalizeRequired(string value, string label, string subject)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Audit record '{subject}' is missing {label}.");
        }

        return value.Trim();
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "windows-shell" : actor.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeTimestamp(string timestamp)
    {
        if (!DateTimeOffset.TryParse(timestamp, out var parsed))
        {
            throw new InvalidOperationException($"Timestamp '{timestamp}' could not be parsed as UTC audit state.");
        }

        return parsed.ToUniversalTime().ToString("O");
    }

    private static bool MatchesStatus(AuditEventSummary summary, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return summary.EventKind.Contains(status, StringComparison.OrdinalIgnoreCase) ||
            summary.DetailJson.Contains($"\"status\":\"{status}\"", StringComparison.OrdinalIgnoreCase) ||
            summary.DetailJson.Contains($"\"status\": \"{status}\"", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesEntry(AuditMirrorEntry entry, AuditQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Lane) &&
            !string.Equals(entry.Dispatch.Lane, query.Lane, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var effectiveStatus = entry.Proof?.Status ?? entry.Dispatch.EventKind;
            if (!effectiveStatus.Contains(query.Status, StringComparison.OrdinalIgnoreCase) &&
                !entry.Dispatch.EventKind.Contains(query.Status, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return MatchesEntrySearch(entry, query.SearchText);
    }

    private static bool MatchesEntrySearch(AuditMirrorEntry entry, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var haystacks = new List<string>
        {
            entry.Dispatch.DispatchJobId,
            entry.Dispatch.JobLineageId,
            entry.Dispatch.Actor,
            entry.Dispatch.Lane,
            entry.Dispatch.TemplateVersion,
            entry.Dispatch.ExternalJobId,
            entry.Dispatch.AdapterKind,
            entry.Dispatch.SubjectSku,
            entry.Dispatch.Brand,
            entry.Dispatch.JanNormalized,
            entry.Dispatch.Qty.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(entry.Dispatch.Reason))
        {
            haystacks.Add(entry.Dispatch.Reason);
        }

        if (entry.Proof is not null)
        {
            haystacks.Add(entry.Proof.Status);
            haystacks.Add(entry.Proof.RequestedBy);
        }

        return haystacks.Any(
            (value) => value.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeExportName(string exportName)
    {
        var trimmed = exportName.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(trimmed.Select((ch) => invalid.Contains(ch) ? '-' : ch).ToArray());
        if (!sanitized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            sanitized += ".json";
        }

        return sanitized;
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

    private static void UpsertAuditEvent(SqliteTransaction transaction, AuditDispatchRecord dispatch)
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
        command.Parameters.AddWithValue("$auditEventId", BuildAuditEventId(dispatch));
        command.Parameters.AddWithValue("$lane", dispatch.Lane);
        command.Parameters.AddWithValue("$subjectKey", dispatch.SubjectSku);
        command.Parameters.AddWithValue("$eventKind", dispatch.EventKind);
        command.Parameters.AddWithValue("$occurredAtUtc", dispatch.OccurredAtUtc);
        command.Parameters.AddWithValue("$actor", dispatch.Actor);
        command.Parameters.AddWithValue("$detailJson", JsonSerializer.Serialize(dispatch, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void UpsertBundle(SqliteTransaction transaction, AuditBackupBundleRecord bundle)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO backup_bundles (
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
                $detailJson)
            ON CONFLICT(bundle_id) DO UPDATE SET
                file_name = excluded.file_name,
                file_path = excluded.file_path,
                created_at_utc = excluded.created_at_utc,
                size_bytes = excluded.size_bytes,
                source = excluded.source,
                detail_json = excluded.detail_json;
            """;
        command.Parameters.AddWithValue("$bundleId", bundle.BundleId);
        command.Parameters.AddWithValue("$fileName", bundle.FileName);
        command.Parameters.AddWithValue("$filePath", bundle.FilePath);
        command.Parameters.AddWithValue("$createdAtUtc", bundle.CreatedAtUtc);
        command.Parameters.AddWithValue("$sizeBytes", bundle.SizeBytes);
        command.Parameters.AddWithValue("$source", bundle.Source);
        command.Parameters.AddWithValue("$detailJson", JsonSerializer.Serialize(bundle, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static string BuildAuditEventId(AuditDispatchRecord dispatch)
    {
        return $"dispatch-{dispatch.DispatchJobId}-{dispatch.EventKind}-{dispatch.OccurredAtUtc}";
    }

    private static AuditDispatchRecord DeserializeDispatch(string detailJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditDispatchRecord>(detailJson, JsonOptions)
                ?? throw new InvalidOperationException("Local dispatch mirror contained an empty record.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Local dispatch mirror detail could not be parsed: {ex.Message}",
                ex);
        }
    }

    private static AuditBackupBundleRecord DeserializeBundle(string detailJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditBackupBundleRecord>(detailJson, JsonOptions)
                ?? throw new InvalidOperationException("Local bundle mirror contained an empty record.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Local bundle mirror detail could not be parsed: {ex.Message}",
                ex);
        }
    }

    private static ProofLedgerRecord ReadProofRecord(SqliteDataReader reader)
    {
        var metadata = DeserializeProofMetadata(reader.IsDBNull(9) ? "{}" : reader.GetString(9));
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

    private static ProofLedgerMetadata DeserializeProofMetadata(string notesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ProofLedgerMetadata>(notesJson, JsonOptions)
                ?? new ProofLedgerMetadata(null, null, null);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Local proof ledger metadata could not be parsed from the audit mirror: {ex.Message}",
                ex);
        }
    }

    private sealed record ProofLedgerMetadata(
        string? RequestedBy,
        string? RequestNotes,
        ProofReviewSnapshot? Review);
}
