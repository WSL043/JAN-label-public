use audit_log::{
    AuditActor, AuditArtifactInfo, AuditExportRequest, AuditExportResult, AuditLedgerScope,
    AuditLedgerSnapshot, AuditQuery, AuditRetentionRequest, AuditRetentionResult,
    AuditSearchEntry, AuditSearchResult, DispatchMatchSubject, PersistedDispatchRecord,
    ProofRecord, ProofStatus,
};
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::fs::{self, OpenOptions};
use std::io::{ErrorKind, Write};
use std::path::{Path, PathBuf};
use std::process;
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

const DISPATCH_LEDGER_FILE: &str = "dispatch-ledger.json";
const PROOF_LEDGER_FILE: &str = "proof-ledger.json";
const STORE_LOCK_FILE: &str = "audit-store.lock";
const BACKUP_DIR: &str = "backups";
const MAX_AUDIT_RESULT_LIMIT: usize = 200;
const DEFAULT_AUDIT_RESULT_LIMIT: usize = 50;
const LOCK_WAIT_TIMEOUT: Duration = Duration::from_secs(3);
const LOCK_WAIT_INTERVAL: Duration = Duration::from_millis(25);
const AUDIT_BUNDLE_SCHEMA_VERSION: &str = "audit-ledger-bundle-v1";

#[derive(Debug, Clone)]
pub struct AuditStore {
    root: PathBuf,
}

#[derive(Debug, Clone)]
pub struct LegacyProofSeedRecord {
    pub dispatch: PersistedDispatchRecord,
    pub proof: ProofRecord,
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

    pub fn assert_dispatch_ledger_writable(&self) -> Result<(), String> {
        self.with_lock(|| {
            let records = self.load_dispatch_records()?;
            self.save_dispatch_records(&records)
        })
    }

    pub fn assert_dispatch_and_proof_ledgers_writable(&self) -> Result<(), String> {
        self.with_lock(|| {
            let dispatches = self.load_dispatch_records()?;
            let proofs = self.load_proof_records()?;
            self.save_dispatch_records(&dispatches)?;
            self.save_proof_records(&proofs)
        })
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
            let mut matching_dispatches = dispatches
                .into_iter()
                .filter(|record| record.mode == "proof" && record.audit.job_id.0 == proof_job_id);
            let proof_dispatch = matching_dispatches.next().ok_or_else(|| {
                format!(
                    "approved proof job '{}' has no persisted proof dispatch record",
                    proof_job_id
                )
            })?;
            if matching_dispatches.next().is_some() {
                return Err(format!(
                    "approved proof job '{}' has multiple persisted proof dispatch records",
                    proof_job_id
                ));
            }

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

    pub fn snapshot(&self) -> Result<AuditLedgerSnapshot, String> {
        self.with_lock(|| {
            Ok(AuditLedgerSnapshot {
                dispatches: self.load_dispatch_records()?,
                proofs: self.load_proof_records()?,
            })
        })
    }

    pub fn export(&self, request: AuditExportRequest) -> Result<AuditExportResult, String> {
        self.with_lock(|| {
            let snapshot = scoped_snapshot(
                AuditLedgerSnapshot {
                    dispatches: self.load_dispatch_records()?,
                    proofs: self.load_proof_records()?,
                },
                request.scope.unwrap_or_default(),
            );
            Ok(AuditExportResult {
                scope: request.scope.unwrap_or_default(),
                dispatch_count: snapshot.dispatches.len(),
                proof_count: snapshot.proofs.len(),
                snapshot,
            })
        })
    }

    pub fn trim(&self, request: AuditRetentionRequest) -> Result<AuditRetentionResult, String> {
        validate_retention_request(&request)?;
        let scope = request.scope.unwrap_or_default();
        self.with_lock(|| {
            let dispatches = self.load_dispatch_records()?;
            let proofs = self.load_proof_records()?;
            let mut plan = build_retention_plan(&dispatches, &proofs, scope, &request)?;

            if !request.dry_run {
                if let Some(bundle) = plan.backup_bundle.as_mut() {
                    self.write_backup_bundle(bundle)?;
                }
                if scope != AuditLedgerScope::Proof {
                    self.save_dispatch_records(&plan.retained_dispatches)?;
                }
                if scope != AuditLedgerScope::Dispatch {
                    self.save_proof_records(&plan.retained_proofs)?;
                }
            }

            Ok(AuditRetentionResult {
                scope,
                dry_run: request.dry_run,
                retained_dispatch_count: plan.retained_dispatches.len(),
                retained_proof_count: plan.retained_proofs.len(),
                removed_dispatch_count: plan.removed_dispatches.len(),
                removed_proof_count: plan.removed_proofs.len(),
                backup: if request.dry_run {
                    None
                } else {
                    plan.backup_bundle.as_ref().map(|bundle| bundle.artifact.clone())
                },
            })
        })
    }

    pub fn seed_legacy_proofs(&self, records: Vec<LegacyProofSeedRecord>) -> Result<(), String> {
        self.with_lock(|| {
            let mut dispatches = self.load_dispatch_records()?;
            let mut proofs = self.load_proof_records()?;
            validate_legacy_seed_records(&dispatches, &proofs, &records)?;

            for record in records {
                dispatches.push(record.dispatch);
                proofs.push(record.proof);
            }

            self.save_dispatch_records(&dispatches)?;
            self.save_proof_records(&proofs)
        })
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

    pub fn backup_dir(&self) -> PathBuf {
        self.root.join(BACKUP_DIR)
    }

    fn lock_path(&self) -> PathBuf {
        self.root.join(STORE_LOCK_FILE)
    }

    fn write_backup_bundle(&self, bundle: &mut AuditLedgerBundle) -> Result<(), String> {
        let path = self.backup_dir().join(&bundle.artifact.file_name);
        bundle.artifact.file_path = path.to_string_lossy().into_owned();
        let size_bytes = write_json_file(&path, bundle)?;
        bundle.artifact.size_bytes = size_bytes;
        Ok(())
    }

    fn with_lock<T>(&self, operation: impl FnOnce() -> Result<T, String>) -> Result<T, String> {
        let _guard = acquire_store_lock(&self.lock_path())?;
        operation()
    }
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct AuditLedgerBundle {
    schema_version: &'static str,
    created_at_utc: String,
    scope: AuditLedgerScope,
    dispatches: Vec<PersistedDispatchRecord>,
    proofs: Vec<ProofRecord>,
    artifact: AuditArtifactInfo,
}

#[derive(Debug, Clone)]
struct RetentionPlan {
    retained_dispatches: Vec<PersistedDispatchRecord>,
    retained_proofs: Vec<ProofRecord>,
    removed_dispatches: Vec<PersistedDispatchRecord>,
    removed_proofs: Vec<ProofRecord>,
    backup_bundle: Option<AuditLedgerBundle>,
}

fn scoped_snapshot(snapshot: AuditLedgerSnapshot, scope: AuditLedgerScope) -> AuditLedgerSnapshot {
    match scope {
        AuditLedgerScope::All => snapshot,
        AuditLedgerScope::Dispatch => AuditLedgerSnapshot {
            dispatches: snapshot.dispatches,
            proofs: Vec::new(),
        },
        AuditLedgerScope::Proof => AuditLedgerSnapshot {
            dispatches: Vec::new(),
            proofs: snapshot.proofs,
        },
    }
}

fn validate_retention_request(request: &AuditRetentionRequest) -> Result<(), String> {
    if request.max_age_days.is_none() && request.max_entries.is_none() {
        return Err("retention requires maxAgeDays, maxEntries, or both".to_string());
    }
    if matches!(request.max_age_days, Some(0)) {
        return Err("maxAgeDays must be greater than or equal to 1".to_string());
    }
    if matches!(request.max_entries, Some(0)) {
        return Err("maxEntries must be greater than or equal to 1".to_string());
    }
    Ok(())
}

fn build_retention_plan(
    dispatches: &[PersistedDispatchRecord],
    proofs: &[ProofRecord],
    scope: AuditLedgerScope,
    request: &AuditRetentionRequest,
) -> Result<RetentionPlan, String> {
    let dispatch_keep = match scope {
        AuditLedgerScope::Proof => (0..dispatches.len()).collect::<HashSet<_>>(),
        AuditLedgerScope::All | AuditLedgerScope::Dispatch => {
            select_dispatch_keep_indices(dispatches, request)?
        }
    };
    let proof_keep = match scope {
        AuditLedgerScope::Dispatch => (0..proofs.len()).collect::<HashSet<_>>(),
        AuditLedgerScope::All | AuditLedgerScope::Proof => select_proof_keep_indices(proofs, request)?,
    };

    let mut dispatch_keep = dispatch_keep;
    preserve_required_dispatches(dispatches, proofs, &proof_keep, &mut dispatch_keep);

    let retained_dispatches = dispatches
        .iter()
        .enumerate()
        .filter(|(index, _)| dispatch_keep.contains(index))
        .map(|(_, record)| record.clone())
        .collect::<Vec<_>>();
    let removed_dispatches = dispatches
        .iter()
        .enumerate()
        .filter(|(index, _)| !dispatch_keep.contains(index))
        .map(|(_, record)| record.clone())
        .collect::<Vec<_>>();
    let retained_proofs = proofs
        .iter()
        .enumerate()
        .filter(|(index, _)| proof_keep.contains(index))
        .map(|(_, record)| record.clone())
        .collect::<Vec<_>>();
    let removed_proofs = proofs
        .iter()
        .enumerate()
        .filter(|(index, _)| !proof_keep.contains(index))
        .map(|(_, record)| record.clone())
        .collect::<Vec<_>>();

    let backup_bundle = if removed_dispatches.is_empty() && removed_proofs.is_empty() {
        None
    } else {
        Some(build_backup_bundle(
            scope,
            &removed_dispatches,
            &removed_proofs,
        ))
    };

    Ok(RetentionPlan {
        retained_dispatches,
        retained_proofs,
        removed_dispatches,
        removed_proofs,
        backup_bundle,
    })
}

fn select_dispatch_keep_indices(
    dispatches: &[PersistedDispatchRecord],
    request: &AuditRetentionRequest,
) -> Result<HashSet<usize>, String> {
    let cutoff_epoch = request
        .max_age_days
        .map(|days| current_unix_timestamp_secs() - i64::from(days) * 86_400);
    let mut ranked = dispatches
        .iter()
        .enumerate()
        .map(|(index, record)| {
            let epoch = if cutoff_epoch.is_some() {
                parse_rfc3339_utc_timestamp_to_unix_secs(&record.audit.occurred_at).map_err(|error| {
                    format!(
                        "dispatch record '{}' has invalid occurredAt '{}': {error}",
                        record.audit.job_id.0, record.audit.occurred_at
                    )
                })?
            } else {
                0
            };
            Ok((index, epoch, record.audit.occurred_at.clone()))
        })
        .collect::<Result<Vec<_>, String>>()?;
    ranked.sort_by(|left, right| {
        right
            .1
            .cmp(&left.1)
            .then_with(|| right.2.cmp(&left.2))
            .then_with(|| left.0.cmp(&right.0))
    });

    let mut keep = HashSet::new();
    for (position, (index, epoch, _)) in ranked.into_iter().enumerate() {
        let within_entries = request.max_entries.map(|limit| position < limit).unwrap_or(true);
        let within_age = cutoff_epoch.map(|cutoff| epoch >= cutoff).unwrap_or(true);
        if within_entries && within_age {
            keep.insert(index);
        }
    }
    Ok(keep)
}

fn select_proof_keep_indices(
    proofs: &[ProofRecord],
    request: &AuditRetentionRequest,
) -> Result<HashSet<usize>, String> {
    let cutoff_epoch = request
        .max_age_days
        .map(|days| current_unix_timestamp_secs() - i64::from(days) * 86_400);
    let mut ranked = proofs
        .iter()
        .enumerate()
        .map(|(index, record)| {
            let timestamp = proof_retention_timestamp(record);
            let epoch = if cutoff_epoch.is_some() {
                parse_rfc3339_utc_timestamp_to_unix_secs(&timestamp).map_err(|error| {
                    format!(
                        "proof record '{}' has invalid retention timestamp '{}': {error}",
                        record.proof_job_id.0, timestamp
                    )
                })?
            } else {
                0
            };
            Ok((index, epoch, timestamp))
        })
        .collect::<Result<Vec<_>, String>>()?;
    ranked.sort_by(|left, right| {
        right
            .1
            .cmp(&left.1)
            .then_with(|| right.2.cmp(&left.2))
            .then_with(|| left.0.cmp(&right.0))
    });

    let mut keep = HashSet::new();
    for (position, (index, epoch, _)) in ranked.into_iter().enumerate() {
        let within_entries = request.max_entries.map(|limit| position < limit).unwrap_or(true);
        let within_age = cutoff_epoch.map(|cutoff| epoch >= cutoff).unwrap_or(true);
        if within_entries && within_age {
            keep.insert(index);
        }
    }
    Ok(keep)
}

fn preserve_required_dispatches(
    dispatches: &[PersistedDispatchRecord],
    proofs: &[ProofRecord],
    proof_keep: &HashSet<usize>,
    dispatch_keep: &mut HashSet<usize>,
) {
    for proof_index in proof_keep {
        let proof_job_id = &proofs[*proof_index].proof_job_id.0;
        for (dispatch_index, dispatch) in dispatches.iter().enumerate() {
            if dispatch.mode == "proof" && dispatch.audit.job_id.0 == *proof_job_id {
                dispatch_keep.insert(dispatch_index);
            }
        }
    }

    let mut dispatches_by_job_id = HashMap::<String, Vec<usize>>::new();
    for (index, record) in dispatches.iter().enumerate() {
        dispatches_by_job_id
            .entry(record.audit.job_id.0.clone())
            .or_default()
            .push(index);
    }

    let mut stack = dispatch_keep.iter().copied().collect::<Vec<_>>();
    while let Some(index) = stack.pop() {
        let record = &dispatches[index];
        if let Some(parent_job_id) = record.audit.parent_job_id.as_ref() {
            if let Some(parent_indexes) = dispatches_by_job_id.get(&parent_job_id.0) {
                for parent_index in parent_indexes {
                    if dispatch_keep.insert(*parent_index) {
                        stack.push(*parent_index);
                    }
                }
            }
        }
        let lineage_root = &record.audit.job_lineage_id.0;
        if lineage_root != &record.audit.job_id.0 {
            if let Some(root_indexes) = dispatches_by_job_id.get(lineage_root) {
                for root_index in root_indexes {
                    if dispatch_keep.insert(*root_index) {
                        stack.push(*root_index);
                    }
                }
            }
        }
    }
}

fn proof_retention_timestamp(record: &ProofRecord) -> String {
    record
        .decision
        .as_ref()
        .map(|decision| decision.occurred_at.clone())
        .unwrap_or_else(|| record.requested_at.clone())
}

fn build_backup_bundle(
    scope: AuditLedgerScope,
    removed_dispatches: &[PersistedDispatchRecord],
    removed_proofs: &[ProofRecord],
) -> AuditLedgerBundle {
    let created_at_utc = current_utc_timestamp_string();
    let file_name = format!(
        "audit-retention-{}-{}.json",
        scope_file_token(scope),
        compact_timestamp_token(&created_at_utc)
    );
    let artifact = AuditArtifactInfo {
        file_name: file_name.clone(),
        file_path: format!("backups/{file_name}"),
        created_at_utc: created_at_utc.clone(),
        size_bytes: 0,
    };

    AuditLedgerBundle {
        schema_version: AUDIT_BUNDLE_SCHEMA_VERSION,
        created_at_utc,
        scope,
        dispatches: removed_dispatches.to_vec(),
        proofs: removed_proofs.to_vec(),
        artifact,
    }
}

fn scope_file_token(scope: AuditLedgerScope) -> &'static str {
    match scope {
        AuditLedgerScope::All => "all",
        AuditLedgerScope::Dispatch => "dispatch",
        AuditLedgerScope::Proof => "proof",
    }
}

fn compact_timestamp_token(value: &str) -> String {
    value.chars().filter(|value| value.is_ascii_digit()).collect()
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

fn validate_legacy_seed_records(
    existing_dispatches: &[PersistedDispatchRecord],
    existing_proofs: &[ProofRecord],
    records: &[LegacyProofSeedRecord],
) -> Result<(), String> {
    let mut existing_job_ids = HashSet::new();
    let mut existing_lineages = HashSet::new();
    for dispatch in existing_dispatches
        .iter()
        .filter(|record| record.mode == "proof")
    {
        existing_job_ids.insert(dispatch.audit.job_id.0.as_str());
        existing_lineages.insert(dispatch.audit.job_lineage_id.0.as_str());
    }
    for proof in existing_proofs {
        existing_job_ids.insert(proof.proof_job_id.0.as_str());
        existing_lineages.insert(proof.job_lineage_id.0.as_str());
    }

    let mut batch_job_ids = HashSet::new();
    let mut batch_lineages = HashSet::new();
    for record in records {
        let proof_job_id = record.proof.proof_job_id.0.as_str();
        let job_lineage_id = record.proof.job_lineage_id.0.as_str();
        if existing_job_ids.contains(proof_job_id) {
            return Err(format!(
                "legacy proof job '{}' already exists in the audit ledger",
                proof_job_id
            ));
        }
        if existing_lineages.contains(job_lineage_id) {
            return Err(format!(
                "legacy proof lineage '{}' already exists in the audit ledger",
                job_lineage_id
            ));
        }
        if !batch_job_ids.insert(proof_job_id) {
            return Err(format!(
                "legacy proof job '{}' is duplicated in the seed batch",
                proof_job_id
            ));
        }
        if !batch_lineages.insert(job_lineage_id) {
            return Err(format!(
                "legacy proof lineage '{}' is duplicated in the seed batch",
                job_lineage_id
            ));
        }
    }
    Ok(())
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
    write_json_file(path, records).map(|_| ())
}

fn write_json_file<T>(path: &Path, value: &T) -> Result<u64, String>
where
    T: Serialize + ?Sized,
{
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| {
            format!(
                "failed to create audit ledger directory '{}': {error}",
                parent.display()
            )
        })?;
    }

    let payload = serde_json::to_string_pretty(value)
        .map_err(|error| format!("failed to serialize audit ledger '{}': {error}", path.display()))?;
    let temp_path = unique_temp_path(path);
    let mut file = fs::File::create(&temp_path)
        .map_err(|error| format!("failed to create audit ledger '{}': {error}", temp_path.display()))?;
    file.write_all(payload.as_bytes())
        .map_err(|error| format!("failed to write audit ledger '{}': {error}", temp_path.display()))?;
    file.sync_all()
        .map_err(|error| format!("failed to flush audit ledger '{}': {error}", temp_path.display()))?;
    drop(file);
    fs::rename(&temp_path, path).map_err(|error| {
        format!(
            "failed to finalize audit ledger '{}' from '{}': {error}",
            path.display(),
            temp_path.display()
        )
    })?;
    Ok(payload.len() as u64)
}

fn current_unix_timestamp_secs() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("system time should be after unix epoch")
        .as_secs() as i64
}

fn current_utc_timestamp_string() -> String {
    format_unix_timestamp_rfc3339(current_unix_timestamp_secs())
}

fn parse_rfc3339_utc_timestamp_to_unix_secs(value: &str) -> Result<i64, String> {
    let value = value.trim();
    if !value.ends_with('Z') {
        return Err("timestamp must end with 'Z'".to_string());
    }
    let core = &value[..value.len() - 1];
    let (date_part, time_part) = core
        .split_once('T')
        .ok_or_else(|| "timestamp must include 'T' separator".to_string())?;
    let mut date = date_part.split('-');
    let year = parse_i32_component(date.next(), "year")?;
    let month = parse_u32_component(date.next(), "month")?;
    let day = parse_u32_component(date.next(), "day")?;
    if date.next().is_some() {
        return Err("timestamp has extra date components".to_string());
    }

    let mut time = time_part.split(':');
    let hour = parse_u32_component(time.next(), "hour")?;
    let minute = parse_u32_component(time.next(), "minute")?;
    let second_value = time
        .next()
        .ok_or_else(|| "timestamp missing second component".to_string())?;
    if time.next().is_some() {
        return Err("timestamp has extra time components".to_string());
    }
    let second_component = second_value
        .split_once('.')
        .map(|(head, _)| head)
        .unwrap_or(second_value);
    let second = parse_u32_component(Some(second_component), "second")?;

    validate_calendar_components(year, month, day, hour, minute, second)?;

    let days = days_from_civil(year, month, day);
    Ok(days * 86_400 + i64::from(hour) * 3_600 + i64::from(minute) * 60 + i64::from(second))
}

fn parse_i32_component(value: Option<&str>, label: &str) -> Result<i32, String> {
    let value = value.ok_or_else(|| format!("timestamp missing {label} component"))?;
    value
        .parse::<i32>()
        .map_err(|_| format!("timestamp has invalid {label} component"))
}

fn parse_u32_component(value: Option<&str>, label: &str) -> Result<u32, String> {
    let value = value.ok_or_else(|| format!("timestamp missing {label} component"))?;
    value
        .parse::<u32>()
        .map_err(|_| format!("timestamp has invalid {label} component"))
}

fn validate_calendar_components(
    year: i32,
    month: u32,
    day: u32,
    hour: u32,
    minute: u32,
    second: u32,
) -> Result<(), String> {
    if !(1..=12).contains(&month) {
        return Err("timestamp month must be between 1 and 12".to_string());
    }
    if hour > 23 {
        return Err("timestamp hour must be between 0 and 23".to_string());
    }
    if minute > 59 {
        return Err("timestamp minute must be between 0 and 59".to_string());
    }
    if second > 59 {
        return Err("timestamp second must be between 0 and 59".to_string());
    }
    let max_day = days_in_month(year, month);
    if day == 0 || day > max_day {
        return Err(format!("timestamp day must be between 1 and {max_day}"));
    }
    Ok(())
}

fn days_in_month(year: i32, month: u32) -> u32 {
    match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 if is_leap_year(year) => 29,
        2 => 28,
        _ => 31,
    }
}

fn is_leap_year(year: i32) -> bool {
    (year % 4 == 0 && year % 100 != 0) || year % 400 == 0
}

fn days_from_civil(year: i32, month: u32, day: u32) -> i64 {
    let year = year - if month <= 2 { 1 } else { 0 };
    let era = if year >= 0 { year } else { year - 399 } / 400;
    let year_of_era = year - era * 400;
    let month = month as i32;
    let day_of_year =
        (153 * (month + if month > 2 { -3 } else { 9 }) + 2) / 5 + day as i32 - 1;
    let day_of_era = year_of_era * 365 + year_of_era / 4 - year_of_era / 100 + day_of_year;
    i64::from(era) * 146_097 + i64::from(day_of_era) - 719_468
}

fn format_unix_timestamp_rfc3339(value: i64) -> String {
    let days = value.div_euclid(86_400);
    let seconds_of_day = value.rem_euclid(86_400);
    let (year, month, day) = civil_from_days(days);
    let hour = seconds_of_day / 3_600;
    let minute = (seconds_of_day % 3_600) / 60;
    let second = seconds_of_day % 60;
    format!("{year:04}-{month:02}-{day:02}T{hour:02}:{minute:02}:{second:02}Z")
}

fn civil_from_days(days: i64) -> (i32, u32, u32) {
    let days = days + 719_468;
    let era = if days >= 0 { days } else { days - 146_096 } / 146_097;
    let day_of_era = days - era * 146_097;
    let year_of_era =
        (day_of_era - day_of_era / 1_460 + day_of_era / 36_524 - day_of_era / 146_096) / 365;
    let year = year_of_era as i32 + era as i32 * 400;
    let day_of_year = day_of_era - (365 * year_of_era + year_of_era / 4 - year_of_era / 100);
    let month_piece = (5 * day_of_year + 2) / 153;
    let day = day_of_year - (153 * month_piece + 2) / 5 + 1;
    let month = month_piece + if month_piece < 10 { 3 } else { -9 };
    let year = year + if month <= 2 { 1 } else { 0 };
    (year, month as u32, day as u32)
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
    use super::{AuditStore, LegacyProofSeedRecord, ProofReviewRequest};
    use audit_log::{
        AuditActor, AuditExportRequest, AuditLedgerScope, AuditQuery, AuditRetentionRequest,
        DispatchMatchSubject, PersistedDispatchRecord, PrintAuditRecord, PrintJobId,
        PrintJobLineageId, ProofRecord, ProofStatus,
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

    #[test]
    fn duplicate_proof_dispatch_records_are_rejected() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-0005");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(sample_proof_dispatch(&proof))
            .expect("dispatch should persist");
        store
            .record_dispatch(sample_proof_dispatch(&proof))
            .expect("duplicate dispatch should persist for regression coverage");
        store
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-PROOF-0005".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T12:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");

        let err = store
            .ensure_approved_proof_matches(
                "JOB-PROOF-0005",
                "basic-50x30@v1",
                &sample_match_subject(),
            )
            .expect_err("duplicate proof dispatch rows must be rejected");

        assert!(err.contains("multiple persisted proof dispatch records"));
    }

    #[test]
    fn legacy_seed_rejects_existing_lineage_conflicts() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-0006");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(sample_proof_dispatch(&proof))
            .expect("dispatch should persist");

        let err = store
            .seed_legacy_proofs(vec![sample_legacy_seed_record(
                "JOB-LEGACY-0001",
                &proof.job_lineage_id.0,
            )])
            .expect_err("existing lineage conflict should be rejected");

        assert!(err.contains("already exists"));
    }

    #[test]
    fn export_scopes_return_expected_ledgers() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-EXPORT");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(sample_proof_dispatch(&proof))
            .expect("dispatch should persist");

        let dispatch_only = store
            .export(AuditExportRequest {
                scope: Some(AuditLedgerScope::Dispatch),
            })
            .expect("dispatch export should succeed");
        assert_eq!(dispatch_only.dispatch_count, 1);
        assert_eq!(dispatch_only.proof_count, 0);

        let proof_only = store
            .export(AuditExportRequest {
                scope: Some(AuditLedgerScope::Proof),
            })
            .expect("proof export should succeed");
        assert_eq!(proof_only.dispatch_count, 0);
        assert_eq!(proof_only.proof_count, 1);
    }

    #[test]
    fn trim_dry_run_does_not_mutate_ledgers() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0001",
                "JOB-PRINT-0001",
                None,
                "2026-04-15T09:00:00Z",
            ))
            .expect("dispatch should persist");
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0002",
                "JOB-PRINT-0002",
                None,
                "2026-04-15T10:00:00Z",
            ))
            .expect("dispatch should persist");

        let result = store
            .trim(AuditRetentionRequest {
                scope: Some(AuditLedgerScope::Dispatch),
                max_age_days: None,
                max_entries: Some(1),
                dry_run: true,
            })
            .expect("dry run should succeed");

        assert_eq!(result.removed_dispatch_count, 1);
        assert!(result.backup.is_none());

        let snapshot = store.snapshot().expect("snapshot should load");
        assert_eq!(snapshot.dispatches.len(), 2);
    }

    #[test]
    fn trim_all_scope_keeps_proof_dispatch_dependency_chain() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        let proof = sample_proof("JOB-PROOF-TRIM");
        store
            .register_pending_proof(proof.clone())
            .expect("pending proof should persist");
        store
            .record_dispatch(sample_proof_dispatch_with_time(
                &proof,
                "2026-04-15T08:00:00Z",
            ))
            .expect("proof dispatch should persist");
        store
            .approve_proof(ProofReviewRequest {
                proof_job_id: "JOB-PROOF-TRIM".to_string(),
                actor_user_id: "manager.user".to_string(),
                actor_display_name: "Manager".to_string(),
                decided_at: "2026-04-15T09:00:00Z".to_string(),
                notes: Some("approved".to_string()),
            })
            .expect("proof should be approved");
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0100",
                "JOB-PROOF-TRIM",
                Some("JOB-PROOF-TRIM"),
                "2026-04-15T10:00:00Z",
            ))
            .expect("newer print dispatch should persist");
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0101",
                "JOB-PROOF-TRIM",
                Some("JOB-PRINT-0100"),
                "2026-04-15T11:00:00Z",
            ))
            .expect("newest print dispatch should persist");

        let result = store
            .trim(AuditRetentionRequest {
                scope: Some(AuditLedgerScope::All),
                max_age_days: None,
                max_entries: Some(1),
                dry_run: false,
            })
            .expect("trim should succeed");

        assert_eq!(result.retained_proof_count, 1);
        assert_eq!(result.retained_dispatch_count, 3);
        assert_eq!(result.removed_dispatch_count, 0);
        assert!(result.backup.is_none());

        let snapshot = store.snapshot().expect("snapshot should load");
        assert_eq!(snapshot.proofs.len(), 1);
        assert_eq!(snapshot.dispatches.len(), 3);
        assert!(snapshot
            .dispatches
            .iter()
            .any(|record| record.audit.job_id.0 == "JOB-PROOF-TRIM"));
    }

    #[test]
    fn trim_dispatch_scope_writes_backup_bundle() {
        let temp_dir = TestDir::new();
        let store = AuditStore::new(temp_dir.path().to_path_buf());
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0200",
                "JOB-PRINT-0200",
                None,
                "2026-04-15T09:00:00Z",
            ))
            .expect("dispatch should persist");
        store
            .record_dispatch(sample_print_dispatch(
                "JOB-PRINT-0201",
                "JOB-PRINT-0201",
                None,
                "2026-04-15T10:00:00Z",
            ))
            .expect("dispatch should persist");

        let result = store
            .trim(AuditRetentionRequest {
                scope: Some(AuditLedgerScope::Dispatch),
                max_age_days: None,
                max_entries: Some(1),
                dry_run: false,
            })
            .expect("trim should succeed");

        let backup = result.backup.expect("backup should be written");
        assert!(Path::new(&backup.file_path).exists());
        assert!(backup.file_name.ends_with(".json"));
        assert!(backup.size_bytes > 0);

        let snapshot = store.snapshot().expect("snapshot should load");
        assert_eq!(snapshot.dispatches.len(), 1);
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

    fn sample_proof_dispatch(proof: &ProofRecord) -> PersistedDispatchRecord {
        sample_proof_dispatch_with_time(proof, &proof.requested_at)
    }

    fn sample_proof_dispatch_with_time(
        proof: &ProofRecord,
        occurred_at: &str,
    ) -> PersistedDispatchRecord {
        PersistedDispatchRecord {
            audit: PrintAuditRecord::dispatch(
                proof.proof_job_id.clone(),
                proof.job_lineage_id.clone(),
                None,
                proof.requested_by.clone(),
                occurred_at.to_string(),
                None,
            ),
            mode: "proof".to_string(),
            template_version: "basic-50x30@v1".to_string(),
            match_subject: sample_match_subject(),
            artifact_media_type: "application/pdf".to_string(),
            artifact_byte_size: 128,
            submission_adapter_kind: "pdf".to_string(),
            submission_external_job_id: "proof-0001".to_string(),
        }
    }

    fn sample_legacy_seed_record(job_id: &str, lineage_id: &str) -> LegacyProofSeedRecord {
        let proof = ProofRecord::pending(
            PrintJobId(job_id.to_string()),
            PrintJobLineageId(lineage_id.to_string()),
            AuditActor {
                user_id: "legacy.user".to_string(),
                display_name: "Legacy Import".to_string(),
            },
            "2026-04-15T09:00:00Z".to_string(),
            format!("proofs/{job_id}.pdf"),
            Some("legacy seed".to_string()),
        );
        LegacyProofSeedRecord {
            dispatch: PersistedDispatchRecord {
                audit: PrintAuditRecord::dispatch(
                    proof.proof_job_id.clone(),
                    proof.job_lineage_id.clone(),
                    None,
                    proof.requested_by.clone(),
                    proof.requested_at.clone(),
                    Some("legacy proof seed".to_string()),
                ),
                mode: "proof".to_string(),
                template_version: "basic-50x30@v1".to_string(),
                match_subject: sample_match_subject(),
                artifact_media_type: "application/pdf".to_string(),
                artifact_byte_size: 128,
                submission_adapter_kind: "pdf".to_string(),
                submission_external_job_id: format!("legacy-seed:{job_id}"),
            },
            proof,
        }
    }

    fn sample_print_dispatch(
        job_id: &str,
        lineage_id: &str,
        parent_job_id: Option<&str>,
        occurred_at: &str,
    ) -> PersistedDispatchRecord {
        PersistedDispatchRecord {
            audit: PrintAuditRecord::dispatch(
                PrintJobId(job_id.to_string()),
                PrintJobLineageId(lineage_id.to_string()),
                parent_job_id.map(|value| PrintJobId(value.to_string())),
                AuditActor {
                    user_id: "ops.user".to_string(),
                    display_name: "Ops User".to_string(),
                },
                occurred_at.to_string(),
                None,
            ),
            mode: "print".to_string(),
            template_version: "basic-50x30@v1".to_string(),
            match_subject: sample_match_subject(),
            artifact_media_type: "application/pdf".to_string(),
            artifact_byte_size: 256,
            submission_adapter_kind: "pdf".to_string(),
            submission_external_job_id: format!("print:{job_id}"),
        }
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
