#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PrintJobId(pub String);

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
    pub actor: AuditActor,
    pub event: AuditEventKind,
    pub occurred_at: String,
    pub reason: Option<String>,
}

