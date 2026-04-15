use audit_log::{AuditActor, PrintAuditRecord, PrintJobId, PrintJobLineageId};
use barcode::{BarcodeEngine, BarcodeRequest};
use domain::{Jan, JanError};
use printer_adapters::{PrintArtifact, PrinterAdapter, PrinterAdapterKind};
use render::{render_pdf, render_svg, RenderLabelRequest};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintDispatchArtifactReport {
    pub media_type: String,
    pub byte_size: usize,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintDispatchSubmission {
    pub adapter_kind: String,
    pub external_job_id: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintDispatchAudit {
    pub event: PrintDispatchAuditEvent,
    pub occurred_at: String,
    pub job_id: String,
    pub job_lineage_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub parent_job_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reason: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintDispatchResult {
    pub mode: String,
    pub template_version: String,
    pub artifact: PrintDispatchArtifactReport,
    pub submission: PrintDispatchSubmission,
    pub audit: PrintDispatchAudit,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct LabelTemplateRef {
    pub id: String,
    pub version: String,
}

impl LabelTemplateRef {
    fn to_template_version(&self) -> Result<String, String> {
        let id = require_non_empty("template.id", &self.id)?;
        let version = require_non_empty("template.version", &self.version)?;
        Ok(format!("{id}@{version}"))
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DispatchPrinterProfile {
    pub id: String,
    pub adapter: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProofExecution {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub requested_by: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub expires_at: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintExecution {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub approved_by: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub approved_at: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub source_proof_job_id: Option<String>,
    #[serde(default)]
    pub allow_without_proof: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "mode", rename_all = "lowercase")]
pub enum ExecutionIntent {
    Proof(ProofExecution),
    Print(PrintExecution),
}

impl Default for ExecutionIntent {
    fn default() -> Self {
        Self::Print(PrintExecution {
            approved_by: None,
            approved_at: None,
            source_proof_job_id: None,
            allow_without_proof: false,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DispatchRequest {
    pub job_id: String,
    pub sku: String,
    pub brand: String,
    pub jan: String,
    pub qty: u32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub template_version: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub template: Option<LabelTemplateRef>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub printer_profile: Option<DispatchPrinterProfile>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub execution: Option<ExecutionIntent>,
    pub actor_user_id: String,
    pub actor_display_name: String,
    pub requested_at: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub job_lineage_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reprint_of_job_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reason: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum PrintDispatchAuditEvent {
    Submitted,
    Reprinted,
    Created,
    Completed,
    Failed,
}

pub struct PrintAgent<A, B>
where
    A: PrinterAdapter,
    B: BarcodeEngine,
{
    adapter: A,
    barcode_engine: B,
    policy: PrintAgentPolicy,
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct PrintAgentPolicy {
    pub allow_print_without_proof: bool,
}

impl<A, B> PrintAgent<A, B>
where
    A: PrinterAdapter,
    B: BarcodeEngine,
{
    pub fn new(adapter: A, barcode_engine: B) -> Self {
        Self {
            adapter,
            barcode_engine,
            policy: PrintAgentPolicy::default(),
        }
    }

    pub fn with_policy(mut self, policy: PrintAgentPolicy) -> Self {
        self.policy = policy;
        self
    }

    pub fn dispatch(&self, request: DispatchRequest) -> Result<PrintDispatchResult, String> {
        let DispatchRequest {
            job_id,
            sku,
            brand,
            jan,
            qty,
            template_version,
            template,
            printer_profile: _,
            execution,
            actor_user_id,
            actor_display_name,
            requested_at,
            job_lineage_id,
            reprint_of_job_id,
            reason,
        } = request;

        let job_id = require_non_empty("job_id", &job_id)?;
        let sku = require_non_empty("sku", &sku)?;
        let brand = require_non_empty("brand", &brand)?;
        let actor_user_id = require_non_empty("actor_user_id", &actor_user_id)?;
        let actor_display_name = require_non_empty("actor_display_name", &actor_display_name)?;
        let requested_at = require_non_empty("requested_at", &requested_at)?;
        let template_version = resolve_template_version(template_version, template)?;
        let execution = execution.unwrap_or_default();

        if qty == 0 {
            return Err("qty must be greater than or equal to 1".to_string());
        }

        let jan = Jan::parse(&jan).map_err(jan_error_message)?;
        if has_path_separator(&job_id) {
            return Err("job_id must not contain path separators".to_string());
        }

        validate_execution(&execution, &self.policy)?;

        let lineage_id = optional_trimmed_non_empty(job_lineage_id)
            .or_else(|| optional_trimmed_non_empty(reprint_of_job_id.clone()))
            .unwrap_or_else(|| job_id.clone());

        let mode = execution_mode_as_string(&execution);
        let job_lineage_id = PrintJobLineageId(lineage_id.clone());
        let parent_job_id = optional_trimmed_non_empty(reprint_of_job_id).map(PrintJobId);
        let reason = optional_trimmed_non_empty(reason);

        let adapter_kind = self.adapter.kind();
        self.barcode_engine
            .render(&BarcodeRequest {
                jan: jan.as_str().to_string(),
                format: barcode::BarcodeFormat::Jan13,
                output_path: format!("tmp/{job_id}.svg"),
            })
            .map_err(|err| err.message)?;

        let render_request = RenderLabelRequest {
            job_id: job_id.clone(),
            sku,
            brand,
            jan: jan.as_str().to_string(),
            qty,
            template_version: template_version.clone(),
        };

        let artifact = build_artifact(&execution, &adapter_kind, &render_request)?;
        let artifact_report = PrintDispatchArtifactReport {
            media_type: artifact.media_type.clone(),
            byte_size: artifact.bytes.len(),
        };

        let submission = self.adapter.submit(&artifact).map_err(|err| err.message)?;
        let submission_report = PrintDispatchSubmission {
            adapter_kind: adapter_kind_as_string(&submission.adapter_kind),
            external_job_id: submission.external_job_id,
        };

        let audit_record = PrintAuditRecord::dispatch(
            PrintJobId(job_id),
            job_lineage_id,
            parent_job_id,
            AuditActor {
                user_id: actor_user_id,
                display_name: actor_display_name,
            },
            requested_at,
            reason,
        );

        Ok(PrintDispatchResult {
            mode: mode.to_string(),
            template_version,
            artifact: artifact_report,
            submission: submission_report,
            audit: PrintDispatchAudit {
                event: audit_event_name(&audit_record.event),
                occurred_at: audit_record.occurred_at,
                job_id: audit_record.job_id.0,
                job_lineage_id: audit_record.job_lineage_id.0,
                parent_job_id: audit_record.parent_job_id.map(|value| value.0),
                reason: audit_record.reason,
            },
        })
    }
}

fn has_path_separator(job_id: &str) -> bool {
    job_id.contains('/') || job_id.contains('\\')
}

fn resolve_template_version(
    template_version: Option<String>,
    template: Option<LabelTemplateRef>,
) -> Result<String, String> {
    let template_version = template_version
        .map(|value| require_non_empty("template_version", &value))
        .transpose()?;
    let template_version_from_ref = template
        .as_ref()
        .map(LabelTemplateRef::to_template_version)
        .transpose()?;

    match (template_version, template_version_from_ref) {
        (Some(value), Some(resolved)) if value != resolved => Err(
            "template_version and template id/version must refer to the same template".to_string(),
        ),
        (Some(value), Some(_)) => Ok(value),
        (Some(value), None) => Ok(value),
        (None, Some(resolved)) => Ok(resolved),
        (None, None) => Err(
            "template must include either template_version or template { id, version }".to_string(),
        ),
    }
}

fn validate_execution(
    execution: &ExecutionIntent,
    policy: &PrintAgentPolicy,
) -> Result<(), String> {
    match execution {
        ExecutionIntent::Proof(proof) => {
            if let Some(requested_by) = &proof.requested_by {
                require_non_empty("execution.requested_by", requested_by)?;
            }
            if let Some(expires_at) = &proof.expires_at {
                require_non_empty("execution.expires_at", expires_at)?;
            }
            Ok(())
        }
        ExecutionIntent::Print(print) => {
            if print.approved_by.is_none() && print.approved_at.is_some() {
                return Err(
                    "execution.approved_at cannot be set without execution.approved_by".to_string(),
                );
            }

            let approved_by = print.approved_by.as_ref().ok_or_else(|| {
                "execution.approvedBy is required for print execution".to_string()
            })?;
            require_non_empty("execution.approved_by", approved_by)?;

            let approved_at = print.approved_at.as_ref().ok_or_else(|| {
                "execution.approvedAt is required for print execution".to_string()
            })?;
            require_non_empty("execution.approved_at", approved_at)?;

            if print.allow_without_proof && !policy.allow_print_without_proof {
                return Err(
                    "execution.allowWithoutProof is disabled by current print policy".to_string(),
                );
            }

            if !print.allow_without_proof && print.source_proof_job_id.is_none() {
                return Err(
                    "execution.sourceProofJobId is required when source proof gate is enforced"
                        .to_string(),
                );
            }

            if let Some(source_proof_job_id) = &print.source_proof_job_id {
                let source_proof_job_id =
                    require_non_empty("execution.source_proof_job_id", source_proof_job_id)?;
                if has_path_separator(&source_proof_job_id) {
                    return Err(
                        "execution.sourceProofJobId must not contain path separators".to_string(),
                    );
                }
            }

            Ok(())
        }
    }
}

fn build_artifact(
    execution: &ExecutionIntent,
    adapter_kind: &PrinterAdapterKind,
    render_request: &RenderLabelRequest,
) -> Result<PrintArtifact, String> {
    let is_proof = matches!(execution, ExecutionIntent::Proof(_));

    match (is_proof, adapter_kind) {
        (true, PrinterAdapterKind::Pdf) => Ok(PrintArtifact {
            media_type: "application/pdf".to_string(),
            bytes: render_pdf(render_request).map_err(|error| error.to_string())?,
        }),
        (true, _) => Err("proof execution currently requires PDF adapter".to_string()),
        (false, PrinterAdapterKind::Pdf) => Ok(PrintArtifact {
            media_type: "application/pdf".to_string(),
            bytes: render_pdf(render_request).map_err(|error| error.to_string())?,
        }),
        (false, PrinterAdapterKind::WindowsSpooler) => Ok(PrintArtifact {
            media_type: "image/svg+xml".to_string(),
            bytes: render_svg(render_request)
                .map_err(|error| error.to_string())?
                .into_bytes(),
        }),
        (false, unsupported) => Err(format!("unsupported printer adapter '{unsupported:?}'")),
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

fn require_non_empty(field_name: &str, value: &str) -> Result<String, String> {
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
        JanError::NonDigit => "jan must contain digits only".to_string(),
        JanError::InvalidLength { actual } => {
            format!("jan must be 12 or 13 digits, got {actual}")
        }
        JanError::InvalidChecksum { expected, actual } => {
            format!("jan checksum invalid: expected {expected}, got {actual}")
        }
    }
}

fn execution_mode_as_string(execution: &ExecutionIntent) -> &'static str {
    match execution {
        ExecutionIntent::Proof(_) => "proof",
        ExecutionIntent::Print(_) => "print",
    }
}

fn adapter_kind_as_string(kind: &PrinterAdapterKind) -> String {
    match kind {
        PrinterAdapterKind::Pdf => "pdf".to_string(),
        PrinterAdapterKind::WindowsSpooler => "windows-spooler".to_string(),
        PrinterAdapterKind::Zpl => "zpl".to_string(),
        PrinterAdapterKind::Tspl => "tspl".to_string(),
        PrinterAdapterKind::Qz => "qz".to_string(),
    }
}

fn audit_event_name(event: &audit_log::AuditEventKind) -> PrintDispatchAuditEvent {
    match event {
        audit_log::AuditEventKind::Submitted => PrintDispatchAuditEvent::Submitted,
        audit_log::AuditEventKind::Reprinted => PrintDispatchAuditEvent::Reprinted,
        audit_log::AuditEventKind::Created => PrintDispatchAuditEvent::Created,
        audit_log::AuditEventKind::Completed => PrintDispatchAuditEvent::Completed,
        audit_log::AuditEventKind::Failed => PrintDispatchAuditEvent::Failed,
    }
}

#[cfg(test)]
mod tests {
    use super::{
        DispatchPrinterProfile, DispatchRequest, ExecutionIntent, LabelTemplateRef, PrintAgent,
        PrintAgentPolicy, PrintDispatchAuditEvent, PrintExecution, ProofExecution,
    };
    use audit_log::{PrintJobId, PrintJobLineageId};
    use barcode::{BarcodeArtifact, BarcodeEngine, BarcodeError, BarcodeFormat, BarcodeRequest};
    use printer_adapters::{
        AdapterError, PrintArtifact, PrinterAdapter, PrinterAdapterKind, SubmissionReceipt,
    };
    use serde_json::{json, Value};
    use std::sync::{Arc, Mutex};

    #[test]
    fn dispatch_records_submitted_event_for_original_job() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter.clone(), barcode_engine);

        let record = agent
            .dispatch(sample_request())
            .expect("original dispatch should succeed");
        let submission = adapter.last_submission();

        assert_eq!(record.audit.event, PrintDispatchAuditEvent::Submitted);
        assert_eq!(record.audit.parent_job_id, None);
        assert_eq!(record.audit.reason, None);
        assert_eq!(
            record.audit.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string()).0
        );
        assert_eq!(record.mode, "print");
        assert_eq!(record.template_version, "basic-50x30@v1");
        assert_eq!(record.artifact.media_type, "application/pdf");
        assert_eq!(record.artifact.byte_size, submission.bytes.len());
        assert_eq!(record.submission.adapter_kind, "pdf");
        assert_eq!(record.submission.external_job_id, "proof-0001");
        assert_eq!(record.audit.event, PrintDispatchAuditEvent::Submitted);
        assert!(
            submission.bytes.starts_with(b"%PDF-1.4"),
            "pdf proof output should be rendered as a PDF artifact"
        );
    }

    #[test]
    fn dispatch_result_serializes_to_camel_case_payload() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);

        let result = agent
            .dispatch(sample_request())
            .expect("dispatch should emit result payload");

        let payload: Value = serde_json::to_value(&result).expect("result should serialize");

        assert!(payload.get("mode").is_some());
        assert!(payload.get("templateVersion").is_some());
        assert!(payload.get("template_version").is_none());
        assert!(payload.get("artifact").and_then(Value::as_object).is_some());
        let artifact = payload.get("artifact").expect("artifact should be present");
        assert!(artifact.get("mediaType").is_some());
        assert!(artifact.get("byteSize").is_some());
        assert!(artifact.get("media_type").is_none());
        assert!(artifact.get("byte_size").is_none());
        let submission = payload
            .get("submission")
            .expect("submission should be present");
        assert!(submission.get("adapterKind").is_some());
        assert!(submission.get("externalJobId").is_some());
        assert!(submission.get("adapter_kind").is_none());
        assert!(submission.get("external_job_id").is_none());
        let audit = payload.get("audit").expect("audit should be present");
        assert!(audit.get("jobId").is_some());
        assert!(audit.get("jobLineageId").is_some());
        assert!(audit.get("occurredAt").is_some());
        assert_eq!(audit.get("event"), Some(&json!("submitted")));
        assert!(audit.get("event").is_some());
    }

    #[test]
    fn dispatch_request_serializes_execution_as_mode_payload() {
        let request = DispatchRequest {
            printer_profile: Some(DispatchPrinterProfile {
                id: "pdf-a4-proof".to_string(),
                adapter: "pdf".to_string(),
            }),
            execution: Some(ExecutionIntent::Proof(ProofExecution {
                requested_by: Some("proof.user".to_string()),
                notes: Some("quick check".to_string()),
                expires_at: Some("2026-04-16T00:00:00Z".to_string()),
            })),
            ..sample_request()
        };

        let payload: Value = serde_json::to_value(&request).expect("request should serialize");
        assert!(payload
            .get("execution")
            .and_then(Value::as_object)
            .is_some());
        let execution = payload
            .get("execution")
            .expect("execution should be present");
        assert_eq!(execution.get("mode"), Some(&json!("proof")));
        assert_eq!(execution.get("requestedBy"), Some(&json!("proof.user")));
        assert_eq!(execution.get("notes"), Some(&json!("quick check")));
        assert_eq!(
            payload.get("templateVersion"),
            Some(&json!("basic-50x30@v1"))
        );
        assert_eq!(
            payload
                .get("printerProfile")
                .and_then(Value::as_object)
                .and_then(|value| value.get("adapter")),
            Some(&json!("pdf"))
        );
        assert!(payload.get("template_version").is_none());
    }

    #[test]
    fn dispatch_request_parses_print_execution_without_allow_without_proof_field() {
        let payload = serde_json::json!({
            "jobId": "JOB-20260415-0002",
            "sku": "SKU-0001",
            "jan": "4006381333931",
            "qty": 1,
            "brand": "Acme",
            "templateVersion": "basic-50x30@v1",
            "printerProfile": {
                "id": "pdf-a4-proof",
                "adapter": "pdf"
            },
            "execution": {
                "mode": "print",
                "approvedBy": "manager.user",
                "approvedAt": "2026-04-15T10:00:00Z",
                "sourceProofJobId": "JOB-20260415-PROOF"
            },
            "actorUserId": "ops.user",
            "actorDisplayName": "Ops User",
            "requestedAt": "2026-04-15T09:00:00Z"
        });

        let request: DispatchRequest =
            serde_json::from_value(payload).expect("request should parse from camelCase payload");

        let ExecutionIntent::Print(execution) = request
            .execution
            .expect("execution should be present and printed")
        else {
            panic!("execution mode should be print");
        };

        assert_eq!(execution.approved_by.as_deref(), Some("manager.user"));
        assert!(!execution.allow_without_proof);
        assert_eq!(
            request.printer_profile,
            Some(DispatchPrinterProfile {
                id: "pdf-a4-proof".to_string(),
                adapter: "pdf".to_string(),
            })
        );
    }

    #[test]
    fn dispatch_accepts_template_reference_object() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);

        let mut request = sample_request();
        request.template_version = None;
        request.template = Some(LabelTemplateRef {
            id: "basic-50x30".to_string(),
            version: "v1".to_string(),
        });

        let result = agent
            .dispatch(request)
            .expect("template object should be accepted");

        assert_eq!(
            result.audit.job_id,
            PrintJobId("JOB-20260415-0001".to_string()).0
        );
    }

    #[test]
    fn dispatch_rejects_missing_template_reference() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.template_version = None;
        request.template = None;

        let err = agent
            .dispatch(request)
            .expect_err("template reference is required");

        assert_eq!(
            err,
            "template must include either template_version or template { id, version }"
        );
    }

    #[test]
    fn dispatch_rejects_template_reference_mismatch() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.template = Some(LabelTemplateRef {
            id: "basic-50x30".to_string(),
            version: "v1".to_string(),
        });

        request.template_version = Some("basic-50x30@v2".to_string());

        let err = agent
            .dispatch(request)
            .expect_err("template mismatch should be rejected");

        assert_eq!(
            err,
            "template_version and template id/version must refer to the same template"
        );
    }

    #[test]
    fn dispatch_rejects_proof_execution_with_non_pdf_adapter() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::WindowsSpooler);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.execution = Some(ExecutionIntent::Proof(ProofExecution {
            requested_by: Some("ops.user".to_string()),
            notes: Some("preprint check".to_string()),
            expires_at: None,
        }));

        let err = agent
            .dispatch(request)
            .expect_err("proof execution should require PDF adapter");

        assert_eq!(err, "proof execution currently requires PDF adapter");
    }

    #[test]
    fn dispatch_reports_proof_mode_with_artifact_and_submission_metadata() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.execution = Some(ExecutionIntent::Proof(ProofExecution {
            requested_by: Some("proof.user".to_string()),
            notes: Some("check".to_string()),
            expires_at: Some("2026-04-16T00:00:00Z".to_string()),
        }));

        let result = agent
            .dispatch(request)
            .expect("proof execution should emit proof result metadata");

        assert_eq!(result.mode, "proof");
        assert_eq!(result.template_version, "basic-50x30@v1");
        assert_eq!(result.artifact.media_type, "application/pdf");
        assert_eq!(result.submission.adapter_kind, "pdf");
        assert_eq!(result.submission.external_job_id, "proof-0001");
        assert_eq!(result.audit.event, PrintDispatchAuditEvent::Submitted);
    }

    #[test]
    fn dispatch_rejects_proof_execution_with_empty_requested_by() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.execution = Some(ExecutionIntent::Proof(ProofExecution {
            requested_by: Some("   ".to_string()),
            notes: None,
            expires_at: None,
        }));

        let err = agent
            .dispatch(request)
            .expect_err("empty requested_by should be rejected");

        assert_eq!(err, "execution.requested_by must not be empty");
    }

    #[test]
    fn dispatch_rejects_print_execution_approved_at_without_approver() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.execution = Some(ExecutionIntent::Print(PrintExecution {
            approved_by: None,
            approved_at: Some("2026-04-15T09:05:00Z".to_string()),
            source_proof_job_id: None,
            allow_without_proof: false,
        }));

        let err = agent
            .dispatch(request)
            .expect_err("approved_at without approved_by should reject");

        assert_eq!(
            err,
            "execution.approved_at cannot be set without execution.approved_by"
        );
    }

    #[test]
    fn dispatch_rejects_omitted_execution_without_source_proof() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.execution = None;

        let err = agent
            .dispatch(request)
            .expect_err("omitted execution should still enforce print gate");

        assert_eq!(
            err,
            "execution.approvedBy is required for print execution"
        );
    }

    #[test]
    fn dispatch_rejects_print_execution_without_source_proof_when_not_allowed() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T09:05:00Z".to_string()),
                source_proof_job_id: None,
                allow_without_proof: false,
            })),
            ..sample_request()
        };

        let err = agent
            .dispatch(request)
            .expect_err("print execution without source proof should be rejected");

        assert_eq!(
            err,
            "execution.sourceProofJobId is required when source proof gate is enforced"
        );
    }

    #[test]
    fn dispatch_rejects_allow_without_proof_when_policy_disables_it() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T09:05:00Z".to_string()),
                source_proof_job_id: None,
                allow_without_proof: true,
            })),
            ..sample_request()
        };

        let err = agent
            .dispatch(request)
            .expect_err("allow_without_proof should be blocked by default policy");

        assert_eq!(
            err,
            "execution.allowWithoutProof is disabled by current print policy"
        );
    }

    #[test]
    fn dispatch_accepts_allow_without_proof_when_policy_enables_it() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine).with_policy(PrintAgentPolicy {
            allow_print_without_proof: true,
        });
        let request = DispatchRequest {
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T09:05:00Z".to_string()),
                source_proof_job_id: None,
                allow_without_proof: true,
            })),
            ..sample_request()
        };

        let result = agent
            .dispatch(request)
            .expect("allow_without_proof should work only when policy enables it");

        assert_eq!(result.mode, "print");
    }

    #[test]
    fn dispatch_rejects_path_separators_in_source_proof_job_id() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T09:05:00Z".to_string()),
                source_proof_job_id: Some("proof/../../job".to_string()),
                allow_without_proof: false,
            })),
            ..sample_request()
        };

        let err = agent
            .dispatch(request)
            .expect_err("path separators in source proof id should be rejected");

        assert_eq!(
            err,
            "execution.sourceProofJobId must not contain path separators"
        );
    }

    #[test]
    fn dispatch_records_reprint_event_with_lineage_and_reason() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            job_id: "JOB-20260415-0002".to_string(),
            job_lineage_id: Some("JOB-20260415-0001".to_string()),
            reprint_of_job_id: Some("JOB-20260415-0001".to_string()),
            reason: Some("damaged label".to_string()),
            ..sample_request()
        };

        let record = agent
            .dispatch(request)
            .expect("reprint dispatch should succeed");

        assert_eq!(record.audit.event, PrintDispatchAuditEvent::Reprinted);
        assert_eq!(
            record.audit.job_id,
            PrintJobId("JOB-20260415-0002".to_string()).0
        );
        assert_eq!(
            record.audit.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string()).0
        );
        assert_eq!(
            record.audit.parent_job_id,
            Some(PrintJobId("JOB-20260415-0001".to_string()).0)
        );
        assert_eq!(record.audit.reason, Some("damaged label".to_string()));
    }

    #[test]
    fn dispatch_uses_parent_job_as_lineage_when_explicit_lineage_is_missing() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            job_id: "JOB-20260415-0003".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: Some("JOB-20260415-0001".to_string()),
            reason: Some("operator retry".to_string()),
            ..sample_request()
        };

        let record = agent
            .dispatch(request)
            .expect("reprint dispatch should succeed");

        assert_eq!(
            record.audit.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string()).0
        );
        assert_eq!(record.audit.event, PrintDispatchAuditEvent::Reprinted);
    }

    #[test]
    fn dispatch_uses_svg_for_windows_spooler_adapter() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::WindowsSpooler);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter.clone(), barcode_engine);

        let result = agent
            .dispatch(sample_request())
            .expect("non-pdf dispatch should succeed");

        let submission = adapter.last_submission();
        assert_eq!(submission.media_type, "image/svg+xml");
        assert_eq!(result.submission.adapter_kind, "windows-spooler");
        assert!(
            !submission.bytes.is_empty(),
            "svg output should not be empty"
        );
        assert!(
            submission.bytes.starts_with(b"<svg"),
            "windows spooler adapters should keep receiving SVG artifacts"
        );
    }

    #[test]
    fn dispatch_rejects_invalid_jan_before_submit() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.jan = "4006381333930".to_string();

        let err = agent
            .dispatch(request)
            .expect_err("dispatch should reject invalid JAN");

        assert_eq!(err, "jan checksum invalid: expected 1, got 0");
    }

    #[test]
    fn dispatch_accepts_12_digit_jan_with_normalization() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.jan = "400638133393".to_string();

        let record = agent
            .dispatch(request)
            .expect("12-digit JAN should be normalized before dispatch");

        assert_eq!(
            record.audit.job_id,
            PrintJobId("JOB-20260415-0001".to_string()).0
        );
    }

    #[test]
    fn dispatch_rejects_zero_qty() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.qty = 0;

        let err = agent
            .dispatch(request)
            .expect_err("dispatch should reject zero qty");

        assert_eq!(err, "qty must be greater than or equal to 1");
    }

    #[test]
    fn dispatch_rejects_missing_job_id() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.job_id = "  ".to_string();

        let err = agent
            .dispatch(request)
            .expect_err("dispatch should reject blank job id");

        assert_eq!(err, "job_id must not be empty");
    }

    #[test]
    fn dispatch_rejects_path_separators_in_job_id() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let mut request = sample_request();
        request.job_id = "JOB/20260415/0001".to_string();

        let err = agent
            .dispatch(request)
            .expect_err("dispatch should reject path separators in job id");

        assert_eq!(err, "job_id must not contain path separators");
    }

    #[test]
    fn dispatch_rejects_unsupported_adapter() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Zpl);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter.clone(), barcode_engine);

        let err = agent
            .dispatch(sample_request())
            .expect_err("unsupported adapters should fail fast");

        assert_eq!(err, "unsupported printer adapter 'Zpl'");
        assert_eq!(adapter.submission_count(), 0);
    }

    fn sample_request() -> DispatchRequest {
        DispatchRequest {
            job_id: "JOB-20260415-0001".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 24,
            template_version: Some("basic-50x30@v1".to_string()),
            template: None,
            printer_profile: Some(DispatchPrinterProfile {
                id: "pdf-a4-proof".to_string(),
                adapter: "pdf".to_string(),
            }),
            execution: Some(ExecutionIntent::Print(PrintExecution {
                approved_by: Some("manager.user".to_string()),
                approved_at: Some("2026-04-15T09:00:00Z".to_string()),
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

    #[derive(Clone)]
    struct FakeAdapter {
        kind: PrinterAdapterKind,
        submissions: Arc<Mutex<Vec<PrintArtifact>>>,
    }

    impl FakeAdapter {
        fn new(kind: PrinterAdapterKind) -> Self {
            Self {
                kind,
                submissions: Arc::new(Mutex::new(Vec::new())),
            }
        }

        fn last_submission(&self) -> PrintArtifact {
            self.submissions
                .lock()
                .expect("fake adapter submissions lock should be available")
                .last()
                .cloned()
                .expect("a submission should have been recorded")
        }

        fn submission_count(&self) -> usize {
            self.submissions
                .lock()
                .expect("fake adapter submissions lock should be available")
                .len()
        }
    }

    impl PrinterAdapter for FakeAdapter {
        fn kind(&self) -> PrinterAdapterKind {
            self.kind.clone()
        }

        fn submit(&self, artifact: &PrintArtifact) -> Result<SubmissionReceipt, AdapterError> {
            self.submissions
                .lock()
                .expect("fake adapter submissions lock should be available")
                .push(artifact.clone());

            Ok(SubmissionReceipt {
                adapter_kind: self.kind.clone(),
                external_job_id: "proof-0001".to_string(),
            })
        }
    }

    struct FakeBarcodeEngine;

    impl BarcodeEngine for FakeBarcodeEngine {
        fn render(&self, request: &BarcodeRequest) -> Result<BarcodeArtifact, BarcodeError> {
            assert_eq!(request.format, BarcodeFormat::Jan13);
            assert!(!request.output_path.is_empty());

            Ok(BarcodeArtifact {
                output_path: request.output_path.clone(),
            })
        }
    }
}
