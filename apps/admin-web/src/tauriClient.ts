import type { DispatchRequest, PrintDispatchResult } from "@label/job-schema";
import { invoke, isTauri } from "@tauri-apps/api/core";

const DISPATCH_PRINT_JOB_COMMAND = "dispatch_print_job";
const PRINT_BRIDGE_STATUS_COMMAND = "print_bridge_status";
const SEARCH_AUDIT_LOG_COMMAND = "search_audit_log";
const EXPORT_AUDIT_LEDGER_COMMAND = "export_audit_ledger";
const TRIM_AUDIT_LEDGER_COMMAND = "trim_audit_ledger";
const LIST_AUDIT_BACKUP_BUNDLES_COMMAND = "list_audit_backup_bundles";
const RESTORE_AUDIT_BACKUP_BUNDLE_COMMAND = "restore_audit_backup_bundle";
const APPROVE_PROOF_COMMAND = "approve_proof";
const REJECT_PROOF_COMMAND = "reject_proof";
const PREVIEW_TEMPLATE_DRAFT_COMMAND = "preview_template_draft";
const TEMPLATE_CATALOG_COMMAND = "template_catalog_command";
const SAVE_TEMPLATE_TO_LOCAL_CATALOG_COMMAND = "save_template_to_local_catalog";
const VALIDATE_LEGACY_PROOF_SEED_COMMAND = "validate_legacy_proof_seed";
const SEED_LEGACY_PROOFS_COMMAND = "seed_legacy_proofs";

export type BridgeWarningSeverity = "info" | "warning" | "error";

export type BridgeWarning = {
  code: string;
  severity: BridgeWarningSeverity;
  message: string;
};

export type PrintBridgeStatus = {
  availableAdapters: string[];
  resolvedZintPath: string;
  proofOutputDir: string;
  printOutputDir: string;
  spoolOutputDir: string;
  auditLogDir: string;
  auditBackupDir: string;
  printAdapterKind: string;
  windowsPrinterName: string;
  allowWithoutProofEnabled: boolean;
  warningDetails?: BridgeWarning[];
  warnings: string[];
};

export type AuditActor = {
  userId: string;
  displayName: string;
};

export type ProofStatus = "pending" | "approved" | "rejected" | "superseded";

export type ProofDecision = {
  status: ProofStatus;
  actor: AuditActor;
  occurredAt: string;
  notes?: string;
};

export type ProofRecord = {
  proofJobId: string;
  jobLineageId: string;
  requestedBy: AuditActor;
  requestedAt: string;
  status: ProofStatus;
  artifactPath: string;
  notes?: string;
  decision?: ProofDecision;
};

export type PersistedDispatchRecord = {
  audit: {
    jobId: string;
    jobLineageId: string;
    parentJobId?: string;
    actor: AuditActor;
    event: "submitted" | "reprinted" | "created" | "completed" | "failed";
    occurredAt: string;
    reason?: string;
  };
  mode: "proof" | "print";
  templateVersion: string;
  matchSubject: {
    sku: string;
    brand: string;
    janNormalized: string;
    qty: number;
  };
  artifactMediaType: string;
  artifactByteSize: number;
  submissionAdapterKind: string;
  submissionExternalJobId: string;
};

export type AuditSearchEntry = {
  dispatch: PersistedDispatchRecord;
  proof?: ProofRecord;
};

export type AuditSearchQuery = {
  searchText?: string;
  limit?: number;
};

export type AuditSearchResult = {
  entries: AuditSearchEntry[];
};

export type AuditLedgerScope = "all" | "dispatch" | "proof";

export type AuditLedgerSnapshot = {
  dispatches: PersistedDispatchRecord[];
  proofs: ProofRecord[];
};

export type AuditArtifactInfo = {
  fileName: string;
  filePath: string;
  createdAtUtc: string;
  sizeBytes: number;
};

export type AuditExportRequest = {
  scope?: AuditLedgerScope;
};

export type AuditExportResult = {
  scope: AuditLedgerScope;
  dispatchCount: number;
  proofCount: number;
  snapshot: AuditLedgerSnapshot;
};

export type AuditRetentionRequest = {
  scope?: AuditLedgerScope;
  maxAgeDays?: number;
  maxEntries?: number;
  dryRun?: boolean;
};

export type AuditRetentionResult = {
  scope: AuditLedgerScope;
  dryRun: boolean;
  retainedDispatchCount: number;
  retainedProofCount: number;
  removedDispatchCount: number;
  removedProofCount: number;
  backup?: AuditArtifactInfo;
};

export type AuditBackupRestoreRequest = {
  filePath: string;
};

export type AuditBackupRestoreResult = {
  restoredDispatchCount: number;
  restoredProofCount: number;
  bundle: AuditArtifactInfo;
  message: string;
};

export type ProofReviewRequest = {
  proofJobId: string;
  actorUserId: string;
  actorDisplayName: string;
  decidedAt: string;
  notes?: string;
};

export type LegacyProofSeedRequest = {
  rows: LegacyProofSeedRowRequest[];
};

export type LegacyProofSeedRowRequest = {
  proofJobId: string;
  artifactPath: string;
  templateVersion: string;
  matchSubject: {
    sku: string;
    brand: string;
    jan: string;
    qty: number;
  };
  requestedBy: AuditActor;
  requestedAt: string;
  jobLineageId?: string;
  notes?: string;
};

export type LegacyProofSeedRowStatus = "ok" | "error";

export type LegacyProofSeedRowResult = {
  rowIndex: number;
  proofJobId: string;
  status: LegacyProofSeedRowStatus;
  message: string;
  normalizedJan?: string;
  resolvedJobLineageId?: string;
  artifactPath?: string;
};

export type LegacyProofSeedResult = {
  applied: boolean;
  seededCount: number;
  message: string;
  rows: LegacyProofSeedRowResult[];
};

export type TemplateDraftPreviewSample = {
  jobId: string;
  sku: string;
  brand: string;
  jan: string;
  qty: number;
};

export type TemplateDraftPreviewRequest = {
  templateSource: string;
  sample: TemplateDraftPreviewSample;
};

export type TemplateDraftPreviewResult = {
  svg: string;
  normalizedJan: string;
  templateVersion: string;
  labelName: string;
  pageWidthMm: number;
  pageHeightMm: number;
  fieldCount: number;
};

export type TemplateCatalogEntry = {
  version: string;
  labelName: string;
  description?: string;
  source?: TemplateCatalogSource;
};
export type TemplateCatalogSource = "packaged" | "local" | "unknown";

export type SaveTemplateToLocalCatalogRequest = {
  templateSource: string;
};

export type SaveTemplateToLocalCatalogResult = {
  templateVersion?: string;
  status?: "created" | "updated" | "unchanged";
  message?: string;
};

export type TemplateCatalogResult = {
  defaultTemplateVersion: string;
  templates: TemplateCatalogEntry[];
};

export function isTauriConnected(): boolean {
  return isTauri();
}

export async function dispatchPrintJob(request: DispatchRequest): Promise<PrintDispatchResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to submit jobs.",
    );
  }
  return invoke<PrintDispatchResult>(DISPATCH_PRINT_JOB_COMMAND, { request });
}

export async function fetchPrintBridgeStatus(): Promise<PrintBridgeStatus> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to view status.",
    );
  }
  return invoke<PrintBridgeStatus>(PRINT_BRIDGE_STATUS_COMMAND, {});
}

export async function searchAuditLog(query: AuditSearchQuery = {}): Promise<AuditSearchResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to search audit data.",
    );
  }
  return invoke<AuditSearchResult>(SEARCH_AUDIT_LOG_COMMAND, { query });
}

export async function exportAuditLedger(
  request: AuditExportRequest = {},
): Promise<AuditExportResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to export audit data.",
    );
  }
  return invoke<AuditExportResult>(EXPORT_AUDIT_LEDGER_COMMAND, { request });
}

export async function trimAuditLedger(
  request: AuditRetentionRequest,
): Promise<AuditRetentionResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to trim audit data.",
    );
  }
  return invoke<AuditRetentionResult>(TRIM_AUDIT_LEDGER_COMMAND, { request });
}

export async function listAuditBackupBundles(): Promise<AuditArtifactInfo[]> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to list audit backup bundles.",
    );
  }
  return invoke<AuditArtifactInfo[]>(LIST_AUDIT_BACKUP_BUNDLES_COMMAND, {});
}

export async function restoreAuditBackupBundle(
  request: AuditBackupRestoreRequest,
): Promise<AuditBackupRestoreResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to restore audit bundles.",
    );
  }
  return invoke<AuditBackupRestoreResult>(RESTORE_AUDIT_BACKUP_BUNDLE_COMMAND, { request });
}

export async function approveProof(request: ProofReviewRequest): Promise<ProofRecord> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to approve proofs.",
    );
  }
  return invoke<ProofRecord>(APPROVE_PROOF_COMMAND, { request });
}

export async function rejectProof(request: ProofReviewRequest): Promise<ProofRecord> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to reject proofs.",
    );
  }
  return invoke<ProofRecord>(REJECT_PROOF_COMMAND, { request });
}

export async function previewTemplateDraft(
  request: TemplateDraftPreviewRequest,
): Promise<TemplateDraftPreviewResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to render template previews.",
    );
  }
  return invoke<TemplateDraftPreviewResult>(PREVIEW_TEMPLATE_DRAFT_COMMAND, { request });
}

export async function fetchTemplateCatalog(): Promise<TemplateCatalogResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to read template catalog.",
    );
  }
  return invoke<TemplateCatalogResult>(TEMPLATE_CATALOG_COMMAND, {});
}

export async function saveTemplateToLocalCatalog(
  request: SaveTemplateToLocalCatalogRequest,
): Promise<SaveTemplateToLocalCatalogResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to save template drafts.",
    );
  }
  return invoke<SaveTemplateToLocalCatalogResult>(SAVE_TEMPLATE_TO_LOCAL_CATALOG_COMMAND, {
    request,
  });
}

export async function validateLegacyProofSeed(
  request: LegacyProofSeedRequest,
): Promise<LegacyProofSeedResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to validate legacy proof seed rows.",
    );
  }
  return invoke<LegacyProofSeedResult>(VALIDATE_LEGACY_PROOF_SEED_COMMAND, { request });
}

export async function seedLegacyProofs(
  request: LegacyProofSeedRequest,
): Promise<LegacyProofSeedResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to seed legacy proofs.",
    );
  }
  return invoke<LegacyProofSeedResult>(SEED_LEGACY_PROOFS_COMMAND, { request });
}
