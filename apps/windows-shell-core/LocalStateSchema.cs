using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public static class LocalStateSchema
{
    public const string SchemaVersion = "1";

    public static void EnsureCreated(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS schema_info (
                schema_key TEXT PRIMARY KEY,
                schema_value TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS migration_runs (
                migration_id TEXT PRIMARY KEY,
                status TEXT NOT NULL,
                started_at_utc TEXT NOT NULL,
                completed_at_utc TEXT,
                detail_json TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS app_settings (
                setting_key TEXT PRIMARY KEY,
                setting_value TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS template_catalog_entries (
                template_version TEXT PRIMARY KEY,
                label_name TEXT NOT NULL,
                source TEXT NOT NULL,
                manifest_path TEXT NOT NULL,
                content_json TEXT NOT NULL,
                is_default INTEGER NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS designer_documents (
                document_id TEXT PRIMARY KEY,
                template_version TEXT NOT NULL,
                title TEXT NOT NULL,
                content_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS batch_import_sessions (
                session_id TEXT PRIMARY KEY,
                source_name TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS batch_import_rows (
                row_id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                row_index INTEGER NOT NULL,
                sku TEXT NOT NULL,
                jan_raw TEXT NOT NULL,
                jan_normalized TEXT,
                qty INTEGER NOT NULL,
                brand TEXT NOT NULL,
                template_version TEXT NOT NULL,
                status TEXT NOT NULL,
                warning_json TEXT NOT NULL,
                error_json TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS proof_records (
                proof_job_id TEXT PRIMARY KEY,
                job_lineage_id TEXT NOT NULL,
                subject_sku TEXT NOT NULL,
                template_version TEXT NOT NULL,
                artifact_path TEXT NOT NULL,
                status TEXT NOT NULL,
                requested_at_utc TEXT NOT NULL,
                reviewed_at_utc TEXT,
                review_actor TEXT,
                notes_json TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS dispatch_records (
                dispatch_job_id TEXT PRIMARY KEY,
                job_lineage_id TEXT NOT NULL,
                template_version TEXT NOT NULL,
                subject_sku TEXT NOT NULL,
                artifact_path TEXT NOT NULL,
                status TEXT NOT NULL,
                submitted_at_utc TEXT NOT NULL,
                completed_at_utc TEXT,
                adapter_kind TEXT NOT NULL,
                detail_json TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS audit_events (
                audit_event_id TEXT PRIMARY KEY,
                lane TEXT NOT NULL,
                subject_key TEXT NOT NULL,
                event_kind TEXT NOT NULL,
                occurred_at_utc TEXT NOT NULL,
                actor TEXT NOT NULL,
                detail_json TEXT NOT NULL
            );
            """);

        Execute(
            transaction,
            """
            CREATE TABLE IF NOT EXISTS backup_bundles (
                bundle_id TEXT PRIMARY KEY,
                file_name TEXT NOT NULL,
                file_path TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                source TEXT NOT NULL,
                detail_json TEXT NOT NULL
            );
            """);

        UpsertSchemaVersion(transaction);
        transaction.Commit();
    }

    private static void Execute(SqliteTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void UpsertSchemaVersion(SqliteTransaction transaction)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO schema_info (schema_key, schema_value, updated_at_utc)
            VALUES ($key, $value, $updatedAtUtc)
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$key", "local_state_schema_version");
        command.Parameters.AddWithValue("$value", SchemaVersion);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
