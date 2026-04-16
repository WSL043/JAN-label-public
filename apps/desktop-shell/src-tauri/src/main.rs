#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod audit_store;

use std::collections::{HashMap, HashSet};
use std::env;
use std::fs;
use std::io::Read;
use std::path::{Path, PathBuf};

use audit_log::{
    AuditActor, AuditExportRequest, AuditExportResult, AuditLedgerSnapshot, AuditQuery,
    AuditArtifactInfo,
    AuditRetentionRequest, AuditRetentionResult, AuditSearchResult, DispatchMatchSubject,
    PersistedDispatchRecord, ProofRecord, PrintJobId, PrintJobLineageId,
};
use audit_store::{
    AuditBackupRestoreResult as StoreAuditBackupRestoreResult, AuditStore, LegacyProofSeedRecord,
    ProofReviewRequest,
};
use barcode::ZintCli;
use domain::{Jan, JanError};
use print_agent::{DispatchRequest, ExecutionIntent, PrintAgent, PrintAgentPolicy, PrintDispatchResult};
use printer_adapters::{PdfFileAdapter, WindowsSpoolerAdapter};
use render::{
    parse_template_source, render_svg_with_template, resolve_template, resolve_template_with_overlay,
    template_catalog, template_catalog_with_overlay, RenderLabelRequest, TemplateManifest,
    TemplateManifestEntry,
};
use serde::Serialize;
use tauri::command;

const ENV_PRINT_OUTPUT_DIR: &str = "JAN_LABEL_PRINT_OUTPUT_DIR";
const ENV_SPOOL_OUTPUT_DIR: &str = "JAN_LABEL_SPOOL_OUTPUT_DIR";
const ENV_ZINT_BINARY_PATH: &str = "JAN_LABEL_ZINT_BINARY_PATH";
const ENV_PRINT_ADAPTER: &str = "JAN_LABEL_PRINT_ADAPTER";
const ENV_WINDOWS_PRINTER_NAME: &str = "JAN_LABEL_WINDOWS_PRINTER_NAME";
const ENV_ALLOW_PRINT_WITHOUT_PROOF: &str = "JAN_LABEL_ALLOW_PRINT_WITHOUT_PROOF";
const ENV_AUDIT_LOG_DIR: &str = "JAN_LABEL_AUDIT_LOG_DIR";
const ENV_TEMPLATE_OVERLAY_DIR: &str = "JAN_LABEL_TEMPLATE_OVERLAY_DIR";

const DEFAULT_ZINT_BINARY_PATH: &str = "zint";
const DEFAULT_WINDOWS_PRINTER_NAME: &str = "Default Printer";

#[command]
fn dispatch_print_job(request: DispatchRequest) -> Result<PrintDispatchResult, String> {
    let mut request = request;
    let config = PrintBridgeConfig::load();
    config.validate_and_normalize_request(&mut request)?;
    config.ensure_audit_store_ready_for_request(&request)?;
    let is_proof = matches!(request.execution.as_ref(), Some(ExecutionIntent::Proof(_)));
    let job_id = request.job_id.clone();
    let request_for_audit = request.clone();

    if is_proof {
        let proof_output_path = config.proof_output_path(&job_id);
        let barcode_engine = ZintCli {
            binary_path: config.zint_binary_path.clone(),
        };
        let adapter = PdfFileAdapter {
            output_path: proof_output_path.to_string_lossy().into_owned(),
        };
        let agent = PrintAgent::new(adapter, barcode_engine).with_policy(config.agent_policy());
        let result = agent.dispatch(request)?;
        config.record_proof_dispatch(&request_for_audit, &result, &proof_output_path)?;
        return Ok(result);
    }

    let barcode_engine = ZintCli {
        binary_path: config.zint_binary_path.clone(),
    };

    match config.resolve_print_adapter_for_request(&request)? {
        BridgePrintAdapter::Pdf => {
            let adapter = PdfFileAdapter {
                output_path: config
                    .print_output_path(&job_id)
                    .to_string_lossy()
                    .into_owned(),
            };
            let agent = PrintAgent::new(adapter, barcode_engine).with_policy(config.agent_policy());
            let result = agent.dispatch(request)?;
            config.record_dispatch(&request_for_audit, &result)?;
            Ok(result)
        }
        BridgePrintAdapter::WindowsSpooler => {
            let adapter = WindowsSpoolerAdapter {
                printer_name: config.windows_printer_name.clone(),
                spool_path: config
                    .spool_output_path(&job_id)
                    .to_string_lossy()
                    .into_owned(),
            };
            let agent = PrintAgent::new(adapter, barcode_engine).with_policy(config.agent_policy());
            let result = agent.dispatch(request)?;
            config.record_dispatch(&request_for_audit, &result)?;
            Ok(result)
        }
    }
}

#[command]
fn print_bridge_status() -> PrintBridgeStatus {
    PrintBridgeStatus::from_environment()
}

#[command]
fn search_audit_log(query: Option<AuditQuery>) -> Result<AuditSearchResult, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().search(query.unwrap_or_default())
}

#[command]
fn export_audit_ledger(request: Option<AuditExportRequest>) -> Result<AuditExportResult, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().export(request.unwrap_or_default())
}

#[command]
fn trim_audit_ledger(request: AuditRetentionRequest) -> Result<AuditRetentionResult, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().trim(request)
}

#[command]
fn list_audit_backup_bundles() -> Result<Vec<AuditBackupEntry>, String> {
    let config = PrintBridgeConfig::load();
    let backup_dir = config.audit_store().backup_dir();
    let bundles = collect_audit_backup_bundles(&backup_dir)
        .map_err(|error| format!("failed to list audit backups from '{}': {error}", backup_dir.display()))?;
    Ok(bundles)
}

#[command]
fn restore_audit_backup_bundle(
    request: AuditBackupRestoreRequest,
) -> Result<AuditBackupRestoreResult, String> {
    let file_path = request.file_path.trim();
    if file_path.is_empty() {
        return Err("filePath must not be empty".to_string());
    }

    let config = PrintBridgeConfig::load();
    let result = config
        .audit_store()
        .restore_backup_bundle(Path::new(file_path))?;
    Ok(AuditBackupRestoreResult::from_store(result))
}

#[command]
fn approve_proof(request: ProofReviewRequest) -> Result<ProofRecord, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().approve_proof(request)
}

#[command]
fn reject_proof(request: ProofReviewRequest) -> Result<ProofRecord, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().reject_proof(request)
}

#[command]
fn validate_legacy_proof_seed(
    request: LegacyProofSeedRequest,
) -> Result<LegacyProofSeedResult, String> {
    let config = PrintBridgeConfig::load();
    config.process_legacy_proof_seed(request, false)
}

#[command]
fn seed_legacy_proofs(request: LegacyProofSeedRequest) -> Result<LegacyProofSeedResult, String> {
    let config = PrintBridgeConfig::load();
    config.process_legacy_proof_seed(request, true)
}

#[command]
fn template_catalog_command() -> Result<TemplateCatalogResult, String> {
    let local_manifest_path = template_catalog_manifest_path_if_exists();
    let local_versions = local_template_versions(local_manifest_path.as_deref())?;
    let catalog = template_catalog_with_overlay(local_manifest_path.as_deref())
        .map_err(|error| error.to_string())?;
    Ok(TemplateCatalogResult {
        default_template_version: catalog.default_template_version,
        templates: catalog
            .templates
            .into_iter()
            .filter(|entry| entry.enabled)
            .map(|entry| TemplateCatalogEntryResult {
                source: if local_versions.contains(&entry.version) {
                    "local".to_string()
                } else {
                    "packaged".to_string()
                },
                version: entry.version,
                label_name: entry.label_name,
                description: entry.description,
            })
            .collect(),
    })
}

#[command]
fn template_catalog_governance_command() -> Result<TemplateCatalogGovernanceResult, String> {
    inspect_template_catalog_governance()
}

#[command]
fn save_template_to_local_catalog(
    request: TemplateCatalogWritebackRequest,
) -> Result<TemplateCatalogWritebackResult, String> {
    let template_source = request.template_source.trim();
    if template_source.is_empty() {
        return Err("templateSource must not be empty".to_string());
    }

    let template =
        parse_template_source(template_source, "<template-source>").map_err(|error| error.to_string())?;
    if template.template_version.trim().is_empty() {
        return Err("template_source.template_version is required".to_string());
    }

    let template_version = template.template_version.clone();
    let overlay_dir = template_overlay_dir();
    fs::create_dir_all(&overlay_dir).map_err(|error| {
        format!(
            "failed to create template overlay directory '{}': {error}",
            overlay_dir.display()
        )
    })?;

    let template_filename = format!("{}.json", sanitize_template_filename(&template_version));
    let template_path = overlay_dir.join(&template_filename);
    fs::write(&template_path, template_source).map_err(|error| {
        format!(
            "failed to write template source '{}': {error}",
            template_path.display()
        )
    })?;

    let mut manifest = load_or_initialize_template_manifest()?;
    let action = if manifest
        .templates
        .iter()
        .any(|entry| entry.version == template_version)
    {
        "updated"
    } else {
        "created"
    };

    manifest.templates.retain(|entry| entry.version != template_version);
    manifest.templates.push(TemplateManifestEntry {
        version: template_version.clone(),
        path: template_filename,
        label_name: request.label_name.or(template.label_name).unwrap_or_else(|| {
            format!("local-{template_version}")
        }),
        enabled: request.enabled,
        description: request.description.or(template.description),
    });
    if request.set_as_default {
        manifest.default_template_version = template_version.clone();
    }

    let manifest_path = template_catalog_manifest_path();
    let manifest_payload = serde_json::json!({
        "schema_version": manifest.schema_version,
        "default_template_version": manifest.default_template_version,
        "templates": manifest
            .templates
            .iter()
            .map(
                |entry| serde_json::json!({
                    "version": entry.version,
                    "path": entry.path,
                    "label_name": entry.label_name,
                    "enabled": entry.enabled,
                    "description": entry.description,
                })
            )
            .collect::<Vec<_>>()
    });
    let manifest_text = serde_json::to_string_pretty(&manifest_payload).map_err(|error| {
        format!(
            "failed to serialize template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;
    fs::write(&manifest_path, manifest_text).map_err(|error| {
        format!(
            "failed to write template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;

    let catalog = template_catalog_with_overlay(Some(&manifest_path))
        .map_err(|error| error.to_string())?;
    let save_message = format!(
        "Template {} {} in desktop local catalog.",
        template_version,
        if action == "created" {
            "created"
        } else {
            "updated"
        }
    );
    Ok(TemplateCatalogWritebackResult {
        template_version,
        status: action.to_string(),
        message: save_message,
        template_path: template_path.to_string_lossy().into_owned(),
        manifest_path: manifest_path.to_string_lossy().into_owned(),
        default_template_version: catalog.default_template_version,
        action: action.to_string(),
    })
}

#[command]
fn preview_template_draft(
    request: TemplateDraftPreviewRequest,
) -> Result<TemplateDraftPreviewResult, String> {
    let template_source = request.template_source.trim();
    if template_source.is_empty() {
        return Err("templateSource must not be empty".to_string());
    }
    let template =
        parse_template_source(template_source, "<template-draft>").map_err(|error| error.to_string())?;
    let job_id = require_non_empty_preview_field("sample.jobId", &request.sample.job_id)?;
    let sku = require_non_empty_preview_field("sample.sku", &request.sample.sku)?;
    let brand = require_non_empty_preview_field("sample.brand", &request.sample.brand)?;
    let jan = Jan::parse(&request.sample.jan).map_err(jan_error_message)?;
    if request.sample.qty == 0 {
        return Err("sample.qty must be greater than or equal to 1".to_string());
    }

    let render_request = RenderLabelRequest {
        job_id,
        sku,
        brand,
        jan: jan.as_str().to_string(),
        qty: request.sample.qty,
        template_version: template.template_version.clone(),
    };
    let svg = render_svg_with_template(&render_request, &template);

    Ok(TemplateDraftPreviewResult {
        svg,
        normalized_jan: jan.as_str().to_string(),
        template_version: template.template_version,
        label_name: template.label_name.unwrap_or_else(|| "unnamed-template".to_string()),
        page_width_mm: template.page.width_mm,
        page_height_mm: template.page.height_mm,
        field_count: template.fields.len(),
    })
}

#[derive(Debug, Clone)]
struct PrintBridgeConfig {
    print_output_dir: PathBuf,
    spool_output_dir: PathBuf,
    audit_log_dir: PathBuf,
    zint_binary_path: String,
    print_adapter: BridgePrintAdapter,
    windows_printer_name: String,
    allow_print_without_proof: bool,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct PrintBridgeStatus {
    available_adapters: Vec<String>,
    resolved_zint_path: String,
    proof_output_dir: String,
    print_output_dir: String,
    spool_output_dir: String,
    audit_log_dir: String,
    audit_backup_dir: String,
    print_adapter_kind: String,
    windows_printer_name: String,
    allow_without_proof_enabled: bool,
    warning_details: Vec<BridgeWarning>,
    warnings: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BridgeWarning {
    code: &'static str,
    severity: BridgeWarningSeverity,
    message: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "lowercase")]
enum BridgeWarningSeverity {
    Info,
    Warning,
    Error,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct LegacyProofSeedRequest {
    rows: Vec<LegacyProofSeedRowInput>,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct LegacyProofSeedRowInput {
    proof_job_id: String,
    artifact_path: String,
    template_version: String,
    match_subject: LegacyProofSeedMatchSubjectInput,
    requested_by: AuditActor,
    requested_at: String,
    #[serde(default)]
    job_lineage_id: Option<String>,
    #[serde(default)]
    notes: Option<String>,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct LegacyProofSeedMatchSubjectInput {
    sku: String,
    brand: String,
    jan: String,
    qty: u32,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct LegacyProofSeedResult {
    applied: bool,
    seeded_count: usize,
    message: String,
    rows: Vec<LegacyProofSeedRowResult>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct LegacyProofSeedRowResult {
    row_index: usize,
    proof_job_id: String,
    status: LegacyProofSeedRowStatus,
    message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    normalized_jan: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    resolved_job_lineage_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    artifact_path: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "lowercase")]
enum LegacyProofSeedRowStatus {
    Ok,
    Error,
}

#[derive(Debug, Clone)]
struct ValidatedLegacyProofSeedRow {
    record: Option<LegacyProofSeedRecord>,
    result: LegacyProofSeedRowResult,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct TemplateDraftPreviewRequest {
    template_source: String,
    sample: TemplateDraftPreviewSample,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct TemplateDraftPreviewSample {
    job_id: String,
    sku: String,
    brand: String,
    jan: String,
    qty: u32,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateDraftPreviewResult {
    svg: String,
    normalized_jan: String,
    template_version: String,
    label_name: String,
    page_width_mm: f64,
    page_height_mm: f64,
    field_count: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogResult {
    default_template_version: String,
    templates: Vec<TemplateCatalogEntryResult>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogEntryResult {
    source: String,
    version: String,
    label_name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    description: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogGovernanceResult {
    manifest_status: String,
    overlay_directory_path: String,
    manifest_path: String,
    manifest_exists: bool,
    effective_default_template_version: String,
    effective_default_source: String,
    local_entry_count: usize,
    overlay_json_file_count: usize,
    local_entries: Vec<TemplateCatalogGovernanceEntryResult>,
    issues: Vec<TemplateCatalogGovernanceIssueResult>,
    backup_guidance: Vec<String>,
    repair_guidance: Vec<String>,
    single_writer_guidance: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogGovernanceEntryResult {
    version: String,
    label_name: String,
    path: String,
    resolved_path: String,
    enabled: bool,
    file_exists: bool,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogGovernanceIssueResult {
    severity: String,
    code: String,
    message: String,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogWritebackRequest {
    template_source: String,
    #[serde(default = "template_entry_enabled_default")]
    enabled: bool,
    #[serde(default)]
    set_as_default: bool,
    #[serde(default)]
    label_name: Option<String>,
    #[serde(default)]
    description: Option<String>,
}

fn template_entry_enabled_default() -> bool {
    true
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TemplateCatalogWritebackResult {
    template_version: String,
    status: String,
    message: String,
    template_path: String,
    manifest_path: String,
    default_template_version: String,
    action: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct AuditBackupEntry {
    file_name: String,
    file_path: String,
    created_at_utc: String,
    size_bytes: u64,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct AuditBackupRestoreRequest {
    file_path: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct AuditBackupRestoreResult {
    restored_dispatch_count: usize,
    restored_proof_count: usize,
    bundle: AuditArtifactInfo,
    message: String,
}

#[derive(Debug, Clone, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct AuditBundleEnvelope {
    artifact: AuditArtifactInfo,
}

impl AuditBackupRestoreResult {
    fn from_store(result: StoreAuditBackupRestoreResult) -> Self {
        let restored_dispatch_count = result.restored_dispatch_count;
        let restored_proof_count = result.restored_proof_count;
        let bundle = result.bundle;
        let message = format!(
            "Restored {} dispatch entr{} and {} proof entr{} from {}.",
            restored_dispatch_count,
            if restored_dispatch_count == 1 { "y" } else { "ies" },
            restored_proof_count,
            if restored_proof_count == 1 { "y" } else { "ies" },
            bundle.file_name
        );
        Self {
            restored_dispatch_count,
            restored_proof_count,
            bundle,
            message,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum BridgePrintAdapter {
    Pdf,
    WindowsSpooler,
}

const BRIDGE_PRINT_ADAPTER_UNSUPPORTED: &str = "BRIDGE_PRINT_ADAPTER_UNSUPPORTED";
const BRIDGE_PRINT_ADAPTER_UNAVAILABLE_ON_HOST: &str =
    "BRIDGE_PRINT_ADAPTER_UNAVAILABLE_ON_HOST";
const BRIDGE_PRINT_ADAPTER_DEFAULTED: &str = "BRIDGE_PRINT_ADAPTER_DEFAULTED";
const BRIDGE_ZINT_ABSOLUTE_PATH_MISSING: &str = "BRIDGE_ZINT_ABSOLUTE_PATH_MISSING";
const BRIDGE_ZINT_DEFAULTED: &str = "BRIDGE_ZINT_DEFAULTED";
const BRIDGE_OUTPUT_DIR_CONFIGURED_MISSING: &str = "BRIDGE_OUTPUT_DIR_CONFIGURED_MISSING";
const BRIDGE_OUTPUT_DIR_DEFAULTED: &str = "BRIDGE_OUTPUT_DIR_DEFAULTED";
const BRIDGE_OUTPUT_DIR_DEFAULTED_MISSING_WILL_CREATE: &str =
    "BRIDGE_OUTPUT_DIR_DEFAULTED_MISSING_WILL_CREATE";
const BRIDGE_WINDOWS_PRINTER_DEFAULTED_WHILE_SPOOLER: &str =
    "BRIDGE_WINDOWS_PRINTER_DEFAULTED_WHILE_SPOOLER";
const BRIDGE_ALLOW_WITHOUT_PROOF_IGNORED: &str = "BRIDGE_ALLOW_WITHOUT_PROOF_IGNORED";

impl BridgePrintAdapter {
    fn kind(self) -> &'static str {
        match self {
            Self::Pdf => "pdf",
            Self::WindowsSpooler => "windows-spooler",
        }
    }
}

impl BridgeWarning {
    fn info(code: &'static str, message: impl Into<String>) -> Self {
        Self {
            code,
            severity: BridgeWarningSeverity::Info,
            message: message.into(),
        }
    }

    fn warning(code: &'static str, message: impl Into<String>) -> Self {
        Self {
            code,
            severity: BridgeWarningSeverity::Warning,
            message: message.into(),
        }
    }

    fn error(code: &'static str, message: impl Into<String>) -> Self {
        Self {
            code,
            severity: BridgeWarningSeverity::Error,
            message: message.into(),
        }
    }
}

impl PrintBridgeConfig {
    fn load() -> Self {
        let print_adapter_raw = resolve_optional_env(ENV_PRINT_ADAPTER).unwrap_or_default();

        Self {
            print_output_dir: resolve_output_dir(
                ENV_PRINT_OUTPUT_DIR,
                env::temp_dir().join("jan-label").join("proofs"),
            ),
            spool_output_dir: resolve_output_dir(
                ENV_SPOOL_OUTPUT_DIR,
                env::temp_dir().join("jan-label").join("spool"),
            ),
            audit_log_dir: resolve_output_dir(
                ENV_AUDIT_LOG_DIR,
                env::temp_dir().join("jan-label").join("audit"),
            ),
            zint_binary_path: resolve_optional_env(ENV_ZINT_BINARY_PATH)
                .filter(|value| !value.trim().is_empty())
                .unwrap_or_else(|| DEFAULT_ZINT_BINARY_PATH.to_string()),
            print_adapter: resolve_print_adapter(&print_adapter_raw),
            windows_printer_name: resolve_optional_env(ENV_WINDOWS_PRINTER_NAME)
                .filter(|value| !value.trim().is_empty())
                .unwrap_or_else(|| DEFAULT_WINDOWS_PRINTER_NAME.to_string()),
            allow_print_without_proof: false,
        }
    }

    fn agent_policy(&self) -> PrintAgentPolicy {
        PrintAgentPolicy {
            allow_print_without_proof: self.allow_print_without_proof,
            local_template_manifest_path: template_catalog_manifest_path_if_exists(),
        }
    }

    fn audit_store(&self) -> AuditStore {
        AuditStore::new(self.audit_log_dir.clone())
    }

    fn process_legacy_proof_seed(
        &self,
        request: LegacyProofSeedRequest,
        apply: bool,
    ) -> Result<LegacyProofSeedResult, String> {
        if request.rows.is_empty() {
            return Ok(LegacyProofSeedResult {
                applied: false,
                seeded_count: 0,
                message: "No legacy proof rows were provided.".to_string(),
                rows: Vec::new(),
            });
        }

        let snapshot = self.audit_store().snapshot()?;
        let validated_rows = validate_legacy_proof_seed_rows(self, request.rows, &snapshot);
        let has_errors = validated_rows
            .iter()
            .any(|entry| entry.result.status == LegacyProofSeedRowStatus::Error);
        let records = validated_rows
            .iter()
            .map(|entry| entry.record.clone())
            .collect::<Option<Vec<_>>>();
        let rows = validated_rows
            .into_iter()
            .map(|entry| entry.result)
            .collect::<Vec<_>>();

        if has_errors {
            return Ok(LegacyProofSeedResult {
                applied: false,
                seeded_count: 0,
                message: "Legacy proof seed validation failed. Fix row errors before seeding."
                    .to_string(),
                rows,
            });
        }

        if !apply {
            return Ok(LegacyProofSeedResult {
                applied: false,
                seeded_count: 0,
                message: format!("Validated {} legacy proof rows.", rows.len()),
                rows,
            });
        }

        let records = records.unwrap_or_default();
        self.audit_store().seed_legacy_proofs(records)?;
        Ok(LegacyProofSeedResult {
            applied: true,
            seeded_count: rows.len(),
            message: format!("Seeded {} legacy proof rows as pending review.", rows.len()),
            rows,
        })
    }

    fn proof_output_path(&self, job_id: &str) -> PathBuf {
        self.print_output_dir
            .join(format!("{}-proof.pdf", sanitize_path_component(job_id)))
    }

    fn print_output_path(&self, job_id: &str) -> PathBuf {
        self.print_output_dir
            .join(format!("{}-print.pdf", sanitize_path_component(job_id)))
    }

    fn spool_output_path(&self, job_id: &str) -> PathBuf {
        self.spool_output_dir
            .join(format!("{}-print.svg", sanitize_path_component(job_id)))
    }

    fn resolve_print_adapter_for_request(
        &self,
        request: &DispatchRequest,
    ) -> Result<BridgePrintAdapter, String> {
        let Some(printer_profile) = request.printer_profile.as_ref() else {
            return Ok(self.print_adapter);
        };

        match printer_profile.adapter.trim().to_ascii_lowercase().as_str() {
            "pdf" => Ok(BridgePrintAdapter::Pdf),
            "spool" | "spooler" | "windows-spooler" | "windows" => {
                if host_supports_windows_spooler() {
                    Ok(BridgePrintAdapter::WindowsSpooler)
                } else {
                    Err(format!(
                        "printer profile '{}' requires windows-spooler, but this host only supports pdf",
                        printer_profile.id
                    ))
                }
            }
            other => Err(format!(
                "printer profile '{}' requests unsupported adapter '{}'",
                printer_profile.id, other
            )),
        }
    }

    fn validate_and_normalize_request(&self, request: &mut DispatchRequest) -> Result<(), String> {
        let Some(ExecutionIntent::Print(print)) = request.execution.as_ref() else {
            ensure_template_version_available(&resolve_template_version_for_request(request)?)?;
            return Ok(());
        };

        if print.allow_without_proof && !self.allow_print_without_proof {
            return Err(format!(
                "{ENV_ALLOW_PRINT_WITHOUT_PROOF} is not enabled; print execution cannot bypass proof"
            ));
        }

        if print.allow_without_proof {
            ensure_template_version_available(&resolve_template_version_for_request(request)?)?;
            return Ok(());
        }

        let source_proof_job_id = print
            .source_proof_job_id
            .as_deref()
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .ok_or_else(|| {
                "execution.sourceProofJobId is required when source proof gate is enforced"
                    .to_string()
            })?;
        let expected_template_version = resolve_template_version_for_request(request)?;
        ensure_template_version_available(&expected_template_version)?;
        let expected_subject = build_dispatch_match_subject(request)?;
        let approved_proof = self.audit_store().ensure_approved_proof_matches(
            source_proof_job_id,
            &expected_template_version,
            &expected_subject,
        )?;
        validate_existing_proof_artifact_path(self, source_proof_job_id, &approved_proof.artifact_path)?;

        let expected_lineage = approved_proof.job_lineage_id.0.clone();
        if let Some(explicit_lineage) = optional_trimmed_non_empty(request.job_lineage_id.clone()) {
            if explicit_lineage != expected_lineage {
                return Err(format!(
                    "request lineage '{}' does not match approved proof lineage '{}'",
                    explicit_lineage, expected_lineage
                ));
            }
        } else if let Some(reprint_parent_job_id) =
            optional_trimmed_non_empty(request.reprint_of_job_id.clone())
        {
            if reprint_parent_job_id != expected_lineage {
                return Err(format!(
                    "reprintOfJobId '{}' does not match approved proof lineage '{}'",
                    reprint_parent_job_id, expected_lineage
                ));
            }
        } else {
            request.job_lineage_id = Some(expected_lineage);
        }

        Ok(())
    }

    fn ensure_audit_store_ready_for_request(&self, request: &DispatchRequest) -> Result<(), String> {
        let audit_store = self.audit_store();
        if matches!(request.execution.as_ref(), Some(ExecutionIntent::Proof(_))) {
            audit_store.assert_dispatch_and_proof_ledgers_writable()
        } else {
            audit_store.assert_dispatch_ledger_writable()
        }
    }

    fn record_dispatch(
        &self,
        request: &DispatchRequest,
        result: &PrintDispatchResult,
    ) -> Result<(), String> {
        let dispatch = self.build_dispatch_record(request, result)?;
        self.audit_store().record_dispatch(dispatch)
    }

    fn record_proof_dispatch(
        &self,
        request: &DispatchRequest,
        result: &PrintDispatchResult,
        artifact_path: &Path,
    ) -> Result<(), String> {
        let dispatch = self.build_dispatch_record(request, result)?;
        let proof = self
            .build_pending_proof_record(request, artifact_path)
            .ok_or_else(|| "proof dispatch requires proof execution metadata".to_string())?;
        self.audit_store()
            .record_proof_dispatch_transaction(dispatch, proof)
    }

    fn build_dispatch_record(
        &self,
        request: &DispatchRequest,
        result: &PrintDispatchResult,
    ) -> Result<PersistedDispatchRecord, String> {
        let reason = request
            .reason
            .as_deref()
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .map(str::to_string)
            .or_else(|| result.audit.reason.clone());
        Ok(PersistedDispatchRecord {
            audit: audit_log::PrintAuditRecord {
                job_id: PrintJobId(result.audit.job_id.clone()),
                job_lineage_id: PrintJobLineageId(result.audit.job_lineage_id.clone()),
                parent_job_id: result.audit.parent_job_id.clone().map(PrintJobId),
                actor: AuditActor {
                    user_id: request.actor_user_id.clone(),
                    display_name: request.actor_display_name.clone(),
                },
                event: map_event_kind(&result.audit.event),
                occurred_at: result.audit.occurred_at.clone(),
                reason,
            },
            mode: result.mode.clone(),
            template_version: result.template_version.clone(),
            match_subject: build_dispatch_match_subject(request)?,
            artifact_media_type: result.artifact.media_type.clone(),
            artifact_byte_size: result.artifact.byte_size,
            submission_adapter_kind: result.submission.adapter_kind.clone(),
            submission_external_job_id: result.submission.external_job_id.clone(),
        })
    }

    fn build_pending_proof_record(
        &self,
        request: &DispatchRequest,
        artifact_path: &Path,
    ) -> Option<ProofRecord> {
        let ExecutionIntent::Proof(execution) = request.execution.as_ref()? else {
            return None;
        };

        let actor = AuditActor {
            user_id: execution
                .requested_by
                .clone()
                .unwrap_or_else(|| request.actor_user_id.clone()),
            display_name: request.actor_display_name.clone(),
        };
        let job_lineage_id = request
            .job_lineage_id
            .clone()
            .or_else(|| request.reprint_of_job_id.clone())
            .unwrap_or_else(|| request.job_id.clone());

        Some(ProofRecord::pending(
            PrintJobId(request.job_id.clone()),
            PrintJobLineageId(job_lineage_id),
            actor,
            request.requested_at.clone(),
            artifact_path.to_string_lossy().into_owned(),
            execution.notes.clone(),
        ))
    }
}

impl PrintBridgeStatus {
    fn from_environment() -> Self {
        let host_supports_windows_spooler = host_supports_windows_spooler();
        let mut warning_details = Vec::new();

        let (print_output_dir, print_output_fallback_warning) = resolve_output_dir_with_warning(
            ENV_PRINT_OUTPUT_DIR,
            env::temp_dir().join("jan-label").join("proofs"),
        );
        report_output_dir_warning(
            ENV_PRINT_OUTPUT_DIR,
            &print_output_dir,
            print_output_fallback_warning.is_some(),
            &mut warning_details,
        );
        if let Some(warning) = print_output_fallback_warning {
            warning_details.push(warning);
        }

        let (spool_output_dir, spool_output_fallback_warning) = resolve_output_dir_with_warning(
            ENV_SPOOL_OUTPUT_DIR,
            env::temp_dir().join("jan-label").join("spool"),
        );
        report_output_dir_warning(
            ENV_SPOOL_OUTPUT_DIR,
            &spool_output_dir,
            spool_output_fallback_warning.is_some(),
            &mut warning_details,
        );
        if let Some(warning) = spool_output_fallback_warning {
            warning_details.push(warning);
        }

        let (audit_log_dir, audit_log_fallback_warning) = resolve_output_dir_with_warning(
            ENV_AUDIT_LOG_DIR,
            env::temp_dir().join("jan-label").join("audit"),
        );
        report_output_dir_warning(
            ENV_AUDIT_LOG_DIR,
            &audit_log_dir,
            audit_log_fallback_warning.is_some(),
            &mut warning_details,
        );
        if let Some(warning) = audit_log_fallback_warning {
            warning_details.push(warning);
        }
        let audit_backup_dir = AuditStore::new(audit_log_dir.clone()).backup_dir();

        let (zint_binary_path, zint_warning) =
            resolve_zint_binary_path_with_warning(ENV_ZINT_BINARY_PATH, DEFAULT_ZINT_BINARY_PATH);
        report_zint_warning(&zint_binary_path, &mut warning_details);
        if let Some(warning) = zint_warning {
            warning_details.push(warning);
        }

        let print_adapter_raw = resolve_optional_env(ENV_PRINT_ADAPTER).unwrap_or_default();
        let (print_adapter, adapter_warning) = resolve_print_adapter_with_warning_for_host(
            &print_adapter_raw,
            host_supports_windows_spooler,
        );
        if let Some(warning) = adapter_warning {
            warning_details.push(warning);
        }

        let (windows_printer_name, printer_warning) = resolve_windows_printer_name_with_warning(
            ENV_WINDOWS_PRINTER_NAME,
            DEFAULT_WINDOWS_PRINTER_NAME,
        );
        let is_fallback_printer_name = printer_warning.is_some();
        let allow_print_without_proof_requested = resolve_bool_env(ENV_ALLOW_PRINT_WITHOUT_PROOF);
        if print_adapter == BridgePrintAdapter::WindowsSpooler {
            if let Some(warning) = printer_warning {
                warning_details.push(warning);
            }
            if is_fallback_printer_name {
                warning_details.push(BridgeWarning::error(
                    BRIDGE_WINDOWS_PRINTER_DEFAULTED_WHILE_SPOOLER,
                    "JAN_LABEL_WINDOWS_PRINTER_NAME is not set or empty; using fallback 'Default Printer' while windows-spooler is selected",
                ));
            }
        }
        if allow_print_without_proof_requested {
            warning_details.push(BridgeWarning::warning(
                BRIDGE_ALLOW_WITHOUT_PROOF_IGNORED,
                "JAN_LABEL_ALLOW_PRINT_WITHOUT_PROOF is set, but allowWithoutProof stays disabled until proof approval workflow is implemented",
            ));
        }
        let warnings = warning_details
            .iter()
            .map(|warning| warning.message.clone())
            .collect::<Vec<_>>();

        Self {
            available_adapters: available_adapters_for_host(host_supports_windows_spooler),
            resolved_zint_path: zint_binary_path,
            proof_output_dir: print_output_dir.to_string_lossy().into_owned(),
            print_output_dir: print_output_dir.to_string_lossy().into_owned(),
            spool_output_dir: spool_output_dir.to_string_lossy().into_owned(),
            audit_log_dir: audit_log_dir.to_string_lossy().into_owned(),
            audit_backup_dir: audit_backup_dir.to_string_lossy().into_owned(),
            print_adapter_kind: print_adapter.kind().to_string(),
            windows_printer_name,
            allow_without_proof_enabled: false,
            warning_details,
            warnings,
        }
    }
}

fn host_supports_windows_spooler() -> bool {
    cfg!(windows)
}

fn default_print_adapter() -> BridgePrintAdapter {
    BridgePrintAdapter::Pdf
}

fn available_adapters_for_host(host_supports_windows_spooler: bool) -> Vec<String> {
    let mut adapters = vec![BridgePrintAdapter::Pdf.kind().to_string()];
    if host_supports_windows_spooler {
        adapters.push(BridgePrintAdapter::WindowsSpooler.kind().to_string());
    }
    adapters
}

fn resolve_print_adapter(raw: &str) -> BridgePrintAdapter {
    resolve_print_adapter_for_host(raw, host_supports_windows_spooler())
}

fn resolve_print_adapter_for_host(
    raw: &str,
    host_supports_windows_spooler: bool,
) -> BridgePrintAdapter {
    resolve_print_adapter_with_warning_for_host(raw, host_supports_windows_spooler).0
}

fn resolve_print_adapter_with_warning_for_host(
    raw: &str,
    host_supports_windows_spooler: bool,
) -> (BridgePrintAdapter, Option<BridgeWarning>) {
    let normalized = raw.trim().to_ascii_lowercase();
    let default_adapter = default_print_adapter();
    let default_kind = default_adapter.kind();

    if normalized.is_empty() {
        return (
            default_adapter,
            Some(BridgeWarning::info(
                BRIDGE_PRINT_ADAPTER_DEFAULTED,
                format!("{ENV_PRINT_ADAPTER} is not set; defaulting to {default_kind}"),
            )),
        );
    }

    match normalized.as_str() {
        "pdf" => (BridgePrintAdapter::Pdf, None),
        "spool" | "spooler" | "windows-spooler" | "windows" => {
            if host_supports_windows_spooler {
                (BridgePrintAdapter::WindowsSpooler, None)
            } else {
                (
                    default_adapter,
                    Some(BridgeWarning::error(
                        BRIDGE_PRINT_ADAPTER_UNAVAILABLE_ON_HOST,
                        format!(
                            "{ENV_PRINT_ADAPTER}='{raw}' is not available on this host; defaulting to {default_kind}"
                        ),
                    )),
                )
            }
        }
        _ => (
            default_adapter,
            Some(BridgeWarning::error(
                BRIDGE_PRINT_ADAPTER_UNSUPPORTED,
                format!("{ENV_PRINT_ADAPTER}='{raw}' is unsupported; defaulting to {default_kind}"),
            )),
        ),
    }
}

fn resolve_output_dir(env_key: &str, fallback: PathBuf) -> PathBuf {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => PathBuf::from(value),
        _ => fallback,
    }
}

fn resolve_output_dir_with_warning(
    env_key: &str,
    fallback: PathBuf,
) -> (PathBuf, Option<BridgeWarning>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (PathBuf::from(value), None),
        _ => (
            fallback.clone(),
            Some(BridgeWarning::info(
                BRIDGE_OUTPUT_DIR_DEFAULTED,
                format!("{env_key} is not set; defaulting to {}", fallback.display()),
            )),
        ),
    }
}

fn template_overlay_dir() -> PathBuf {
    resolve_output_dir(
        ENV_TEMPLATE_OVERLAY_DIR,
        env::temp_dir().join("jan-label").join("template-overlays"),
    )
}

fn template_catalog_manifest_path() -> PathBuf {
    template_overlay_dir().join("template-manifest.json")
}

fn template_catalog_manifest_path_if_exists() -> Option<PathBuf> {
    let manifest_path = template_catalog_manifest_path();
    if manifest_path.exists() {
        Some(manifest_path)
    } else {
        None
    }
}

fn collect_audit_backup_bundles(backup_dir: &Path) -> Result<Vec<AuditBackupEntry>, String> {
    if !backup_dir.exists() {
        return Ok(Vec::new());
    }

    let mut bundles = fs::read_dir(backup_dir)
        .map_err(|error| {
            format!(
                "failed to read audit backup directory '{}': {error}",
                backup_dir.display()
            )
        })?
        .filter_map(|entry| {
            let entry = entry.ok()?;
            if !entry
                .file_type()
                .ok()?
                .is_file()
            {
                return None;
            }

            let path = entry.path();
            if path.extension().and_then(|ext| ext.to_str()) != Some("json") {
                return None;
            }

            let metadata = fs::metadata(&path).ok()?;
            let mut artifact = read_audit_bundle_artifact(&path).unwrap_or_else(|| AuditArtifactInfo {
                file_name: entry
                    .file_name()
                    .to_string_lossy()
                    .into_owned(),
                file_path: path.to_string_lossy().into_owned(),
                created_at_utc: String::new(),
                size_bytes: metadata.len(),
            });
            artifact.file_path = path.to_string_lossy().into_owned();
            artifact.size_bytes = metadata.len();
            Some(AuditBackupEntry {
                file_name: artifact.file_name,
                file_path: artifact.file_path,
                created_at_utc: artifact.created_at_utc,
                size_bytes: artifact.size_bytes,
            })
        })
        .collect::<Vec<_>>();

    bundles.sort_by(|a, b| {
        b.created_at_utc
            .cmp(&a.created_at_utc)
            .then_with(|| b.file_name.cmp(&a.file_name))
    });
    Ok(bundles)
}

fn read_audit_bundle_artifact(path: &Path) -> Option<AuditArtifactInfo> {
    let raw = fs::read_to_string(path).ok()?;
    let envelope: AuditBundleEnvelope = serde_json::from_str(&raw).ok()?;
    Some(envelope.artifact)
}

fn sanitize_template_filename(raw: &str) -> String {
    let sanitized = sanitize_template_version_token(raw);
    if sanitized.is_empty() {
        "template".to_string()
    } else {
        sanitized
    }
}

fn sanitize_template_version_token(raw: &str) -> String {
    raw.chars()
        .map(|character| match character {
            '/' | '\\' | '\0' => '_',
            '<' | '>' | ':' | '"' | '|' | '?' | '*' => '_',
            c if c.is_control() => '_',
            c => c,
        })
        .collect::<String>()
}

fn load_or_initialize_template_manifest() -> Result<TemplateManifest, String> {
    let packaged = template_catalog().map_err(|error| error.to_string())?;
    let manifest_path = template_catalog_manifest_path();
    if !manifest_path.exists() {
        return Ok(TemplateManifest {
            schema_version: packaged.schema_version,
            default_template_version: packaged.default_template_version,
            templates: Vec::new(),
        });
    }

    let manifest_contents = fs::read_to_string(&manifest_path).map_err(|error| {
        format!(
            "failed to read template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;
    let mut manifest = serde_json::from_str::<TemplateManifest>(&manifest_contents).map_err(|error| {
        format!(
            "failed to parse template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;
    if manifest.schema_version.trim().is_empty() {
        manifest.schema_version = packaged.schema_version;
    }
    if manifest.default_template_version.trim().is_empty() {
        manifest.default_template_version = packaged.default_template_version;
    }
    Ok(manifest)
}

fn local_template_versions(manifest_path: Option<&Path>) -> Result<HashSet<String>, String> {
    let Some(manifest_path) = manifest_path else {
        return Ok(HashSet::new());
    };

    let manifest_contents = fs::read_to_string(manifest_path).map_err(|error| {
        format!(
            "failed to read template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;
    let manifest = serde_json::from_str::<TemplateManifest>(&manifest_contents).map_err(|error| {
        format!(
            "failed to parse template manifest '{}': {error}",
            manifest_path.display()
        )
    })?;
    Ok(manifest
        .templates
        .into_iter()
        .filter(|entry| entry.enabled)
        .map(|entry| entry.version)
        .collect())
}

fn inspect_template_catalog_governance() -> Result<TemplateCatalogGovernanceResult, String> {
    let packaged_manifest = template_catalog().map_err(|error| error.to_string())?;
    let packaged_versions = packaged_manifest
        .templates
        .iter()
        .filter(|entry| entry.enabled)
        .map(|entry| entry.version.clone())
        .collect::<HashSet<_>>();
    let overlay_dir = template_overlay_dir();
    let manifest_path = template_catalog_manifest_path();
    let manifest_exists = manifest_path.exists();
    let manifest_file_name = manifest_path
        .file_name()
        .and_then(|value| value.to_str())
        .unwrap_or("template-manifest.json")
        .to_string();
    let overlay_json_files = overlay_json_files(&overlay_dir, &manifest_file_name)?;
    let overlay_json_file_count = overlay_json_files.len();
    let mut manifest_status = if manifest_exists { "ready" } else { "missing" }.to_string();
    let mut effective_default_template_version = packaged_manifest.default_template_version.clone();
    let mut effective_default_source = "packaged".to_string();
    let mut local_entry_count = 0usize;
    let mut local_entries = Vec::new();
    let mut issues = Vec::new();
    let mut referenced_local_paths = HashSet::new();

    if !manifest_exists {
        issues.push(governance_info(
            "manifest_missing",
            "The desktop local template manifest does not exist yet. Saving a template from the Catalog lane will create it.",
        ));
    } else {
        let manifest_contents = match fs::read_to_string(&manifest_path) {
            Ok(contents) => contents,
            Err(error) => {
                manifest_status = "error".to_string();
                issues.push(governance_error(
                    "manifest_read_failed",
                    format!(
                        "Template manifest '{}' could not be read: {error}",
                        manifest_path.display()
                    ),
                ));
                String::new()
            }
        };

        if manifest_status != "error" {
            match serde_json::from_str::<TemplateManifest>(&manifest_contents) {
                Ok(local_manifest) => {
                    local_entry_count = local_manifest.templates.len();

                    if local_manifest.schema_version.trim().is_empty() {
                        issues.push(governance_warning(
                            "manifest_schema_missing",
                            "The local template manifest is missing schema_version. The packaged schema will be used until the manifest is rewritten.",
                        ));
                    }
                    if local_manifest.default_template_version.trim().is_empty() {
                        issues.push(governance_warning(
                            "manifest_default_missing",
                            "The local template manifest is missing default_template_version. The packaged default will be used until the manifest is rewritten.",
                        ));
                    }

                    for (version, count) in duplicate_template_versions(&local_manifest.templates) {
                        issues.push(governance_warning(
                            "duplicate_local_version",
                            format!(
                                "Local template manifest contains {count} entries for template_version '{version}'. Keep a single entry per template_version."
                            ),
                        ));
                    }

                    for entry in &local_manifest.templates {
                        let normalized_path = entry.path.replace('\\', "/");
                        let path_is_absolute = Path::new(&normalized_path).is_absolute();
                        let path_has_parent_traversal = contains_parent_traversal(&normalized_path);
                        let resolved_path = if path_is_absolute {
                            PathBuf::from(&normalized_path)
                        } else {
                            overlay_dir.join(&normalized_path)
                        };
                        let file_exists =
                            !path_is_absolute && !path_has_parent_traversal && resolved_path.exists();

                        if !path_is_absolute && !path_has_parent_traversal {
                            referenced_local_paths.insert(normalized_path.clone());
                        }
                        if path_is_absolute {
                            issues.push(governance_error(
                                "absolute_template_path",
                                format!(
                                    "Local template entry '{}' points to an absolute path '{}'. Local overlay paths must stay relative to the overlay directory.",
                                    entry.version, entry.path
                                ),
                            ));
                        }
                        if path_has_parent_traversal {
                            issues.push(governance_error(
                                "template_path_traversal",
                                format!(
                                    "Local template entry '{}' contains parent traversal in '{}'. Parent traversal is rejected for overlay templates.",
                                    entry.version, entry.path
                                ),
                            ));
                        } else if !file_exists {
                            issues.push(governance_error(
                                "missing_template_file",
                                format!(
                                    "Local template entry '{}' references '{}' but the file is missing from the overlay directory.",
                                    entry.version, entry.path
                                ),
                            ));
                        }

                        local_entries.push(TemplateCatalogGovernanceEntryResult {
                            version: entry.version.clone(),
                            label_name: entry.label_name.clone(),
                            path: entry.path.clone(),
                            resolved_path: resolved_path.to_string_lossy().into_owned(),
                            enabled: entry.enabled,
                            file_exists,
                        });
                    }

                    let effective_manifest = template_catalog_with_overlay(Some(&manifest_path))
                        .map_err(|error| error.to_string())?;
                    effective_default_template_version =
                        effective_manifest.default_template_version.clone();
                    effective_default_source = if local_manifest
                        .templates
                        .iter()
                        .any(|entry| entry.enabled && entry.version == effective_default_template_version)
                    {
                        "local".to_string()
                    } else if packaged_versions.contains(&effective_default_template_version) {
                        "packaged".to_string()
                    } else {
                        "unknown".to_string()
                    };

                    let effective_versions = effective_manifest
                        .templates
                        .iter()
                        .filter(|entry| entry.enabled)
                        .map(|entry| entry.version.clone())
                        .collect::<HashSet<_>>();
                    if !effective_versions.contains(&effective_default_template_version) {
                        issues.push(governance_error(
                            "default_template_missing",
                            format!(
                                "Effective default template_version '{}' is not available in the merged packaged/local catalog.",
                                effective_default_template_version
                            ),
                        ));
                    }
                }
                Err(error) => {
                    manifest_status = "error".to_string();
                    issues.push(governance_error(
                        "manifest_parse_failed",
                        format!(
                            "Template manifest '{}' is not valid JSON: {error}",
                            manifest_path.display()
                        ),
                    ));
                }
            }
        }
    }

    let unreferenced_local_files = overlay_json_files
        .iter()
        .filter(|path| !referenced_local_paths.contains(*path))
        .cloned()
        .collect::<Vec<_>>();
    if !unreferenced_local_files.is_empty() {
        issues.push(governance_warning(
            "unreferenced_local_file",
            format!(
                "Overlay directory contains JSON files that are not referenced by the manifest: {}.",
                unreferenced_local_files.join(", ")
            ),
        ));
    }

    let mut repair_guidance = vec![
        format!(
            "Close desktop-shell before manually editing or replacing '{}'.",
            manifest_path.display()
        ),
        format!(
            "Keep a backup of '{}' before any manual manifest repair.",
            overlay_dir.display()
        ),
    ];
    if issues
        .iter()
        .any(|issue| issue.code == "manifest_parse_failed" || issue.code == "manifest_read_failed")
    {
        repair_guidance.push(format!(
            "If '{}' is broken, rename it out of the overlay directory, reopen desktop-shell, and save one valid template from the Catalog lane to regenerate a clean manifest.",
            manifest_path.display()
        ));
    }
    if issues
        .iter()
        .any(|issue| issue.code == "missing_template_file")
    {
        repair_guidance.push(
            "If a manifest entry points to a missing JSON file, restore that file from backup or save the same template_version again from the Catalog lane.".to_string(),
        );
    }
    if issues
        .iter()
        .any(|issue| issue.code == "duplicate_local_version")
    {
        repair_guidance.push(
            "Keep exactly one manifest entry per template_version. Remove duplicate entries before using proof or print dispatch.".to_string(),
        );
    }
    if issues
        .iter()
        .any(|issue| issue.code == "unreferenced_local_file")
    {
        repair_guidance.push(
            "JSON files that are not referenced by template-manifest.json do not dispatch. Add a manifest entry or remove the orphaned file after backup.".to_string(),
        );
    }
    if repair_guidance.len() == 2 {
        repair_guidance.push(
            "If the catalog route looks wrong, refresh the desktop catalog and compare template-manifest.json against the JSON files in the overlay directory.".to_string(),
        );
    }

    Ok(TemplateCatalogGovernanceResult {
        manifest_status,
        overlay_directory_path: overlay_dir.to_string_lossy().into_owned(),
        manifest_path: manifest_path.to_string_lossy().into_owned(),
        manifest_exists,
        effective_default_template_version,
        effective_default_source,
        local_entry_count,
        overlay_json_file_count,
        local_entries,
        issues,
        backup_guidance: vec![
            "Close desktop-shell before copying or restoring the local template catalog.".to_string(),
            format!(
                "Back up the entire overlay directory as a unit: {}",
                overlay_dir.display()
            ),
            format!(
                "The manifest file and local template JSON files must stay together. Manifest path: {}",
                manifest_path.display()
            ),
            "Restore by replacing the overlay directory, then reopen desktop-shell and refresh the catalog.".to_string(),
        ],
        repair_guidance,
        single_writer_guidance: vec![
            "Treat the local template catalog as single-writer state.".to_string(),
            "Do not save templates from multiple desktop-shell instances against the same overlay directory.".to_string(),
            "Do not mix manual file edits with live Catalog-lane saves while desktop-shell is running.".to_string(),
            "Finish any backup or restore with desktop-shell closed before reopening the operator workstation.".to_string(),
        ],
    })
}

fn overlay_json_files(overlay_dir: &Path, manifest_file_name: &str) -> Result<Vec<String>, String> {
    if !overlay_dir.exists() {
        return Ok(Vec::new());
    }

    let mut files = fs::read_dir(overlay_dir)
        .map_err(|error| {
            format!(
                "failed to read template overlay directory '{}': {error}",
                overlay_dir.display()
            )
        })?
        .filter_map(|entry| {
            let entry = entry.ok()?;
            if !entry.file_type().ok()?.is_file() {
                return None;
            }

            let path = entry.path();
            if path.extension().and_then(|ext| ext.to_str()) != Some("json") {
                return None;
            }

            let file_name = path.file_name()?.to_str()?.to_string();
            if file_name == manifest_file_name {
                return None;
            }

            Some(file_name)
        })
        .collect::<Vec<_>>();
    files.sort();
    Ok(files)
}

fn contains_parent_traversal(path: &str) -> bool {
    path.split('/').any(|segment| segment == "..")
}

fn duplicate_template_versions(entries: &[TemplateManifestEntry]) -> Vec<(String, usize)> {
    let mut counts = HashMap::new();
    for entry in entries {
        *counts.entry(entry.version.clone()).or_insert(0usize) += 1;
    }

    let mut duplicates = counts
        .into_iter()
        .filter(|(_, count)| *count > 1)
        .collect::<Vec<_>>();
    duplicates.sort_by(|left, right| left.0.cmp(&right.0));
    duplicates
}

fn governance_error(code: &str, message: impl Into<String>) -> TemplateCatalogGovernanceIssueResult {
    TemplateCatalogGovernanceIssueResult {
        severity: "error".to_string(),
        code: code.to_string(),
        message: message.into(),
    }
}

fn governance_warning(
    code: &str,
    message: impl Into<String>,
) -> TemplateCatalogGovernanceIssueResult {
    TemplateCatalogGovernanceIssueResult {
        severity: "warning".to_string(),
        code: code.to_string(),
        message: message.into(),
    }
}

fn governance_info(code: &str, message: impl Into<String>) -> TemplateCatalogGovernanceIssueResult {
    TemplateCatalogGovernanceIssueResult {
        severity: "info".to_string(),
        code: code.to_string(),
        message: message.into(),
    }
}

fn resolve_optional_env(key: &str) -> Option<String> {
    env::var(key).ok()
}

fn resolve_bool_env(key: &str) -> bool {
    resolve_optional_env(key).is_some_and(|value| {
        matches!(
            value.trim().to_ascii_lowercase().as_str(),
            "1" | "true" | "yes" | "on"
        )
    })
}

fn resolve_template_version_for_request(request: &DispatchRequest) -> Result<String, String> {
    let template_version = request
        .template_version
        .as_deref()
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .map(str::to_string);
    let template_version_from_ref = request.template.as_ref().map(|template| {
        let id = template.id.trim();
        let version = template.version.trim();
        if id.is_empty() || version.is_empty() {
            Err("template id/version must not be empty".to_string())
        } else {
            Ok(format!("{id}@{version}"))
        }
    }).transpose()?;

    match (template_version, template_version_from_ref) {
        (Some(value), Some(resolved)) if value != resolved => Err(
            "templateVersion and template.id/template.version must resolve to the same value"
                .to_string(),
        ),
        (Some(value), Some(_)) => Ok(value),
        (Some(value), None) => Ok(value),
        (None, Some(resolved)) => Ok(resolved),
        (None, None) => Err("templateVersion or template.id/template.version is required".to_string()),
    }
}

fn build_dispatch_match_subject(request: &DispatchRequest) -> Result<DispatchMatchSubject, String> {
    let sku = require_trimmed_non_empty("sku", &request.sku)?;
    let brand = require_trimmed_non_empty("brand", &request.brand)?;
    let jan_normalized = Jan::parse(&request.jan)
        .map(|value| value.as_str().to_string())
        .map_err(jan_error_message)?;
    if request.qty == 0 {
        return Err("qty must be greater than or equal to 1".to_string());
    }

    Ok(DispatchMatchSubject {
        sku,
        brand,
        jan_normalized,
        qty: request.qty,
    })
}

fn require_trimmed_non_empty(field_name: &str, value: &str) -> Result<String, String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        Err(format!("{field_name} must not be empty"))
    } else {
        Ok(trimmed.to_string())
    }
}

fn optional_trimmed_non_empty(value: Option<String>) -> Option<String> {
    let value = value?;
    let trimmed = value.trim();
    if trimmed.is_empty() {
        None
    } else {
        Some(trimmed.to_string())
    }
}

fn jan_error_message(error: JanError) -> String {
    match error {
        JanError::Empty => "jan must not be empty".to_string(),
        JanError::NonDigit => "jan must contain only digits".to_string(),
        JanError::InvalidLength { actual } => {
            format!("jan must contain 12 or 13 digits, got {actual}")
        }
        JanError::InvalidChecksum { expected, actual } => {
            format!("jan checksum is invalid: expected {expected}, got {actual}")
        }
    }
}

fn validate_legacy_proof_seed_rows(
    config: &PrintBridgeConfig,
    rows: Vec<LegacyProofSeedRowInput>,
    snapshot: &AuditLedgerSnapshot,
) -> Vec<ValidatedLegacyProofSeedRow> {
    let mut existing_job_ids = HashSet::new();
    let mut existing_lineages = HashSet::new();
    for dispatch in snapshot.dispatches.iter().filter(|record| record.mode == "proof") {
        existing_job_ids.insert(dispatch.audit.job_id.0.clone());
        existing_lineages.insert(dispatch.audit.job_lineage_id.0.clone());
    }
    for proof in &snapshot.proofs {
        existing_job_ids.insert(proof.proof_job_id.0.clone());
        existing_lineages.insert(proof.job_lineage_id.0.clone());
    }

    let mut batch_job_ids = HashSet::new();
    let mut batch_lineages = HashSet::new();
    rows.into_iter()
        .enumerate()
        .map(|(row_index, row)| {
            match validate_legacy_proof_seed_row(
                config,
                row_index,
                row,
                &existing_job_ids,
                &existing_lineages,
                &mut batch_job_ids,
                &mut batch_lineages,
            ) {
                Ok(validated) => validated,
                Err(result) => ValidatedLegacyProofSeedRow {
                    record: None,
                    result,
                },
            }
        })
        .collect()
}

fn validate_legacy_proof_seed_row(
    config: &PrintBridgeConfig,
    row_index: usize,
    row: LegacyProofSeedRowInput,
    existing_job_ids: &HashSet<String>,
    existing_lineages: &HashSet<String>,
    batch_job_ids: &mut HashSet<String>,
    batch_lineages: &mut HashSet<String>,
) -> Result<ValidatedLegacyProofSeedRow, LegacyProofSeedRowResult> {
    let proof_job_id = row.proof_job_id.trim().to_string();
    let error = |message: String| LegacyProofSeedRowResult {
        row_index,
        proof_job_id: proof_job_id.clone(),
        status: LegacyProofSeedRowStatus::Error,
        message,
        normalized_jan: None,
        resolved_job_lineage_id: None,
        artifact_path: None,
    };

    if proof_job_id.is_empty() {
        return Err(error("proofJobId is required.".to_string()));
    }

    let resolved_job_lineage_id = row
        .job_lineage_id
        .as_deref()
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .unwrap_or(&proof_job_id)
        .to_string();
    if existing_job_ids.contains(&proof_job_id) {
        return Err(error(format!(
            "proofJobId '{}' already exists in the audit ledger.",
            proof_job_id
        )));
    }
    if existing_lineages.contains(&resolved_job_lineage_id) {
        return Err(error(format!(
            "jobLineageId '{}' already exists in the audit ledger.",
            resolved_job_lineage_id
        )));
    }
    if !batch_job_ids.insert(proof_job_id.clone()) {
        return Err(error(format!(
            "proofJobId '{}' is duplicated in this seed batch.",
            proof_job_id
        )));
    }
    if !batch_lineages.insert(resolved_job_lineage_id.clone()) {
        return Err(error(format!(
            "jobLineageId '{}' is duplicated in this seed batch.",
            resolved_job_lineage_id
        )));
    }

    let template_version = require_trimmed_non_empty("templateVersion", &row.template_version)
        .map_err(error)?;
    let requested_by = validate_legacy_seed_actor(&row.requested_by).map_err(error)?;
    let requested_at = validate_legacy_seed_requested_at(&row.requested_at).map_err(error)?;
    let notes = optional_trimmed_non_empty(row.notes);
    let match_subject =
        validate_legacy_seed_match_subject(&row.match_subject).map_err(error)?;
    let artifact_path = validate_legacy_seed_artifact_path(config, &row.artifact_path).map_err(error)?;
    let artifact_size = fs::metadata(&artifact_path)
        .map_err(|err| error(format!("failed to stat artifactPath '{}': {err}", artifact_path.display())))?
        .len() as usize;

    let proof_record = ProofRecord::pending(
        PrintJobId(proof_job_id.clone()),
        PrintJobLineageId(resolved_job_lineage_id.clone()),
        requested_by.clone(),
        requested_at.clone(),
        artifact_path.to_string_lossy().into_owned(),
        notes.clone(),
    );
    let dispatch_record = PersistedDispatchRecord {
        audit: audit_log::PrintAuditRecord::dispatch(
            PrintJobId(proof_job_id.clone()),
            PrintJobLineageId(resolved_job_lineage_id.clone()),
            None,
            requested_by,
            requested_at,
            Some("legacy proof seed".to_string()),
        ),
        mode: "proof".to_string(),
        template_version,
        match_subject: match_subject.clone(),
        artifact_media_type: "application/pdf".to_string(),
        artifact_byte_size: artifact_size,
        submission_adapter_kind: "pdf".to_string(),
        submission_external_job_id: format!("legacy-seed:{proof_job_id}"),
    };

    Ok(ValidatedLegacyProofSeedRow {
        record: Some(LegacyProofSeedRecord {
            dispatch: dispatch_record,
            proof: proof_record,
        }),
        result: LegacyProofSeedRowResult {
            row_index,
            proof_job_id,
            status: LegacyProofSeedRowStatus::Ok,
            message: "Ready to seed as pending proof.".to_string(),
            normalized_jan: Some(match_subject.jan_normalized),
            resolved_job_lineage_id: Some(resolved_job_lineage_id),
            artifact_path: Some(artifact_path.to_string_lossy().into_owned()),
        },
    })
}

fn validate_legacy_seed_actor(actor: &AuditActor) -> Result<AuditActor, String> {
    Ok(AuditActor {
        user_id: require_trimmed_non_empty("requestedBy.userId", &actor.user_id)?,
        display_name: require_trimmed_non_empty(
            "requestedBy.displayName",
            &actor.display_name,
        )?,
    })
}

fn validate_legacy_seed_requested_at(value: &str) -> Result<String, String> {
    require_trimmed_non_empty("requestedAt", value)
}

fn validate_legacy_seed_match_subject(
    input: &LegacyProofSeedMatchSubjectInput,
) -> Result<DispatchMatchSubject, String> {
    let sku = require_trimmed_non_empty("matchSubject.sku", &input.sku)?;
    let brand = require_trimmed_non_empty("matchSubject.brand", &input.brand)?;
    if input.qty == 0 {
        return Err("matchSubject.qty must be greater than or equal to 1.".to_string());
    }
    let jan_normalized = Jan::parse(&input.jan)
        .map(|value| value.as_str().to_string())
        .map_err(jan_error_message)?;
    Ok(DispatchMatchSubject {
        sku,
        brand,
        jan_normalized,
        qty: input.qty,
    })
}

fn validate_legacy_seed_artifact_path(
    config: &PrintBridgeConfig,
    artifact_path: &str,
) -> Result<PathBuf, String> {
    let artifact_path = require_trimmed_non_empty("artifactPath", artifact_path)?;
    let artifact_path = PathBuf::from(artifact_path);
    if !artifact_path.is_absolute() {
        return Err("artifactPath must be an absolute path.".to_string());
    }
    if artifact_path
        .extension()
        .and_then(|value| value.to_str())
        .map(|value| !value.eq_ignore_ascii_case("pdf"))
        .unwrap_or(true)
    {
        return Err("artifactPath must point to a PDF file.".to_string());
    }
    if !artifact_path.exists() {
        return Err(format!(
            "artifactPath '{}' does not exist.",
            artifact_path.display()
        ));
    }
    let canonical_artifact = fs::canonicalize(&artifact_path).map_err(|err| {
        format!(
            "failed to canonicalize artifactPath '{}': {err}",
            artifact_path.display()
        )
    })?;
    let canonical_proof_dir = fs::canonicalize(&config.print_output_dir).map_err(|err| {
        format!(
            "failed to canonicalize proof output dir '{}': {err}",
            config.print_output_dir.display()
        )
    })?;
    if !canonical_artifact.starts_with(&canonical_proof_dir) {
        return Err(format!(
            "artifactPath '{}' must stay under proof output dir '{}'.",
            canonical_artifact.display(),
            canonical_proof_dir.display()
        ));
    }
    Ok(canonical_artifact)
}

fn validate_existing_proof_artifact_path(
    config: &PrintBridgeConfig,
    proof_job_id: &str,
    artifact_path: &str,
) -> Result<PathBuf, String> {
    let canonical_artifact =
        validate_legacy_seed_artifact_path(config, artifact_path).map_err(|error| {
            format!("source proof job '{proof_job_id}' has invalid artifactPath: {error}")
        })?;
    let metadata = fs::metadata(&canonical_artifact).map_err(|err| {
        format!(
            "failed to read source proof artifact '{}': {err}",
            canonical_artifact.display()
        )
    })?;
    if metadata.len() == 0 {
        return Err(format!(
            "source proof job '{proof_job_id}' points to empty PDF '{}'",
            canonical_artifact.display()
        ));
    }

    let mut file = fs::File::open(&canonical_artifact).map_err(|err| {
        format!(
            "failed to open source proof artifact '{}': {err}",
            canonical_artifact.display()
        )
    })?;
    let mut header = [0_u8; 5];
    let bytes_read = file.read(&mut header).map_err(|err| {
        format!(
            "failed to read source proof artifact '{}': {err}",
            canonical_artifact.display()
        )
    })?;
    if bytes_read < header.len() || header != *b"%PDF-" {
        return Err(format!(
            "source proof job '{proof_job_id}' does not point to a readable PDF header at '{}'",
            canonical_artifact.display()
        ));
    }

    Ok(canonical_artifact)
}

fn ensure_template_version_available(template_version: &str) -> Result<(), String> {
    let local_manifest = template_catalog_manifest_path_if_exists();
    if let Some(path) = local_manifest {
        resolve_template_with_overlay(template_version, Some(path.as_path()))
    } else {
        resolve_template(template_version)
    }
        .map(|_| ())
        .map_err(|error| error.to_string())
}

fn map_event_kind(event: &print_agent::PrintDispatchAuditEvent) -> audit_log::AuditEventKind {
    match event {
        print_agent::PrintDispatchAuditEvent::Submitted => audit_log::AuditEventKind::Submitted,
        print_agent::PrintDispatchAuditEvent::Reprinted => audit_log::AuditEventKind::Reprinted,
        print_agent::PrintDispatchAuditEvent::Created => audit_log::AuditEventKind::Created,
        print_agent::PrintDispatchAuditEvent::Completed => audit_log::AuditEventKind::Completed,
        print_agent::PrintDispatchAuditEvent::Failed => audit_log::AuditEventKind::Failed,
    }
}

fn resolve_zint_binary_path_with_warning(
    env_key: &str,
    fallback: &str,
) -> (String, Option<BridgeWarning>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (value, None),
        _ => (
            fallback.to_string(),
            Some(BridgeWarning::info(
                BRIDGE_ZINT_DEFAULTED,
                format!("{env_key} is not set; defaulting to {fallback}"),
            )),
        ),
    }
}

fn report_zint_warning(path: &str, warnings: &mut Vec<BridgeWarning>) {
    if Path::new(path).is_absolute() && !Path::new(path).exists() {
        warnings.push(BridgeWarning::error(
            BRIDGE_ZINT_ABSOLUTE_PATH_MISSING,
            format!(
                "{ENV_ZINT_BINARY_PATH} is set to absolute path '{path}', but the file does not exist"
            ),
        ));
    }
}

fn report_output_dir_warning(
    env_key: &str,
    dir: &Path,
    from_fallback: bool,
    warnings: &mut Vec<BridgeWarning>,
) {
    if dir.exists() {
        return;
    }

    if from_fallback {
        warnings.push(BridgeWarning::info(
            BRIDGE_OUTPUT_DIR_DEFAULTED_MISSING_WILL_CREATE,
            format!(
                "{env_key} is not set; defaulting to {}; directory does not exist and will be created on demand",
                dir.display()
            ),
        ));
    } else {
        warnings.push(BridgeWarning::error(
            BRIDGE_OUTPUT_DIR_CONFIGURED_MISSING,
            format!(
                "{env_key} is set to {} but does not exist; it will be created on demand",
                dir.display()
            ),
        ));
    }
}

fn resolve_windows_printer_name_with_warning(
    env_key: &str,
    fallback: &str,
) -> (String, Option<BridgeWarning>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (value, None),
        _ => (
            fallback.to_string(),
            Some(BridgeWarning::info(
                BRIDGE_WINDOWS_PRINTER_DEFAULTED_WHILE_SPOOLER,
                format!("{env_key} is not set; defaulting to '{fallback}'"),
            )),
        ),
    }
}

fn sanitize_path_component(raw: &str) -> String {
    raw.chars()
        .map(|character| match character {
            '/' | '\\' | '\0' => '_',
            c if c.is_control() => '_',
            c => c,
        })
        .collect::<String>()
}

fn require_non_empty_preview_field(field: &str, value: &str) -> Result<String, String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        Err(format!("{field} must not be empty"))
    } else {
        Ok(trimmed.to_string())
    }
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            dispatch_print_job,
            print_bridge_status,
            search_audit_log,
            export_audit_ledger,
            trim_audit_ledger,
            list_audit_backup_bundles,
            restore_audit_backup_bundle,
            approve_proof,
            reject_proof,
            template_catalog_command,
            template_catalog_governance_command,
            save_template_to_local_catalog,
            preview_template_draft,
            validate_legacy_proof_seed,
            seed_legacy_proofs
        ])
        .run(tauri::generate_context!())
        .expect("failed to run desktop shell");
}

#[cfg(test)]
mod tests {
    use super::{
        available_adapters_for_host, dispatch_print_job, export_audit_ledger,
        list_audit_backup_bundles, preview_template_draft, resolve_optional_env, resolve_output_dir,
        resolve_print_adapter_for_host, resolve_print_adapter_with_warning_for_host,
        sanitize_path_component, save_template_to_local_catalog, template_catalog_command,
        template_catalog_governance_command, trim_audit_ledger, TemplateCatalogWritebackRequest,
        BridgePrintAdapter, PrintBridgeConfig, PrintBridgeStatus, TemplateDraftPreviewRequest,
        TemplateDraftPreviewSample,
        ENV_ALLOW_PRINT_WITHOUT_PROOF, ENV_AUDIT_LOG_DIR, ENV_PRINT_ADAPTER,
        ENV_PRINT_OUTPUT_DIR, ENV_SPOOL_OUTPUT_DIR, ENV_TEMPLATE_OVERLAY_DIR,
        ENV_WINDOWS_PRINTER_NAME, ENV_ZINT_BINARY_PATH,
    };
    use crate::audit_store::{AuditStore, ProofReviewRequest};
    use audit_log::{
        AuditActor, AuditExportRequest, AuditLedgerScope, AuditQuery, AuditRetentionRequest,
        DispatchMatchSubject, PersistedDispatchRecord, ProofRecord, PrintAuditRecord, PrintJobId,
        PrintJobLineageId,
    };
    use print_agent::{DispatchPrinterProfile, DispatchRequest, ExecutionIntent, PrintExecution};
    #[cfg(unix)]
    use std::os::unix::fs::PermissionsExt;
    use std::{env, fs, path::{Path, PathBuf}, sync::Mutex};

    static TEST_MUTEX: Mutex<()> = Mutex::new(());

    #[test]
    fn sanitize_path_component_replaces_path_controls() {
        let sanitized = sanitize_path_component("A/B\\C\0D");
        assert_eq!(sanitized, "A_B_C_D");
    }

    #[test]
    fn preview_template_draft_renders_inline_svg() {
        let result = preview_template_draft(TemplateDraftPreviewRequest {
            template_source: r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "inline-preview@v1",
              "label_name": "inline-preview",
              "page": { "width_mm": 40, "height_mm": 20, "background_fill": "#ffeecc" },
              "border": { "visible": false, "color": "#112233", "width_mm": 0.4 },
              "fields": [
                {
                  "name": "sku",
                  "x_mm": 2,
                  "y_mm": 5,
                  "font_size_mm": 3,
                  "template": "sku:{sku}",
                  "color": "#123abc"
                }
              ]
            }
            "##
            .to_string(),
            sample: TemplateDraftPreviewSample {
                job_id: "JOB-20260415-PREVIEW".to_string(),
                sku: "SKU-0001".to_string(),
                brand: "Acme".to_string(),
                jan: "4901234567894".to_string(),
                qty: 1,
            },
        })
        .expect("inline preview should render");

        assert_eq!(result.template_version, "inline-preview@v1");
        assert_eq!(result.normalized_jan, "4901234567894");
        assert!(result.svg.contains("sku:SKU-0001"));
        assert!(result.svg.contains("stroke=\"none\""));
        assert!(result.svg.contains("fill=\"#123abc\""));
    }

    #[test]
    fn preview_template_draft_rejects_invalid_jan() {
        let error = preview_template_draft(TemplateDraftPreviewRequest {
            template_source: r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "inline-preview@v1",
              "label_name": "inline-preview",
              "page": { "width_mm": 40, "height_mm": 20 },
              "fields": [
                { "name": "sku", "x_mm": 2, "y_mm": 5, "font_size_mm": 3, "template": "sku:{sku}" }
              ]
            }
            "##
            .to_string(),
            sample: TemplateDraftPreviewSample {
                job_id: "JOB-20260415-PREVIEW".to_string(),
                sku: "SKU-0001".to_string(),
                brand: "Acme".to_string(),
                jan: "123".to_string(),
                qty: 1,
            },
        })
        .expect_err("invalid jan should be rejected");

        assert!(error.contains("jan"));
    }

    #[test]
    fn template_catalog_command_exposes_packaged_templates() {
        let catalog = template_catalog_command().expect("catalog command should succeed");

        assert_eq!(catalog.default_template_version, "basic-50x30@v1");
        assert_eq!(catalog.templates.len(), 1);
        assert_eq!(catalog.templates[0].version, "basic-50x30@v1");
        assert_eq!(catalog.templates[0].source, "packaged");
    }

    #[test]
    fn template_catalog_command_prefers_local_overlay_when_present() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_TEMPLATE_OVERLAY_DIR]);
        let root = env::temp_dir().join("jan-label-template-overlay-present");
        let _ = fs::remove_dir_all(&root);
        let overlay_dir = root.join("catalog-overlays");
        let manifest_path = overlay_dir.join("template-manifest.json");
        env::set_var(ENV_TEMPLATE_OVERLAY_DIR, overlay_dir.clone());

        let manifest = r##"
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "overlay-basic@v1",
              "templates": [
                {
                  "version": "overlay-basic@v1",
                  "path": "overlay-basic@v1.json",
                  "label_name": "overlay",
                  "description": "local overlay entry",
                  "enabled": true
                }
              ]
            }
            "##;
        fs::create_dir_all(&overlay_dir).expect("create overlay dir for test");
        fs::write(&manifest_path, manifest).expect("write local manifest for test");

        let catalog = template_catalog_command().expect("catalog command should merge overlay");

        assert_eq!(catalog.default_template_version, "overlay-basic@v1");
        assert_eq!(catalog.templates.len(), 2);
        assert!(catalog
            .templates
            .iter()
            .any(|entry| entry.version == "basic-50x30@v1"));
        assert!(catalog
            .templates
            .iter()
            .any(|entry| entry.version == "overlay-basic@v1"));
        assert_eq!(
            catalog
                .templates
                .iter()
                .find(|entry| entry.version == "overlay-basic@v1")
                .map(|entry| entry.source.as_str()),
            Some("local")
        );

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn write_template_catalog_entry_persists_overlay_manifest_and_template() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_TEMPLATE_OVERLAY_DIR]);
        let root = env::temp_dir().join("jan-label-template-writeback");
        let _ = fs::remove_dir_all(&root);
        let overlay_dir = root.join("catalog-overlays");
        env::set_var(ENV_TEMPLATE_OVERLAY_DIR, &overlay_dir);

        let created = save_template_to_local_catalog(TemplateCatalogWritebackRequest {
            template_source: r##"
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "overlay-writeback@v1",
                  "label_name": "overlay-writeback",
                  "page": { "width_mm": 62, "height_mm": 29 },
                  "fields": [
                    {
                      "name": "sku",
                      "x_mm": 2,
                      "y_mm": 5,
                      "font_size_mm": 3,
                      "template": "sku:{sku}"
                    }
                  ]
                }
                "##.to_string(),
            enabled: true,
            set_as_default: true,
            label_name: Some("overlay-writeback".to_string()),
            description: Some("created by test".to_string()),
        })
        .expect("create overlay template should succeed");

        let catalog = template_catalog_command().expect("catalog should include created overlay template");
        assert_eq!(created.action, "created");
        assert_eq!(created.status, "created");
        assert!(created.message.contains("overlay-writeback@v1"));
        assert_eq!(created.default_template_version, "overlay-writeback@v1");
        assert!(catalog.templates.iter().any(|entry| entry.version == "overlay-writeback@v1"));
        assert_eq!(catalog.default_template_version, "overlay-writeback@v1");
        assert_eq!(Path::new(&created.template_path).exists(), true);
        assert_eq!(Path::new(&created.manifest_path).exists(), true);

        let updated = save_template_to_local_catalog(TemplateCatalogWritebackRequest {
            template_source: r##"
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "overlay-writeback@v1",
                  "label_name": "overlay-writeback-renamed",
                  "page": { "width_mm": 62, "height_mm": 29 },
                  "fields": [
                    {
                      "name": "sku",
                      "x_mm": 2,
                      "y_mm": 5,
                      "font_size_mm": 3,
                      "template": "sku:{sku}"
                    }
                  ]
                }
                "##.to_string(),
            enabled: false,
            set_as_default: false,
            label_name: None,
            description: Some("updated by test".to_string()),
        })
        .expect("update overlay template should succeed");

        assert_eq!(updated.action, "updated");
        assert_eq!(updated.status, "updated");
        assert_eq!(created.template_version, updated.template_version);

        let catalog = template_catalog_command().expect("catalog should reflect updated template entry");
        assert!(
            catalog
                .templates
                .iter()
                .all(|entry| entry.version != "overlay-writeback@v1")
        );

        let manifest_contents =
            fs::read_to_string(&updated.manifest_path).expect("manifest should be readable");
        let manifest_value: serde_json::Value = serde_json::from_str(&manifest_contents)
            .expect("manifest should be valid JSON");
        let writeback = manifest_value
            .get("templates")
            .and_then(|templates| templates.as_array())
            .expect("manifest templates")
            .iter()
            .find(|entry| entry.get("version").and_then(|value| value.as_str()) == Some("overlay-writeback@v1"))
            .expect("overlay writeback entry should remain in manifest");
        assert_eq!(writeback.get("label_name").and_then(|value| value.as_str()), Some("overlay-writeback-renamed"));
        assert_eq!(writeback.get("enabled").and_then(|value| value.as_bool()), Some(false));

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn template_catalog_governance_reports_local_overlay_health() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_TEMPLATE_OVERLAY_DIR]);
        let root = env::temp_dir().join("jan-label-template-governance-ready");
        let _ = fs::remove_dir_all(&root);
        let overlay_dir = root.join("catalog-overlays");
        env::set_var(ENV_TEMPLATE_OVERLAY_DIR, &overlay_dir);

        save_template_to_local_catalog(TemplateCatalogWritebackRequest {
            template_source: r##"
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "overlay-governance@v1",
                  "label_name": "overlay-governance",
                  "page": { "width_mm": 62, "height_mm": 29 },
                  "fields": [
                    {
                      "name": "sku",
                      "x_mm": 2,
                      "y_mm": 5,
                      "font_size_mm": 3,
                      "template": "sku:{sku}"
                    }
                  ]
                }
                "##
            .to_string(),
            enabled: true,
            set_as_default: true,
            label_name: Some("overlay-governance".to_string()),
            description: Some("governance health test".to_string()),
        })
        .expect("write local governance template should succeed");

        let governance =
            template_catalog_governance_command().expect("governance diagnostics should succeed");

        assert_eq!(governance.manifest_status, "ready");
        assert_eq!(governance.effective_default_template_version, "overlay-governance@v1");
        assert_eq!(governance.effective_default_source, "local");
        assert_eq!(governance.local_entry_count, 1);
        assert!(governance
            .local_entries
            .iter()
            .any(|entry| entry.version == "overlay-governance@v1" && entry.file_exists));
        assert!(!governance
            .issues
            .iter()
            .any(|issue| issue.severity == "error"));

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn template_catalog_governance_reports_missing_local_template_file() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_TEMPLATE_OVERLAY_DIR]);
        let root = env::temp_dir().join("jan-label-template-governance-missing-file");
        let _ = fs::remove_dir_all(&root);
        let overlay_dir = root.join("catalog-overlays");
        let manifest_path = overlay_dir.join("template-manifest.json");
        env::set_var(ENV_TEMPLATE_OVERLAY_DIR, overlay_dir.clone());

        fs::create_dir_all(&overlay_dir).expect("create overlay dir for diagnostics test");
        fs::write(
            &manifest_path,
            r##"
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "overlay-missing@v1",
              "templates": [
                {
                  "version": "overlay-missing@v1",
                  "path": "overlay-missing@v1.json",
                  "label_name": "overlay-missing",
                  "description": "missing local file",
                  "enabled": true
                }
              ]
            }
            "##,
        )
        .expect("write manifest for missing-file test");

        let governance =
            template_catalog_governance_command().expect("governance diagnostics should succeed");

        assert_eq!(governance.manifest_status, "ready");
        assert!(governance.issues.iter().any(|issue| {
            issue.code == "missing_template_file"
                && issue.message.contains("overlay-missing@v1")
        }));
        assert!(governance.repair_guidance.iter().any(|step: &String| {
            step.contains("save the same template_version again")
        }));

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn template_catalog_governance_reports_manifest_parse_failures_with_repair_guidance() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_TEMPLATE_OVERLAY_DIR]);
        let root = env::temp_dir().join("jan-label-template-governance-bad-manifest");
        let _ = fs::remove_dir_all(&root);
        let overlay_dir = root.join("catalog-overlays");
        let manifest_path = overlay_dir.join("template-manifest.json");
        env::set_var(ENV_TEMPLATE_OVERLAY_DIR, overlay_dir.clone());

        fs::create_dir_all(&overlay_dir).expect("create overlay dir for malformed manifest test");
        fs::write(&manifest_path, "{ not-valid-json").expect("write malformed manifest");

        let governance =
            template_catalog_governance_command().expect("governance diagnostics should succeed");

        assert_eq!(governance.manifest_status, "error");
        assert!(governance
            .issues
            .iter()
            .any(|issue| issue.code == "manifest_parse_failed"));
        assert!(governance.repair_guidance.iter().any(|step: &String| {
            step.contains("rename it out of the overlay directory")
        }));

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn export_audit_ledger_command_supports_scope_filtering() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
        ]);
        let root = env::temp_dir().join("jan-label-audit-export-command");
        let _ = fs::remove_dir_all(&root);
        let audit_log_dir = root.join("audit");
        env::set_var(ENV_AUDIT_LOG_DIR, &audit_log_dir);
        env::set_var(ENV_PRINT_OUTPUT_DIR, root.join("proofs"));
        env::set_var(ENV_SPOOL_OUTPUT_DIR, root.join("spool"));

        let store = AuditStore::new(audit_log_dir.clone());
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-DISP-EXPORT-001",
                "print",
                "2026-04-15T09:00:00Z",
                None,
            ))
            .expect("dispatch should persist");
        store
            .register_pending_proof(sample_pending_proof_record("JOB-PROOF-EXPORT-001"))
            .expect("proof should persist");
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-PROOF-EXPORT-001",
                "proof",
                "2026-04-15T09:05:00Z",
                None,
            ))
            .expect("proof dispatch should persist");

        let all_export = export_audit_ledger(None)
            .expect("audit export without scope should default to all");
        assert_eq!(all_export.scope, AuditLedgerScope::All);
        assert_eq!(all_export.dispatch_count, 2);
        assert_eq!(all_export.proof_count, 1);

        let proof_only = export_audit_ledger(Some(AuditExportRequest {
            scope: Some(AuditLedgerScope::Proof),
        }))
        .expect("proof-only export should succeed");
        assert_eq!(proof_only.scope, AuditLedgerScope::Proof);
        assert_eq!(proof_only.dispatch_count, 0);
        assert_eq!(proof_only.proof_count, 1);

        let dispatch_only = export_audit_ledger(Some(AuditExportRequest {
            scope: Some(AuditLedgerScope::Dispatch),
        }))
        .expect("dispatch-only export should succeed");
        assert_eq!(dispatch_only.scope, AuditLedgerScope::Dispatch);
        assert_eq!(dispatch_only.dispatch_count, 2);
        assert_eq!(dispatch_only.proof_count, 0);

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn list_audit_backup_bundles_returns_empty_when_none_exist() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
        ]);
        let root = env::temp_dir().join("jan-label-audit-backups-empty");
        let _ = fs::remove_dir_all(&root);
        env::set_var(ENV_AUDIT_LOG_DIR, root.join("audit"));
        env::set_var(ENV_PRINT_OUTPUT_DIR, root.join("proofs"));
        env::set_var(ENV_SPOOL_OUTPUT_DIR, root.join("spool"));

        let response = list_audit_backup_bundles().expect("list backups should succeed");
        assert!(response.is_empty());

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn list_audit_backup_bundles_includes_created_bundles_with_metadata() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
        ]);
        let root = env::temp_dir().join("jan-label-audit-backups-listing");
        let _ = fs::remove_dir_all(&root);
        let audit_log_dir = root.join("audit");
        env::set_var(ENV_AUDIT_LOG_DIR, &audit_log_dir);
        env::set_var(ENV_PRINT_OUTPUT_DIR, root.join("proofs"));
        env::set_var(ENV_SPOOL_OUTPUT_DIR, root.join("spool"));

        let store = AuditStore::new(audit_log_dir.clone());
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-DISP-BACKUP-001",
                "print",
                "2026-04-15T09:00:00Z",
                None,
            ))
            .expect("dispatch should persist");
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-DISP-BACKUP-002",
                "print",
                "2026-04-15T10:00:00Z",
                None,
            ))
            .expect("dispatch should persist");

        let applied = trim_audit_ledger(AuditRetentionRequest {
            scope: Some(audit_log::AuditLedgerScope::Dispatch),
            max_age_days: None,
            max_entries: Some(1),
            dry_run: false,
        })
        .expect("trim should generate backup bundle");
        let backup_file = applied.backup.expect("backup should be emitted");
        assert!(Path::new(&backup_file.file_path).exists());

        let response = list_audit_backup_bundles().expect("list backups should succeed");
        let observed = response
            .into_iter()
            .find(|bundle| bundle.file_name == backup_file.file_name)
            .expect("backup bundle should be listed");
        assert_eq!(observed.size_bytes, backup_file.size_bytes);
        assert!(!observed.created_at_utc.is_empty());
        assert!(Path::new(&observed.file_path).exists());

        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn trim_audit_ledger_command_respects_dry_run_and_backup() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
        ]);
        let root = env::temp_dir().join("jan-label-audit-trim-command");
        let _ = fs::remove_dir_all(&root);
        let audit_log_dir = root.join("audit");
        env::set_var(ENV_AUDIT_LOG_DIR, &audit_log_dir);
        env::set_var(ENV_PRINT_OUTPUT_DIR, root.join("proofs"));
        env::set_var(ENV_SPOOL_OUTPUT_DIR, root.join("spool"));

        let store = AuditStore::new(audit_log_dir.clone());
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-DISP-TRIM-001",
                "print",
                "2026-04-15T09:00:00Z",
                None,
            ))
            .expect("dispatch should persist");
        store
            .record_dispatch(sample_dispatch_record(
                "JOB-DISP-TRIM-002",
                "print",
                "2026-04-15T10:00:00Z",
                None,
            ))
            .expect("dispatch should persist");

        let dry_run = trim_audit_ledger(AuditRetentionRequest {
            scope: Some(AuditLedgerScope::Dispatch),
            max_age_days: None,
            max_entries: Some(1),
            dry_run: true,
        })
        .expect("dry run trim should succeed");
        assert_eq!(dry_run.removed_dispatch_count, 1);
        assert!(dry_run.backup.is_none());

        let snapshot = store.snapshot().expect("snapshot should still have two rows");
        assert_eq!(snapshot.dispatches.len(), 2);

        let applied = trim_audit_ledger(AuditRetentionRequest {
            scope: Some(AuditLedgerScope::Dispatch),
            max_age_days: None,
            max_entries: Some(1),
            dry_run: false,
        })
        .expect("trim should succeed");
        assert_eq!(applied.removed_dispatch_count, 1);
        assert!(applied.backup.is_some());
        let backup_file = applied
            .backup
            .expect("backup should be returned");
        assert!(Path::new(&backup_file.file_path).exists());
        assert!(backup_file.file_name.ends_with(".json"));

        let snapshot = store.snapshot().expect("snapshot should contain one row");
        assert_eq!(snapshot.dispatches.len(), 1);
        restore_env_vars(backup);
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn resolve_output_dir_uses_fallback_when_env_is_missing() {
        let _guard = TEST_MUTEX.lock().unwrap();
        env::remove_var(ENV_PRINT_OUTPUT_DIR);
        let fallback = PathBuf::from("fallback");
        let resolved = resolve_output_dir(ENV_PRINT_OUTPUT_DIR, fallback.clone());
        assert_eq!(resolved, fallback);
    }

    #[test]
    fn resolve_output_dir_uses_env_value_when_set() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = env::var_os(ENV_PRINT_OUTPUT_DIR);
        let custom = PathBuf::from("custom-output");
        env::set_var(ENV_PRINT_OUTPUT_DIR, &custom);
        let resolved = resolve_output_dir(ENV_PRINT_OUTPUT_DIR, PathBuf::from("fallback"));
        assert_eq!(resolved, custom);
        match backup {
            Some(value) => env::set_var(ENV_PRINT_OUTPUT_DIR, value),
            None => env::remove_var(ENV_PRINT_OUTPUT_DIR),
        }
    }

    #[test]
    fn resolve_print_adapter_defaults_to_pdf_when_unset() {
        let (adapter, warning) = resolve_print_adapter_with_warning_for_host("", true);
        assert_eq!(adapter, BridgePrintAdapter::Pdf);
        assert!(warning
            .as_ref()
            .is_some_and(|warning| warning.message.contains("defaulting to pdf")));
    }

    #[test]
    fn resolve_print_adapter_rejects_windows_spooler_on_non_windows_host() {
        let (adapter, warning) =
            resolve_print_adapter_with_warning_for_host("windows-spooler", false);
        assert_eq!(adapter, BridgePrintAdapter::Pdf);
        assert!(warning
            .as_ref()
            .is_some_and(|warning| warning.message.contains("not available on this host")));
    }

    #[test]
    fn available_adapters_follow_host_capability() {
        assert_eq!(available_adapters_for_host(false), vec!["pdf".to_string()]);
        assert_eq!(
            available_adapters_for_host(true),
            vec!["pdf".to_string(), "windows-spooler".to_string()]
        );
    }

    #[test]
    fn print_bridge_config_load_uses_safe_fallback_values() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_ALLOW_PRINT_WITHOUT_PROOF,
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
            ENV_ZINT_BINARY_PATH,
            ENV_PRINT_ADAPTER,
            ENV_WINDOWS_PRINTER_NAME,
        ]);

        env::remove_var(ENV_ALLOW_PRINT_WITHOUT_PROOF);
        env::remove_var(ENV_AUDIT_LOG_DIR);
        env::remove_var(ENV_PRINT_OUTPUT_DIR);
        env::remove_var(ENV_SPOOL_OUTPUT_DIR);
        env::remove_var(ENV_ZINT_BINARY_PATH);
        env::remove_var(ENV_PRINT_ADAPTER);
        env::remove_var(ENV_WINDOWS_PRINTER_NAME);

        let config = PrintBridgeConfig::load();
        assert_eq!(config.zint_binary_path, "zint");
        assert_eq!(config.windows_printer_name, "Default Printer");
        assert_eq!(config.print_adapter, BridgePrintAdapter::Pdf);
        assert!(!config.allow_print_without_proof);
        assert!(!config.print_output_dir.as_os_str().is_empty());
        assert!(!config.spool_output_dir.as_os_str().is_empty());

        restore_env_vars(backup);
    }

    #[test]
    fn resolve_optional_env_reads_value() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = env::var_os(ENV_ZINT_BINARY_PATH);
        env::set_var(ENV_ZINT_BINARY_PATH, "custom-zint");
        let env_value = resolve_optional_env(ENV_ZINT_BINARY_PATH);
        assert_eq!(env_value.as_deref(), Some("custom-zint"));
        match backup {
            Some(value) => env::set_var(ENV_ZINT_BINARY_PATH, value),
            None => env::remove_var(ENV_ZINT_BINARY_PATH),
        }
    }

    #[test]
    fn print_bridge_status_collects_default_warnings_with_safe_defaults() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_ALLOW_PRINT_WITHOUT_PROOF,
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
            ENV_ZINT_BINARY_PATH,
            ENV_PRINT_ADAPTER,
            ENV_WINDOWS_PRINTER_NAME,
        ]);

        env::remove_var(ENV_ALLOW_PRINT_WITHOUT_PROOF);
        env::remove_var(ENV_AUDIT_LOG_DIR);
        env::remove_var(ENV_PRINT_OUTPUT_DIR);
        env::remove_var(ENV_SPOOL_OUTPUT_DIR);
        env::remove_var(ENV_ZINT_BINARY_PATH);
        env::remove_var(ENV_PRINT_ADAPTER);
        env::remove_var(ENV_WINDOWS_PRINTER_NAME);

        let status = PrintBridgeStatus::from_environment();
        assert_eq!(status.print_adapter_kind, "pdf");
        assert_eq!(status.resolved_zint_path, "zint");
        assert_eq!(status.windows_printer_name, "Default Printer");
        assert!(!status.allow_without_proof_enabled);
        if cfg!(windows) {
            assert_eq!(
                status.available_adapters,
                vec!["pdf".to_string(), "windows-spooler".to_string()]
            );
        } else {
            assert_eq!(status.available_adapters, vec!["pdf".to_string()]);
        }
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_PRINT_OUTPUT_DIR")));
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_SPOOL_OUTPUT_DIR")));
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_ZINT_BINARY_PATH")));
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("defaulting to pdf")));
        assert!(!status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_WINDOWS_PRINTER_NAME")));

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_status_uses_env_values_without_unexpected_warnings() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_ALLOW_PRINT_WITHOUT_PROOF,
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
            ENV_ZINT_BINARY_PATH,
            ENV_PRINT_ADAPTER,
            ENV_WINDOWS_PRINTER_NAME,
        ]);

        let print_output_dir = env::temp_dir().join("jan-label-env-print-output");
        let spool_output_dir = env::temp_dir().join("jan-label-env-spool-output");
        fs::create_dir_all(&print_output_dir).expect("create print output dir for test");
        fs::create_dir_all(&spool_output_dir).expect("create spool output dir for test");
        env::set_var(ENV_PRINT_OUTPUT_DIR, &print_output_dir);
        env::set_var(ENV_SPOOL_OUTPUT_DIR, &spool_output_dir);
        env::set_var(ENV_AUDIT_LOG_DIR, env::temp_dir().join("jan-label-env-audit-output"));
        env::set_var(ENV_ZINT_BINARY_PATH, "zint");
        env::set_var(ENV_PRINT_ADAPTER, "pdf");
        env::set_var(ENV_WINDOWS_PRINTER_NAME, "Main Office Printer");
        env::remove_var(ENV_ALLOW_PRINT_WITHOUT_PROOF);

        let status = PrintBridgeStatus::from_environment();
        assert_eq!(status.print_adapter_kind, "pdf");
        assert_eq!(status.resolved_zint_path, "zint");
        assert_eq!(status.windows_printer_name, "Main Office Printer");
        assert!(!status.allow_without_proof_enabled);
        assert_eq!(
            status.proof_output_dir,
            print_output_dir.to_string_lossy().to_string()
        );
        assert_eq!(
            status.print_output_dir,
            print_output_dir.to_string_lossy().to_string()
        );
        assert_eq!(
            status.spool_output_dir,
            spool_output_dir.to_string_lossy().to_string()
        );
        assert!(!status
            .warnings
            .iter()
            .any(|warning| warning.contains("not set")));

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_status_warns_when_zint_absolute_path_missing() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_ZINT_BINARY_PATH]);

        let missing_path = env::temp_dir().join("jan-label-missing-zint-binary.exe");
        let _ = fs::remove_file(&missing_path);
        env::set_var(ENV_ZINT_BINARY_PATH, &missing_path);

        let status = PrintBridgeStatus::from_environment();
        assert!(status.warnings.iter().any(|warning| {
            warning.contains(&format!("{ENV_ZINT_BINARY_PATH} is set to absolute path"))
        }));

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_status_warns_missing_output_dirs_as_create_plan() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_PRINT_OUTPUT_DIR, ENV_SPOOL_OUTPUT_DIR]);

        let print_output_dir = env::temp_dir().join("jan-label-missing-proof-output");
        let spool_output_dir = env::temp_dir().join("jan-label-missing-spool-output");
        let _ = fs::remove_dir_all(&print_output_dir);
        let _ = fs::remove_dir_all(&spool_output_dir);

        env::set_var(ENV_PRINT_OUTPUT_DIR, &print_output_dir);
        env::set_var(ENV_SPOOL_OUTPUT_DIR, &spool_output_dir);

        let status = PrintBridgeStatus::from_environment();
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_PRINT_OUTPUT_DIR")));
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("JAN_LABEL_SPOOL_OUTPUT_DIR")));

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_status_warns_for_host_adapter_mismatch() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_PRINT_ADAPTER, ENV_WINDOWS_PRINTER_NAME]);

        env::set_var(ENV_PRINT_ADAPTER, "windows-spooler");
        env::remove_var(ENV_WINDOWS_PRINTER_NAME);

        let status = PrintBridgeStatus::from_environment();
        if cfg!(windows) {
            assert_eq!(status.print_adapter_kind, "windows-spooler");
            assert!(status
                .warnings
                .iter()
                .any(|warning| warning.contains("JAN_LABEL_WINDOWS_PRINTER_NAME")));
        } else {
            assert_eq!(status.print_adapter_kind, "pdf");
            assert!(status
                .warnings
                .iter()
                .any(|warning| warning.contains("not available on this host")));
            assert!(!status
                .warnings
                .iter()
                .any(|warning| warning.contains("JAN_LABEL_WINDOWS_PRINTER_NAME")));
        }

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_status_warns_when_allow_without_proof_is_requested() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[ENV_ALLOW_PRINT_WITHOUT_PROOF]);

        env::set_var(ENV_ALLOW_PRINT_WITHOUT_PROOF, "true");

        let status = PrintBridgeStatus::from_environment();
        assert!(!status.allow_without_proof_enabled);
        assert!(status
            .warnings
            .iter()
            .any(|warning| warning.contains("approval workflow is implemented")));

        restore_env_vars(backup);
    }

    #[test]
    fn print_bridge_config_prefers_requested_printer_profile_adapter() {
        let config = PrintBridgeConfig {
            print_output_dir: PathBuf::from("proofs"),
            spool_output_dir: PathBuf::from("spool"),
            audit_log_dir: PathBuf::from("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        let request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "line-1".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));

        let adapter = config
            .resolve_print_adapter_for_request(&request)
            .expect("printer profile pdf should be accepted");

        assert_eq!(adapter, BridgePrintAdapter::Pdf);
    }

    #[test]
    fn print_bridge_config_rejects_missing_source_proof_file() {
        let temp_root = env::temp_dir().join("jan-label-proof-check");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("missing proof artifact should block print");

        assert!(err.contains("source proof job 'JOB-20260415-PROOF'"));
    }

    #[test]
    fn print_bridge_config_allows_request_when_proof_file_exists() {
        let temp_root = env::temp_dir().join("jan-label-proof-check-existing");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"%PDF-1.4\nproof\n").expect("write proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        config
            .validate_and_normalize_request(&mut request)
            .expect("existing proof artifact should allow print");
        assert_eq!(
            request.job_lineage_id.as_deref(),
            Some("JOB-20260415-PROOF")
        );
    }

    #[test]
    fn print_bridge_config_rejects_empty_source_proof_file() {
        let temp_root = env::temp_dir().join("jan-label-proof-empty-artifact");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"").expect("write empty proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("empty proof artifact should block print");

        assert!(err.contains("empty PDF"));
    }

    #[test]
    fn print_bridge_config_rejects_non_pdf_source_proof_header() {
        let temp_root = env::temp_dir().join("jan-label-proof-invalid-header");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"not-a-pdf").expect("write invalid proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("invalid proof artifact header should block print");

        assert!(err.contains("readable PDF header"));
    }

    #[test]
    fn print_bridge_config_rejects_when_approved_proof_payload_differs() {
        let temp_root = env::temp_dir().join("jan-label-proof-check-mismatch");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"%PDF-1.4\nproof\n").expect("write proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        request.qty = 2;

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("mismatched payload should block print");

        assert!(err.contains("qty"));
    }

    #[test]
    fn print_bridge_config_rejects_explicit_lineage_mismatch_against_approved_proof() {
        let temp_root = env::temp_dir().join("jan-label-proof-lineage-mismatch");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"%PDF-1.4\nproof\n").expect("write proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        request.job_lineage_id = Some("JOB-20260415-OTHER".to_string());

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("mismatched lineage should block print");

        assert!(err.contains("approved proof lineage"));
    }

    #[test]
    fn print_bridge_config_rejects_reprint_parent_that_does_not_match_approved_proof_lineage() {
        let temp_root = env::temp_dir().join("jan-label-proof-reprint-lineage-mismatch");
        let _ = fs::remove_dir_all(&temp_root);
        let proof_dir = temp_root.join("proofs");
        fs::create_dir_all(&proof_dir).expect("create proof dir");
        let proof_path = proof_dir.join("JOB-20260415-PROOF-proof.pdf");
        fs::write(&proof_path, b"%PDF-1.4\nproof\n").expect("write proof fixture");
        let config = PrintBridgeConfig {
            print_output_dir: proof_dir,
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        config
            .record_proof_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
                &proof_path,
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-20260415-PROOF".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T10:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let mut request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        request.reprint_of_job_id = Some("JOB-20260415-PRINT-0001".to_string());

        let err = config
            .validate_and_normalize_request(&mut request)
            .expect_err("reprint parent mismatch should block print");

        assert!(err.contains("reprintOfJobId"));
    }

    #[test]
    fn print_bridge_config_preflight_rejects_unwritable_audit_store() {
        let temp_root = env::temp_dir().join("jan-label-audit-preflight");
        let _ = fs::remove_dir_all(&temp_root);
        fs::create_dir_all(&temp_root).expect("create root dir");
        let audit_file = temp_root.join("audit-file");
        fs::write(&audit_file, b"not-a-directory").expect("write blocking audit file");
        let config = PrintBridgeConfig {
            print_output_dir: temp_root.join("proofs"),
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: audit_file,
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };

        let err = config
            .ensure_audit_store_ready_for_request(&sample_proof_dispatch_request())
            .expect_err("audit preflight should fail when audit root is a file");

        assert!(err.contains("failed to create audit lock directory"));
    }

    #[test]
    fn dispatch_print_job_persists_proof_audit_records_atomically() {
        let _guard = TEST_MUTEX.lock().unwrap();
        let backup = backup_env_vars(&[
            ENV_AUDIT_LOG_DIR,
            ENV_PRINT_OUTPUT_DIR,
            ENV_SPOOL_OUTPUT_DIR,
            ENV_ZINT_BINARY_PATH,
        ]);
        let temp_root = env::temp_dir().join("jan-label-proof-dispatch-command");
        let _ = fs::remove_dir_all(&temp_root);
        fs::create_dir_all(&temp_root).expect("create temp root");

        let proof_dir = temp_root.join("proofs");
        let audit_log_dir = temp_root.join("audit");
        env::set_var(ENV_PRINT_OUTPUT_DIR, &proof_dir);
        env::set_var(ENV_SPOOL_OUTPUT_DIR, temp_root.join("spool"));
        env::set_var(ENV_AUDIT_LOG_DIR, &audit_log_dir);
        env::set_var(ENV_ZINT_BINARY_PATH, create_fake_zint(&temp_root));

        let result = dispatch_print_job(sample_proof_dispatch_request())
            .expect("proof dispatch command should succeed with fake zint");

        assert_eq!(result.mode, "proof");
        assert_eq!(result.audit.job_id, "JOB-20260415-PROOF");
        assert!(
            Path::new(&result.submission.external_job_id).exists(),
            "proof artifact should be created"
        );

        let snapshot = AuditStore::new(audit_log_dir.clone())
            .snapshot()
            .expect("audit snapshot should load");
        assert_eq!(snapshot.dispatches.len(), 1);
        assert_eq!(snapshot.proofs.len(), 1);
        assert_eq!(snapshot.proofs[0].proof_job_id.0, "JOB-20260415-PROOF");
        assert!(
            !audit_log_dir.join("proof-dispatch-transaction.json").exists(),
            "transaction marker should be cleared after dispatch"
        );

        restore_env_vars(backup);
        let _ = fs::remove_dir_all("tmp");
        let _ = fs::remove_dir_all(&temp_root);
    }

    #[test]
    fn audit_store_search_returns_proof_entries_for_session() {
        let temp_root = env::temp_dir().join("jan-label-audit-search");
        let _ = fs::remove_dir_all(&temp_root);
        let config = PrintBridgeConfig {
            print_output_dir: temp_root.join("proofs"),
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        let proof_path = config.proof_output_path("JOB-20260415-PROOF");
        fs::create_dir_all(proof_path.parent().expect("proof path parent should exist"))
            .expect("create proof dir");
        fs::write(&proof_path, b"%PDF-1.4\nproof\n").expect("write proof");
        let request = DispatchRequest {
            job_id: "JOB-20260415-PROOF".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 1,
            template_version: Some("basic-50x30@v1".to_string()),
            template: None,
            printer_profile: Some(DispatchPrinterProfile {
                id: "pdf-a4-proof".to_string(),
                adapter: "pdf".to_string(),
                paper_size: "A4".to_string(),
                dpi: 300,
                scale_policy: "fixed-100".to_string(),
            }),
            execution: Some(ExecutionIntent::Proof(print_agent::ProofExecution {
                requested_by: Some("proof.user".to_string()),
                notes: Some("review".to_string()),
                expires_at: None,
            })),
            actor_user_id: "ops.user".to_string(),
            actor_display_name: "Ops User".to_string(),
            requested_at: "2026-04-15T09:00:00Z".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: None,
            reason: None,
        };
        let result = print_agent::PrintDispatchResult {
            mode: "proof".to_string(),
            template_version: "basic-50x30@v1".to_string(),
            artifact: print_agent::PrintDispatchArtifactReport {
                media_type: "application/pdf".to_string(),
                byte_size: 128,
            },
            submission: print_agent::PrintDispatchSubmission {
                adapter_kind: "pdf".to_string(),
                external_job_id: proof_path.to_string_lossy().into_owned(),
            },
            audit: print_agent::PrintDispatchAudit {
                event: print_agent::PrintDispatchAuditEvent::Submitted,
                occurred_at: "2026-04-15T09:00:00Z".to_string(),
                job_id: "JOB-20260415-PROOF".to_string(),
                job_lineage_id: "JOB-20260415-PROOF".to_string(),
                parent_job_id: None,
                reason: None,
            },
        };

        config
            .record_proof_dispatch(&request, &result, &proof_path)
            .expect("dispatch record should persist");

        let search = config
            .audit_store()
            .search(AuditQuery {
                search_text: Some("JOB-20260415-PROOF".to_string()),
                limit: Some(10),
            })
            .expect("search should work");

        assert_eq!(search.entries.len(), 1);
        assert!(search.entries[0].proof.is_some());
    }

    #[test]
    fn resolve_print_adapter_for_host_uses_windows_spooler_only_when_supported() {
        assert_eq!(
            resolve_print_adapter_for_host("windows-spooler", true),
            BridgePrintAdapter::WindowsSpooler
        );
        assert_eq!(
            resolve_print_adapter_for_host("windows-spooler", false),
            BridgePrintAdapter::Pdf
        );
    }

    fn sample_dispatch_record(
        job_id: &str,
        mode: &str,
        occurred_at: &str,
        parent_job_id: Option<&str>,
    ) -> PersistedDispatchRecord {
        PersistedDispatchRecord {
            audit: PrintAuditRecord {
                job_id: PrintJobId(job_id.to_string()),
                job_lineage_id: PrintJobLineageId(job_id.to_string()),
                parent_job_id: parent_job_id.map(|value| PrintJobId(value.to_string())),
                actor: AuditActor {
                    user_id: "audit-bot".to_string(),
                    display_name: "Audit Bot".to_string(),
                },
                event: audit_log::AuditEventKind::Submitted,
                occurred_at: occurred_at.to_string(),
                reason: None,
            },
            mode: mode.to_string(),
            template_version: "basic-50x30@v1".to_string(),
            match_subject: DispatchMatchSubject {
                sku: "SKU-001".to_string(),
                brand: "Acme".to_string(),
                jan_normalized: "4006381333931".to_string(),
                qty: 1,
            },
            artifact_media_type: "application/pdf".to_string(),
            artifact_byte_size: 1024,
            submission_adapter_kind: "pdf".to_string(),
            submission_external_job_id: format!("{job_id}-submission"),
        }
    }

    fn sample_pending_proof_record(job_id: &str) -> ProofRecord {
        ProofRecord::pending(
            PrintJobId(job_id.to_string()),
            PrintJobLineageId(job_id.to_string()),
            AuditActor {
                user_id: "proof.user".to_string(),
                display_name: "Proof User".to_string(),
            },
            "2026-04-15T09:00:00Z".to_string(),
            format!("proofs/{job_id}-proof.pdf"),
            Some("review".to_string()),
        )
    }

    fn create_fake_zint(root: &Path) -> PathBuf {
        #[cfg(windows)]
        let path = root.join("fake-zint.cmd");
        #[cfg(not(windows))]
        let path = root.join("fake-zint.sh");

        #[cfg(windows)]
        let script = r#"@echo off
setlocal
:args_loop
if "%~1"=="" goto done
if /I "%~1"=="--output" goto write_output
shift
goto args_loop
:write_output
shift
if "%~1"=="" exit /b 1
for %%I in ("%~1") do if not exist "%%~dpI" mkdir "%%~dpI" >nul 2>nul
type nul > "%~1"
shift
goto args_loop
:done
exit /b 0
"#;
        #[cfg(not(windows))]
        let script = r#"#!/bin/sh
while [ "$#" -gt 0 ]; do
  if [ "$1" = "--output" ]; then
    shift
    if [ -z "${1:-}" ]; then
      exit 1
    fi
    mkdir -p "$(dirname "$1")"
    : > "$1"
  fi
  shift
done
exit 0
"#;

        fs::write(&path, script).expect("fake zint script should write");
        #[cfg(unix)]
        {
            let mut permissions = fs::metadata(&path)
                .expect("fake zint metadata should load")
                .permissions();
            permissions.set_mode(0o755);
            fs::set_permissions(&path, permissions)
                .expect("fake zint permissions should be executable");
        }
        path
    }

    fn backup_env_vars(keys: &[&str]) -> Vec<(String, Option<String>)> {
        keys.iter()
            .map(|key| ((*key).to_string(), env::var(key).ok()))
            .collect()
    }

    fn restore_env_vars(entries: Vec<(String, Option<String>)>) {
        for (key, value) in entries {
            match value {
                Some(saved) => env::set_var(key, saved),
                None => env::remove_var(key),
            }
        }
    }

    fn sample_dispatch_request(printer_profile: Option<DispatchPrinterProfile>) -> DispatchRequest {
        DispatchRequest {
            job_id: "JOB-20260415-0001".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 1,
            template_version: Some("basic-50x30@v1".to_string()),
            template: None,
            printer_profile,
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T10:00:00Z".to_string()),
                source_proof_job_id: Some("JOB-20260415-PROOF".to_string()),
                allow_without_proof: false,
            })),
            actor_user_id: "ops.user".to_string(),
            actor_display_name: "Ops User".to_string(),
            requested_at: "2026-04-15T09:00:00Z".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: None,
            reason: None,
        }
    }

    fn sample_proof_dispatch_request() -> DispatchRequest {
        DispatchRequest {
            job_id: "JOB-20260415-PROOF".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 1,
            template_version: Some("basic-50x30@v1".to_string()),
            template: None,
            printer_profile: Some(DispatchPrinterProfile {
                id: "pdf-a4-proof".to_string(),
                adapter: "pdf".to_string(),
                paper_size: "A4".to_string(),
                dpi: 300,
                scale_policy: "fixed-100".to_string(),
            }),
            execution: Some(ExecutionIntent::Proof(print_agent::ProofExecution {
                requested_by: Some("proof.user".to_string()),
                notes: Some("review".to_string()),
                expires_at: None,
            })),
            actor_user_id: "ops.user".to_string(),
            actor_display_name: "Ops User".to_string(),
            requested_at: "2026-04-15T09:00:00Z".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: None,
            reason: None,
        }
    }

    fn sample_proof_dispatch_result(job_id: &str) -> print_agent::PrintDispatchResult {
        print_agent::PrintDispatchResult {
            mode: "proof".to_string(),
            template_version: "basic-50x30@v1".to_string(),
            artifact: print_agent::PrintDispatchArtifactReport {
                media_type: "application/pdf".to_string(),
                byte_size: 128,
            },
            submission: print_agent::PrintDispatchSubmission {
                adapter_kind: "pdf".to_string(),
                external_job_id: format!("{job_id}-submission"),
            },
            audit: print_agent::PrintDispatchAudit {
                event: print_agent::PrintDispatchAuditEvent::Submitted,
                occurred_at: "2026-04-15T09:00:00Z".to_string(),
                job_id: job_id.to_string(),
                job_lineage_id: job_id.to_string(),
                parent_job_id: None,
                reason: None,
            },
        }
    }
}
