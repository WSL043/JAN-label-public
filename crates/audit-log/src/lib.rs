#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PrintJobId(pub String);

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PrintJobLineageId(pub String);

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AuditActor {
    pub user_id: String,
    pub display_name: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AuditEventKind {
    Created,
    Submitted,
    Completed,
    Reprinted,
    Failed,
}

#[derive(Debug, Clone, PartialEq, Eq)]
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

#[cfg(test)]
mod tests {
    use super::{AuditActor, AuditEventKind, PrintAuditRecord, PrintJobId, PrintJobLineageId};

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
}
