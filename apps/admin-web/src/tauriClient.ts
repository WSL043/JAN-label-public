import type { DispatchRequest, PrintDispatchResult } from "@label/job-schema";
import { invoke, isTauri } from "@tauri-apps/api/core";

const DISPATCH_PRINT_JOB_COMMAND = "dispatch_print_job";
const PRINT_BRIDGE_STATUS_COMMAND = "print_bridge_status";
const SEARCH_AUDIT_LOG_COMMAND = "search_audit_log";
const APPROVE_PROOF_COMMAND = "approve_proof";
const REJECT_PROOF_COMMAND = "reject_proof";

export type PrintBridgeStatus = {
  availableAdapters: string[];
  resolvedZintPath: string;
  proofOutputDir: string;
  printOutputDir: string;
  spoolOutputDir: string;
  printAdapterKind: string;
  windowsPrinterName: string;
  allowWithoutProofEnabled: boolean;
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

export type ProofReviewRequest = {
  proofJobId: string;
  actorUserId: string;
  actorDisplayName: string;
  decidedAt: string;
  notes?: string;
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
