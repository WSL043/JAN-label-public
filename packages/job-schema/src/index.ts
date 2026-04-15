export type JanInput = {
  raw: string;
  normalized: string;
  source: "manual" | "import";
};

export type PrinterProfile = {
  id: string;
  adapter: "pdf" | "windows-spooler" | "zpl" | "tspl" | "qz";
  paperSize: string;
  dpi: number;
  scalePolicy: "fixed-100";
};

export type LabelTemplateRef = {
  id: string;
  version: string;
};

export type ExecutionMode = "proof" | "print";

export type ProofExecutionContext = {
  mode: "proof";
  requestedBy?: string;
  notes?: string;
  expiresAt?: string;
};

export type PrintExecutionContext = {
  mode: "print";
  approvedBy?: string;
  approvedAt?: string;
  sourceProofJobId?: string;
  allowWithoutProof?: boolean;
};

export type PrintExecutionIntent = ProofExecutionContext | PrintExecutionContext;

export type DispatchRequest = {
  jobId: string;
  sku: string;
  jan: string;
  qty: number;
  brand: string;
  templateVersion?: string;
  template?: LabelTemplateRef;
  printerProfile?: PrinterProfile;
  execution?: PrintExecutionIntent;
  actorUserId: string;
  actorDisplayName: string;
  requestedAt: string;
  jobLineageId?: string;
  reprintOfJobId?: string;
  reason?: string;
};

export type DispatchActor = {
  actorUserId: string;
  actorDisplayName: string;
};

export type DispatchRequestOptions = {
  templateVersion?: string;
  execution?: PrintExecutionIntent;
  jobLineageId?: string;
  reprintOfJobId?: string;
  reason?: string;
};

export const toDispatchRequest = (
  draft: PrintJobDraft,
  actor: DispatchActor,
  options: DispatchRequestOptions = {},
): DispatchRequest => ({
  jobId: draft.jobId,
  sku: draft.sku,
  jan: draft.jan.normalized,
  qty: draft.qty,
  brand: draft.brand,
  templateVersion: options.templateVersion ?? templateVersionOf(draft.template),
  printerProfile: draft.printerProfile,
  execution: options.execution ?? draft.execution,
  actorUserId: actor.actorUserId,
  actorDisplayName: actor.actorDisplayName,
  requestedAt: draft.requestedAt,
  jobLineageId: options.jobLineageId,
  reprintOfJobId: options.reprintOfJobId,
  reason: options.reason,
});

export const templateVersionOf = (template: LabelTemplateRef): string =>
  `${template.id}@${template.version}`;

export type PrintArtifactReport = {
  mediaType: string;
  byteSize: number;
};

export type PrintSubmissionReport = {
  adapterKind: string;
  externalJobId: string;
};

export type PrintDispatchAuditLineage = {
  jobId: string;
  jobLineageId: string;
  parentJobId?: string;
};

export type PrintDispatchAuditEvent =
  | "submitted"
  | "reprinted"
  | "created"
  | "completed"
  | "failed";

export type PrintDispatchResult = {
  mode: ExecutionMode;
  templateVersion: string;
  artifact: PrintArtifactReport;
  submission: PrintSubmissionReport;
  audit: {
    event: PrintDispatchAuditEvent;
    occurredAt: string;
    reason?: string;
  } & PrintDispatchAuditLineage;
};

export type PrintJobDraft = {
  jobId: string;
  parentSku: string;
  sku: string;
  jan: JanInput;
  qty: number;
  brand: string;
  template: LabelTemplateRef;
  execution?: PrintExecutionIntent;
  printerProfile: PrinterProfile;
  actor: string;
  requestedAt: string;
};
