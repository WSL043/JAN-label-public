#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod audit_store;

use std::env;
use std::path::{Path, PathBuf};

use audit_log::{
    AuditActor, AuditQuery, AuditSearchResult, DispatchMatchSubject, PersistedDispatchRecord,
    ProofRecord, PrintJobId, PrintJobLineageId,
};
use audit_store::{AuditStore, ProofReviewRequest};
use barcode::ZintCli;
use domain::{Jan, JanError};
use print_agent::{DispatchRequest, ExecutionIntent, PrintAgent, PrintAgentPolicy, PrintDispatchResult};
use printer_adapters::{PdfFileAdapter, WindowsSpoolerAdapter};
use serde::Serialize;
use tauri::command;

const ENV_PRINT_OUTPUT_DIR: &str = "JAN_LABEL_PRINT_OUTPUT_DIR";
const ENV_SPOOL_OUTPUT_DIR: &str = "JAN_LABEL_SPOOL_OUTPUT_DIR";
const ENV_ZINT_BINARY_PATH: &str = "JAN_LABEL_ZINT_BINARY_PATH";
const ENV_PRINT_ADAPTER: &str = "JAN_LABEL_PRINT_ADAPTER";
const ENV_WINDOWS_PRINTER_NAME: &str = "JAN_LABEL_WINDOWS_PRINTER_NAME";
const ENV_ALLOW_PRINT_WITHOUT_PROOF: &str = "JAN_LABEL_ALLOW_PRINT_WITHOUT_PROOF";
const ENV_AUDIT_LOG_DIR: &str = "JAN_LABEL_AUDIT_LOG_DIR";

const DEFAULT_ZINT_BINARY_PATH: &str = "zint";
const DEFAULT_WINDOWS_PRINTER_NAME: &str = "Default Printer";

#[command]
fn dispatch_print_job(request: DispatchRequest) -> Result<PrintDispatchResult, String> {
    let config = PrintBridgeConfig::load();
    let is_proof = matches!(request.execution.as_ref(), Some(ExecutionIntent::Proof(_)));
    let job_id = request.job_id.clone();
    config.validate_request(&request)?;
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
        config.record_dispatch(&request_for_audit, &result)?;
        config.register_pending_proof(&request_for_audit, &proof_output_path)?;
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
            if let Err(error) = config.record_dispatch(&request_for_audit, &result) {
                log_non_fatal_audit_error("print dispatch", &result.audit.job_id, &error);
            }
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
            if let Err(error) = config.record_dispatch(&request_for_audit, &result) {
                log_non_fatal_audit_error("print dispatch", &result.audit.job_id, &error);
            }
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
fn approve_proof(request: ProofReviewRequest) -> Result<ProofRecord, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().approve_proof(request)
}

#[command]
fn reject_proof(request: ProofReviewRequest) -> Result<ProofRecord, String> {
    let config = PrintBridgeConfig::load();
    config.audit_store().reject_proof(request)
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
    print_adapter_kind: String,
    windows_printer_name: String,
    allow_without_proof_enabled: bool,
    warnings: Vec<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum BridgePrintAdapter {
    Pdf,
    WindowsSpooler,
}

impl BridgePrintAdapter {
    fn kind(self) -> &'static str {
        match self {
            Self::Pdf => "pdf",
            Self::WindowsSpooler => "windows-spooler",
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
        }
    }

    fn audit_store(&self) -> AuditStore {
        AuditStore::new(self.audit_log_dir.clone())
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

    fn validate_request(&self, request: &DispatchRequest) -> Result<(), String> {
        let Some(ExecutionIntent::Print(print)) = request.execution.as_ref() else {
            return Ok(());
        };

        if print.allow_without_proof && !self.allow_print_without_proof {
            return Err(format!(
                "{ENV_ALLOW_PRINT_WITHOUT_PROOF} is not enabled; print execution cannot bypass proof"
            ));
        }

        if print.allow_without_proof {
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
        let proof_output_path = self.proof_output_path(source_proof_job_id);
        if !proof_output_path.exists() {
            return Err(format!(
                "source proof job '{}' was not found at '{}'",
                source_proof_job_id,
                proof_output_path.display()
            ));
        }
        let expected_template_version = resolve_template_version_for_request(request)?;
        let expected_subject = build_dispatch_match_subject(request)?;
        self.audit_store().ensure_approved_proof_matches(
            source_proof_job_id,
            &expected_template_version,
            &expected_subject,
        )?;

        Ok(())
    }

    fn record_dispatch(
        &self,
        request: &DispatchRequest,
        result: &PrintDispatchResult,
    ) -> Result<(), String> {
        let reason = request
            .reason
            .as_deref()
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .map(str::to_string)
            .or_else(|| result.audit.reason.clone());
        self.audit_store().record_dispatch(PersistedDispatchRecord {
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

    fn register_pending_proof(
        &self,
        request: &DispatchRequest,
        artifact_path: &Path,
    ) -> Result<(), String> {
        let Some(ExecutionIntent::Proof(execution)) = request.execution.as_ref() else {
            return Ok(());
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

        self.audit_store().register_pending_proof(ProofRecord::pending(
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
        let mut warnings = Vec::new();

        let (print_output_dir, print_output_fallback_warning) = resolve_output_dir_with_warning(
            ENV_PRINT_OUTPUT_DIR,
            env::temp_dir().join("jan-label").join("proofs"),
        );
        report_output_dir_warning(
            ENV_PRINT_OUTPUT_DIR,
            &print_output_dir,
            print_output_fallback_warning.is_some(),
            &mut warnings,
        );
        if let Some(message) = print_output_fallback_warning {
            warnings.push(message);
        }

        let (spool_output_dir, spool_output_fallback_warning) = resolve_output_dir_with_warning(
            ENV_SPOOL_OUTPUT_DIR,
            env::temp_dir().join("jan-label").join("spool"),
        );
        report_output_dir_warning(
            ENV_SPOOL_OUTPUT_DIR,
            &spool_output_dir,
            spool_output_fallback_warning.is_some(),
            &mut warnings,
        );
        if let Some(message) = spool_output_fallback_warning {
            warnings.push(message);
        }

        let (zint_binary_path, zint_warning) =
            resolve_zint_binary_path_with_warning(ENV_ZINT_BINARY_PATH, DEFAULT_ZINT_BINARY_PATH);
        report_zint_warning(&zint_binary_path, &mut warnings);
        if let Some(message) = zint_warning {
            warnings.push(message);
        }

        let print_adapter_raw = resolve_optional_env(ENV_PRINT_ADAPTER).unwrap_or_default();
        let (print_adapter, adapter_warning) = resolve_print_adapter_with_warning_for_host(
            &print_adapter_raw,
            host_supports_windows_spooler,
        );
        if let Some(message) = adapter_warning {
            warnings.push(message);
        }

        let (windows_printer_name, printer_warning) = resolve_windows_printer_name_with_warning(
            ENV_WINDOWS_PRINTER_NAME,
            DEFAULT_WINDOWS_PRINTER_NAME,
        );
        let is_fallback_printer_name = printer_warning.is_some();
        let allow_print_without_proof_requested = resolve_bool_env(ENV_ALLOW_PRINT_WITHOUT_PROOF);
        if print_adapter == BridgePrintAdapter::WindowsSpooler {
            if let Some(message) = printer_warning {
                warnings.push(message);
            }
            if is_fallback_printer_name {
                warnings.push(
                    "JAN_LABEL_WINDOWS_PRINTER_NAME is not set or empty; using fallback 'Default Printer' while windows-spooler is selected".to_string(),
                );
            }
        }
        if allow_print_without_proof_requested {
            warnings.push(
                "JAN_LABEL_ALLOW_PRINT_WITHOUT_PROOF is set, but allowWithoutProof stays disabled until proof approval workflow is implemented".to_string(),
            );
        }

        Self {
            available_adapters: available_adapters_for_host(host_supports_windows_spooler),
            resolved_zint_path: zint_binary_path,
            proof_output_dir: print_output_dir.to_string_lossy().into_owned(),
            print_output_dir: print_output_dir.to_string_lossy().into_owned(),
            spool_output_dir: spool_output_dir.to_string_lossy().into_owned(),
            print_adapter_kind: print_adapter.kind().to_string(),
            windows_printer_name,
            allow_without_proof_enabled: false,
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
) -> (BridgePrintAdapter, Option<String>) {
    let normalized = raw.trim().to_ascii_lowercase();
    let default_adapter = default_print_adapter();
    let default_kind = default_adapter.kind();

    if normalized.is_empty() {
        return (
            default_adapter,
            Some(format!(
                "{ENV_PRINT_ADAPTER} is not set; defaulting to {default_kind}"
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
                    Some(format!(
                        "{ENV_PRINT_ADAPTER}='{raw}' is not available on this host; defaulting to {default_kind}"
                    )),
                )
            }
        }
        _ => (
            default_adapter,
            Some(format!(
                "{ENV_PRINT_ADAPTER}='{raw}' is unsupported; defaulting to {default_kind}"
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

fn resolve_output_dir_with_warning(env_key: &str, fallback: PathBuf) -> (PathBuf, Option<String>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (PathBuf::from(value), None),
        _ => (
            fallback.clone(),
            Some(format!(
                "{env_key} is not set; defaulting to {}",
                fallback.display()
            )),
        ),
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

fn log_non_fatal_audit_error(context: &str, job_id: &str, error: &str) {
    eprintln!("warning: {context} for job '{job_id}' completed, but audit persistence failed: {error}");
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
) -> (String, Option<String>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (value, None),
        _ => (
            fallback.to_string(),
            Some(format!("{env_key} is not set; defaulting to {fallback}")),
        ),
    }
}

fn report_zint_warning(path: &str, warnings: &mut Vec<String>) {
    if Path::new(path).is_absolute() && !Path::new(path).exists() {
        warnings.push(format!(
            "{ENV_ZINT_BINARY_PATH} is set to absolute path '{path}', but the file does not exist"
        ));
    }
}

fn report_output_dir_warning(
    env_key: &str,
    dir: &Path,
    from_fallback: bool,
    warnings: &mut Vec<String>,
) {
    if dir.exists() {
        return;
    }

    if from_fallback {
        warnings.push(format!(
            "{env_key} is not set; defaulting to {}; directory does not exist and will be created on demand",
            dir.display()
        ));
    } else {
        warnings.push(format!(
            "{env_key} is set to {} but does not exist; it will be created on demand",
            dir.display()
        ));
    }
}

fn resolve_windows_printer_name_with_warning(
    env_key: &str,
    fallback: &str,
) -> (String, Option<String>) {
    match resolve_optional_env(env_key).map(|value| value.trim().to_string()) {
        Some(value) if !value.is_empty() => (value, None),
        _ => (
            fallback.to_string(),
            Some(format!("{env_key} is not set; defaulting to '{fallback}'")),
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

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            dispatch_print_job,
            print_bridge_status,
            search_audit_log,
            approve_proof,
            reject_proof
        ])
        .run(tauri::generate_context!())
        .expect("failed to run desktop shell");
}

#[cfg(test)]
mod tests {
    use super::{
        available_adapters_for_host, resolve_optional_env, resolve_output_dir,
        resolve_print_adapter_for_host, resolve_print_adapter_with_warning_for_host,
        sanitize_path_component, BridgePrintAdapter, PrintBridgeConfig, PrintBridgeStatus,
        ENV_ALLOW_PRINT_WITHOUT_PROOF, ENV_AUDIT_LOG_DIR, ENV_PRINT_ADAPTER, ENV_PRINT_OUTPUT_DIR,
        ENV_SPOOL_OUTPUT_DIR, ENV_WINDOWS_PRINTER_NAME, ENV_ZINT_BINARY_PATH,
    };
    use crate::audit_store::ProofReviewRequest;
    use audit_log::{AuditQuery, ProofRecord, PrintJobId, PrintJobLineageId};
    use print_agent::{DispatchPrinterProfile, DispatchRequest, ExecutionIntent, PrintExecution};
    use std::{env, fs, path::PathBuf, sync::Mutex};

    static TEST_MUTEX: Mutex<()> = Mutex::new(());

    #[test]
    fn sanitize_path_component_replaces_path_controls() {
        let sanitized = sanitize_path_component("A/B\\C\0D");
        assert_eq!(sanitized, "A_B_C_D");
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
            .as_deref()
            .is_some_and(|message| message.contains("defaulting to pdf")));
    }

    #[test]
    fn resolve_print_adapter_rejects_windows_spooler_on_non_windows_host() {
        let (adapter, warning) =
            resolve_print_adapter_with_warning_for_host("windows-spooler", false);
        assert_eq!(adapter, BridgePrintAdapter::Pdf);
        assert!(warning
            .as_deref()
            .is_some_and(|message| message.contains("not available on this host")));
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
        let config = PrintBridgeConfig {
            print_output_dir: temp_root.join("proofs"),
            spool_output_dir: temp_root.join("spool"),
            audit_log_dir: temp_root.join("audit"),
            zint_binary_path: "zint".to_string(),
            print_adapter: BridgePrintAdapter::Pdf,
            windows_printer_name: "Default Printer".to_string(),
            allow_print_without_proof: false,
        };
        let request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));

        let err = config
            .validate_request(&request)
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
        let request = sample_dispatch_request(Some(DispatchPrinterProfile {
            id: "pdf-a4-proof".to_string(),
            adapter: "pdf".to_string(),
            paper_size: "A4".to_string(),
            dpi: 300,
            scale_policy: "fixed-100".to_string(),
        }));
        config
            .record_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .register_pending_proof(sample_pending_proof("JOB-20260415-PROOF", &proof_path))
            .expect("pending proof should persist");
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
            .validate_request(&request)
            .expect("existing proof artifact should allow print");
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
            .record_dispatch(
                &sample_proof_dispatch_request(),
                &sample_proof_dispatch_result("JOB-20260415-PROOF"),
            )
            .expect("proof dispatch record should persist");
        config
            .audit_store()
            .register_pending_proof(sample_pending_proof("JOB-20260415-PROOF", &proof_path))
            .expect("pending proof should persist");
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
            .validate_request(&request)
            .expect_err("mismatched payload should block print");

        assert!(err.contains("qty"));
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
            .record_dispatch(&request, &result)
            .expect("dispatch record should persist");
        config
            .register_pending_proof(&request, &proof_path)
            .expect("proof record should persist");

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

    fn sample_pending_proof(job_id: &str, proof_path: &std::path::Path) -> ProofRecord {
        ProofRecord::pending(
            PrintJobId(job_id.to_string()),
            PrintJobLineageId(job_id.to_string()),
            audit_log::AuditActor {
                user_id: "proof.user".to_string(),
                display_name: "Proof User".to_string(),
            },
            "2026-04-15T09:00:00Z".to_string(),
            proof_path.to_string_lossy().into_owned(),
            Some("review".to_string()),
        )
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
