use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct PrintJobId(pub String);

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct PrintJobLineageId(pub String);

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuditActor {
    pub user_id: String,
    pub display_name: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum AuditEventKind {
    Created,
    Submitted,
    Completed,
    Reprinted,
    Failed,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrintAuditRecord {
    pub job_id: PrintJobId,
    pub job_lineage_id: PrintJobLineageId,
    pub parent_job_id: Option<PrintJobId>,
    pub actor: AuditActor,
    pub event: AuditEventKind,
    pub occurred_at: String,
    pub reason: Option<String>,
}

impl PrintAuditRecord {
    pub fn dispatch(
        job_id: PrintJobId,
        job_lineage_id: PrintJobLineageId,
        parent_job_id: Option<PrintJobId>,
        actor: AuditActor,
        occurred_at: String,
        reason: Option<String>,
    ) -> Self {
        let event = if parent_job_id.is_some() {
            AuditEventKind::Reprinted
        } else {
            AuditEventKind::Submitted
        };

        Self {
            job_id,
            job_lineage_id,
            parent_job_id,
            actor,
            event,
            occurred_at,
            reason,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum ProofStatus {
    Pending,
    Approved,
    Rejected,
    Superseded,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProofDecision {
    pub status: ProofStatus,
    pub actor: AuditActor,
    pub occurred_at: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProofRecord {
    pub proof_job_id: PrintJobId,
    pub job_lineage_id: PrintJobLineageId,
    pub requested_by: AuditActor,
    pub requested_at: String,
    pub status: ProofStatus,
    pub artifact_path: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub decision: Option<ProofDecision>,
}

impl ProofRecord {
    pub fn pending(
        proof_job_id: PrintJobId,
        job_lineage_id: PrintJobLineageId,
        requested_by: AuditActor,
        requested_at: String,
        artifact_path: String,
        notes: Option<String>,
    ) -> Self {
        Self {
            proof_job_id,
            job_lineage_id,
            requested_by,
            requested_at,
            status: ProofStatus::Pending,
            artifact_path,
            notes,
            decision: None,
        }
    }

    pub fn decide(
        &mut self,
        status: ProofStatus,
        actor: AuditActor,
        occurred_at: String,
        notes: Option<String>,
    ) {
        self.status = status.clone();
        self.decision = Some(ProofDecision {
            status,
            actor,
            occurred_at,
            notes,
        });
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DispatchMatchSubject {
    pub sku: String,
    pub brand: String,
    pub jan_normalized: String,
    pub qty: u32,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PersistedDispatchRecord {
    pub audit: PrintAuditRecord,
    pub mode: String,
    pub template_version: String,
    pub match_subject: DispatchMatchSubject,
    pub artifact_media_type: String,
    pub artifact_byte_size: usize,
    pub submission_adapter_kind: String,
    pub submission_external_job_id: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuditQuery {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub search_text: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub limit: Option<usize>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuditSearchEntry {
    pub dispatch: PersistedDispatchRecord,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub proof: Option<ProofRecord>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuditSearchResult {
    pub entries: Vec<AuditSearchEntry>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "lowercase")]
pub enum AuditLedgerScope {
    #[default]
    All,
    Dispatch,
    Proof,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuditLedgerSnapshot {
    pub dispatches: Vec<PersistedDispatchRecord>,
    pub proofs: Vec<ProofRecord>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuditArtifactInfo {
    pub file_name: String,
    pub file_path: String,
    pub created_at_utc: String,
    pub size_bytes: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuditExportRequest {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub scope: Option<AuditLedgerScope>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuditExportResult {
    pub scope: AuditLedgerScope,
    pub dispatch_count: usize,
    pub proof_count: usize,
    pub snapshot: AuditLedgerSnapshot,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuditRetentionRequest {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub scope: Option<AuditLedgerScope>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_age_days: Option<u32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_entries: Option<usize>,
    #[serde(default)]
    pub dry_run: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuditRetentionResult {
    pub scope: AuditLedgerScope,
    pub dry_run: bool,
    pub retained_dispatch_count: usize,
    pub retained_proof_count: usize,
    pub removed_dispatch_count: usize,
    pub removed_proof_count: usize,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub backup: Option<AuditArtifactInfo>,
}

#[cfg(test)]
mod tests {
    use super::{
        AuditActor, AuditEventKind, AuditQuery, DispatchMatchSubject, PersistedDispatchRecord,
        PrintAuditRecord, PrintJobId, PrintJobLineageId, ProofRecord, ProofStatus,
    };

    #[test]
    fn dispatch_record_for_original_job_keeps_lineage_and_submitted_event() {
        let record = PrintAuditRecord::dispatch(
            PrintJobId("JOB-20260415-0001".to_string()),
            PrintJobLineageId("JOB-20260415-0001".to_string()),
            None,
            AuditActor {
                user_id: "ops.user".to_string(),
                display_name: "Ops User".to_string(),
            },
            "2026-04-15T09:00:00Z".to_string(),
            None,
        );

        assert_eq!(record.event, AuditEventKind::Submitted);
        assert_eq!(
            record.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string())
        );
        assert_eq!(record.parent_job_id, None);
    }

    #[test]
    fn dispatch_record_for_reprint_keeps_parent_job_and_reason() {
        let record = PrintAuditRecord::dispatch(
            PrintJobId("JOB-20260415-0002".to_string()),
            PrintJobLineageId("JOB-20260415-0001".to_string()),
            Some(PrintJobId("JOB-20260415-0001".to_string())),
            AuditActor {
                user_id: "ops.user".to_string(),
                display_name: "Ops User".to_string(),
            },
            "2026-04-15T09:05:00Z".to_string(),
            Some("damaged label".to_string()),
        );

        assert_eq!(record.event, AuditEventKind::Reprinted);
        assert_eq!(
            record.parent_job_id,
            Some(PrintJobId("JOB-20260415-0001".to_string()))
        );
        assert_eq!(record.reason, Some("damaged label".to_string()));
    }

    #[test]
    fn proof_record_can_transition_from_pending_to_approved() {
        let mut record = ProofRecord::pending(
            PrintJobId("JOB-20260415-PROOF".to_string()),
            PrintJobLineageId("JOB-20260415-PROOF".to_string()),
            AuditActor {
                user_id: "proof.user".to_string(),
                display_name: "Proof User".to_string(),
            },
            "2026-04-15T09:00:00Z".to_string(),
            "proofs/JOB-20260415-PROOF-proof.pdf".to_string(),
            Some("initial proof".to_string()),
        );

        record.decide(
            ProofStatus::Approved,
            AuditActor {
                user_id: "manager.user".to_string(),
                display_name: "Manager User".to_string(),
            },
            "2026-04-15T09:05:00Z".to_string(),
            Some("approved".to_string()),
        );

        assert_eq!(record.status, ProofStatus::Approved);
        assert_eq!(
            record
                .decision
                .expect("approval decision should exist")
                .status,
            ProofStatus::Approved
        );
    }

    #[test]
    fn audit_query_defaults_to_empty_search() {
        let query = AuditQuery::default();
        assert_eq!(query.search_text, None);
        assert_eq!(query.limit, None);
    }

    #[test]
    fn persisted_dispatch_record_keeps_summary_fields() {
        let record = PersistedDispatchRecord {
            audit: PrintAuditRecord::dispatch(
                PrintJobId("JOB-20260415-0001".to_string()),
                PrintJobLineageId("JOB-20260415-0001".to_string()),
                None,
                AuditActor {
                    user_id: "ops.user".to_string(),
                    display_name: "Ops User".to_string(),
                },
                "2026-04-15T09:00:00Z".to_string(),
                None,
            ),
            mode: "proof".to_string(),
            template_version: "basic-50x30@v1".to_string(),
            match_subject: DispatchMatchSubject {
                sku: "SKU-001".to_string(),
                brand: "Brand".to_string(),
                jan_normalized: "4006381333931".to_string(),
                qty: 1,
            },
            artifact_media_type: "application/pdf".to_string(),
            artifact_byte_size: 128,
            submission_adapter_kind: "pdf".to_string(),
            submission_external_job_id: "proof-0001".to_string(),
        };

        assert_eq!(record.mode, "proof");
        assert_eq!(record.match_subject.jan_normalized, "4006381333931");
        assert_eq!(record.submission_adapter_kind, "pdf");
    }
}
