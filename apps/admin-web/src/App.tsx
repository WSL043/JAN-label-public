import type {
  DispatchActor,
  DispatchRequest,
  DispatchRequestOptions,
  ExecutionMode,
  LabelTemplateRef,
  PrintDispatchResult,
  PrintExecutionContext,
  PrintExecutionIntent,
  PrintJobDraft,
  PrinterProfile,
  ProofExecutionContext,
} from "@label/job-schema";
import { toDispatchRequest } from "@label/job-schema";
import { useEffect, useEffectEvent, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import {
  type AuditSearchEntry,
  type PrintBridgeStatus,
  approveProof,
  dispatchPrintJob,
  fetchPrintBridgeStatus,
  isTauriConnected,
  rejectProof,
  searchAuditLog,
} from "./tauriClient";

const corePillars = [
  "Print correctness stays in Rust, not the UI.",
  "The admin screen only assembles a draft and operator intent.",
  "Printer differences stay behind printer profiles and adapters.",
];

const operatorNotes = [
  "JAN normalization remains in Rust. This interface flags 12-digit input but does not invent checksums.",
  "Template and printer route stay explicit and stable to keep core integration deterministic.",
  "Operators can map uploaded columns to draft fields so spreadsheets do not need perfect structure.",
];

const templateOptions: Array<LabelTemplateRef & { label: string; size: string }> = [
  {
    id: "basic-50x30",
    version: "v1",
    label: "Basic 50 x 30",
    size: "50mm x 30mm",
  },
];

const printerProfiles: Array<PrinterProfile & { label: string }> = [
  {
    id: "pdf-a4-proof",
    adapter: "pdf",
    paperSize: "A4",
    dpi: 300,
    scalePolicy: "fixed-100",
    label: "PDF A4 proof",
  },
];

const STORAGE_KEYS = {
  template: "label-admin-web.templateDraft.v1",
  mapping: "label-admin-web.columnMapping.v1",
  source: "label-admin-web.sourceReview.v1",
};
const TEMPLATE_ASSET_KIND = "admin-template-asset";
const TEMPLATE_ASSET_SCHEMA_VERSION = "template-asset-v1";

const TEMPLATE_SCHEMA_VERSION = "template-spec-v1";
const MAX_PERSISTED_SOURCE_ROWS = 200;
const HIGH_RISK_BRIDGE_WARNING_PATTERNS: ReadonlyArray<RegExp> = [
  /zint.*absolute path/i,
  /absolute path ['"].+?['"], but the file does not exist/i,
  /env.*zint.*does not exist/i,
  /not set; defaulting to 'default printer' while windows-spooler is selected/i,
  /may not be supported on non-windows hosts/i,
  /is set to .* but does not exist/i,
  /not available on this host; defaulting/i,
  /is unsupported; defaulting/i,
  /allow_print_without_proof is enabled/i,
];
const NON_BLOCKING_WARNING_PATTERNS: ReadonlyArray<RegExp> = [
  /is not set; defaulting/i,
  /will be created/i,
];

type PersistedItem<T> = {
  data: T;
  savedAt: string;
};

type PersistedTemplateStatus = {
  savedAt: string | null;
  status: "none" | "ok" | "stale" | "invalid";
  message: string | null;
};

type PersistedSourceStatus = {
  savedAt: string | null;
  status: "none" | "ok" | "invalid";
  message: string | null;
  rowsTruncated: boolean;
};
type PreparedRowStatus = "ready" | "pending" | "error";
type QueuedRowStatus = "ready" | "submitting" | "submitted" | "failed";
type SubmitPhase = "idle" | "submitting" | "success" | "error";
type SubmitState = {
  phase: SubmitPhase;
  message: string;
};
type ManualSubmitState = SubmitState & {
  result: PrintDispatchResult | null;
};
type BatchSubmitState = SubmitState & {
  results: PrintDispatchResult[];
};
type BridgeStatusPhase = "idle" | "loading" | "ready" | "unavailable" | "error";
type BridgeStatusState = {
  phase: BridgeStatusPhase;
  status: PrintBridgeStatus | null;
  message: string;
};
type AuditSearchPhase = "idle" | "loading" | "ready" | "unavailable" | "error";
type AuditSearchState = {
  phase: AuditSearchPhase;
  entries: AuditSearchEntry[];
  message: string;
  lastUpdatedAt: string | null;
};
type ProofReviewAction = "approve" | "reject" | null;
type ProofReviewState = SubmitState & {
  proofJobId: string | null;
  action: ProofReviewAction;
};

type FormState = {
  parentSku: string;
  sku: string;
  jan: string;
  qty: string;
  brand: string;
  templateId: string;
  printerProfileId: string;
  actor: string;
  executionMode: ExecutionMode;
  executionRequestedBy: string;
  executionNotes: string;
  executionApprovedBy: string;
  executionApprovedAt: string;
  executionSourceProofJobId: string;
  executionAllowWithoutProof: boolean;
};

type DraftSession = { jobId: string; requestedAt: string };
type TemplateAssetFormState = {
  parentSku?: string;
  sku?: string;
  jan?: string;
  qty?: string;
  brand?: string;
  templateId?: string;
  printerProfileId?: string;
  actor?: string;
  executionMode?: ExecutionMode;
  executionRequestedBy?: string;
  executionNotes?: string;
  executionApprovedBy?: string;
  executionApprovedAt?: string;
  executionSourceProofJobId?: string;
  executionAllowWithoutProof?: boolean;
};
type TemplateAssetPayload = {
  kind: typeof TEMPLATE_ASSET_KIND;
  schema_version: typeof TEMPLATE_ASSET_SCHEMA_VERSION;
  exportedAt: string;
  templateSource: string;
  formState?: TemplateAssetFormState;
  fieldMapping?: unknown;
  draftSnapshot?: PrintJobDraft | null;
};
type FormErrorKey =
  | keyof FormState
  | "executionApprovedAt"
  | "executionRequestedBy"
  | "executionApprovedBy"
  | "executionSourceProofJobId"
  | "executionNotes";
type FormErrors = Partial<Record<FormErrorKey, string>>;
type FieldKey = "parentSku" | "sku" | "jan" | "qty" | "brand";
type TemplateSpec = Record<string, unknown>;
type DataSource = {
  fileName: string;
  source: "csv" | "xlsx";
  headers: string[];
  rows: string[][];
};
type DataRow = Record<string, string>;
type ColumnMapping = Record<FieldKey, string>;
type PreparedRow = {
  rowIndex: number;
  sourceRow: DataRow;
  draft: PrintJobDraft | null;
  errors: string[];
  status: PreparedRowStatus;
  pendingReason: string | null;
};
type QueuedRow = PreparedRow & {
  submissionStatus: QueuedRowStatus;
  dispatchResult: PrintDispatchResult | null;
  dispatchError: string | null;
  retryLineageJobId: string | null;
};
type LabelTemplateMeta = {
  schemaVersion: string;
  templateVersion: string;
  labelName: string;
  description: string;
};

const defaultTemplateSpec: TemplateSpec = {
  schema_version: "template-spec-v1",
  template_version: "basic-50x30@v1",
  label_name: "basic-label",
  description: "Editable template spec used by the operator-facing draft builder.",
  page: { width_mm: 50, height_mm: 30, background_fill: "#ffffff" },
  fields: [
    {
      name: "job",
      x_mm: 2,
      y_mm: 5,
      font_size_mm: 3,
      template: "job:{job_id}",
    },
    {
      name: "brand",
      x_mm: 2,
      y_mm: 10,
      font_size_mm: 4,
      template: "brand:{brand}",
    },
    { name: "sku", x_mm: 2, y_mm: 15, font_size_mm: 4, template: "sku:{sku}" },
    { name: "jan", x_mm: 2, y_mm: 20, font_size_mm: 4, template: "jan:{jan}" },
    { name: "qty", x_mm: 2, y_mm: 25, font_size_mm: 4, template: "qty:{qty}" },
  ],
};

const fieldAliases: Record<FieldKey, string[]> = {
  parentSku: [
    "parentsku",
    "parent_sku",
    "parent-sku",
    "parentitem",
    "parent_item",
    "main_sku",
    "mainsku",
    "product_parent",
  ],
  sku: ["sku", "itemsku", "item_sku", "item-sku", "product_code", "item_code", "sku_code"],
  jan: ["jan", "ean", "jan_code", "jancode", "barcode", "bar_code", "upc"],
  qty: ["qty", "quantity", "qty_ordered", "count", "num", "num_piece", "amount"],
  brand: ["brand", "maker", "manufacturer", "label", "company", "brand_name"],
};
const TEMPLATE_SOURCE_ALIASES = [
  "template",
  "template_id",
  "template-id",
  "templateversion",
  "template_version",
  "template-version",
];
const PRINTER_SOURCE_ALIASES = [
  "printer_profile",
  "printer-profile",
  "printerprofile",
  "printer_id",
  "printer-id",
  "printer",
];
const ENABLED_SOURCE_ALIASES = [
  "enabled",
  "enable",
  "is_enabled",
  "is-enabled",
  "active",
  "is_active",
  "isactive",
];

const requiredFieldList: Array<{ key: FieldKey; label: string }> = [
  { key: "parentSku", label: "Parent SKU" },
  { key: "sku", label: "SKU" },
  { key: "jan", label: "JAN" },
  { key: "qty", label: "Quantity" },
  { key: "brand", label: "Brand" },
];

let sessionNonce = 0;

function createSession(): DraftSession {
  const requestedAt = new Date().toISOString();
  return { requestedAt, jobId: createJobId(requestedAt, ++sessionNonce) };
}

function createJobId(requestedAt: string, nonce: number): string {
  const digits = requestedAt.replace(/\D/g, "");
  return `JOB-${digits.slice(0, 8)}-${digits.slice(8, 14)}-${String(nonce).padStart(4, "0")}`;
}

function makeBatchJobId(requestedAt: string, rowIndex: number): string {
  return `${createJobId(requestedAt, rowIndex + 1)}-${String(rowIndex + 1).padStart(3, "0")}`;
}

function createRetriedDraft(
  draft: PrintJobDraft,
  rowIndex: number,
): { draft: PrintJobDraft; lineageJobId: string; retryReason: string } {
  const requestedAt = new Date().toISOString();
  return {
    draft: {
      ...draft,
      jobId: makeBatchJobId(requestedAt, rowIndex),
      requestedAt,
    },
    lineageJobId: draft.jobId,
    retryReason: `retry after failed queued submit from ${draft.jobId}`,
  };
}

function createInitialFormState(): FormState {
  return {
    parentSku: "",
    sku: "",
    jan: "",
    qty: "1",
    brand: "",
    templateId: templateOptions[0].id,
    printerProfileId: printerProfiles[0].id,
    actor: "ops.user",
    executionMode: "proof",
    executionRequestedBy: "",
    executionNotes: "",
    executionApprovedBy: "",
    executionApprovedAt: "",
    executionSourceProofJobId: "",
    executionAllowWithoutProof: false,
  };
}

function sanitizeTemplateString(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

function readString(spec: TemplateSpec, key: string): string {
  const value = spec[key];
  return typeof value === "string" ? value : "";
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeTemplateAssetFormState(raw: unknown): Partial<FormState> {
  const candidate = isPlainObject(raw) ? raw : null;
  if (!candidate) {
    return {};
  }
  const normalized: Partial<FormState> = {};
  if (typeof candidate.parentSku === "string") normalized.parentSku = candidate.parentSku;
  if (typeof candidate.sku === "string") normalized.sku = candidate.sku;
  if (typeof candidate.jan === "string") normalized.jan = candidate.jan;
  if (typeof candidate.qty === "string") normalized.qty = candidate.qty;
  if (typeof candidate.brand === "string") normalized.brand = candidate.brand;
  if (typeof candidate.templateId === "string") normalized.templateId = candidate.templateId;
  if (typeof candidate.printerProfileId === "string")
    normalized.printerProfileId = candidate.printerProfileId;
  if (typeof candidate.actor === "string") normalized.actor = candidate.actor;
  if (candidate.executionMode === "proof" || candidate.executionMode === "print") {
    normalized.executionMode = candidate.executionMode;
  }
  if (typeof candidate.executionRequestedBy === "string") {
    normalized.executionRequestedBy = candidate.executionRequestedBy;
  }
  if (typeof candidate.executionNotes === "string")
    normalized.executionNotes = candidate.executionNotes;
  if (typeof candidate.executionApprovedBy === "string")
    normalized.executionApprovedBy = candidate.executionApprovedBy;
  if (typeof candidate.executionApprovedAt === "string")
    normalized.executionApprovedAt = candidate.executionApprovedAt;
  if (typeof candidate.executionSourceProofJobId === "string") {
    normalized.executionSourceProofJobId = candidate.executionSourceProofJobId;
  }
  if (typeof candidate.executionAllowWithoutProof === "boolean") {
    normalized.executionAllowWithoutProof = candidate.executionAllowWithoutProof;
  }
  return normalized;
}

function parseDraftSnapshot(raw: unknown): PrintJobDraft | null {
  if (!isPlainObject(raw)) return null;
  const draft = raw as Partial<PrintJobDraft>;
  if (typeof draft.jobId !== "string" || draft.jobId.length === 0) return null;
  if (typeof draft.parentSku !== "string") return null;
  if (typeof draft.sku !== "string") return null;
  if (!isPlainObject(draft.jan)) return null;
  if (typeof draft.jan.raw !== "string" || typeof draft.jan.normalized !== "string") return null;
  if (!["manual", "import"].includes(draft.jan.source ?? "")) return null;
  if (typeof draft.qty !== "number" || !Number.isFinite(draft.qty)) return null;
  if (typeof draft.brand !== "string") return null;
  if (
    !isPlainObject(draft.template) ||
    typeof draft.template.id !== "string" ||
    typeof draft.template.version !== "string"
  )
    return null;
  if (!isPlainObject(draft.printerProfile)) return null;
  if (typeof draft.actor !== "string") return null;
  if (typeof draft.requestedAt !== "string") return null;
  return draft as PrintJobDraft;
}

function parseTemplateAsset(rawText: string):
  | {
      ok: true;
      templateSource: string;
      formState: Partial<FormState> | null;
      fieldMapping: ColumnMapping | null;
      draftSnapshot: PrintJobDraft | null;
      error: null;
    }
  | { ok: false; error: string } {
  const parsed = safeParseJson<unknown>(rawText);
  if (parsed === null) {
    return { ok: false, error: "Template import failed: invalid JSON." };
  }

  if (!isPlainObject(parsed)) {
    return {
      ok: false,
      error: "Template import failed: expected a JSON object.",
    };
  }

  const candidate = parsed;
  const likelyAsset =
    candidate.kind === TEMPLATE_ASSET_KIND ||
    candidate.schema_version === TEMPLATE_ASSET_SCHEMA_VERSION ||
    candidate.formState !== undefined ||
    candidate.fieldMapping !== undefined;

  let templateSource: string | null = null;
  if (likelyAsset) {
    const templateText =
      typeof candidate.templateSource === "string"
        ? candidate.templateSource
        : isPlainObject(candidate.template)
          ? JSON.stringify(candidate.template)
          : typeof candidate.template === "string"
            ? candidate.template
            : null;
    if (typeof templateText !== "string") {
      return {
        ok: false,
        error: "Template asset import requires `templateSource` or `template`.",
      };
    }
    templateSource = sanitizeTemplateString(templateText);
  } else {
    try {
      templateSource = sanitizeTemplateString(rawText);
    } catch {
      return { ok: false, error: "Template import failed: invalid JSON." };
    }
  }

  const parsedTemplate = parseTemplateSpec(templateSource);
  if (parsedTemplate.error || !parsedTemplate.spec) {
    return {
      ok: false,
      error: parsedTemplate.error ?? "Template import failed: template spec is invalid.",
    };
  }

  return {
    ok: true,
    templateSource,
    formState: likelyAsset
      ? normalizeTemplateAssetFormState((candidate.formState ?? candidate.form) as unknown)
      : null,
    fieldMapping: isPlainObject(candidate.fieldMapping)
      ? normalizeColumnMapping(candidate.fieldMapping)
      : isPlainObject(candidate.mapping)
        ? normalizeColumnMapping(candidate.mapping)
        : null,
    draftSnapshot: parseDraftSnapshot(candidate.draftSnapshot),
    error: null,
  };
}

function normalizeHeader(raw: string): string {
  return raw
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "");
}

function findSourceKey(row: DataRow, aliases: string[]): string | null {
  const normalizedCandidates = aliases.map((alias) => normalizeHeader(alias));
  return (
    Object.keys(row).find((key) => {
      const normalizedKey = normalizeHeader(key);
      return normalizedCandidates.includes(normalizedKey);
    }) ?? null
  );
}

function readSourceValue(row: DataRow, aliases: string[]): string {
  const sourceKey = findSourceKey(row, aliases);
  if (!sourceKey) {
    return "";
  }
  return (row[sourceKey] ?? "").trim();
}

function resolveEnabledSourceValue(row: DataRow): {
  enabled: boolean;
  invalid: boolean;
  reason: string | null;
} {
  const sourceKey = findSourceKey(row, ENABLED_SOURCE_ALIASES);
  if (!sourceKey) {
    return { enabled: true, invalid: false, reason: null };
  }

  const raw = (row[sourceKey] ?? "").trim();
  if (raw === "") {
    return {
      enabled: false,
      invalid: true,
      reason: `Enabled column '${sourceKey}' must be 'true' or 'false'.`,
    };
  }

  if (raw.toLowerCase() === "true") {
    return { enabled: true, invalid: false, reason: null };
  }

  if (raw.toLowerCase() === "false") {
    return {
      enabled: false,
      invalid: false,
      reason: "Row disabled by enabled column.",
    };
  }

  return {
    enabled: false,
    invalid: true,
    reason: `Enabled column '${sourceKey}' must be 'true' or 'false', got '${raw}'.`,
  };
}

function extractJanDigits(raw: string): string | null {
  const trimmed = raw.trim();
  return /^\d{12,13}$/.test(trimmed) ? trimmed : null;
}

function spreadsheetCellToString(cell: unknown): string {
  if (cell === null || cell === undefined) {
    return "";
  }

  if (typeof cell === "string") {
    return cell.trim();
  }

  if (typeof cell === "number" || typeof cell === "boolean" || typeof cell === "bigint") {
    return String(cell);
  }

  if (typeof cell === "object") {
    const candidate = cell as { w?: unknown; v?: unknown };
    if (typeof candidate.w === "string" && candidate.w.trim().length > 0) {
      return candidate.w.trim();
    }
    if (candidate.v !== undefined && candidate.v !== null) {
      return String(candidate.v).trim();
    }
  }

  return String(cell).trim();
}

function isHighRiskBridgeWarning(warning: string): boolean {
  if (NON_BLOCKING_WARNING_PATTERNS.some((pattern) => pattern.test(warning))) {
    return false;
  }
  return HIGH_RISK_BRIDGE_WARNING_PATTERNS.some((pattern) => pattern.test(warning));
}

function resolveTemplateFromSourceValue(
  raw: string,
  fallback: LabelTemplateRef | null,
  templates: LabelTemplateRef[],
): { template: LabelTemplateRef | null; reason: string | null } {
  const value = raw.trim();
  if (!value) {
    if (fallback) {
      return { template: fallback, reason: null };
    }
    return {
      template: null,
      reason: "Template is missing and no fallback is selected.",
    };
  }

  const at = value.lastIndexOf("@");
  const parsed: LabelTemplateRef =
    at > 0 && at < value.length - 1
      ? { id: value.slice(0, at).trim(), version: value.slice(at + 1).trim() }
      : { id: value.trim(), version: "" };

  const exact = templates.find((option) =>
    parsed.version
      ? option.id === parsed.id && option.version === parsed.version
      : option.id === parsed.id,
  );
  if (exact) {
    return { template: exact, reason: null };
  }

  if (!parsed.version && fallback && fallback.id === parsed.id) {
    return { template: fallback, reason: null };
  }

  return {
    template: null,
    reason: `Template override '${value}' is unknown. Use template_id[@version] from available templates.`,
  };
}

function resolvePrinterFromSourceValue(
  raw: string,
  fallback: PrinterProfile | null,
  printers: PrinterProfile[],
): { printer: PrinterProfile | null; reason: string | null } {
  const value = raw.trim();
  if (!value) {
    if (fallback) {
      return { printer: fallback, reason: null };
    }
    return {
      printer: null,
      reason: "Printer profile is missing and no fallback is selected.",
    };
  }
  const exact = printers.find((option) => option.id === value);
  if (exact) {
    return { printer: exact, reason: null };
  }
  return {
    printer: null,
    reason: `Printer override '${value}' is unknown. Use printer_profile id from available printer list.`,
  };
}

function parseTemplateSpec(raw: string): {
  spec: TemplateSpec | null;
  error: string | null;
} {
  try {
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return { spec: null, error: "Template file must be a JSON object." };
    }
    return { spec: parsed as TemplateSpec, error: null };
  } catch {
    return {
      spec: null,
      error: "Invalid JSON. Please fix syntax before continuing.",
    };
  }
}

function parseTemplateRef(spec: TemplateSpec | null): LabelTemplateRef | null {
  if (!spec) {
    return null;
  }
  const versionValue = readString(spec, "template_version");
  const at = versionValue.lastIndexOf("@");
  if (at <= 0 || at >= versionValue.length - 1) {
    return null;
  }
  return {
    id: versionValue.slice(0, at).trim(),
    version: versionValue.slice(at + 1).trim(),
  };
}

function templateMeta(spec: TemplateSpec | null): LabelTemplateMeta | null {
  if (!spec) {
    return null;
  }
  const schemaVersion = readString(spec, "schema_version");
  const templateVersion = readString(spec, "template_version");
  const labelName = readString(spec, "label_name");
  const description = readString(spec, "description");
  if (!schemaVersion && !templateVersion && !labelName && !description) {
    return null;
  }
  return {
    schemaVersion: schemaVersion || "missing",
    templateVersion: templateVersion || "missing",
    labelName: labelName || "missing",
    description,
  };
}

function queuedRowStatusClass(status: QueuedRowStatus): string {
  switch (status) {
    case "ready":
      return "status-ok";
    case "submitting":
      return "status-submitting";
    case "submitted":
      return "status-ok";
    case "failed":
      return "status-fail";
    default:
      return "";
  }
}

function formatQueuedRowResult(row: QueuedRow): string {
  if (row.dispatchResult) {
    return `Dispatch accepted (${row.dispatchResult.mode} / ${row.dispatchResult.submission.externalJobId})`;
  }
  if (row.dispatchError) {
    return `Failed: ${row.dispatchError}`;
  }
  if (row.submissionStatus === "ready") {
    return "Ready";
  }
  if (row.submissionStatus === "submitting") {
    return "Submitting...";
  }
  return "Not submitted";
}

function pickTemplateRef(
  formTemplateId: string,
  parsedTemplate: LabelTemplateRef | null,
): LabelTemplateRef | null {
  if (parsedTemplate) {
    return parsedTemplate;
  }
  return templateOptions.find((option) => option.id === formTemplateId) ?? null;
}

type ParsedCsv = { headers: string[]; rows: string[][] };

type PersistedTemplatePayload = {
  templateSource: string;
};
type PersistedMappingPayload = {
  mapping: ColumnMapping;
};
type PersistedSourcePayload = {
  sourceData: Omit<DataSource, "rows"> & {
    rows: string[][];
    source: "csv" | "xlsx";
  };
  rowsTruncated: boolean;
  originalRowCount: number;
};

const defaultTemplateSource = () => sanitizeTemplateString(JSON.stringify(defaultTemplateSpec));

function isBrowserStorageAvailable(): boolean {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

function safeParseJson<T>(value: string | null): T | null {
  if (!value) {
    return null;
  }
  try {
    return JSON.parse(value) as T;
  } catch {
    return null;
  }
}

function readStorageItem<T>(key: string): PersistedItem<T> | null {
  if (!isBrowserStorageAvailable()) {
    return null;
  }
  const value = localStorage.getItem(key);
  const parsed = safeParseJson<PersistedItem<T>>(value);
  if (!parsed?.data || typeof parsed.savedAt !== "string") {
    return null;
  }
  return parsed;
}

function writeStorageItem<T>(key: string, data: T): void {
  if (!isBrowserStorageAvailable()) {
    return;
  }
  try {
    const payload = JSON.stringify({
      data,
      savedAt: new Date().toISOString(),
    });
    localStorage.setItem(key, payload);
  } catch {
    // Intentionally ignore storage failures (for example quota limits), preserving operator flow.
  }
}

function removeStorageItem(key: string): void {
  if (!isBrowserStorageAvailable()) {
    return;
  }
  try {
    localStorage.removeItem(key);
  } catch {
    // Ignore for resilience.
  }
}

function formatSavedAt(value: string | null): string {
  if (!value) {
    return "never";
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.valueOf())) {
    return value;
  }
  return parsed.toLocaleString();
}

function toDateTimeLocalInput(value: string | null | undefined): string {
  if (!value) {
    return "";
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.valueOf())) {
    return "";
  }
  const offset = parsed.getTimezoneOffset() * 60_000;
  return new Date(parsed.getTime() - offset).toISOString().slice(0, 16);
}

function proofStatusClass(status: string | null | undefined): string {
  switch (status) {
    case "approved":
      return "status-ok";
    case "pending":
      return "status-pending";
    case "rejected":
    case "superseded":
      return "status-fail";
    default:
      return "";
  }
}

function normalizeColumnMapping(raw: unknown): ColumnMapping {
  const empty: ColumnMapping = {
    parentSku: "",
    sku: "",
    jan: "",
    qty: "",
    brand: "",
  };
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return empty;
  }
  for (const key of Object.keys(empty) as FieldKey[]) {
    const candidate = (raw as Record<string, unknown>)[key];
    if (typeof candidate === "string") {
      empty[key] = candidate.trim();
    }
  }
  return empty;
}

function sanitizeTemplateDraft(raw: string): string {
  const parsed = safeParseJson<TemplateSpec>(raw);
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    return raw;
  }
  return sanitizeTemplateString(raw);
}

function validateTemplateSource(raw: string): {
  status: "ok" | "stale" | "invalid";
  message: string | null;
} {
  const parsed = parseTemplateSpec(raw);
  if (parsed.error || !parsed.spec) {
    return {
      status: "invalid",
      message: parsed.error ?? "Template draft cannot be parsed as JSON.",
    };
  }
  const spec = parsed.spec;
  const templateRef = parseTemplateRef(spec);
  const schemaVersion = readString(spec, "schema_version");
  const templateVersion = readString(spec, "template_version");
  const detail: string[] = [];
  if (!templateRef) {
    detail.push("template_version is missing or invalid (expected template-id@version).");
  }
  if (!schemaVersion) {
    detail.push(`schema_version is missing (expected ${TEMPLATE_SCHEMA_VERSION}).`);
  } else if (schemaVersion !== TEMPLATE_SCHEMA_VERSION) {
    detail.push(`schema_version is ${schemaVersion}; expected ${TEMPLATE_SCHEMA_VERSION}.`);
  }
  if (!templateVersion) {
    detail.push("template_version is missing.");
  }
  if (detail.length > 0) {
    return templateRef
      ? { status: "stale", message: detail.join(" ") }
      : { status: "invalid", message: detail.join(" ") };
  }
  return { status: "ok", message: null };
}

function loadTemplateDraftFromStorage(): {
  templateSource: string;
  status: PersistedTemplateStatus;
} {
  const stored = readStorageItem<PersistedTemplatePayload>(STORAGE_KEYS.template);
  if (!stored) {
    const templateSource = defaultTemplateSource();
    return {
      templateSource,
      status: { savedAt: null, status: "none", message: null },
    };
  }
  const templateSource = sanitizeTemplateDraft(stored.data.templateSource);
  const validation = validateTemplateSource(templateSource);
  const savedAt = stored.savedAt || null;
  return {
    templateSource,
    status: {
      savedAt,
      status: validation.status,
      message:
        validation.status === "ok"
          ? "Saved template draft restored."
          : `Saved template draft restored with ${validation.status} validation issue: ${validation.message}`,
    },
  };
}

function loadColumnMappingFromStorage(): {
  mapping: ColumnMapping;
  status: PersistedTemplateStatus;
} {
  const stored = readStorageItem<PersistedMappingPayload>(STORAGE_KEYS.mapping);
  const mapping = normalizeColumnMapping(stored?.data.mapping);
  const hasStorage = !!stored;
  return {
    mapping,
    status: {
      savedAt: stored?.savedAt ?? null,
      status: hasStorage ? "ok" : "none",
      message: hasStorage ? "Saved column mapping loaded." : null,
    },
  };
}

function restoreSourceFromStorage(): {
  source: DataSource | null;
  status: PersistedSourceStatus;
} {
  const stored = readStorageItem<PersistedSourcePayload>(STORAGE_KEYS.source);
  if (!stored) {
    return {
      source: null,
      status: {
        savedAt: null,
        status: "none",
        message: null,
        rowsTruncated: false,
      },
    };
  }

  const data = stored.data?.sourceData;
  if (
    !data ||
    typeof data.fileName !== "string" ||
    (data.source !== "csv" && data.source !== "xlsx") ||
    !Array.isArray(data.headers) ||
    !Array.isArray(data.rows)
  ) {
    return {
      source: null,
      status: {
        savedAt: stored.savedAt ?? null,
        status: "invalid",
        message: "Saved source data shape is invalid.",
        rowsTruncated: false,
      },
    };
  }
  const cleanedHeaders = (data.headers as unknown[])
    .map((header: unknown) => String(header ?? "").trim())
    .filter((header) => header.length > 0);
  const cleanedRows = data.rows
    .map((row: unknown) =>
      Array.isArray(row) ? row.map((cell: unknown) => String(cell ?? "").trim()) : [],
    )
    .filter((row: string[]) => row.length > 0 && row.some((cell) => cell.length > 0))
    .slice(0, MAX_PERSISTED_SOURCE_ROWS);
  return {
    source: {
      fileName: data.fileName,
      source: data.source,
      headers: cleanedHeaders,
      rows: cleanedRows,
    },
    status: {
      savedAt: stored.savedAt ?? null,
      status: "ok",
      message:
        stored.data.rowsTruncated || stored.data.originalRowCount > MAX_PERSISTED_SOURCE_ROWS
          ? `Source restored partially. Only first ${MAX_PERSISTED_SOURCE_ROWS} rows kept in local storage.`
          : "Saved source review state restored.",
      rowsTruncated:
        stored.data.rowsTruncated || stored.data.originalRowCount > MAX_PERSISTED_SOURCE_ROWS,
    },
  };
}

function parseCsvRows(content: string): ParsedCsv {
  const rows: string[][] = [];
  let row: string[] = [];
  let current = "";
  let inQuotes = false;

  const pushField = () => {
    row.push(current);
    current = "";
  };

  const pushRow = () => {
    if (row.length > 1 || (row.length === 1 && row[0].trim() !== "")) {
      rows.push(row.map((cell) => cell.trim()));
    }
    row = [];
  };

  for (let i = 0; i <= content.length; i += 1) {
    const char = content[i] ?? "\n";
    if (char === '"') {
      if (content[i + 1] === '"') {
        current += '"';
        i += 1;
        continue;
      }
      inQuotes = !inQuotes;
      continue;
    }
    if (char === "," && !inQuotes) {
      pushField();
      continue;
    }
    if ((char === "\n" || char === "\r") && !inQuotes) {
      if (char === "\r" && content[i + 1] === "\n") {
        i += 1;
      }
      pushField();
      pushRow();
      continue;
    }
    current += char;
  }

  const nonEmptyRows = rows.filter((entry) => entry.some((cell) => cell.length > 0));
  if (nonEmptyRows.length === 0) {
    throw new Error("No rows found in CSV source.");
  }
  const normalizedHeaders = nonEmptyRows[0].map((header) => header.trim());
  if (normalizedHeaders.length === 0 || normalizedHeaders.every((header) => header === "")) {
    throw new Error("CSV header row is empty.");
  }
  return {
    headers: normalizedHeaders,
    rows: nonEmptyRows.slice(1).filter((rowData) => rowData.length > 0),
  };
}

function parseUploadedData(file: File): Promise<DataSource> {
  return new Promise((resolve, reject) => {
    const extension = file.name.split(".").pop()?.toLowerCase() ?? "";
    const isXlsx = extension === "xlsx" || extension === "xls";
    const isCsv = extension === "csv" || extension === "txt";

    if (isCsv) {
      const reader = new FileReader();
      reader.onerror = () => reject(new Error("CSV file read failed."));
      reader.onload = () => {
        const text = String(reader.result || "");
        try {
          const rows = parseCsvRows(text);
          resolve({
            fileName: file.name,
            source: "csv",
            headers: rows.headers,
            rows: rows.rows,
          });
        } catch (error) {
          reject(error instanceof Error ? error : new Error("Failed to parse CSV file."));
        }
      };
      reader.readAsText(file, "utf-8");
      return;
    }

    if (isXlsx) {
      const reader = new FileReader();
      reader.onerror = () => reject(new Error("Spreadsheet read failed."));
      reader.onload = async () => {
        try {
          const xlsx = await import("xlsx");
          const workbook = xlsx.read(reader.result as ArrayBuffer, {
            type: "array",
          });
          const firstSheet = workbook.SheetNames[0];
          if (!firstSheet) {
            reject(new Error("Spreadsheet does not contain a sheet."));
            return;
          }
          const sheet = workbook.Sheets[firstSheet];
          const rangeRef = typeof sheet["!ref"] === "string" ? sheet["!ref"] : null;
          if (!rangeRef) {
            reject(new Error("Spreadsheet has no readable cell range."));
            return;
          }
          const range = xlsx.utils.decode_range(rangeRef);
          const table: string[][] = [];
          for (let rowIndex = range.s.r; rowIndex <= range.e.r; rowIndex += 1) {
            const row: string[] = [];
            for (let columnIndex = range.s.c; columnIndex <= range.e.c; columnIndex += 1) {
              const cellAddress = xlsx.utils.encode_cell({
                r: rowIndex,
                c: columnIndex,
              });
              row.push(spreadsheetCellToString(sheet[cellAddress]));
            }
            table.push(row);
          }
          if (!table.length) {
            reject(new Error("Spreadsheet has no data rows."));
            return;
          }
          const headers = table[0]?.map((header) => header.trim()) ?? [];
          const dataRows = table
            .slice(1)
            .filter((row) => row.some((cell) => cell.trim().length > 0));
          if (!headers.length) {
            reject(new Error("Spreadsheet header row is empty."));
            return;
          }
          resolve({
            fileName: file.name,
            source: "xlsx",
            headers,
            rows: dataRows,
          });
        } catch (error) {
          reject(error instanceof Error ? error : new Error("Failed to parse spreadsheet file."));
        }
      };
      reader.readAsArrayBuffer(file);
      return;
    }

    reject(new Error("Unsupported extension. Please upload CSV or XLSX."));
  });
}

function detectMapping(headers: string[]): ColumnMapping {
  const used = new Set<string>();
  const normalizedToHeaders = new Map<string, string[]>();
  for (const header of headers) {
    const normalized = normalizeHeader(header);
    const existing = normalizedToHeaders.get(normalized);
    if (existing) {
      existing.push(header);
      continue;
    }
    normalizedToHeaders.set(normalized, [header]);
  }
  const mapping: ColumnMapping = {
    parentSku: "",
    sku: "",
    jan: "",
    qty: "",
    brand: "",
  };

  for (const key of Object.keys(fieldAliases) as FieldKey[]) {
    const aliases = [key, ...fieldAliases[key]].map((item) => normalizeHeader(item));
    const matchedHeader = aliases
      .map((alias) => normalizedToHeaders.get(alias)?.[0])
      .find((candidate) => candidate !== undefined && !used.has(candidate));
    if (matchedHeader) {
      mapping[key] = matchedHeader;
      used.add(matchedHeader);
    }
  }
  return mapping;
}

function reconcileMappingWithHeaders(mapping: ColumnMapping, headers: string[]): ColumnMapping {
  const headerSet = new Set(headers);
  const reconciled = { ...mapping };
  for (const key of Object.keys(mapping) as FieldKey[]) {
    if (mapping[key] && !headerSet.has(mapping[key])) {
      reconciled[key] = "";
    }
  }
  return reconciled;
}

function toSourceRows(source: DataSource | null): DataRow[] {
  if (!source) {
    return [];
  }
  return source.rows.map((row) => {
    const record: DataRow = {};
    source.headers.forEach((header, index) => {
      record[header] = (row[index] ?? "").trim();
    });
    return record;
  });
}

function buildExecutionIntent(form: FormState): PrintExecutionIntent {
  if (form.executionMode === "print") {
    const approvedBy = form.executionApprovedBy.trim();
    const approvedAt = form.executionApprovedAt.trim();
    const sourceProofJobId = form.executionSourceProofJobId.trim();
    const intent: PrintExecutionContext = {
      mode: "print",
    };
    if (approvedBy) {
      intent.approvedBy = approvedBy;
    }
    if (approvedAt) {
      intent.approvedAt = approvedAt;
    }
    if (sourceProofJobId) {
      intent.sourceProofJobId = sourceProofJobId;
    }
    if (form.executionAllowWithoutProof) {
      intent.allowWithoutProof = true;
    }
    return intent;
  }
  const requestedBy = form.executionRequestedBy.trim() || form.actor.trim() || "ops.user";
  const notes = form.executionNotes.trim();
  const intent: ProofExecutionContext = {
    mode: "proof",
    requestedBy,
    ...(notes ? { notes } : {}),
  };
  return intent;
}

function toDispatchRequestWithActor(
  draft: PrintJobDraft,
  options: DispatchRequestOptions = {},
): DispatchRequest {
  const actorId = draft.actor.trim() || "ops.user";
  const dispatchActor: DispatchActor = {
    actorUserId: actorId,
    actorDisplayName: actorId,
  };
  return toDispatchRequest(draft, dispatchActor, options);
}

function buildDraftFromRow(input: {
  rowIndex: number;
  sourceRow: DataRow;
  mapping: ColumnMapping;
  templateRef: LabelTemplateRef | null;
  printerProfile: PrinterProfile | null;
  templateRefs: LabelTemplateRef[];
  printerProfiles: PrinterProfile[];
  actor: string;
  execution: PrintExecutionIntent;
  janSourceHint: string;
}): PreparedRow {
  const errors: string[] = [];
  const row = input.sourceRow;
  const actor = input.actor.trim();
  const enabledResolution = resolveEnabledSourceValue(row);
  if (enabledResolution.invalid) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: [enabledResolution.reason ?? "Enabled column is invalid."],
      status: "error",
      pendingReason: null,
    };
  }
  if (!enabledResolution.enabled) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: [],
      status: "pending",
      pendingReason: "Row disabled by enabled column.",
    };
  }
  const templateFromSource = readSourceValue(row, TEMPLATE_SOURCE_ALIASES);
  const printerFromSource = readSourceValue(row, PRINTER_SOURCE_ALIASES);
  const templateResolution = resolveTemplateFromSourceValue(
    templateFromSource,
    input.templateRef,
    input.templateRefs,
  );
  const printerResolution = resolvePrinterFromSourceValue(
    printerFromSource,
    input.printerProfile,
    input.printerProfiles,
  );
  const template = templateResolution.template;
  const printerProfile = printerResolution.printer;
  if (!template) {
    if (templateFromSource) {
      return {
        rowIndex: input.rowIndex,
        sourceRow: row,
        draft: null,
        errors: [],
        status: "pending",
        pendingReason: templateResolution.reason,
      };
    }
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: ["Template reference is missing. Fix template import or template selection."],
      status: "error",
      pendingReason: null,
    };
  }
  if (!printerProfile) {
    if (printerFromSource) {
      return {
        rowIndex: input.rowIndex,
        sourceRow: row,
        draft: null,
        errors: [],
        status: "pending",
        pendingReason: printerResolution.reason,
      };
    }
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: ["Printer profile is required."],
      status: "error",
      pendingReason: null,
    };
  }
  const missingColumn = requiredFieldList.filter((entry) => input.mapping[entry.key].length === 0);
  if (missingColumn.length > 0) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      status: "error",
      errors: [`Map missing columns: ${missingColumn.map((entry) => entry.label).join(", ")}.`],
      pendingReason: null,
    };
  }
  if (!actor) {
    errors.push("Actor is required.");
  }

  const getValue = (key: FieldKey) => {
    const column = input.mapping[key];
    return column ? (row[column] ?? "") : "";
  };
  const parentSku = getValue("parentSku").trim();
  const sku = getValue("sku").trim();
  const janRaw = getValue("jan").trim();
  const brand = getValue("brand").trim();
  const qtyText = getValue("qty").trim();

  if (!parentSku) errors.push("Parent SKU is required.");
  if (!sku) errors.push("SKU is required.");
  if (!brand) errors.push("Brand is required.");
  if (!qtyText || !/^\d+$/.test(qtyText)) errors.push("Quantity must be an integer.");
  const janDigits = extractJanDigits(janRaw);
  if (!janDigits) {
    errors.push("JAN must be 12 or 13 digits with digits only.");
  }
  const qty = Number.parseInt(qtyText.replace(/,/g, ""), 10);
  if (!Number.isFinite(qty) || qty < 1) errors.push("Quantity must be >= 1.");
  if (errors.length > 0) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors,
      status: "error",
      pendingReason: null,
    };
  }
  if (input.execution.mode === "print") {
    if (!input.execution.approvedBy?.trim()) {
      errors.push("Print requires approvedBy for each queued row.");
    }
    if (!input.execution.approvedAt?.trim()) {
      errors.push("Print requires approvedAt for each queued row.");
    }
    if (!input.execution.allowWithoutProof && !input.execution.sourceProofJobId?.trim()) {
      errors.push("Print requires sourceProofJobId unless allowWithoutProof is enabled.");
    }
    if (input.execution.approvedAt && Number.isNaN(Date.parse(input.execution.approvedAt))) {
      errors.push("Print approvedAt must be a valid datetime.");
    }
    if (errors.length > 0) {
      return {
        rowIndex: input.rowIndex,
        sourceRow: row,
        draft: null,
        errors,
        status: "error",
        pendingReason: null,
      };
    }
  }

  const requestedAt = new Date().toISOString();
  const normalizedJan = janDigits ?? "";
  return {
    rowIndex: input.rowIndex,
    sourceRow: row,
    draft: {
      jobId: makeBatchJobId(requestedAt, input.rowIndex),
      parentSku,
      sku,
      jan: {
        raw: janRaw,
        normalized: normalizedJan,
        source: input.janSourceHint === "import" ? "import" : "manual",
      },
      qty,
      brand,
      template,
      printerProfile: {
        id: printerProfile.id,
        adapter: printerProfile.adapter,
        paperSize: printerProfile.paperSize,
        dpi: printerProfile.dpi,
        scalePolicy: printerProfile.scalePolicy,
      },
      execution: input.execution,
      actor,
      requestedAt,
    },
    errors: [],
    status: "ready",
    pendingReason: null,
  };
}

function validateDraft(
  form: FormState,
  session: DraftSession,
  templateRef: LabelTemplateRef | null,
  printerProfile: PrinterProfile | null,
  execution: PrintExecutionIntent,
): { draft: PrintJobDraft | null; errors: FormErrors } {
  const errors: FormErrors = {};
  const parentSku = form.parentSku.trim();
  const sku = form.sku.trim();
  const jan = form.jan.trim();
  const qty = form.qty.trim();
  const brand = form.brand.trim();
  const actor = form.actor.trim();

  if (!parentSku) errors.parentSku = "Parent SKU is required.";
  if (!sku) errors.sku = "SKU is required.";
  if (!brand) errors.brand = "Brand is required.";
  if (!actor) errors.actor = "Actor is required.";
  const janDigits = extractJanDigits(jan);
  if (!/^\d+$/.test(qty) || Number.parseInt(qty, 10) < 1) {
    errors.qty = "Quantity must be an integer greater than or equal to 1.";
  }
  if (!janDigits) {
    errors.jan = "JAN must be 12 or 13 digits with digits only.";
  }
  if (execution.mode === "print") {
    if (!execution.approvedBy?.trim()) {
      errors.executionApprovedBy = "Print requires an approvedBy user.";
    }
    if (!execution.approvedAt?.trim()) {
      errors.executionApprovedAt = "Print requires an approvedAt datetime.";
    }
    if (!execution.allowWithoutProof && !execution.sourceProofJobId?.trim()) {
      errors.executionSourceProofJobId =
        "Print requires sourceProofJobId unless allowWithoutProof is enabled.";
    }
    if (execution.approvedAt && Number.isNaN(Date.parse(execution.approvedAt))) {
      errors.executionApprovedAt = "approvedAt must be a valid datetime.";
    }
  }
  if (!templateRef) errors.templateId = "Template selection is required.";
  if (!printerProfile) errors.printerProfileId = "Printer profile selection is required.";

  if (Object.keys(errors).length > 0 || !templateRef || !printerProfile) {
    return { draft: null, errors };
  }

  const normalizedJan = janDigits ?? "";
  return {
    errors,
    draft: {
      jobId: session.jobId,
      parentSku,
      sku,
      jan: { raw: jan, normalized: normalizedJan, source: "manual" },
      qty: Number.parseInt(qty, 10),
      brand,
      template: templateRef,
      printerProfile: {
        id: printerProfile.id,
        adapter: printerProfile.adapter,
        paperSize: printerProfile.paperSize,
        dpi: printerProfile.dpi,
        scalePolicy: printerProfile.scalePolicy,
      },
      execution,
      actor,
      requestedAt: session.requestedAt,
    },
  };
}

function downloadText(filename: string, value: string): void {
  const blob = new Blob([value], { type: "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function buildTemplateAssetPayload(
  form: FormState,
  mapping: ColumnMapping,
  templateSource: string,
  draftSnapshot: PrintJobDraft | null,
): TemplateAssetPayload {
  return {
    kind: TEMPLATE_ASSET_KIND,
    schema_version: TEMPLATE_ASSET_SCHEMA_VERSION,
    exportedAt: new Date().toISOString(),
    templateSource: sanitizeTemplateString(templateSource),
    formState: {
      parentSku: form.parentSku,
      sku: form.sku,
      jan: form.jan,
      qty: form.qty,
      brand: form.brand,
      templateId: form.templateId,
      printerProfileId: form.printerProfileId,
      actor: form.actor,
      executionMode: form.executionMode,
      executionRequestedBy: form.executionRequestedBy,
      executionNotes: form.executionNotes,
      executionApprovedBy: form.executionApprovedBy,
      executionApprovedAt: form.executionApprovedAt,
      executionSourceProofJobId: form.executionSourceProofJobId,
      executionAllowWithoutProof: form.executionAllowWithoutProof,
    },
    fieldMapping: mapping,
    draftSnapshot,
  };
}

export function App() {
  const initialTemplateState = loadTemplateDraftFromStorage();
  const initialMappingState = loadColumnMappingFromStorage();
  const initialSourceState = restoreSourceFromStorage();

  const [session, setSession] = useState(createSession);
  const [form, setForm] = useState(createInitialFormState);
  const [showErrors, setShowErrors] = useState(false);
  const [draftSnapshot, setDraftSnapshot] = useState<PrintJobDraft | null>(null);
  const [manualSubmit, setManualSubmit] = useState<ManualSubmitState>({
    phase: "idle",
    message: "",
    result: null,
  });
  const [batchSubmit, setBatchSubmit] = useState<BatchSubmitState>({
    phase: "idle",
    message: "",
    results: [],
  });
  const [bridgeStatus, setBridgeStatus] = useState<BridgeStatusState>({
    phase: "loading",
    status: null,
    message: "Checking desktop bridge availability...",
  });
  const [auditQuery, setAuditQuery] = useState("");
  const [auditSearch, setAuditSearch] = useState<AuditSearchState>({
    phase: "idle",
    entries: [],
    message: "Audit ledger has not been loaded yet.",
    lastUpdatedAt: null,
  });
  const [proofReviewNotes, setProofReviewNotes] = useState("");
  const [proofReview, setProofReview] = useState<ProofReviewState>({
    phase: "idle",
    message: "",
    proofJobId: null,
    action: null,
  });

  const [templateSource, setTemplateSource] = useState(initialTemplateState.templateSource);
  const [templateParseError, setTemplateParseError] = useState<string | null>(null);
  const [templateImportError, setTemplateImportError] = useState("");
  const [templatePersistedState, setTemplatePersistedState] = useState(initialTemplateState.status);

  const [sourceData, setSourceData] = useState<DataSource | null>(initialSourceState.source);
  const [sourceError, setSourceError] = useState<string | null>(null);
  const [fieldMapping, setFieldMapping] = useState<ColumnMapping>(
    reconcileMappingWithHeaders(
      initialMappingState.mapping,
      initialSourceState.source?.headers ?? [],
    ),
  );
  const [mappingPersistedState, setMappingPersistedState] = useState(initialMappingState.status);
  const [queuedRows, setQueuedRows] = useState<QueuedRow[]>([]);
  const [restoreStateNotice, setRestoreStateNotice] = useState<string | null>(null);
  const [sourcePersistedState, setSourcePersistedState] = useState(initialSourceState.status);

  const parsedTemplate = useMemo(() => parseTemplateSpec(templateSource), [templateSource]);
  const templateReference = parsedTemplate.spec ? parseTemplateRef(parsedTemplate.spec) : null;
  const templateMetaInfo = useMemo(() => templateMeta(parsedTemplate.spec), [parsedTemplate.spec]);
  const resolvedTemplateRef = useMemo(
    () => pickTemplateRef(form.templateId, templateReference),
    [form.templateId, templateReference],
  );
  const selectedPrinterProfile = useMemo(
    () => printerProfiles.find((option) => option.id === form.printerProfileId),
    [form.printerProfileId],
  );
  const executionIntent = useMemo(() => buildExecutionIntent(form), [form]);
  const templateOptionLabel = resolvedTemplateRef
    ? `${resolvedTemplateRef.id} / ${resolvedTemplateRef.version}`
    : "Missing";
  const templateCandidates = useMemo(() => {
    const candidates: LabelTemplateRef[] = templateOptions.map((entry) => ({
      id: entry.id,
      version: entry.version,
    }));
    if (templateReference) {
      const exists = candidates.some(
        (option) =>
          option.id === templateReference.id && option.version === templateReference.version,
      );
      if (!exists) {
        candidates.push(templateReference);
      }
    }
    return candidates;
  }, [templateReference]);

  const { draft, errors } = validateDraft(
    form,
    session,
    resolvedTemplateRef,
    selectedPrinterProfile ?? null,
    executionIntent,
  );
  const visibleErrors = showErrors ? errors : {};
  const template = templateOptions.find((option) => option.id === resolvedTemplateRef?.id);
  const liveDraftJson = draft ? JSON.stringify(draft, null, 2) : null;
  const snapshotJson = draftSnapshot ? JSON.stringify(draftSnapshot, null, 2) : null;
  const draftIsStale =
    Boolean(snapshotJson) && Boolean(liveDraftJson) && snapshotJson !== liveDraftJson;
  const previewJson = snapshotJson ?? liveDraftJson;

  const sourceRows = useMemo(() => toSourceRows(sourceData), [sourceData]);
  const preparedRows = useMemo(() => {
    if (!sourceData) {
      return [];
    }
    return sourceRows.map((sourceRow, rowIndex) =>
      buildDraftFromRow({
        rowIndex,
        sourceRow,
        mapping: fieldMapping,
        templateRef: resolvedTemplateRef,
        templateRefs: templateCandidates,
        printerProfiles,
        printerProfile: selectedPrinterProfile ?? null,
        actor: form.actor,
        execution: executionIntent,
        janSourceHint: "import",
      }),
    );
  }, [
    executionIntent,
    fieldMapping,
    form.actor,
    resolvedTemplateRef,
    selectedPrinterProfile,
    sourceRows,
    sourceData,
    templateCandidates,
  ]);

  const parsedTemplateRows = preparedRows.slice(0, 8);
  const readyRowsCount = preparedRows.filter((entry) => entry.status === "ready").length;
  const pendingRowsCount = preparedRows.filter((entry) => entry.status === "pending").length;
  const errorRowsCount = preparedRows.length - readyRowsCount - pendingRowsCount;
  const queuedReadyRowsCount = queuedRows.filter(
    (entry) => entry.submissionStatus === "ready",
  ).length;
  const queuedSubmittingRowsCount = queuedRows.filter(
    (entry) => entry.submissionStatus === "submitting",
  ).length;
  const queuedSubmittedRowsCount = queuedRows.filter(
    (entry) => entry.submissionStatus === "submitted",
  ).length;
  const queuedFailedRowsCount = queuedRows.filter(
    (entry) => entry.submissionStatus === "failed",
  ).length;
  const canSubmitQueuedRows = queuedRows.some(
    (entry) => entry.submissionStatus === "ready" || entry.submissionStatus === "failed",
  );
  const isQueueReady = sourceRows.length > 0 && readyRowsCount > 0;
  const sourceSummary = sourceData
    ? `${sourceData.rows.length} rows / ${sourceData.headers.length} columns (${sourceData.source})`
    : "No source loaded";
  const previewBatchJson =
    queuedRows.length > 0 ? JSON.stringify(queuedRows[0].draft, null, 2) : null;
  const hasPersistedState =
    templatePersistedState.status !== "none" ||
    mappingPersistedState.status !== "none" ||
    sourcePersistedState.status !== "none";
  const executionModeLabel = form.executionMode === "print" ? "print-ready" : "proof-only";
  const executionMeta =
    form.executionMode === "print" ? "Print-ready mode" : "Proof-only review mode";
  const executionModeChipClass = form.executionMode === "print" ? "print" : "proof";
  const bridgeStatusAvailable = bridgeStatus.status !== null && bridgeStatus.phase === "ready";
  const bridgeWarnings = bridgeStatus.status?.warnings ?? [];
  const allowWithoutProofEnabled = bridgeStatus.status?.allowWithoutProofEnabled ?? false;
  const blockingBridgeWarnings = bridgeWarnings.filter(isHighRiskBridgeWarning);
  const hasBlockingBridgeWarnings = blockingBridgeWarnings.length > 0;
  const isBridgeSubmitAllowed = bridgeStatusAvailable && !hasBlockingBridgeWarnings;
  const isBridgeSubmitBlocked = !isBridgeSubmitAllowed || bridgeStatus.phase === "loading";
  const bridgeSubmitBlockMessage = isBridgeSubmitBlocked
    ? bridgeStatus.phase === "loading"
      ? "Submit is temporarily disabled while bridge status is being checked."
      : !bridgeStatusAvailable
        ? "Browser preview mode / desktop bridge unavailable. Connect via desktop shell to submit."
        : hasBlockingBridgeWarnings
          ? "Submit is blocked due to high-risk bridge warnings. Resolve warnings before dispatch."
          : null
    : null;
  const reviewActorId = form.executionApprovedBy.trim() || form.actor.trim() || "ops.user";
  const approvedProofEntries = useMemo(() => {
    const deduped = new Map<string, AuditSearchEntry>();
    for (const entry of auditSearch.entries) {
      if (entry.proof?.status !== "approved") {
        continue;
      }
      deduped.set(entry.proof.proofJobId, entry);
    }
    return Array.from(deduped.values()).sort((left, right) =>
      right.dispatch.audit.occurredAt.localeCompare(left.dispatch.audit.occurredAt),
    );
  }, [auditSearch.entries]);

  const templateValidation = validateTemplateSource(templateSource);

  useEffect(() => {
    if (!allowWithoutProofEnabled && form.executionAllowWithoutProof) {
      setForm((current) => ({ ...current, executionAllowWithoutProof: false }));
    }
  }, [allowWithoutProofEnabled, form.executionAllowWithoutProof]);

  function updateField<Key extends keyof FormState>(key: Key, value: FormState[Key]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function updateTemplateSource(value: string) {
    setTemplateSource(value);
    setTemplateParseError(null);
    setTemplateImportError("");
  }

  function updateFieldMapping(field: FieldKey, value: string) {
    setFieldMapping((current) => ({ ...current, [field]: value }));
  }

  function handleTemplateImport(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0];
    if (!file) {
      return;
    }
    const reader = new FileReader();
    reader.onerror = () => {
      setTemplateImportError("Template import failed.");
      setTemplateParseError("Template import failed.");
    };
    reader.onload = () => {
      const raw = String(reader.result || "");
      const parsed = parseTemplateAsset(raw);
      if (!parsed.ok) {
        setTemplateImportError(parsed.error);
        setTemplateParseError(parsed.error);
        return;
      }
      setTemplateSource(parsed.templateSource);

      const parsedTemplate = parseTemplateSpec(parsed.templateSource);
      if (parsedTemplate.error) {
        setTemplateImportError(`Imported template is not valid JSON: ${parsedTemplate.error}`);
        setTemplateParseError(parsedTemplate.error);
        return;
      }
      const sourceValidation = validateTemplateSource(parsed.templateSource);
      if (sourceValidation.status === "invalid") {
        setTemplateImportError(`Imported template is invalid: ${sourceValidation.message}`);
        setTemplateParseError(sourceValidation.message);
        return;
      }
      setTemplateImportError(
        sourceValidation.status === "stale"
          ? `Imported template is stale: ${sourceValidation.message}`
          : "",
      );
      setTemplateParseError(null);

      if (parsed.formState) {
        setForm(() => ({
          ...createInitialFormState(),
          ...parsed.formState,
        }));
      }
      if (parsed.fieldMapping) {
        setFieldMapping(parsed.fieldMapping);
      }
      if (parsed.draftSnapshot) {
        setDraftSnapshot(parsed.draftSnapshot);
      } else {
        setDraftSnapshot(null);
      }
    };
    reader.readAsText(file, "utf-8");
  }

  function handleTemplateAssetExport() {
    const payload = buildTemplateAssetPayload(form, fieldMapping, templateSource, draftSnapshot);
    downloadText("template-asset.json", JSON.stringify(payload, null, 2));
  }

  function handleTemplateExport() {
    downloadText("template-spec.json", templateSource);
  }

  function validateTemplateText() {
    const parsed = parseTemplateSpec(templateSource);
    if (parsed.error) {
      setTemplateParseError(parsed.error);
      return;
    }
    setTemplateParseError(null);
    setTemplateSource(sanitizeTemplateString(templateSource));
  }

  function resetTemplateToDefaults() {
    const text = sanitizeTemplateString(JSON.stringify(defaultTemplateSpec));
    setTemplateSource(text);
    setTemplateParseError(null);
    setTemplateImportError("");
  }

  async function handleDataUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0];
    if (!file) {
      return;
    }
    setSourceError(null);
    setSourceData(null);
    setQueuedRows([]);
    try {
      const parsed = await parseUploadedData(file);
      setSourceData(parsed);
      setFieldMapping(detectMapping(parsed.headers));
    } catch (error) {
      setSourceError(error instanceof Error ? error.message : "Data import failed.");
    }
  }

  function autoDetectMapping() {
    if (!sourceData) return;
    setFieldMapping(detectMapping(sourceData.headers));
  }

  function buildQueueSnapshot() {
    setBatchSubmit({ phase: "idle", message: "", results: [] });
    setQueuedRows(
      preparedRows
        .filter((entry) => entry.status === "ready" && entry.draft !== null)
        .map((entry) => ({
          ...entry,
          submissionStatus: "ready" as const,
          dispatchResult: null,
          dispatchError: null,
          retryLineageJobId: null,
        }))
        .slice(),
    );
  }

  const refreshAuditSearch = useEffectEvent(async (searchText?: string) => {
    if (!isTauriConnected()) {
      setAuditSearch({
        phase: "unavailable",
        entries: [],
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell.",
        lastUpdatedAt: null,
      });
      return;
    }

    setAuditSearch((current) => ({
      ...current,
      phase: "loading",
      message: "Loading proof inbox and audit ledger...",
    }));

    try {
      const result = await searchAuditLog({
        searchText: searchText?.trim() || auditQuery.trim() || undefined,
        limit: 40,
      });
      setAuditSearch({
        phase: "ready",
        entries: result.entries,
        message: `Loaded ${result.entries.length} audit entr${result.entries.length === 1 ? "y" : "ies"}.`,
        lastUpdatedAt: new Date().toISOString(),
      });
    } catch (error) {
      setAuditSearch({
        phase: "error",
        entries: [],
        message: `Audit search failed: ${formatErrorMessage(error)}`,
        lastUpdatedAt: null,
      });
    }
  });

  const refreshBridgeStatus = useEffectEvent(async () => {
    if (!isTauriConnected()) {
      setBridgeStatus({
        phase: "unavailable",
        status: null,
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell.",
      });
      setAuditSearch({
        phase: "unavailable",
        entries: [],
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell.",
        lastUpdatedAt: null,
      });
      return;
    }

    setBridgeStatus((current) => ({
      ...current,
      phase: "loading",
      message: "Reading bridge health and output paths...",
    }));

    try {
      const status = await fetchPrintBridgeStatus();
      setBridgeStatus({
        phase: "ready",
        status,
        message: `Bridge ready. Active adapter: ${status.printAdapterKind}`,
      });
      void refreshAuditSearch();
    } catch (error) {
      setBridgeStatus({
        phase: "error",
        status: null,
        message: `Bridge status check failed: ${formatErrorMessage(error)}`,
      });
    }
  });

  function formatErrorMessage(error: unknown): string {
    if (error instanceof Error) {
      return error.message;
    }
    return "Unknown error while submitting dispatch request.";
  }

  async function dispatchDraft(
    draft: PrintJobDraft,
    options: DispatchRequestOptions = {},
  ): Promise<PrintDispatchResult> {
    const request = toDispatchRequestWithActor(draft, options);
    return dispatchPrintJob(request);
  }

  function applyApprovedProofToForm(entry: AuditSearchEntry) {
    const proof = entry.proof;
    if (proof?.status !== "approved") {
      return;
    }
    setForm((current) => ({
      ...current,
      executionMode: "print",
      executionApprovedBy:
        proof.decision?.actor.displayName ||
        current.executionApprovedBy ||
        current.actor ||
        "ops.user",
      executionApprovedAt:
        toDateTimeLocalInput(proof.decision?.occurredAt) || current.executionApprovedAt,
      executionSourceProofJobId: proof.proofJobId,
      executionAllowWithoutProof: false,
    }));
  }

  const submitProofReview = useEffectEvent(
    async (entry: AuditSearchEntry, action: "approve" | "reject") => {
      if (!entry.proof) {
        return;
      }

      setProofReview({
        phase: "submitting",
        message: `${action === "approve" ? "Approving" : "Rejecting"} ${entry.proof.proofJobId}...`,
        proofJobId: entry.proof.proofJobId,
        action,
      });

      const request = {
        proofJobId: entry.proof.proofJobId,
        actorUserId: reviewActorId,
        actorDisplayName: reviewActorId,
        decidedAt: new Date().toISOString(),
        notes: proofReviewNotes.trim() || undefined,
      };

      try {
        const proof =
          action === "approve" ? await approveProof(request) : await rejectProof(request);
        setProofReview({
          phase: "success",
          message: `Proof ${proof.proofJobId} marked ${proof.status}.`,
          proofJobId: proof.proofJobId,
          action,
        });
        if (proof.status === "approved") {
          applyApprovedProofToForm({ dispatch: entry.dispatch, proof });
        }
        void refreshAuditSearch();
      } catch (error) {
        setProofReview({
          phase: "error",
          message: `Proof review failed: ${formatErrorMessage(error)}`,
          proofJobId: entry.proof.proofJobId,
          action,
        });
      }
    },
  );

  async function submitManualDraft() {
    setShowErrors(true);
    if (!draft) {
      setManualSubmit({
        phase: "error",
        message: "Cannot submit: manual draft is not valid. Complete the form and snapshot first.",
        result: null,
      });
      return;
    }
    if (isBridgeSubmitBlocked || bridgeSubmitBlockMessage) {
      setManualSubmit({
        phase: "error",
        message: bridgeSubmitBlockMessage ?? "Submit is blocked by bridge safety checks.",
        result: null,
      });
      return;
    }
    setManualSubmit({
      phase: "submitting",
      message: "Submitting manual draft to desktop backend...",
      result: null,
    });
    try {
      const result = await dispatchDraft(draft);
      setManualSubmit({
        phase: "success",
        message: `Manual draft submitted for ${result.audit.jobId} (${result.mode}).`,
        result,
      });
      setDraftSnapshot(draft);
      setSession(createSession());
      void refreshAuditSearch();
    } catch (error) {
      setManualSubmit({
        phase: "error",
        message: `Manual submit failed: ${formatErrorMessage(error)}`,
        result: null,
      });
    }
  }

  async function submitQueuedRows() {
    const rows = queuedRows
      .map((entry, queueIndex) => ({ entry, queueIndex }))
      .filter(
        (item): item is { entry: QueuedRow; queueIndex: number } => item.entry.draft !== null,
      );

    if (isBridgeSubmitBlocked || bridgeSubmitBlockMessage) {
      setBatchSubmit({
        phase: "error",
        message: bridgeSubmitBlockMessage ?? "Batch submit is blocked by bridge safety checks.",
        results: [],
      });
      return;
    }
    if (rows.length === 0) {
      setBatchSubmit({
        phase: "error",
        message: "No queued row drafts available. Snapshot rows before submitting.",
        results: [],
      });
      return;
    }
    setBatchSubmit({
      phase: "submitting",
      message: `Submitting ${rows.length} queued draft(s)...`,
      results: [],
    });
    const results: PrintDispatchResult[] = [];
    const failures: string[] = [];

    for (const item of rows) {
      const currentDraft = item.entry.draft as PrintJobDraft;
      const retryContext =
        item.entry.submissionStatus === "failed"
          ? createRetriedDraft(currentDraft, item.queueIndex)
          : null;
      const activeDraft = retryContext?.draft ?? currentDraft;
      const lineageJobId = item.entry.retryLineageJobId ?? retryContext?.lineageJobId ?? null;
      const dispatchOptions: DispatchRequestOptions = {};
      if (lineageJobId) {
        dispatchOptions.jobLineageId = lineageJobId;
      }
      if (retryContext?.retryReason) {
        dispatchOptions.reason = retryContext.retryReason;
      }

      setQueuedRows((current) =>
        current.map((row, queueIndex) =>
          queueIndex === item.queueIndex
            ? {
                ...row,
                draft: activeDraft,
                submissionStatus: "submitting",
                dispatchResult: null,
                dispatchError: null,
                retryLineageJobId: lineageJobId,
              }
            : row,
        ),
      );
      setBatchSubmit({
        phase: "submitting",
        message: `Submitting queued draft ${item.queueIndex + 1}/${rows.length}...`,
        results: [...results],
      });

      try {
        const result = await dispatchDraft(activeDraft, dispatchOptions);
        results.push(result);
        setQueuedRows((current) =>
          current.map((row, queueIndex) =>
            queueIndex === item.queueIndex
              ? {
                  ...row,
                  draft: activeDraft,
                  submissionStatus: "submitted",
                  dispatchResult: result,
                  dispatchError: null,
                  retryLineageJobId: lineageJobId,
                }
              : row,
          ),
        );
      } catch (error) {
        const message = formatErrorMessage(error);
        failures.push(message);
        setQueuedRows((current) =>
          current.map((row, queueIndex) =>
            queueIndex === item.queueIndex
              ? {
                  ...row,
                  draft: activeDraft,
                  submissionStatus: "failed",
                  dispatchResult: null,
                  dispatchError: message,
                  retryLineageJobId: lineageJobId,
                }
              : row,
          ),
        );
      }
    }

    if (failures.length > 0) {
      setBatchSubmit({
        phase: "error",
        message: `Batch submit partially failed (${results.length}/${rows.length} succeeded): ${failures[0]}`,
        results,
      });
      void refreshAuditSearch();
      return;
    }
    setBatchSubmit({
      phase: "success",
      message: `Batch submit succeeded for ${results.length} queued draft(s).`,
      results,
    });
    setSession(createSession());
    void refreshAuditSearch();
  }

  function resetDataSource() {
    setSourceData(null);
    setSourceError(null);
    setFieldMapping({
      parentSku: "",
      sku: "",
      jan: "",
      qty: "",
      brand: "",
    });
    setQueuedRows([]);
    setBatchSubmit({ phase: "idle", message: "", results: [] });
  }

  const persistTemplateDraft = useEffectEvent(() => {
    writeStorageItem(STORAGE_KEYS.template, {
      templateSource,
    });
  });

  const persistMapping = useEffectEvent(() => {
    writeStorageItem(STORAGE_KEYS.mapping, {
      mapping: fieldMapping,
    });
  });

  const persistSourceReview = useEffectEvent(() => {
    if (!sourceData) {
      return;
    }
    const rowsToStore = sourceData.rows.slice(0, MAX_PERSISTED_SOURCE_ROWS);
    writeStorageItem(STORAGE_KEYS.source, {
      sourceData: {
        fileName: sourceData.fileName,
        source: sourceData.source,
        headers: sourceData.headers,
        rows: rowsToStore,
      },
      rowsTruncated: sourceData.rows.length > MAX_PERSISTED_SOURCE_ROWS,
      originalRowCount: sourceData.rows.length,
    });
  });

  function clearPersistedState() {
    removeStorageItem(STORAGE_KEYS.template);
    removeStorageItem(STORAGE_KEYS.mapping);
    removeStorageItem(STORAGE_KEYS.source);
    setTemplatePersistedState({ savedAt: null, status: "none", message: null });
    setMappingPersistedState({ savedAt: null, status: "none", message: null });
    setSourcePersistedState({
      savedAt: null,
      status: "none",
      message: null,
      rowsTruncated: false,
    });
    setRestoreStateNotice(null);
  }

  function restorePersistedState() {
    const templateState = loadTemplateDraftFromStorage();
    const mappingState = loadColumnMappingFromStorage();
    const sourceState = restoreSourceFromStorage();
    setTemplateSource(templateState.templateSource);
    setTemplatePersistedState(templateState.status);
    setTemplateImportError("");
    setFieldMapping(
      reconcileMappingWithHeaders(mappingState.mapping, sourceState.source?.headers ?? []),
    );
    setMappingPersistedState(mappingState.status);
    setSourceData(sourceState.source);
    setSourcePersistedState(sourceState.status);
    setSourceError(null);
    if (queuedRows.length > 0) {
      setRestoreStateNotice(
        "Saved state restored. Existing unsent batch snapshot was preserved; please re-check row and actor validity.",
      );
    } else {
      setRestoreStateNotice(
        "Saved state restored. You can continue from the saved operator context.",
      );
    }
  }

  useEffect(() => {
    persistTemplateDraft();
    const validation = validateTemplateSource(templateSource);
    setTemplatePersistedState((current) => ({
      savedAt: current.savedAt ?? new Date().toISOString(),
      status: validation.status,
      message:
        validation.status === "ok"
          ? "Template draft saved."
          : `Template draft saved with ${validation.status} issue: ${validation.message}`,
    }));
    const templateTextState = validateTemplateSource(templateSource);
    setTemplateParseError(
      templateTextState.status === "invalid" ? templateTextState.message : null,
    );
    setTemplateImportError("");
  }, [persistTemplateDraft, templateSource]);

  useEffect(() => {
    persistMapping();
    const hasAnyValue = Object.values(fieldMapping).some((value) => value.length > 0);
    setMappingPersistedState({
      savedAt: new Date().toISOString(),
      status: hasAnyValue ? "ok" : "none",
      message: hasAnyValue
        ? "Column mapping saved."
        : "Column mapping snapshot cleared (all columns empty).",
    });
  }, [fieldMapping, persistMapping]);

  useEffect(() => {
    if (sourceData) {
      persistSourceReview();
      setSourcePersistedState((current) => ({
        savedAt: new Date().toISOString(),
        status: "ok",
        message:
          current.rowsTruncated || sourceData.rows.length > MAX_PERSISTED_SOURCE_ROWS
            ? `Source review saved partially. Showing first ${MAX_PERSISTED_SOURCE_ROWS} rows in local storage.`
            : "Source review state saved.",
        rowsTruncated: sourceData.rows.length > MAX_PERSISTED_SOURCE_ROWS,
      }));
    } else {
      setSourcePersistedState({
        savedAt: null,
        status: "none",
        message: null,
        rowsTruncated: false,
      });
    }
  }, [persistSourceReview, sourceData]);

  useEffect(() => {
    if (!sourceData) {
      return;
    }
    setFieldMapping((current) => {
      const reconciled = reconcileMappingWithHeaders(current, sourceData.headers);
      if (
        current.parentSku === reconciled.parentSku &&
        current.sku === reconciled.sku &&
        current.jan === reconciled.jan &&
        current.qty === reconciled.qty &&
        current.brand === reconciled.brand
      ) {
        return current;
      }
      return reconciled;
    });
  }, [sourceData]);

  useEffect(() => {
    void refreshBridgeStatus();
  }, [refreshBridgeStatus]);

  function resetForm() {
    setSession(createSession());
    setForm(createInitialFormState());
    setShowErrors(false);
    setDraftSnapshot(null);
    setManualSubmit({ phase: "idle", message: "", result: null });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setShowErrors(true);
    if (!draft) return;
    setDraftSnapshot(draft);
  }

  return (
    <main className="page">
      <section className="hero hero-grid">
        <div>
          <p className="eyebrow">Issue #4</p>
          <h1>Label Authoring</h1>
          <p className="lede">
            Add template authoring and print-data import so operators can create drafts from
            spreadsheets, while core rendering and normalization remain in Rust.
          </p>
        </div>
        <div className="hero-meta">
          <p>
            <strong>Session job</strong>
            <span>{session.jobId}</span>
          </p>
          <p>
            <strong>Requested at</strong>
            <span>{session.requestedAt}</span>
          </p>
          <p>
            <strong>Template route</strong>
            <span>{templateOptionLabel}</span>
          </p>
          <p>
            <strong>Status</strong>
            <span className={`status-pill ${draft ? "ready" : "blocked"}`}>
              {draft ? "Manual draft ready" : "Manual input incomplete"}
            </span>
          </p>
          <p>
            <strong>Execution</strong>
            <span className={`status-pill ${executionModeChipClass}`}>{executionModeLabel}</span>
          </p>
        </div>
      </section>

      <section className="panel">
        <div className="section-heading">
          <h2>Desktop bridge status</h2>
          <p>
            Checks desktop bridge readiness before dispatch and exposes output paths for operator
            verification.
          </p>
        </div>
        <div className="preview-summary">
          <div>
            <span>Status</span>
            <strong>
              {bridgeStatus.phase === "ready"
                ? "Ready"
                : bridgeStatus.phase === "loading"
                  ? "Checking..."
                  : bridgeStatus.phase === "error"
                    ? "Unavailable with error"
                    : "Unavailable (browser preview mode)"}
            </strong>
            <small>{bridgeStatus.message}</small>
          </div>
          <div>
            <span>Bridge adapter</span>
            <strong>
              {bridgeStatusAvailable ? bridgeStatus.status?.printAdapterKind : "unknown"}
            </strong>
            <small>
              Available adapters:{" "}
              {bridgeStatusAvailable
                ? bridgeStatus.status?.availableAdapters.join(", ")
                : "not available"}
            </small>
          </div>
          <div>
            <span>Zint / paths</span>
            <strong>
              {bridgeStatusAvailable ? bridgeStatus.status?.resolvedZintPath : "unknown"}
            </strong>
            <small>
              proof: {bridgeStatusAvailable ? bridgeStatus.status?.proofOutputDir : "-"}
              {" / "}
              print: {bridgeStatusAvailable ? bridgeStatus.status?.printOutputDir : "-"}
              {" / "}
              spool: {bridgeStatusAvailable ? bridgeStatus.status?.spoolOutputDir : "-"}
            </small>
            {bridgeStatusAvailable && bridgeStatus.status?.windowsPrinterName ? (
              <small>Printer: {bridgeStatus.status?.windowsPrinterName}</small>
            ) : null}
          </div>
        </div>
        {bridgeStatusAvailable && bridgeStatus.status ? (
          <div className="proof-note">
            <strong>Bridge warnings</strong>
            {bridgeStatus.status.warnings.length > 0 ? (
              <ul className="card-list">
                {bridgeStatus.status.warnings.map((warning) => (
                  <li
                    key={warning}
                    className={isHighRiskBridgeWarning(warning) ? "status-fail" : undefined}
                  >
                    {warning}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="status-ok">No warnings.</p>
            )}
          </div>
        ) : null}
        {bridgeStatusAvailable && hasBlockingBridgeWarnings ? (
          <p className="status-fail">
            High-risk warnings detected. Submit actions are disabled until the warning list is
            cleared.
          </p>
        ) : null}
        {!bridgeStatusAvailable && bridgeStatus.phase === "unavailable" ? (
          <p className="notice-text">Browser preview mode / desktop bridge unavailable.</p>
        ) : null}
        {bridgeStatus.phase === "error" ? (
          <p className="status-fail">{bridgeStatus.message}</p>
        ) : null}
        <div className="toolbar">
          <button className="button-secondary" type="button" onClick={refreshBridgeStatus}>
            Refresh bridge status
          </button>
        </div>
      </section>

      {hasPersistedState ? (
        <section className="panel">
          <div className="section-heading">
            <h2>Recovered operator state</h2>
            <p>
              Saved operator context can be restored from browser local storage and resumed after
              reload.
            </p>
          </div>
          <p className="data-summary">
            Template draft: {formatSavedAt(templatePersistedState.savedAt)} /{" "}
            {templatePersistedState.status}
            {templatePersistedState.message ? ` (${templatePersistedState.message})` : ""}
          </p>
          {templateValidation.status === "invalid" ? (
            <p className="status-fail">{templateValidation.message}</p>
          ) : null}
          {templateValidation.status === "stale" ? (
            <p className="notice-text">
              Template is not at current schema and may be stale; operator review is required before
              print.
            </p>
          ) : null}
          <p className="data-summary">
            Source review: {formatSavedAt(sourcePersistedState.savedAt)} /{" "}
            {sourcePersistedState.status}
            {sourcePersistedState.message ? ` (${sourcePersistedState.message})` : ""}
          </p>
          <p className="data-summary">
            Column mapping: {formatSavedAt(mappingPersistedState.savedAt)} /{" "}
            {mappingPersistedState.status}
            {mappingPersistedState.message ? ` (${mappingPersistedState.message})` : ""}
          </p>
          {restoreStateNotice ? <p className="notice-text">{restoreStateNotice}</p> : null}
          <div className="toolbar">
            <button type="button" className="button-secondary" onClick={restorePersistedState}>
              Restore saved state
            </button>
            <button type="button" className="button-secondary" onClick={clearPersistedState}>
              Clear saved state
            </button>
          </div>
        </section>
      ) : null}

      <section className="workspace">
        <form className="panel form-panel" noValidate onSubmit={handleSubmit}>
          <div className="section-heading">
            <h2>Create manual draft</h2>
            <p>Keep minimum operator fields and keep payloads aligned with @label/job-schema.</p>
          </div>

          <div className="form-grid">
            <label className="field">
              <span>Parent SKU</span>
              <input
                value={form.parentSku}
                onChange={(event) => updateField("parentSku", event.target.value)}
              />
              {visibleErrors.parentSku ? (
                <small className="error-text">{visibleErrors.parentSku}</small>
              ) : null}
            </label>

            <label className="field">
              <span>SKU</span>
              <input
                value={form.sku}
                onChange={(event) => updateField("sku", event.target.value)}
              />
              {visibleErrors.sku ? <small className="error-text">{visibleErrors.sku}</small> : null}
            </label>

            <label className="field field-wide">
              <span>JAN</span>
              <input
                inputMode="numeric"
                value={form.jan}
                onChange={(event) => updateField("jan", event.target.value)}
                placeholder="4006381333931"
              />
              <small className="hint-text">
                Rust-side normalization accepts 12-digit input; 13-digit checksum is preferred.
              </small>
              {visibleErrors.jan ? <small className="error-text">{visibleErrors.jan}</small> : null}
            </label>

            <label className="field">
              <span>Quantity</span>
              <input
                inputMode="numeric"
                value={form.qty}
                onChange={(event) => updateField("qty", event.target.value)}
              />
              {visibleErrors.qty ? <small className="error-text">{visibleErrors.qty}</small> : null}
            </label>

            <label className="field">
              <span>Brand</span>
              <input
                value={form.brand}
                onChange={(event) => updateField("brand", event.target.value)}
              />
              {visibleErrors.brand ? (
                <small className="error-text">{visibleErrors.brand}</small>
              ) : null}
            </label>

            <label className="field">
              <span>Template</span>
              <select
                value={templateReference ? "template-from-spec" : form.templateId}
                onChange={(event) => {
                  if (event.target.value === "template-from-spec") return;
                  updateField("templateId", event.target.value);
                }}
              >
                {templateReference ? (
                  <option value="template-from-spec">
                    Template from spec ({templateReference.id}@{templateReference.version})
                  </option>
                ) : null}
                {templateOptions.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
              {visibleErrors.templateId ? (
                <small className="error-text">{visibleErrors.templateId}</small>
              ) : null}
            </label>

            <label className="field">
              <span>Printer profile</span>
              <select
                value={form.printerProfileId}
                onChange={(event) => updateField("printerProfileId", event.target.value)}
              >
                {printerProfiles.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
              {visibleErrors.printerProfileId ? (
                <small className="error-text">{visibleErrors.printerProfileId}</small>
              ) : null}
            </label>

            <label className="field field-wide">
              <span>Actor</span>
              <input
                value={form.actor}
                onChange={(event) => updateField("actor", event.target.value)}
              />
              {visibleErrors.actor ? (
                <small className="error-text">{visibleErrors.actor}</small>
              ) : null}
            </label>

            <label className="field field-wide">
              <span>Execution intent</span>
              <select
                value={form.executionMode}
                onChange={(event) =>
                  updateField("executionMode", event.target.value as ExecutionMode)
                }
              >
                <option value="proof">Proof-only (safe default)</option>
                <option value="print">Print-ready</option>
              </select>
              <small className="hint-text">
                Proof mode is the safe default and includes explicit intent in every draft payload.
              </small>
            </label>

            {form.executionMode === "proof" ? (
              <>
                <label className="field field-wide">
                  <span>Requested by</span>
                  <input
                    value={form.executionRequestedBy}
                    onChange={(event) => updateField("executionRequestedBy", event.target.value)}
                    placeholder="Proof requestor"
                  />
                </label>
                <label className="field field-wide">
                  <span>Notes</span>
                  <textarea
                    className="batch-text"
                    rows={4}
                    value={form.executionNotes}
                    onChange={(event) => updateField("executionNotes", event.target.value)}
                    placeholder="Use this for print review context (optional)."
                  />
                </label>
              </>
            ) : (
              <>
                <label className="field">
                  <span>Approved by</span>
                  <input
                    value={form.executionApprovedBy}
                    onChange={(event) => updateField("executionApprovedBy", event.target.value)}
                  />
                  {visibleErrors.executionApprovedBy ? (
                    <small className="error-text">{visibleErrors.executionApprovedBy}</small>
                  ) : null}
                </label>
                <label className="field">
                  <span>Approved at</span>
                  <input
                    type="datetime-local"
                    value={form.executionApprovedAt}
                    onChange={(event) => updateField("executionApprovedAt", event.target.value)}
                  />
                  {visibleErrors.executionApprovedAt ? (
                    <small className="error-text">{visibleErrors.executionApprovedAt}</small>
                  ) : null}
                </label>
                <label className="field">
                  <span>Source proof job ID</span>
                  <input
                    value={form.executionSourceProofJobId}
                    onChange={(event) =>
                      updateField("executionSourceProofJobId", event.target.value)
                    }
                  />
                  {visibleErrors.executionSourceProofJobId ? (
                    <small className="error-text">{visibleErrors.executionSourceProofJobId}</small>
                  ) : null}
                  {approvedProofEntries.length > 0 ? (
                    <div className="proof-picker">
                      <small className="hint-text">
                        Approved proofs from the local audit ledger can be pinned into print
                        execution.
                      </small>
                      <div className="job-actions">
                        {approvedProofEntries.slice(0, 4).map((entry) => (
                          <button
                            key={entry.dispatch.audit.jobId}
                            className="button-secondary proof-chip"
                            type="button"
                            onClick={() => applyApprovedProofToForm(entry)}
                          >
                            {entry.proof?.proofJobId}
                          </button>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </label>
                <label className="field">
                  <span>Allow without proof</span>
                  <input
                    type="checkbox"
                    checked={form.executionAllowWithoutProof}
                    disabled={!allowWithoutProofEnabled}
                    onChange={(event) =>
                      updateField("executionAllowWithoutProof", event.target.checked)
                    }
                  />
                  <small className="hint-text">
                    Print without linked proof job stays disabled until proof approval workflow is
                    implemented.
                  </small>
                </label>
              </>
            )}
          </div>

          <div className="toolbar">
            <button className="button-primary" type="submit">
              Create manual draft snapshot
            </button>
            <button
              className="button-secondary"
              type="button"
              onClick={submitManualDraft}
              disabled={manualSubmit.phase === "submitting" || isBridgeSubmitBlocked}
            >
              Submit manual draft
            </button>
            <button className="button-secondary" onClick={resetForm} type="button">
              Reset session
            </button>
          </div>
          {manualSubmit.phase === "submitting" ? (
            <p className="notice-text">Submitting manual draft via Tauri invoke...</p>
          ) : null}
          {manualSubmit.phase === "error" ? (
            <p className="status-fail">{manualSubmit.message}</p>
          ) : null}
          {bridgeSubmitBlockMessage ? (
            <p className="status-fail">{bridgeSubmitBlockMessage}</p>
          ) : null}
        </form>

        <aside className="panel preview-panel">
          <div className="section-heading">
            <h2>Manual draft preview</h2>
            <p>Preview stays aligned with print job schema and output adapters.</p>
          </div>
          <div className="preview-summary">
            <div>
              <span>Template route</span>
              <strong>{templateOptionLabel}</strong>
              <small>{template ? template.size : "template route inferred"}</small>
            </div>
            <div>
              <span>Output route</span>
              <strong>{selectedPrinterProfile ? selectedPrinterProfile.label : "Missing"}</strong>
              <small>
                {selectedPrinterProfile
                  ? `${selectedPrinterProfile.adapter} / ${selectedPrinterProfile.paperSize} / ${selectedPrinterProfile.dpi} dpi`
                  : "Select printer profile"}
              </small>
            </div>
            <div>
              <span>Execution</span>
              <strong>{executionMeta}</strong>
              <small>{executionModeLabel}</small>
            </div>
          </div>

          {previewJson ? (
            <>
              <pre className="json-block">{previewJson}</pre>
              {draftIsStale ? (
                <p className="notice-text">
                  Live input changed after last snapshot. Click create again to refresh the payload.
                </p>
              ) : null}
              {manualSubmit.phase === "error" ? (
                <p className="status-fail">{manualSubmit.message}</p>
              ) : null}
              {manualSubmit.phase === "success" ? (
                <>
                  <p className="status-ok">{manualSubmit.message}</p>
                  <pre className="json-block">{JSON.stringify(manualSubmit.result, null, 2)}</pre>
                </>
              ) : null}
            </>
          ) : (
            <div className="empty-state">
              <strong>No draft yet</strong>
              <p>Fill required fields and create snapshot.</p>
            </div>
          )}
        </aside>
      </section>

      <section className="panel">
        <div className="section-heading">
          <h2>Template editor</h2>
          <p>Edit the repository template-spec JSON used by label production.</p>
        </div>
        <label className="field field-wide">
          <span>Template spec JSON</span>
          <textarea
            className="batch-text"
            value={templateSource}
            onChange={(event) => updateTemplateSource(event.target.value)}
            onBlur={validateTemplateText}
          />
          {templateParseError ? <small className="error-text">{templateParseError}</small> : null}
          {templateImportError ? <small className="error-text">{templateImportError}</small> : null}
          {templateMetaInfo ? (
            <small className="hint-text">
              schema_version: {templateMetaInfo.schemaVersion} / template_version:{" "}
              {templateMetaInfo.templateVersion}/ label_name: {templateMetaInfo.labelName}
            </small>
          ) : null}
        </label>
        <div className="toolbar">
          <label className="button-secondary fake-button">
            Import template JSON
            <input type="file" accept=".json,application/json" onChange={handleTemplateImport} />
          </label>
          <label className="button-secondary fake-button">
            Import template asset (with state)
            <input type="file" accept=".json,application/json" onChange={handleTemplateImport} />
          </label>
          <button className="button-secondary" type="button" onClick={handleTemplateExport}>
            Export template JSON
          </button>
          <button className="button-secondary" type="button" onClick={handleTemplateAssetExport}>
            Export template asset
          </button>
          <button className="button-secondary" type="button" onClick={resetTemplateToDefaults}>
            Reset template
          </button>
          <button className="button-primary" type="button" onClick={validateTemplateText}>
            Validate JSON
          </button>
        </div>
      </section>

      <section className="panel">
        <div className="section-heading">
          <h2>Data source (Excel / CSV)</h2>
          <p>
            Upload print data and map source columns to required draft fields without strict DB
            schema alignment.
          </p>
        </div>
        <div className="toolbar">
          <label className="button-secondary fake-button">
            Upload CSV/XLSX
            <input type="file" accept=".csv,.xlsx,.xls,text/csv" onChange={handleDataUpload} />
          </label>
          {sourceData ? (
            <>
              <button className="button-secondary" type="button" onClick={autoDetectMapping}>
                Auto detect mapping
              </button>
              <button className="button-secondary" type="button" onClick={resetDataSource}>
                Clear source
              </button>
            </>
          ) : null}
          <button
            className="button-primary"
            type="button"
            onClick={buildQueueSnapshot}
            disabled={!isQueueReady}
          >
            Snapshot valid rows
          </button>
        </div>

        <p className="data-summary">{sourceError ? sourceError : sourceSummary}</p>

        {sourceData ? (
          <>
            <div className="mapping-grid">
              {requiredFieldList.map((entry) => (
                <label className="field" key={entry.key}>
                  <span>Map {entry.label}</span>
                  <select
                    value={fieldMapping[entry.key]}
                    onChange={(event) => updateFieldMapping(entry.key, event.target.value)}
                  >
                    <option value="">-- Select column --</option>
                    {sourceData.headers.map((header) => (
                      <option key={header} value={header}>
                        {header}
                      </option>
                    ))}
                  </select>
                </label>
              ))}
            </div>

            <div className="data-grid">
              <div className="data-grid-header">
                <strong>Source preview (first 8 rows)</strong>
                <span>
                  {readyRowsCount} ready / {pendingRowsCount} pending / {errorRowsCount} invalid /{" "}
                  {preparedRows.length} rows
                </span>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Row</th>
                    <th>Parent SKU</th>
                    <th>SKU</th>
                    <th>JAN</th>
                    <th>Qty</th>
                    <th>Brand</th>
                    <th>Template</th>
                    <th>Printer</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {parsedTemplateRows.map((entry) => (
                    <tr key={entry.rowIndex}>
                      <td>{entry.rowIndex + 1}</td>
                      <td>{entry.sourceRow[fieldMapping.parentSku] ?? "-"}</td>
                      <td>{entry.sourceRow[fieldMapping.sku] ?? "-"}</td>
                      <td>{entry.sourceRow[fieldMapping.jan] ?? "-"}</td>
                      <td>{entry.sourceRow[fieldMapping.qty] ?? "-"}</td>
                      <td>{entry.sourceRow[fieldMapping.brand] ?? "-"}</td>
                      <td>
                        {readSourceValue(entry.sourceRow, TEMPLATE_SOURCE_ALIASES) ||
                          (entry.draft
                            ? `${entry.draft.template.id}@${entry.draft.template.version}`
                            : "default")}
                      </td>
                      <td>
                        {readSourceValue(entry.sourceRow, PRINTER_SOURCE_ALIASES) ||
                          (entry.draft ? entry.draft.printerProfile.id : "default")}
                      </td>
                      <td
                        className={
                          entry.status === "ready"
                            ? "status-ok"
                            : entry.status === "error"
                              ? "status-fail"
                              : "status-pending"
                        }
                      >
                        {entry.status === "pending"
                          ? `pending: ${entry.pendingReason ?? "requires operator review"}`
                          : entry.errors.join(" / ") || "ready"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </section>

      {queuedRows.length > 0 ? (
        <section className="panel">
          <div className="section-heading">
            <h2>Batch draft preview</h2>
            <p>
              Schema-aligned payload for queued rows (first row shown) including execution intent.
            </p>
          </div>
          <pre className="json-block">{previewBatchJson}</pre>
          <p className="notice-text">
            {queuedRows.length} rows captured. ready: {queuedReadyRowsCount}, submitting:{" "}
            {queuedSubmittingRowsCount}, submitted: {queuedSubmittedRowsCount}, failed:{" "}
            {queuedFailedRowsCount}.
          </p>
          <div className="data-grid">
            <div className="data-grid-header">
              <strong>Queue progress</strong>
            </div>
            <table>
              <thead>
                <tr>
                  <th>Row</th>
                  <th>Job ID</th>
                  <th>Status</th>
                  <th>Result</th>
                </tr>
              </thead>
              <tbody>
                {queuedRows.map((entry, index) => (
                  <tr key={entry.draft?.jobId ?? `${entry.rowIndex}-${index}`}>
                    <td>{index + 1}</td>
                    <td>{entry.draft ? entry.draft.jobId : "-"}</td>
                    <td className={queuedRowStatusClass(entry.submissionStatus)}>
                      {entry.submissionStatus}
                    </td>
                    <td>{formatQueuedRowResult(entry)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button
            className="button-primary"
            type="button"
            onClick={submitQueuedRows}
            disabled={
              !canSubmitQueuedRows || batchSubmit.phase === "submitting" || isBridgeSubmitBlocked
            }
          >
            Submit queued rows
          </button>
          <button
            className="button-secondary"
            type="button"
            onClick={() => {
              setQueuedRows([]);
            }}
          >
            Clear batch snapshot
          </button>
          {batchSubmit.phase === "submitting" ? (
            <p className="notice-text">{batchSubmit.message}</p>
          ) : null}
          {bridgeSubmitBlockMessage ? (
            <p className="status-fail">{bridgeSubmitBlockMessage}</p>
          ) : null}
          {batchSubmit.phase === "error" ? (
            <p className="status-fail">{batchSubmit.message}</p>
          ) : null}
          {batchSubmit.phase === "success" ? (
            <p className="status-ok">{batchSubmit.message}</p>
          ) : null}
          {batchSubmit.results.length > 0 ? (
            <pre className="json-block">{JSON.stringify(batchSubmit.results, null, 2)}</pre>
          ) : null}
        </section>
      ) : null}

      <section className="panel">
        <div className="section-heading">
          <h2>Proof inbox / audit search</h2>
          <p>
            Review pending proofs, pin approved proof IDs into print execution, and inspect recent
            dispatch history from the desktop ledger.
          </p>
        </div>
        <div className="form-grid">
          <label className="field field-wide">
            <span>Search ledger</span>
            <input
              value={auditQuery}
              onChange={(event) => setAuditQuery(event.target.value)}
              placeholder="job id, lineage, actor, template, adapter"
            />
          </label>
          <label className="field field-wide">
            <span>Review note</span>
            <textarea
              className="batch-text"
              rows={3}
              value={proofReviewNotes}
              onChange={(event) => setProofReviewNotes(event.target.value)}
              placeholder="Optional approval or rejection note for the proof ledger."
            />
          </label>
        </div>
        <div className="toolbar">
          <button
            className="button-secondary"
            type="button"
            onClick={() => {
              void refreshAuditSearch(auditQuery);
            }}
          >
            Refresh proof inbox
          </button>
          <button
            className="button-secondary"
            type="button"
            onClick={() => {
              setAuditQuery("");
              void refreshAuditSearch("");
            }}
          >
            Clear search
          </button>
        </div>
        <p className="data-summary">
          {auditSearch.message}
          {auditSearch.lastUpdatedAt
            ? ` Last updated: ${formatSavedAt(auditSearch.lastUpdatedAt)}.`
            : ""}
        </p>
        {proofReview.phase === "submitting" ? (
          <p className="notice-text">{proofReview.message}</p>
        ) : null}
        {proofReview.phase === "error" ? (
          <p className="status-fail">{proofReview.message}</p>
        ) : null}
        {proofReview.phase === "success" ? (
          <p className="status-ok">{proofReview.message}</p>
        ) : null}
        {auditSearch.phase === "error" ? (
          <p className="status-fail">{auditSearch.message}</p>
        ) : null}
        {auditSearch.entries.length > 0 ? (
          <div className="data-grid">
            <div className="data-grid-header">
              <strong>Recent dispatches</strong>
              <span>{auditSearch.entries.length} entries</span>
            </div>
            <table>
              <thead>
                <tr>
                  <th>Occurred</th>
                  <th>Job</th>
                  <th>Mode</th>
                  <th>Actor</th>
                  <th>Proof</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {auditSearch.entries.map((entry) => (
                  <tr key={entry.dispatch.audit.jobId}>
                    <td>{formatSavedAt(entry.dispatch.audit.occurredAt)}</td>
                    <td>
                      <strong>{entry.dispatch.audit.jobId}</strong>
                      <br />
                      <small>{entry.dispatch.templateVersion}</small>
                    </td>
                    <td>
                      <span
                        className={`status-pill ${entry.dispatch.mode === "print" ? "print" : "proof"}`}
                      >
                        {entry.dispatch.mode}
                      </span>
                    </td>
                    <td>
                      {entry.dispatch.audit.actor.displayName}
                      <br />
                      <small>{entry.dispatch.audit.actor.userId}</small>
                    </td>
                    <td className={proofStatusClass(entry.proof?.status)}>
                      {entry.proof ? (
                        <>
                          <strong>{entry.proof.status}</strong>
                          <br />
                          <small>{entry.proof.proofJobId}</small>
                        </>
                      ) : (
                        <span>not a proof record</span>
                      )}
                    </td>
                    <td>
                      <div className="audit-actions">
                        {entry.proof?.status === "pending" ? (
                          <>
                            <button
                              className="button-secondary"
                              type="button"
                              disabled={proofReview.phase === "submitting"}
                              onClick={() => {
                                void submitProofReview(entry, "approve");
                              }}
                            >
                              Approve
                            </button>
                            <button
                              className="button-secondary"
                              type="button"
                              disabled={proofReview.phase === "submitting"}
                              onClick={() => {
                                void submitProofReview(entry, "reject");
                              }}
                            >
                              Reject
                            </button>
                          </>
                        ) : null}
                        {entry.proof?.status === "approved" ? (
                          <button
                            className="button-secondary"
                            type="button"
                            onClick={() => applyApprovedProofToForm(entry)}
                          >
                            Use for print
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty-state">
            <strong>No audit entries</strong>
            <p>Submit a proof job from desktop shell or refresh the search after bridge startup.</p>
          </div>
        )}
      </section>

      <section className="grid">
        <article className="card">
          <span>Guardrails</span>
          <strong>Core-first rules</strong>
          <ul className="card-list">
            {corePillars.map((pillar) => (
              <li key={pillar}>{pillar}</li>
            ))}
          </ul>
        </article>
        <article className="card">
          <span>Operator notes</span>
          <strong>Why this version is practical</strong>
          <ul className="card-list">
            {operatorNotes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </article>
      </section>
    </main>
  );
}
