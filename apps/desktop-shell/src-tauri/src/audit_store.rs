use audit_log::{
    AuditActor, AuditQuery, AuditSearchEntry, AuditSearchResult, DispatchMatchSubject,
    PersistedDispatchRecord, ProofRecord, ProofStatus,
};
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use std::fs::{self, OpenOptions};
use std::io::{ErrorKind, Write};
use std::path::{Path, PathBuf};
use std::process;
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

const DISPATCH_LEDGER_FILE: &str = "dispatch-ledger.json";
const PROOF_LEDGER_FILE: &str = "proof-ledger.json";
const STORE_LOCK_FILE: &str = "audit-store.lock";
const MAX_AUDIT_RESULT_LIMIT: usize = 200;
const DEFAULT_AUDIT_RESULT_LIMIT: usize = 50;
const LOCK_WAIT_TIMEOUT: Duration = Duration::from_secs(3);
const LOCK_WAIT_INTERVAL: Duration = Duration::from_millis(25);

#[derive(Debug, Clone)]
pub struct AuditStore {
    root: PathBuf,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProofReviewRequest {
    pub proof_job_id: String,
    pub actor_user_id: String,
    pub actor_display_name: String,
    pub decided_at: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
}

impl AuditStore {
    pub fn new(root: PathBuf) -> Self {
        Self { root }
    }

    pub fn record_dispatch(&self, record: PersistedDispatchRecord) -> Result<(), String> {
        self.with_lock(|| {
            let mut records = self.load_dispatch_records()?;
            records.push(record);
            self.save_dispatch_records(&records)
        })
    }

    pub fn register_pending_proof(&self, record: ProofRecord) -> Result<(), String> {
        self.with_lock(|| {
            let mut records = self.load_proof_records()?;
            for existing in records.iter_mut() {
                if existing.job_lineage_id == record.job_lineage_id
                    && existing.proof_job_id != record.proof_job_id
                    && matches!(existing.status, ProofStatus::Pending | ProofStatus::Approved)
                {
                    existing.decide(
                        ProofStatus::Superseded,
                        record.requested_by.clone(),
                        record.requested_at.clone(),
                        Some(format!(
                            "superseded by newer proof {}",
                            record.proof_job_id.0
                        )),
                    );
                }
            }

            if let Some(index) = records
                .iter()
                .position(|existing| existing.proof_job_id == record.proof_job_id)
            {
                records[index] = record;
            } else {
                records.push(record);
            }

            self.save_proof_records(&records)
        })
    }

    pub fn ensure_approved_proof(&self, proof_job_id: &str) -> Result<ProofRecord, String> {
        self.with_lock(|| {
            let records = self.load_proof_records()?;
            approved_proof_from_records(&records, proof_job_id)
        })
    }

    pub fn ensure_approved_proof_matches(
        &self,
        proof_job_id: &str,
        expected_template_version: &str,
        expected_subject: &DispatchMatchSubject,
    ) -> Result<ProofRecord, String> {
        self.with_lock(|| {
            let proof_records = self.load_proof_records()?;
            let proof = approved_proof_from_records(&proof_records, proof_job_id)?;
            let dispatches = self.load_dispatch_records()?;
            let proof_dispatch = dispatches
                .into_iter()
                .find(|record| record.mode == "proof" && record.audit.job_id.0 == proof_job_id)
                .ok_or_else(|| {
                    format!(
                        "approved proof job '{}' has no persisted proof dispatch record",
                        proof_job_id
                    )
                })?;

            if proof.job_lineage_id != proof_dispatch.audit.job_lineage_id {
                return Err(format!(
                    "approved proof job '{}' has inconsistent lineage between proof ledger and dispatch ledger",
                    proof_job_id
                ));
            }

            let mismatches =
                collect_dispatch_mismatches(&proof_dispatch, expected_template_version, expected_subject);
            if !mismatches.is_empty() {
                return Err(format!(
                    "approved proof job '{}' does not match print payload: {}",
                    proof_job_id,
                    mismatches.join(", ")
                ));
            }

            Ok(proof)
        })
    }

    pub fn approve_proof(&self, request: ProofReviewRequest) -> Result<ProofRecord, String> {
        self.review_proof(request, ProofStatus::Approved)
    }

    pub fn reject_proof(&self, request: ProofReviewRequest) -> Result<ProofRecord, String> {
        self.review_proof(request, ProofStatus::Rejected)
    }

    pub fn search(&self, query: AuditQuery) -> Result<AuditSearchResult, String> {
        let mut dispatches = self.load_dispatch_records()?;
        dispatches.sort_by(|left, right| right.audit.occurred_at.cmp(&left.audit.occurred_at));
        let proofs = self.load_proof_records()?;
        let search_text = query
            .search_text
            .as_deref()
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .map(|value| value.to_ascii_lowercase());
        let limit = query
            .limit
            .unwrap_or(DEFAULT_AUDIT_RESULT_LIMIT)
            .min(MAX_AUDIT_RESULT_LIMIT);

        let mut entries = Vec::new();
        for dispatch in dispatches.into_iter() {
            let proof = proofs
                .iter()
                .find(|proof| proof.proof_job_id == dispatch.audit.job_id)
                .cloned();
            let entry = AuditSearchEntry { dispatch, proof };
            if matches_search(&entry, search_text.as_deref()) {
                entries.push(entry);
            }
            if entries.len() >= limit {
                break;
            }
        }

        Ok(AuditSearchResult { entries })
    }

    fn review_proof(
        &self,
        request: ProofReviewRequest,
        status: ProofStatus,
    ) -> Result<ProofRecord, String> {
        self.with_lock(|| {
            let mut records = self.load_proof_records()?;
            let Some(index) = records
                .iter()
                .position(|record| record.proof_job_id.0 == request.proof_job_id)
            else {
                return Err(format!(
                    "proof job '{}' was not found in approval ledger",
                    request.proof_job_id
                ));
            };

            if matches!(records[index].status, ProofStatus::Superseded) {
                return Err(format!(
                    "proof job '{}' is already superseded and cannot be reviewed",
                    request.proof_job_id
                ));
            }

            records[index].decide(
                status,
                AuditActor {
                    user_id: request.actor_user_id,
                    display_name: request.actor_display_name,
                },
                request.decided_at,
                optional_trimmed_non_empty(request.notes),
            );

            let updated = records[index].clone();
            self.save_proof_records(&records)?;
            Ok(updated)
        })
    }

    fn load_dispatch_records(&self) -> Result<Vec<PersistedDispatchRecord>, String> {
        read_json_vec(&self.dispatch_ledger_path())
    }

    fn save_dispatch_records(&self, records: &[PersistedDispatchRecord]) -> Result<(), String> {
        write_json_vec(&self.dispatch_ledger_path(), records)
    }

    fn load_proof_records(&self) -> Result<Vec<ProofRecord>, String> {
        read_json_vec(&self.proof_ledger_path())
    }

    fn save_proof_records(&self, records: &[ProofRecord]) -> Result<(), String> {
        write_json_vec(&self.proof_ledger_path(), records)
    }

    fn dispatch_ledger_path(&self) -> PathBuf {
        self.root.join(DISPATCH_LEDGER_FILE)
    }

    fn proof_ledger_path(&self) -> PathBuf {
        self.root.join(PROOF_LEDGER_FILE)
    }

    fn lock_path(&self) -> PathBuf {
        self.root.join(STORE_LOCK_FILE)
    }

    fn with_lock<T>(&self, operation: impl FnOnce() -> Result<T, String>) -> Result<T, String> {
        let _guard = acquire_store_lock(&self.lock_path())?;
        operation()
    }
}

fn matches_search(entry: &AuditSearchEntry, search_text: Option<&str>) -> bool {
    let Some(search_text) = search_text else {
        return true;
    };

    let mut haystacks = vec![
        entry.dispatch.audit.job_id.0.to_ascii_lowercase(),
        entry.dispatch.audit.job_lineage_id.0.to_ascii_lowercase(),
        entry
            .dispatch
            .audit
            .actor
            .user_id
            .to_ascii_lowercase(),
        entry
            .dispatch
            .audit
            .actor
            .display_name
            .to_ascii_lowercase(),
        entry.dispatch.mode.to_ascii_lowercase(),
        entry.dispatch.template_version.to_ascii_lowercase(),
        entry
            .dispatch
            .submission_external_job_id
            .to_ascii_lowercase(),
        entry
            .dispatch
            .submission_adapter_kind
            .to_ascii_lowercase(),
    ];
    if let Some(reason) = entry.dispatch.audit.reason.as_deref() {
        haystacks.push(reason.to_ascii_lowercase());
    }
    if let Some(proof) = entry.proof.as_ref() {
        haystacks.push(proof.status_string().to_ascii_lowercase());
        haystacks.push(proof.requested_by.user_id.to_ascii_lowercase());
        haystacks.push(proof.requested_by.display_name.to_ascii_lowercase());
    }
    haystacks.push(entry.dispatch.match_subject.sku.to_ascii_lowercase());
    haystacks.push(entry.dispatch.match_subject.brand.to_ascii_lowercase());
    haystacks.push(entry.dispatch.match_subject.jan_normalized.to_ascii_lowercase());
    haystacks.push(entry.dispatch.match_subject.qty.to_string());

    haystacks.into_iter().any(|value| value.contains(search_text))
}

fn approved_proof_from_records(records: &[ProofRecord], proof_job_id: &str) -> Result<ProofRecord, String> {
    let record = records
        .iter()
        .find(|record| record.proof_job_id.0 == proof_job_id)
        .cloned()
        .ok_or_else(|| format!("proof job '{}' was not found in approval ledger", proof_job_id))?;

    match record.status {
        ProofStatus::Approved => Ok(record),
        ProofStatus::Pending => Err(format!(
            "proof job '{}' exists but is still pending approval",
            proof_job_id
        )),
        ProofStatus::Rejected => Err(format!(
            "proof job '{}' was rejected and cannot be used for print",
            proof_job_id
        )),
        ProofStatus::Superseded => Err(format!(
            "proof job '{}' was superseded by a newer proof",
            proof_job_id
        )),
    }
}

fn collect_dispatch_mismatches(
    record: &PersistedDispatchRecord,
    expected_template_version: &str,
    expected_subject: &DispatchMatchSubject,
) -> Vec<&'static str> {
    let mut mismatches = Vec::new();
    if record.template_version != expected_template_version {
        mismatches.push("templateVersion");
    }
    if record.match_subject.sku != expected_subject.sku {
        mismatches.push("sku");
    }
    if record.match_subject.brand != expected_subject.brand {
        mismatches.push("brand");
    }
    if record.match_subject.jan_normalized != expected_subject.jan_normalized {
        mismatches.push("jan");
    }
    if record.match_subject.qty != expected_subject.qty {
        mismatches.push("qty");
    }
    mismatches
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

fn read_json_vec<T>(path: &Path) -> Result<Vec<T>, String>
where
    T: DeserializeOwned,
{
    if !path.exists() {
        return Ok(Vec::new());
    }

    let content = fs::read_to_string(path)
        .map_err(|error| format!("failed to read audit ledger '{}': {error}", path.display()))?;
    if content.trim().is_empty() {
        return Ok(Vec::new());
    }

    serde_json::from_str(&content)
        .map_err(|error| format!("failed to parse audit ledger '{}': {error}", path.display()))
}

fn write_json_vec<T>(path: &Path, records: &[T]) -> Result<(), String>
where
    T: Serialize,
{
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| {
            format!(
                "failed to create audit ledger directory '{}': {error}",
                parent.display()
            )
        })?;
    }

    let payload = serde_json::to_string_pretty(records)
        .map_err(|error| format!("failed to serialize audit ledger '{}': {error}", path.display()))?;
    let temp_path = unique_temp_path(path);
    fs::write(&temp_path, payload)
        .map_err(|error| format!("failed to write audit ledger '{}': {error}", temp_path.display()))?;
    fs::rename(&temp_path, path).map_err(|error| {
        format!(
            "failed to finalize audit ledger '{}' from '{}': {error}",
            path.display(),
            temp_path.display()
        )
    })
}

fn acquire_store_lock(path: &Path) -> Result<AuditStoreLockGuard, String> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| {
            format!(
                "failed to create audit lock directory '{}': {error}",
                parent.display()
            )
        })?;
    }

    let deadline = Instant::now() + LOCK_WAIT_TIMEOUT;
    loop {
        match OpenOptions::new().write(true).create_new(true).open(path) {
            Ok(mut file) => {
                let payload = format!("pid={} acquired_at={}\n", process::id(), timestamp_nanos());
                file.write_all(payload.as_bytes()).map_err(|error| {
                    format!("failed to initialize audit lock '{}': {error}", path.display())
                })?;
                return Ok(AuditStoreLockGuard {
                    path: path.to_path_buf(),
                });
            }
            Err(error) if error.kind() == ErrorKind::AlreadyExists => {
                if Instant::now() >= deadline {
                    return Err(format!(
                        "timed out waiting for audit ledger lock '{}'",
                        path.display()
                    ));
                }
                thread::sleep(LOCK_WAIT_INTERVAL);
            }
            Err(error) => {
                return Err(format!(
                    "failed to acquire audit ledger lock '{}': {error}",
                    path.display()
                ));
            }
        }
    }
}

fn unique_temp_path(path: &Path) -> PathBuf {
    let suffix = format!("{}.{}.tmp", process::id(), timestamp_nanos());
    let file_name = path
        .file_name()
        .map(|value| value.to_string_lossy().into_owned())
        .unwrap_or_else(|| "ledger".to_string());
    path.with_file_name(format!("{file_name}.{suffix}"))
}

fn timestamp_nanos() -> u128 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("system time should be after unix epoch")
        .as_nanos()
}

struct AuditStoreLockGuard {
    path: PathBuf,
}

impl Drop for AuditStoreLockGuard {
    fn drop(&mut self) {
        match fs::remove_file(&self.path) {
            Ok(()) => {}
            Err(error) if error.kind() == ErrorKind::NotFound => {}
            Err(_) => {}
        }
    }
}

trait ProofStatusLabel {
    fn status_string(&self) -> &'static str;
}

impl ProofStatusLabel for ProofRecord {
    fn status_string(&self) -> &'static str {
        match self.status {
            ProofStatus::Pending => "pending",
            ProofStatus::Approved => "approved",
            ProofStatus::Rejected => "rejected",
            ProofStatus::Superseded => "superseded",
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{AuditStore, ProofReviewRequest};
    use audit_log::{
        AuditActor, AuditQuery, DispatchMatchSubject, PersistedDispatchRecord, PrintAuditRecord,
        PrintJobId, PrintJobLineageId, ProofRecord, ProofStatus,
    };
    use std::env;
    use std::fs;
    use std::path::{Path, PathBuf};
    use std::process;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn approving_proof_updates_record_status() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        store
            .register_pending_proof(sample_proof("JOB-PROOF-0001"))
            .expect("pending proof should persist");

        let approved = store
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-PROOF-0001".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T12:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approvable");

        assert_eq!(approved.status, ProofStatus::Approved);
    }

    #[test]
    fn approved_proof_is_required_by_lookup() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        store
            .register_pending_proof(sample_proof("JOB-PROOF-0002"))
            .expect("pending proof should persist");

        let err = store
            .ensure_approved_proof("JOB-PROOF-0002")
            .expect_err("pending proof should not satisfy approval");
        assert!(err.contains("pending approval"));
    }

    #[test]
    fn search_returns_dispatch_with_proof_summary() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-0003");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(PersistedDispatchRecord {
                audit: PrintAuditRecord::dispatch(
                    proof.proof_job_id.clone(),
                    proof.job_lineage_id.clone(),
                    None,
                    proof.requested_by.clone(),
                    proof.requested_at.clone(),
                    None,
                ),
                mode: "proof".to_string(),
                template_version: "basic-50x30@v1".to_string(),
                match_subject: sample_match_subject(),
                artifact_media_type: "application/pdf".to_string(),
                artifact_byte_size: 128,
                submission_adapter_kind: "pdf".to_string(),
                submission_external_job_id: "proof-0001".to_string(),
            })
            .expect("dispatch should persist");

        let result = store
            .search(AuditQuery {
                search_text: Some("JOB-PROOF-0003".to_string()),
                limit: Some(10),
            })
            .expect("search should load persisted entries");

        assert_eq!(result.entries.len(), 1);
        assert_eq!(
            result.entries[0]
                .proof
                .as_ref()
                .expect("proof summary should join")
                .status,
            ProofStatus::Pending
        );
    }

    #[test]
    fn approved_proof_must_match_dispatch_subject() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-0004");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(PersistedDispatchRecord {
                audit: PrintAuditRecord::dispatch(
                    proof.proof_job_id.clone(),
                    proof.job_lineage_id.clone(),
                    None,
                    proof.requested_by.clone(),
                    proof.requested_at.clone(),
                    None,
                ),
                mode: "proof".to_string(),
                template_version: "basic-50x30@v1".to_string(),
                match_subject: sample_match_subject(),
                artifact_media_type: "application/pdf".to_string(),
                artifact_byte_size: 128,
                submission_adapter_kind: "pdf".to_string(),
                submission_external_job_id: "proof-0001".to_string(),
            })
            .expect("dispatch should persist");
        store
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-PROOF-0004".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T12:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let err = store
            .ensure_approved_proof_matches(
                "JOB-PROOF-0004",
                "basic-50x30@v1",
                &DispatchMatchSubject {
                    qty: 2,
                    ..sample_match_subject()
                },
            )
            .expect_err("qty mismatch should reject print");

        assert!(err.contains("qty"));
    }

    fn sample_proof(job_id: &str) -> ProofRecord {
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

    fn sample_match_subject() -> DispatchMatchSubject {
        DispatchMatchSubject {
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan_normalized: "4006381333931".to_string(),
            qty: 1,
        }
    }

    struct TestDir {
        path: PathBuf,
    }

    impl TestDir {
        fn new() -> Self {
            let unique = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .expect("system time should be after unix epoch")
                .as_nanos();
            let path = env::temp_dir().join(format!(
                "jan-label-audit-store-{}-{}",
                process::id(),
                unique
            ));
            fs::create_dir_all(&path).expect("test dir should be creatable");
            Self { path }
        }

        fn path(&self) -> &Path {
            &self.path
        }
    }

    impl Drop for TestDir {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.path);
        }
    }
}
