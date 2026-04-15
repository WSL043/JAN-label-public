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
import { templateVersionOf, toDispatchRequest } from "@label/job-schema";
import { useEffect, useEffectEvent, useMemo, useState } from "react";
import type { CSSProperties, ChangeEvent, FormEvent } from "react";
import {
  type AuditExportResult,
  type AuditLedgerScope,
  type AuditRetentionResult,
  type AuditSearchEntry,
  type BridgeWarning,
  type LegacyProofSeedRequest,
  type LegacyProofSeedResult,
  type PrintBridgeStatus,
  type SaveTemplateToLocalCatalogResult,
  type TemplateCatalogResult,
  type TemplateDraftPreviewRequest,
  type TemplateDraftPreviewResult,
  approveProof,
  dispatchPrintJob,
  exportAuditLedger,
  fetchPrintBridgeStatus,
  fetchTemplateCatalog,
  isTauriConnected,
  previewTemplateDraft,
  rejectProof,
  saveTemplateToLocalCatalog,
  searchAuditLog,
  seedLegacyProofs,
  trimAuditLedger,
  validateLegacyProofSeed,
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

const templateOptions: TemplateOption[] = [
  {
    id: "basic-50x30",
    version: "v1",
    label: "Basic 50 x 30",
    size: "50mm x 30mm",
    catalogSource: "packaged",
  },
];
const packagedTemplateVersions = new Set(templateOptions.map((entry) => templateVersionOf(entry)));

type TemplateOption = LabelTemplateRef & {
  label: string;
  size: string;
  description?: string | null;
  catalogSource?: "packaged" | "local" | "unknown";
};

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
const LEGACY_PROOF_SEED_REQUIRED_COLUMNS = [
  "proofJobId",
  "artifactPath",
  "templateVersion",
  "sku",
  "brand",
  "jan",
  "qty",
  "requestedByUserId",
  "requestedByDisplayName",
  "requestedAt",
  "jobLineageId",
  "notes",
] as const;
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
const DEFAULT_TEMPLATE_FIELD_COLOR = "#14120f";
const TEMPLATE_CATALOG_BLOCK_PREFIX = "Template catalog mismatch:";
const RUST_RENDER_PLACEHOLDERS = [
  "job_id",
  "sku",
  "brand",
  "jan",
  "qty",
  "template_version",
] as const;
const LOCAL_TEMPLATE_PREVIEW_ONLY_PLACEHOLDERS = ["parent_sku"] as const;
const TEMPLATE_PREVIEW_PLACEHOLDERS = [
  ...RUST_RENDER_PLACEHOLDERS,
  ...LOCAL_TEMPLATE_PREVIEW_ONLY_PLACEHOLDERS,
] as const;

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
type TemplateCatalogPhase = "idle" | "loading" | "ready" | "unavailable" | "error";
type TemplateCatalogState = {
  phase: TemplateCatalogPhase;
  templates: TemplateOption[];
  defaultTemplateVersion: string | null;
  message: string;
};
type LegacyProofSeedPhase = "idle" | "validating" | "seeding" | "success" | "error";
type TemplateRenderPreviewPhase = "idle" | "rendering" | "ready" | "error" | "unavailable";
type AuditSearchPhase = "idle" | "loading" | "ready" | "unavailable" | "error";
type AuditSearchState = {
  phase: AuditSearchPhase;
  entries: AuditSearchEntry[];
  message: string;
  lastUpdatedAt: string | null;
};
type AuditMaintenanceState = SubmitState & {
  detail: string | null;
};
type ProofReviewAction = "approve" | "reject" | null;
type ProofReviewState = SubmitState & {
  proofJobId: string | null;
  action: ProofReviewAction;
};
type LegacyProofSeedState = {
  phase: LegacyProofSeedPhase;
  message: string;
  result: LegacyProofSeedResult | null;
};
type TemplateRenderPreviewState = {
  phase: TemplateRenderPreviewPhase;
  message: string;
  result: TemplateDraftPreviewResult | null;
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
type TemplatePageEditor = {
  widthMm: number;
  heightMm: number;
  backgroundFill: string;
};
type TemplateBorderEditor = {
  visible: boolean;
  color: string;
  widthMm: number;
};
type TemplateFieldEditor = {
  name: string;
  xMm: number;
  yMm: number;
  fontSizeMm: number;
  template: string;
  color: string;
};
type TemplateEditorModel = {
  labelName: string;
  description: string;
  templateVersion: string;
  page: TemplatePageEditor;
  border: TemplateBorderEditor;
  fields: TemplateFieldEditor[];
};
type XlsxCellMeta = {
  isNumeric: boolean;
  scientific: boolean;
  decimal: boolean;
};
type XlsxSourceMeta = {
  cellMetaRows: XlsxCellMeta[][];
};
type DataSource = {
  fileName: string;
  source: "csv" | "xlsx";
  headers: string[];
  rows: string[][];
  xlsxMeta?: XlsxSourceMeta | null;
};
type DataRow = Record<string, string>;
type ColumnMapping = Record<FieldKey, string>;
type PreparedRow = {
  rowIndex: number;
  sourceRow: DataRow;
  draft: PrintJobDraft | null;
  errors: string[];
  warnings: string[];
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
const LEGACY_PROOF_JOB_ID_ALIASES = ["proofjobid", "proof_job_id", "proof-job-id", "jobid"];
const LEGACY_ARTIFACT_PATH_ALIASES = ["artifactpath", "artifact_path", "proofpath", "proof_path"];
const LEGACY_TEMPLATE_VERSION_ALIASES = ["templateversion", "template_version", "template-version"];
const LEGACY_REQUESTED_BY_USER_ID_ALIASES = [
  "requestedbyuserid",
  "requested_by_user_id",
  "requested-by-user-id",
  "requestedby",
];
const LEGACY_REQUESTED_BY_DISPLAY_NAME_ALIASES = [
  "requestedbydisplayname",
  "requested_by_display_name",
  "requested-by-display-name",
  "requestedbyname",
];
const LEGACY_REQUESTED_AT_ALIASES = ["requestedat", "requested_at", "requested-at"];
const LEGACY_JOB_LINEAGE_ID_ALIASES = ["joblineageid", "job_lineage_id", "job-lineage-id"];
const LEGACY_NOTES_ALIASES = ["notes", "note", "memo"];

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

function readNumber(spec: TemplateSpec, key: string): number | null {
  const value = spec[key];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function readBoolean(spec: TemplateSpec, key: string): boolean | null {
  const value = spec[key];
  return typeof value === "boolean" ? value : null;
}

function normalizeTemplateColor(raw: string, fallback = DEFAULT_TEMPLATE_FIELD_COLOR): string {
  const value = raw.trim();
  return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(value) ? value : fallback;
}

function parseTemplateEditorModel(spec: TemplateSpec | null): TemplateEditorModel | null {
  if (!spec) {
    return null;
  }

  const pageSpec = isPlainObject(spec.page) ? (spec.page as TemplateSpec) : {};
  const borderSpec = isPlainObject(spec.border) ? (spec.border as TemplateSpec) : {};
  const fields = Array.isArray(spec.fields) ? spec.fields : [];

  return {
    labelName: readString(spec, "label_name"),
    description: readString(spec, "description"),
    templateVersion: readString(spec, "template_version"),
    page: {
      widthMm: readNumber(pageSpec, "width_mm") ?? 50,
      heightMm: readNumber(pageSpec, "height_mm") ?? 30,
      backgroundFill: normalizeTemplateColor(readString(pageSpec, "background_fill"), "#ffffff"),
    },
    border: {
      visible: readBoolean(borderSpec, "visible") ?? false,
      color: normalizeTemplateColor(readString(borderSpec, "color"), "#000000"),
      widthMm: readNumber(borderSpec, "width_mm") ?? 0.2,
    },
    fields: fields.map((field, index) => {
      const candidate = isPlainObject(field) ? (field as TemplateSpec) : {};
      return {
        name: readString(candidate, "name") || `field_${index + 1}`,
        xMm: readNumber(candidate, "x_mm") ?? 0,
        yMm: readNumber(candidate, "y_mm") ?? 0,
        fontSizeMm: readNumber(candidate, "font_size_mm") ?? 3,
        template: readString(candidate, "template") || "{sku}",
        color: normalizeTemplateColor(readString(candidate, "color")),
      };
    }),
  };
}

function createTemplateField(partial: Partial<TemplateFieldEditor> = {}): TemplateFieldEditor {
  return {
    name: partial.name ?? "text",
    xMm: partial.xMm ?? 2,
    yMm: partial.yMm ?? 2,
    fontSizeMm: partial.fontSizeMm ?? 3,
    template: partial.template ?? "text:{sku}",
    color: partial.color ?? DEFAULT_TEMPLATE_FIELD_COLOR,
  };
}

function updateTemplateSpecSource(
  raw: string,
  mutate: (spec: TemplateSpec) => void,
): { next: string | null; error: string | null } {
  const parsed = parseTemplateSpec(raw);
  if (parsed.error || !parsed.spec) {
    return {
      next: null,
      error: parsed.error ?? "Template JSON must be valid before using the structured editor.",
    };
  }
  const nextSpec = structuredClone(parsed.spec);
  mutate(nextSpec);
  return {
    next: sanitizeTemplateString(JSON.stringify(nextSpec)),
    error: null,
  };
}

function buildTemplatePreviewBindings(input: {
  draft: PrintJobDraft | null;
  form: FormState;
  session: DraftSession;
  resolvedTemplateRef: LabelTemplateRef | null;
}): Record<(typeof TEMPLATE_PREVIEW_PLACEHOLDERS)[number], string> {
  const normalizedJan = input.draft?.jan.normalized || input.form.jan.trim();
  return {
    job_id: input.draft?.jobId || input.session.jobId,
    parent_sku: input.draft?.parentSku || input.form.parentSku.trim() || "PARENT-SKU",
    sku: input.draft?.sku || input.form.sku.trim() || "SKU-0001",
    jan: normalizedJan || "4901234567894",
    qty: input.draft ? String(input.draft.qty) : input.form.qty.trim() || "1",
    brand: input.draft?.brand || input.form.brand.trim() || "Brand",
    template_version:
      (input.draft?.template ? templateVersionOf(input.draft.template) : null) ||
      (input.resolvedTemplateRef ? templateVersionOf(input.resolvedTemplateRef) : null) ||
      "basic-50x30@v1",
  };
}

function buildTemplateDraftPreviewRequest(input: {
  templateSource: string;
  draft: PrintJobDraft | null;
  form: FormState;
  session: DraftSession;
}): TemplateDraftPreviewRequest {
  const qtyValue = input.draft?.qty ?? Number.parseInt(input.form.qty.trim(), 10);
  return {
    templateSource: input.templateSource,
    sample: {
      jobId: input.draft?.jobId || input.session.jobId,
      sku: input.draft?.sku || input.form.sku.trim() || "SKU-0001",
      brand: input.draft?.brand || input.form.brand.trim() || "Brand",
      jan: input.draft?.jan.normalized || input.form.jan.trim() || "4901234567894",
      qty: Number.isFinite(qtyValue) && qtyValue > 0 ? qtyValue : 1,
    },
  };
}

function renderTemplatePreviewText(template: string, bindings: Record<string, string>): string {
  const rendered = template.replace(/\{([^}]+)\}/g, (_, key: string) => {
    const value = bindings[key];
    return value && value.trim().length > 0 ? value : `{${key}}`;
  });
  return rendered.trim() || template;
}

function extractTemplatePlaceholders(template: string): string[] {
  const placeholders = new Set<string>();
  for (const match of template.matchAll(/\{([^}]+)\}/g)) {
    const token = match[1]?.trim();
    if (token) {
      placeholders.add(token);
    }
  }
  return Array.from(placeholders);
}

function validateStructuredTemplateSemantics(spec: TemplateSpec): string[] {
  const model = parseTemplateEditorModel(spec);
  if (!model) {
    return ["template JSON could not be mapped into the structured editor model."];
  }

  const issues: string[] = [];
  const names = new Set<string>();

  for (const field of model.fields) {
    if (names.has(field.name)) {
      issues.push(`field '${field.name}' is duplicated.`);
    }
    names.add(field.name);

    if (field.xMm > model.page.widthMm || field.yMm > model.page.heightMm) {
      issues.push(
        `field '${field.name}' is outside the page bounds (${field.xMm}mm, ${field.yMm}mm on ${model.page.widthMm}mm x ${model.page.heightMm}mm).`,
      );
    }

    for (const token of extractTemplatePlaceholders(field.template)) {
      if ((RUST_RENDER_PLACEHOLDERS as readonly string[]).includes(token)) {
        continue;
      }
      if ((LOCAL_TEMPLATE_PREVIEW_ONLY_PLACEHOLDERS as readonly string[]).includes(token)) {
        issues.push(
          `field '${field.name}' uses preview-only placeholder '{${token}}'; Rust proof/PDF output will not substitute it.`,
        );
        continue;
      }
      issues.push(`field '${field.name}' uses unsupported placeholder '{${token}}'.`);
    }
  }

  return issues;
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

function spreadsheetCellMeta(cell: unknown): XlsxCellMeta {
  if (cell === null || cell === undefined) {
    return { isNumeric: false, scientific: false, decimal: false };
  }

  if (typeof cell === "number") {
    const text = String(cell);
    return {
      isNumeric: true,
      scientific: /e/i.test(text),
      decimal: !Number.isInteger(cell),
    };
  }

  if (typeof cell !== "object") {
    return { isNumeric: false, scientific: false, decimal: false };
  }

  const candidate = cell as { t?: unknown; w?: unknown; v?: unknown };
  const formatted = typeof candidate.w === "string" ? candidate.w.trim() : "";
  const numericValue = typeof candidate.v === "number" ? candidate.v : null;
  const isNumeric = candidate.t === "n" || numericValue !== null;
  if (!isNumeric) {
    return { isNumeric: false, scientific: false, decimal: false };
  }

  return {
    isNumeric: true,
    scientific:
      /e[+-]?\d+/i.test(formatted) ||
      (typeof numericValue === "number" && /e/i.test(String(numericValue))),
    decimal:
      (formatted.includes(".") && /\d\.\d/.test(formatted)) ||
      (typeof numericValue === "number" && !Number.isInteger(numericValue)),
  };
}

function normalizeBridgeWarningSeverity(warning: string): BridgeWarning["severity"] {
  if (NON_BLOCKING_WARNING_PATTERNS.some((pattern) => pattern.test(warning))) {
    return "info";
  }
  return HIGH_RISK_BRIDGE_WARNING_PATTERNS.some((pattern) => pattern.test(warning))
    ? "error"
    : "warning";
}

function normalizeBridgeWarnings(status: PrintBridgeStatus | null): BridgeWarning[] {
  if (!status) {
    return [];
  }
  if (status.warningDetails && status.warningDetails.length > 0) {
    return status.warningDetails;
  }
  return status.warnings.map((warning, index) => ({
    code: `LEGACY_WARNING_${index + 1}`,
    severity: normalizeBridgeWarningSeverity(warning),
    message: warning,
  }));
}

function isBlockingBridgeWarning(warning: BridgeWarning): boolean {
  return warning.severity === "error";
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

function buildTemplateCatalogBlockMessage(issue: string): string {
  return `${TEMPLATE_CATALOG_BLOCK_PREFIX} ${issue}`;
}

function catalogSourceFromVersion(
  options: TemplateOption[],
  templateVersion: string | null,
): NonNullable<TemplateOption["catalogSource"]> {
  if (!templateVersion) {
    return "unknown";
  }
  const matched = options.find((option) => templateVersionOf(option) === templateVersion);
  return matched?.catalogSource ?? "unknown";
}

function buildTemplateSourceSummary(options: TemplateOption[]): string {
  const counts = options.reduce(
    (acc, option) => {
      const source = option.catalogSource ?? "unknown";
      acc[source] = (acc[source] || 0) + 1;
      return acc;
    },
    { packaged: 0, local: 0, unknown: 0 } as Record<"packaged" | "local" | "unknown", number>,
  );
  const packaged = `${counts.packaged} packaged`;
  const local = `${counts.local} local`;
  const unknown = counts.unknown > 0 ? `, ${counts.unknown} unknown` : "";
  return `${packaged}, ${local}${unknown}`;
}

function isTemplateCatalogBlockedQueuedRow(row: QueuedRow): boolean {
  return Boolean(row.dispatchError?.startsWith(TEMPLATE_CATALOG_BLOCK_PREFIX));
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
  return parseTemplateVersionValue(readString(spec, "template_version"));
}

function parseTemplateVersionValue(versionValue: string): LabelTemplateRef | null {
  const at = versionValue.lastIndexOf("@");
  if (at <= 0 || at >= versionValue.length - 1) {
    return null;
  }
  return {
    id: versionValue.slice(0, at).trim(),
    version: versionValue.slice(at + 1).trim(),
  };
}

function toTemplateOptionsFromCatalog(catalog: TemplateCatalogResult): TemplateOption[] {
  return catalog.templates.reduce<TemplateOption[]>((options, entry) => {
    const ref = parseTemplateVersionValue(entry.version);
    if (!ref) {
      return options;
    }
    const source = entry.source ?? "unknown";
    options.push({
      ...ref,
      label: entry.labelName,
      size: entry.version,
      catalogSource:
        source === "packaged" || source === "local" || source === "unknown"
          ? source
          : packagedTemplateVersions.has(entry.version)
            ? "packaged"
            : "local",
      description: entry.description ?? null,
    });
    return options;
  }, []);
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
  templates: LabelTemplateRef[],
): LabelTemplateRef | null {
  if (parsedTemplate) {
    return parsedTemplate;
  }
  return templates.find((option) => option.id === formTemplateId) ?? null;
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
    xlsxMeta?: XlsxSourceMeta | null;
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

function normalizeXlsxSourceMeta(raw: unknown): XlsxSourceMeta | null {
  if (!isPlainObject(raw) || !Array.isArray(raw.cellMetaRows)) {
    return null;
  }
  return {
    cellMetaRows: raw.cellMetaRows.map((row) =>
      Array.isArray(row)
        ? row.map((cell) => ({
            isNumeric: Boolean((cell as XlsxCellMeta | null)?.isNumeric),
            scientific: Boolean((cell as XlsxCellMeta | null)?.scientific),
            decimal: Boolean((cell as XlsxCellMeta | null)?.decimal),
          }))
        : [],
    ),
  };
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
  const semanticIssues = validateStructuredTemplateSemantics(spec);
  if (semanticIssues.length > 0) {
    return {
      status: "invalid",
      message: semanticIssues.join(" "),
    };
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
      xlsxMeta: normalizeXlsxSourceMeta(data.xlsxMeta),
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
            xlsxMeta: null,
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
          const cellMetaRows: XlsxCellMeta[][] = [];
          for (let rowIndex = range.s.r; rowIndex <= range.e.r; rowIndex += 1) {
            const row: string[] = [];
            const metaRow: XlsxCellMeta[] = [];
            for (let columnIndex = range.s.c; columnIndex <= range.e.c; columnIndex += 1) {
              const cellAddress = xlsx.utils.encode_cell({
                r: rowIndex,
                c: columnIndex,
              });
              const cell = sheet[cellAddress];
              row.push(spreadsheetCellToString(cell));
              metaRow.push(spreadsheetCellMeta(cell));
            }
            table.push(row);
            cellMetaRows.push(metaRow);
          }
          if (!table.length) {
            reject(new Error("Spreadsheet has no data rows."));
            return;
          }
          const headers = table[0]?.map((header) => header.trim()) ?? [];
          const dataRows = table
            .slice(1)
            .filter((row) => row.some((cell) => cell.trim().length > 0));
          const metaRows = cellMetaRows
            .slice(1)
            .filter((_, index) => table[index + 1]?.some((cell) => cell.trim().length > 0));
          if (!headers.length) {
            reject(new Error("Spreadsheet header row is empty."));
            return;
          }
          resolve({
            fileName: file.name,
            source: "xlsx",
            headers,
            rows: dataRows,
            xlsxMeta: { cellMetaRows: metaRows },
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

function resolveXlsxJanWarnings(
  sourceData: DataSource | null,
  rowIndex: number,
  mapping: ColumnMapping,
  janDigits: string | null,
): string[] {
  if (!sourceData || sourceData.source !== "xlsx") {
    return [];
  }
  const janHeader = mapping.jan;
  const janColumnIndex = janHeader ? sourceData.headers.indexOf(janHeader) : -1;
  if (janColumnIndex < 0) {
    return [];
  }
  const meta = sourceData.xlsxMeta?.cellMetaRows[rowIndex]?.[janColumnIndex];
  if (!meta?.isNumeric) {
    return [];
  }
  if (meta.scientific || meta.decimal) {
    return [
      "JAN came from an XLSX numeric cell with scientific notation or decimals. Format the column as text before import.",
    ];
  }
  if (!janDigits) {
    return [
      "JAN came from an XLSX numeric cell and could not be preserved as 12/13 digits. Format the column as text before import.",
    ];
  }
  if (janDigits.length === 12) {
    return [
      "JAN came from an XLSX numeric cell with 12 digits. Leading zero intent is ambiguous; store the column as text before import.",
    ];
  }
  return [
    "JAN came from an XLSX numeric cell. Verify that leading zeros were preserved before dispatch.",
  ];
}

function buildDraftFromRow(input: {
  rowIndex: number;
  sourceRow: DataRow;
  sourceData: DataSource | null;
  mapping: ColumnMapping;
  templateCatalogIssue: string | null;
  templateRef: LabelTemplateRef | null;
  printerProfile: PrinterProfile | null;
  templateRefs: LabelTemplateRef[];
  printerProfiles: PrinterProfile[];
  actor: string;
  execution: PrintExecutionIntent;
  janSourceHint: string;
}): PreparedRow {
  const errors: string[] = [];
  const warnings: string[] = [];
  const row = input.sourceRow;
  const actor = input.actor.trim();
  const enabledResolution = resolveEnabledSourceValue(row);
  if (enabledResolution.invalid) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: [enabledResolution.reason ?? "Enabled column is invalid."],
      warnings,
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
      warnings,
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
        warnings,
        status: "pending",
        pendingReason: templateResolution.reason,
      };
    }
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: ["Template reference is missing. Fix template import or template selection."],
      warnings,
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
        warnings,
        status: "pending",
        pendingReason: printerResolution.reason,
      };
    }
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors: ["Printer profile is required."],
      warnings,
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
      warnings,
      pendingReason: null,
    };
  }
  if (!actor) {
    errors.push("Actor is required.");
  }
  if (input.templateCatalogIssue) {
    errors.push(input.templateCatalogIssue);
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
  warnings.push(
    ...resolveXlsxJanWarnings(input.sourceData, input.rowIndex, input.mapping, janDigits),
  );
  if (!janDigits) {
    errors.push("JAN must be 12 or 13 digits with digits only.");
  }
  if (
    warnings.some(
      (warning) =>
        warning.includes("could not be preserved") ||
        warning.includes("ambiguous") ||
        warning.includes("scientific notation"),
    )
  ) {
    errors.push(...warnings);
  }
  const qty = Number.parseInt(qtyText.replace(/,/g, ""), 10);
  if (!Number.isFinite(qty) || qty < 1) errors.push("Quantity must be >= 1.");
  if (errors.length > 0) {
    return {
      rowIndex: input.rowIndex,
      sourceRow: row,
      draft: null,
      errors,
      warnings,
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
        warnings,
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
    warnings,
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
  templateCatalogIssue: string | null,
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
  if (templateCatalogIssue) errors.templateId = templateCatalogIssue;
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

function buildAuditExportFilename(scope: AuditLedgerScope, exportedAt: string): string {
  const token = exportedAt.replace(/[:.]/g, "-");
  return `audit-ledger-${scope}-${token}.json`;
}

function buildAuditExportDocument(result: AuditExportResult): string {
  return JSON.stringify(
    {
      schema_version: "audit-ledger-export-v1",
      exported_at: new Date().toISOString(),
      scope: result.scope,
      snapshot: result.snapshot,
    },
    null,
    2,
  );
}

function parseOptionalPositiveInteger(value: string, label: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  if (!/^\d+$/.test(trimmed)) {
    throw new Error(`${label} must be a whole number.`);
  }
  const parsed = Number.parseInt(trimmed, 10);
  if (parsed < 1) {
    throw new Error(`${label} must be greater than or equal to 1.`);
  }
  return parsed;
}

function describeAuditTrimResult(result: AuditRetentionResult): string {
  const summary = `${result.removedDispatchCount} dispatch and ${result.removedProofCount} proof entr${
    result.removedDispatchCount + result.removedProofCount === 1 ? "y" : "ies"
  }`;
  const retainedSummary = `${result.retainedDispatchCount} dispatch and ${result.retainedProofCount} proof entr${
    result.retainedDispatchCount + result.retainedProofCount === 1 ? "y" : "ies"
  } remain`;
  if (result.dryRun) {
    return `Dry run complete. ${summary} would be removed. ${retainedSummary} remain.`;
  }
  if (result.backup) {
    return `Trim complete. ${summary} removed and ${retainedSummary}. Backup saved at ${result.backup.filePath}.`;
  }
  return `Trim complete. ${summary} removed. ${retainedSummary}.`;
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
  const [templateCatalogState, setTemplateCatalogState] = useState<TemplateCatalogState>({
    phase: "idle",
    templates: templateOptions,
    defaultTemplateVersion: templateVersionOf(templateOptions[0]),
    message: "Using bundled template catalog fallback.",
  });
  const [templateCatalogWriteState, setTemplateCatalogWriteState] = useState<SubmitState>({
    phase: "idle",
    message: "",
  });
  const [auditQuery, setAuditQuery] = useState("");
  const [auditSearch, setAuditSearch] = useState<AuditSearchState>({
    phase: "idle",
    entries: [],
    message: "Audit ledger has not been loaded yet.",
    lastUpdatedAt: null,
  });
  const [auditScope, setAuditScope] = useState<AuditLedgerScope>("all");
  const [auditMaxAgeDays, setAuditMaxAgeDays] = useState("30");
  const [auditMaxEntries, setAuditMaxEntries] = useState("500");
  const [auditDryRun, setAuditDryRun] = useState(true);
  const [auditExportState, setAuditExportState] = useState<AuditMaintenanceState>({
    phase: "idle",
    message: "",
    detail: null,
  });
  const [auditTrimState, setAuditTrimState] = useState<AuditMaintenanceState>({
    phase: "idle",
    message: "",
    detail: null,
  });
  const [proofReviewNotes, setProofReviewNotes] = useState("");
  const [proofReview, setProofReview] = useState<ProofReviewState>({
    phase: "idle",
    message: "",
    proofJobId: null,
    action: null,
  });
  const [legacyProofSeedSource, setLegacyProofSeedSource] = useState<DataSource | null>(null);
  const [legacyProofSeedError, setLegacyProofSeedError] = useState<string | null>(null);
  const [legacyProofSeedState, setLegacyProofSeedState] = useState<LegacyProofSeedState>({
    phase: "idle",
    message: "",
    result: null,
  });
  const [templateRenderPreview, setTemplateRenderPreview] = useState<TemplateRenderPreviewState>({
    phase: "idle",
    message: "Run Rust preview to compare the live editor against the renderer path.",
    result: null,
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
  const templateEditorModel = useMemo(
    () => parseTemplateEditorModel(parsedTemplate.spec),
    [parsedTemplate.spec],
  );
  const availableTemplateOptions = useMemo(
    () =>
      templateCatalogState.templates.length > 0 ? templateCatalogState.templates : templateOptions,
    [templateCatalogState.templates],
  );
  const knownTemplateVersions = useMemo(
    () => new Set(availableTemplateOptions.map((option) => templateVersionOf(option))),
    [availableTemplateOptions],
  );
  const templateReferenceIsKnown = templateReference
    ? knownTemplateVersions.has(templateVersionOf(templateReference))
    : false;
  const resolvedTemplateRef = useMemo(
    () =>
      pickTemplateRef(
        form.templateId,
        templateReferenceIsKnown ? templateReference : null,
        availableTemplateOptions,
      ),
    [availableTemplateOptions, form.templateId, templateReference, templateReferenceIsKnown],
  );
  const selectedPrinterProfile = useMemo(
    () => printerProfiles.find((option) => option.id === form.printerProfileId),
    [form.printerProfileId],
  );
  const executionIntent = useMemo(() => buildExecutionIntent(form), [form]);
  const templateOptionLabel = resolvedTemplateRef
    ? `${resolvedTemplateRef.id} / ${resolvedTemplateRef.version}`
    : "Missing";
  const templateReferenceVersion = templateReference ? templateVersionOf(templateReference) : null;
  const templateReferenceCatalogSource = catalogSourceFromVersion(
    availableTemplateOptions,
    templateReferenceVersion,
  );
  const templateCatalogSummary = buildTemplateSourceSummary(availableTemplateOptions);
  const selectedTemplateOption =
    availableTemplateOptions.find(
      (option) =>
        option.id === resolvedTemplateRef?.id && option.version === resolvedTemplateRef?.version,
    ) ?? null;
  const templateCandidates = useMemo(() => {
    const candidates: LabelTemplateRef[] = availableTemplateOptions.map((entry) => ({
      id: entry.id,
      version: entry.version,
    }));
    if (templateReference && templateReferenceIsKnown) {
      const exists = candidates.some(
        (option) =>
          option.id === templateReference.id && option.version === templateReference.version,
      );
      if (!exists) {
        candidates.push(templateReference);
      }
    }
    return candidates;
  }, [availableTemplateOptions, templateReference, templateReferenceIsKnown]);
  const templateCatalogIssue =
    templateReference && !templateReferenceIsKnown
      ? `template_version '${templateVersionOf(templateReference)}' is not present in the desktop template catalog. Proof/print dispatch is blocked until the packaged or local catalog is updated.`
      : null;

  const { draft, errors } = validateDraft(
    form,
    session,
    resolvedTemplateRef,
    selectedPrinterProfile ?? null,
    executionIntent,
    templateCatalogIssue,
  );
  const visibleErrors = showErrors ? errors : {};
  const templatePreviewBindings = useMemo(
    () =>
      buildTemplatePreviewBindings({
        draft,
        form,
        session,
        resolvedTemplateRef,
      }),
    [draft, form, resolvedTemplateRef, session],
  );
  const templateDraftPreviewRequest = useMemo(
    () =>
      buildTemplateDraftPreviewRequest({
        templateSource,
        draft,
        form,
        session,
      }),
    [draft, form, session, templateSource],
  );
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
        sourceData,
        mapping: fieldMapping,
        templateCatalogIssue,
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
    templateCatalogIssue,
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
  const canSubmitQueuedRows =
    !templateCatalogIssue &&
    queuedRows.some(
      (entry) => entry.submissionStatus === "ready" || entry.submissionStatus === "failed",
    );
  const isQueueReady = sourceRows.length > 0 && readyRowsCount > 0;
  const sourceSummary = sourceData
    ? `${sourceData.rows.length} rows / ${sourceData.headers.length} columns (${sourceData.source})`
    : "No source loaded";
  const legacyProofSeedRequest = useMemo(
    () => buildLegacyProofSeedRequest(legacyProofSeedSource),
    [legacyProofSeedSource],
  );
  const legacyProofSeedSummary = legacyProofSeedSource
    ? `${legacyProofSeedSource.rows.length} rows / ${legacyProofSeedSource.headers.length} columns (${legacyProofSeedSource.source})`
    : "No legacy proof seed file loaded";
  const legacyProofSeedPreviewRows = legacyProofSeedRequest?.rows.slice(0, 6) ?? [];
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
  const bridgeWarnings = useMemo(
    () => normalizeBridgeWarnings(bridgeStatus.status),
    [bridgeStatus.status],
  );
  const allowWithoutProofEnabled = bridgeStatus.status?.allowWithoutProofEnabled ?? false;
  const blockingBridgeWarnings = bridgeWarnings.filter(isBlockingBridgeWarning);
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
  const approvedProofEntryByJobId = useMemo(() => {
    const index = new Map<string, AuditSearchEntry>();
    for (const entry of approvedProofEntries) {
      if (entry.proof) {
        index.set(entry.proof.proofJobId, entry);
      }
    }
    return index;
  }, [approvedProofEntries]);

  const templateValidation = validateTemplateSource(templateSource);
  const templateRenderPreviewSvgDataUrl = templateRenderPreview.result
    ? `data:image/svg+xml;charset=utf-8,${encodeURIComponent(templateRenderPreview.result.svg)}`
    : null;
  const templateUsesPreviewOnlyPlaceholder = templateSource.includes("{parent_sku}");
  const templateCanvasStyle: CSSProperties | undefined = templateEditorModel
    ? {
        aspectRatio: `${Math.max(templateEditorModel.page.widthMm, 1)} / ${Math.max(
          templateEditorModel.page.heightMm,
          1,
        )}`,
        backgroundColor: templateEditorModel.page.backgroundFill,
        borderColor: templateEditorModel.border.visible
          ? templateEditorModel.border.color
          : "transparent",
        borderWidth: templateEditorModel.border.visible
          ? `${Math.max(templateEditorModel.border.widthMm * 2, 1)}px`
          : "1px",
      }
    : undefined;

  useEffect(() => {
    if (!allowWithoutProofEnabled && form.executionAllowWithoutProof) {
      setForm((current) => ({ ...current, executionAllowWithoutProof: false }));
    }
  }, [allowWithoutProofEnabled, form.executionAllowWithoutProof]);

  useEffect(() => {
    setQueuedRows((current) => {
      if (current.length === 0) {
        return current;
      }
      if (templateCatalogIssue) {
        const blockMessage = buildTemplateCatalogBlockMessage(templateCatalogIssue);
        let changed = false;
        const next = current.map((row) => {
          if (
            row.submissionStatus === "ready" ||
            row.submissionStatus === "submitting" ||
            isTemplateCatalogBlockedQueuedRow(row)
          ) {
            const alreadyBlocked =
              row.submissionStatus === "failed" && row.dispatchError === blockMessage;
            if (alreadyBlocked) {
              return row;
            }
            changed = true;
            return {
              ...row,
              submissionStatus: "failed" as const,
              dispatchResult: null,
              dispatchError: blockMessage,
            };
          }
          return row;
        });
        return changed ? next : current;
      }

      let changed = false;
      const next = current.map((row) => {
        if (
          row.submissionStatus === "failed" &&
          isTemplateCatalogBlockedQueuedRow(row) &&
          row.dispatchResult === null
        ) {
          changed = true;
          return {
            ...row,
            submissionStatus: "ready" as const,
            dispatchError: null,
          };
        }
        return row;
      });
      return changed ? next : current;
    });
  }, [templateCatalogIssue]);

  function updateField<Key extends keyof FormState>(key: Key, value: FormState[Key]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function updateTemplateSource(value: string) {
    setTemplateSource(value);
    setTemplateParseError(null);
    setTemplateImportError("");
    setTemplateCatalogWriteState({ phase: "idle", message: "" });
  }

  function applyStructuredTemplateUpdate(
    mutate: (spec: TemplateSpec) => void,
    invalidMessage = "Fix template JSON before using the structured editor.",
  ) {
    const updated = updateTemplateSpecSource(templateSource, mutate);
    if (!updated.next) {
      setTemplateParseError(updated.error ?? invalidMessage);
      return;
    }
    setTemplateSource(updated.next);
    setTemplateParseError(null);
    setTemplateImportError("");
  }

  function updateTemplateMetaField(
    key: "label_name" | "description" | "template_version",
    value: string,
  ) {
    applyStructuredTemplateUpdate((spec) => {
      spec[key] = value;
    });
  }

  function updateTemplatePageField(
    key: "width_mm" | "height_mm" | "background_fill",
    value: number | string,
  ) {
    applyStructuredTemplateUpdate((spec) => {
      const page = isPlainObject(spec.page) ? { ...spec.page } : {};
      page[key] =
        typeof value === "number"
          ? Math.max(0.1, Number(value) || 0.1)
          : normalizeTemplateColor(value, "#ffffff");
      spec.page = page;
    });
  }

  function updateTemplateBorderField(
    key: "visible" | "color" | "width_mm",
    value: boolean | number | string,
  ) {
    applyStructuredTemplateUpdate((spec) => {
      const border = isPlainObject(spec.border) ? { ...spec.border } : {};
      if (key === "visible") {
        border[key] = Boolean(value);
      } else if (key === "width_mm") {
        border[key] = Math.max(0, Number(value) || 0);
      } else {
        border[key] = normalizeTemplateColor(String(value), "#000000");
      }
      spec.border = border;
    });
  }

  function updateTemplateFieldRow(
    index: number,
    key: keyof TemplateFieldEditor,
    value: string | number,
  ) {
    applyStructuredTemplateUpdate((spec) => {
      const fields = Array.isArray(spec.fields) ? [...spec.fields] : [];
      const current = isPlainObject(fields[index]) ? { ...fields[index] } : {};
      if (key === "color") {
        current.color = normalizeTemplateColor(String(value));
      } else if (key === "xMm") {
        current.x_mm = Math.max(0, Number(value) || 0);
      } else if (key === "yMm") {
        current.y_mm = Math.max(0, Number(value) || 0);
      } else if (key === "fontSizeMm") {
        current.font_size_mm = Math.max(0.1, Number(value) || 0.1);
      } else if (key === "name") {
        current.name = String(value);
      } else if (key === "template") {
        current.template = String(value);
      }
      fields[index] = current;
      spec.fields = fields;
    });
  }

  function moveTemplateField(index: number, direction: -1 | 1) {
    applyStructuredTemplateUpdate((spec) => {
      const fields = Array.isArray(spec.fields) ? [...spec.fields] : [];
      const nextIndex = index + direction;
      if (index < 0 || nextIndex < 0 || index >= fields.length || nextIndex >= fields.length) {
        return;
      }
      const [field] = fields.splice(index, 1);
      fields.splice(nextIndex, 0, field);
      spec.fields = fields;
    });
  }

  function addTemplateField() {
    applyStructuredTemplateUpdate((spec) => {
      const fields = Array.isArray(spec.fields) ? [...spec.fields] : [];
      fields.push({
        name: `field_${fields.length + 1}`,
        x_mm: 2,
        y_mm: Math.min(26, 4 + fields.length * 4),
        font_size_mm: 3,
        template: "text:{sku}",
        color: DEFAULT_TEMPLATE_FIELD_COLOR,
      });
      spec.fields = fields;
    });
  }

  function duplicateTemplateField(index: number) {
    applyStructuredTemplateUpdate((spec) => {
      const fields = Array.isArray(spec.fields) ? [...spec.fields] : [];
      const field = fields[index];
      if (!isPlainObject(field)) {
        return;
      }
      const clone = { ...field };
      clone.name =
        typeof clone.name === "string" && clone.name.trim().length > 0
          ? `${clone.name}_copy`
          : `field_${fields.length + 1}`;
      clone.y_mm = Math.max(0, Number(clone.y_mm ?? 0) + 3);
      fields.splice(index + 1, 0, clone);
      spec.fields = fields;
    });
  }

  function removeTemplateField(index: number) {
    applyStructuredTemplateUpdate((spec) => {
      const fields = Array.isArray(spec.fields) ? [...spec.fields] : [];
      if (fields.length <= 1) {
        return;
      }
      fields.splice(index, 1);
      spec.fields = fields;
    });
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

  async function runTemplateCatalogSave() {
    const parsed = parseTemplateSpec(templateSource);
    if (parsed.error || !parsed.spec) {
      setTemplateCatalogWriteState({
        phase: "error",
        message: parsed.error ?? "Template JSON must be valid before saving.",
      });
      return;
    }
    const templateRef = parseTemplateRef(parsed.spec);
    if (!templateRef) {
      setTemplateCatalogWriteState({
        phase: "error",
        message:
          "Template JSON must include a valid template_version (for example `template-id@version`) before saving.",
      });
      return;
    }
    const templateVersion = templateVersionOf(templateRef);
    const validation = validateTemplateSource(templateSource);
    if (validation.status === "invalid") {
      setTemplateCatalogWriteState({
        phase: "error",
        message: `Cannot save invalid template: ${validation.message}`,
      });
      return;
    }

    setTemplateCatalogWriteState({
      phase: "submitting",
      message: `Saving ${templateVersion} to desktop local catalog...`,
    });

    try {
      const result: SaveTemplateToLocalCatalogResult = await saveTemplateToLocalCatalog({
        templateSource,
      });
      const responseVersion = result.templateVersion ?? templateVersion;
      const action = result.status === "updated" ? "updated" : "saved";
      setTemplateCatalogWriteState({
        phase: "success",
        message:
          result.message ?? `Template ${responseVersion} ${action} in desktop local catalog.`,
      });
      await refreshTemplateCatalog();
    } catch (error) {
      setTemplateCatalogWriteState({
        phase: "error",
        message: `Template catalog save failed: ${formatErrorMessage(error)}`,
      });
    }
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
    setTemplateCatalogWriteState({
      phase: "idle",
      message: "",
    });
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

  async function handleLegacyProofSeedUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0];
    if (!file) {
      return;
    }
    setLegacyProofSeedError(null);
    setLegacyProofSeedSource(null);
    setLegacyProofSeedState({
      phase: "idle",
      message: "",
      result: null,
    });
    try {
      const parsed = await parseUploadedData(file);
      setLegacyProofSeedSource(parsed);
    } catch (error) {
      setLegacyProofSeedError(
        error instanceof Error ? error.message : "Legacy proof import failed.",
      );
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

  async function runLegacyProofSeed(action: LegacyProofSeedPhase) {
    if (!legacyProofSeedRequest) {
      setLegacyProofSeedState({
        phase: "error",
        message: "Load a CSV/XLSX file before validating or seeding legacy proofs.",
        result: null,
      });
      return;
    }
    setLegacyProofSeedState({
      phase: action,
      message:
        action === "validating"
          ? "Validating legacy proof seed rows..."
          : "Seeding legacy proof rows as pending review...",
      result: null,
    });
    try {
      const result =
        action === "validating"
          ? await validateLegacyProofSeed(legacyProofSeedRequest)
          : await seedLegacyProofs(legacyProofSeedRequest);
      setLegacyProofSeedState({
        phase: result.applied ? "success" : action === "validating" ? "success" : "error",
        message: result.message,
        result,
      });
      if (result.applied) {
        void refreshAuditSearch(auditQuery);
      }
    } catch (error) {
      setLegacyProofSeedState({
        phase: "error",
        message: `Legacy proof seed failed: ${formatErrorMessage(error)}`,
        result: null,
      });
    }
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

  const runAuditExport = useEffectEvent(async () => {
    if (!isTauriConnected()) {
      setAuditExportState({
        phase: "error",
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell.",
        detail: null,
      });
      return;
    }

    setAuditExportState({
      phase: "submitting",
      message: "Building audit export from desktop ledger...",
      detail: null,
    });

    try {
      const result = await exportAuditLedger({ scope: auditScope });
      const exportedAt = new Date().toISOString();
      const fileName = buildAuditExportFilename(result.scope, exportedAt);
      downloadText(fileName, buildAuditExportDocument(result));
      setAuditExportState({
        phase: "success",
        message: `Exported ${result.dispatchCount} dispatch and ${result.proofCount} proof entries as JSON.`,
        detail: `Saved ${fileName}.`,
      });
    } catch (error) {
      setAuditExportState({
        phase: "error",
        message: `Audit export failed: ${formatErrorMessage(error)}`,
        detail: null,
      });
    }
  });

  const runAuditTrim = useEffectEvent(async () => {
    if (!isTauriConnected()) {
      setAuditTrimState({
        phase: "error",
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell.",
        detail: null,
      });
      return;
    }

    let maxAgeDays: number | null;
    let maxEntries: number | null;
    try {
      maxAgeDays = parseOptionalPositiveInteger(auditMaxAgeDays, "Max age days");
      maxEntries = parseOptionalPositiveInteger(auditMaxEntries, "Max entries");
    } catch (error) {
      setAuditTrimState({
        phase: "error",
        message: formatErrorMessage(error),
        detail: null,
      });
      return;
    }

    if (maxAgeDays === null && maxEntries === null) {
      setAuditTrimState({
        phase: "error",
        message: "Set max age days, max entries, or both before running retention.",
        detail: null,
      });
      return;
    }

    setAuditTrimState({
      phase: "submitting",
      message: auditDryRun
        ? "Running audit retention dry run..."
        : "Trimming audit ledger and writing backup bundle...",
      detail: null,
    });

    try {
      const result = await trimAuditLedger({
        scope: auditScope,
        maxAgeDays: maxAgeDays ?? undefined,
        maxEntries: maxEntries ?? undefined,
        dryRun: auditDryRun,
      });
      setAuditTrimState({
        phase: "success",
        message: describeAuditTrimResult(result),
        detail: result.backup?.filePath ?? null,
      });
      if (!result.dryRun) {
        void refreshAuditSearch(auditQuery);
      }
    } catch (error) {
      setAuditTrimState({
        phase: "error",
        message: `Audit retention failed: ${formatErrorMessage(error)}`,
        detail: null,
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
      setTemplateCatalogState({
        phase: "unavailable",
        templates: templateOptions,
        defaultTemplateVersion: templateVersionOf(templateOptions[0]),
        message:
          "Browser preview mode / desktop bridge unavailable. Using bundled template catalog fallback.",
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
      void refreshTemplateCatalog();
      void refreshAuditSearch();
    } catch (error) {
      setBridgeStatus({
        phase: "error",
        status: null,
        message: `Bridge status check failed: ${formatErrorMessage(error)}`,
      });
    }
  });

  const refreshTemplateCatalog = useEffectEvent(async () => {
    if (!isTauriConnected()) {
      setTemplateCatalogState({
        phase: "unavailable",
        templates: templateOptions,
        defaultTemplateVersion: templateVersionOf(templateOptions[0]),
        message:
          "Browser preview mode / desktop bridge unavailable. Using bundled template catalog fallback.",
      });
      return;
    }

    setTemplateCatalogState((current) => ({
      ...current,
      phase: "loading",
      message: "Reading desktop template catalog...",
    }));

    try {
      const catalog = await fetchTemplateCatalog();
      const options = toTemplateOptionsFromCatalog(catalog);
      setTemplateCatalogState({
        phase: "ready",
        templates: options.length > 0 ? options : templateOptions,
        defaultTemplateVersion: catalog.defaultTemplateVersion,
        message: `Loaded ${catalog.templates.length} desktop template entr${
          catalog.templates.length === 1 ? "y" : "ies"
        }.`,
      });
    } catch (error) {
      setTemplateCatalogState({
        phase: "error",
        templates: templateOptions,
        defaultTemplateVersion: templateVersionOf(templateOptions[0]),
        message: `Template catalog sync failed: ${formatErrorMessage(error)}`,
      });
    }
  });

  const refreshTemplateRenderPreview = useEffectEvent(async () => {
    if (!isTauriConnected()) {
      setTemplateRenderPreview({
        phase: "unavailable",
        message:
          "Browser preview mode / desktop bridge unavailable. Open admin-web from desktop shell to render Rust previews.",
        result: null,
      });
      return;
    }

    setTemplateRenderPreview({
      phase: "rendering",
      message: "Rendering SVG preview through desktop-shell and Rust renderer...",
      result: null,
    });

    try {
      const result = await previewTemplateDraft(templateDraftPreviewRequest);
      setTemplateRenderPreview({
        phase: "ready",
        message: `Rust preview ready for ${result.templateVersion} (${result.fieldCount} fields).`,
        result,
      });
    } catch (error) {
      setTemplateRenderPreview({
        phase: "error",
        message: `Rust preview failed: ${formatErrorMessage(error)}`,
        result: null,
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

  function buildProofLinkedDispatchOptions(
    draft: PrintJobDraft,
    options: DispatchRequestOptions = {},
  ): DispatchRequestOptions {
    if (options.jobLineageId) {
      return options;
    }
    if (draft.execution?.mode !== "print") {
      return options;
    }
    const sourceProofJobId = draft.execution.sourceProofJobId?.trim();
    if (!sourceProofJobId) {
      return options;
    }
    const proofEntry = approvedProofEntryByJobId.get(sourceProofJobId);
    if (!proofEntry?.proof) {
      return options;
    }
    return {
      ...options,
      jobLineageId: proofEntry.proof.jobLineageId,
    };
  }

  function buildLegacyProofSeedRequest(
    sourceData: DataSource | null,
  ): LegacyProofSeedRequest | null {
    if (!sourceData) {
      return null;
    }
    const rows = toSourceRows(sourceData).map((row) => {
      const qtyText = readSourceValue(row, fieldAliases.qty).replace(/,/g, "").trim();
      const qty = /^\d+$/.test(qtyText) ? Number.parseInt(qtyText, 10) : 0;
      const jobLineageId = readSourceValue(row, LEGACY_JOB_LINEAGE_ID_ALIASES);
      const notes = readSourceValue(row, LEGACY_NOTES_ALIASES);
      return {
        proofJobId: readSourceValue(row, LEGACY_PROOF_JOB_ID_ALIASES),
        artifactPath: readSourceValue(row, LEGACY_ARTIFACT_PATH_ALIASES),
        templateVersion: readSourceValue(row, LEGACY_TEMPLATE_VERSION_ALIASES),
        matchSubject: {
          sku: readSourceValue(row, fieldAliases.sku),
          brand: readSourceValue(row, fieldAliases.brand),
          jan: readSourceValue(row, fieldAliases.jan),
          qty,
        },
        requestedBy: {
          userId: readSourceValue(row, LEGACY_REQUESTED_BY_USER_ID_ALIASES),
          displayName: readSourceValue(row, LEGACY_REQUESTED_BY_DISPLAY_NAME_ALIASES),
        },
        requestedAt: readSourceValue(row, LEGACY_REQUESTED_AT_ALIASES),
        jobLineageId: jobLineageId || undefined,
        notes: notes || undefined,
      };
    });
    return { rows };
  }

  function applyApprovedProofToForm(entry: AuditSearchEntry) {
    const proof = entry.proof;
    if (proof?.status !== "approved") {
      return;
    }
    setForm((current) => ({
      ...current,
      sku: entry.dispatch.matchSubject.sku,
      brand: entry.dispatch.matchSubject.brand,
      jan: entry.dispatch.matchSubject.janNormalized,
      qty: String(entry.dispatch.matchSubject.qty),
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
    if (templateCatalogIssue) {
      setManualSubmit({
        phase: "error",
        message: templateCatalogIssue,
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
      const result = await dispatchDraft(draft, buildProofLinkedDispatchOptions(draft));
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
        (item): item is { entry: QueuedRow; queueIndex: number } =>
          item.entry.draft !== null &&
          (item.entry.submissionStatus === "ready" || item.entry.submissionStatus === "failed"),
      );

    if (isBridgeSubmitBlocked || bridgeSubmitBlockMessage) {
      setBatchSubmit({
        phase: "error",
        message: bridgeSubmitBlockMessage ?? "Batch submit is blocked by bridge safety checks.",
        results: [],
      });
      return;
    }
    if (templateCatalogIssue) {
      setBatchSubmit({
        phase: "error",
        message: templateCatalogIssue,
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
      const linkedDispatchOptions = buildProofLinkedDispatchOptions(activeDraft, dispatchOptions);

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
        const result = await dispatchDraft(activeDraft, linkedDispatchOptions);
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
        xlsxMeta: sourceData.xlsxMeta ?? null,
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

  useEffect(() => {
    const previewJobId = templateDraftPreviewRequest.sample.jobId;
    setTemplateRenderPreview((current) => {
      if (current.phase === "idle" && current.result === null) {
        return current;
      }
      return {
        phase: "idle",
        message: `Template or sample data changed for ${previewJobId}. Refresh Rust preview to compare the renderer path.`,
        result: null,
      };
    });
  }, [templateDraftPreviewRequest]);

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
            {bridgeWarnings.length > 0 ? (
              <ul className="card-list">
                {bridgeWarnings.map((warning) => (
                  <li
                    key={`${warning.code}-${warning.message}`}
                    className={
                      warning.severity === "error"
                        ? "status-fail"
                        : warning.severity === "warning"
                          ? "status-pending"
                          : undefined
                    }
                  >
                    <strong>{warning.code}</strong>: {warning.message}
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
                {availableTemplateOptions.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label} ({option.catalogSource ?? "unknown"})
                  </option>
                ))}
              </select>
              {visibleErrors.templateId ? (
                <small className="error-text">{visibleErrors.templateId}</small>
              ) : null}
              {templateCatalogState.phase !== "ready" ? (
                <small className="hint-text">{templateCatalogState.message}</small>
              ) : null}
              {templateReference ? (
                <small className="hint-text">
                  Template route {templateVersionOf(templateReference)} is sourced as{" "}
                  {templateReferenceCatalogSource}.
                </small>
              ) : null}
              <small className="hint-text">Desktop catalog: {templateCatalogSummary}.</small>
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
              <small>
                {selectedTemplateOption ? selectedTemplateOption.size : "template route inferred"}
              </small>
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
          <p>
            Edit page settings and text fields in a structured authoring view, while keeping the
            underlying template-spec JSON available for low-level changes.
          </p>
        </div>
        <div className="proof-note">
          <strong>Current release boundary</strong>
          <p>
            Rust preview below renders the live JSON draft, and template JSON can now be written to
            the desktop local catalog via Tauri. Keep template_version explicit so catalog
            resolution stays aligned with dispatch gates.
          </p>
        </div>
        <div className="template-workbench">
          <div className="template-editor-column">
            {templateEditorModel ? (
              <>
                <div className="template-control-grid">
                  <label className="field">
                    <span>Label name</span>
                    <input
                      value={templateEditorModel.labelName}
                      onChange={(event) =>
                        updateTemplateMetaField("label_name", event.target.value)
                      }
                    />
                  </label>
                  <label className="field">
                    <span>Template version</span>
                    <input
                      value={templateEditorModel.templateVersion}
                      onChange={(event) =>
                        updateTemplateMetaField("template_version", event.target.value)
                      }
                    />
                  </label>
                  <label className="field field-wide">
                    <span>Description</span>
                    <input
                      value={templateEditorModel.description}
                      onChange={(event) =>
                        updateTemplateMetaField("description", event.target.value)
                      }
                    />
                  </label>
                  <label className="field">
                    <span>Width (mm)</span>
                    <input
                      type="number"
                      min="1"
                      step="0.1"
                      value={templateEditorModel.page.widthMm}
                      onChange={(event) =>
                        updateTemplatePageField(
                          "width_mm",
                          Number.parseFloat(event.target.value || "0"),
                        )
                      }
                    />
                  </label>
                  <label className="field">
                    <span>Height (mm)</span>
                    <input
                      type="number"
                      min="1"
                      step="0.1"
                      value={templateEditorModel.page.heightMm}
                      onChange={(event) =>
                        updateTemplatePageField(
                          "height_mm",
                          Number.parseFloat(event.target.value || "0"),
                        )
                      }
                    />
                  </label>
                  <label className="field">
                    <span>Background</span>
                    <div className="color-row">
                      <input
                        type="color"
                        value={templateEditorModel.page.backgroundFill}
                        onChange={(event) =>
                          updateTemplatePageField("background_fill", event.target.value)
                        }
                      />
                      <input
                        value={templateEditorModel.page.backgroundFill}
                        onChange={(event) =>
                          updateTemplatePageField("background_fill", event.target.value)
                        }
                      />
                    </div>
                  </label>
                  <div className="field checkbox-field">
                    <span>Border</span>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={templateEditorModel.border.visible}
                        onChange={(event) =>
                          updateTemplateBorderField("visible", event.target.checked)
                        }
                      />
                      <span>Show border in render and preview</span>
                    </label>
                  </div>
                  <label className="field">
                    <span>Border color</span>
                    <div className="color-row">
                      <input
                        type="color"
                        value={templateEditorModel.border.color}
                        onChange={(event) => updateTemplateBorderField("color", event.target.value)}
                      />
                      <input
                        value={templateEditorModel.border.color}
                        onChange={(event) => updateTemplateBorderField("color", event.target.value)}
                      />
                    </div>
                  </label>
                  <label className="field">
                    <span>Border width (mm)</span>
                    <input
                      type="number"
                      min="0"
                      step="0.1"
                      value={templateEditorModel.border.widthMm}
                      onChange={(event) =>
                        updateTemplateBorderField(
                          "width_mm",
                          Number.parseFloat(event.target.value || "0"),
                        )
                      }
                    />
                  </label>
                </div>

                <div className="template-fields-panel">
                  <div className="data-grid-header">
                    <strong>Template fields</strong>
                    <span>{templateEditorModel.fields.length} fields</span>
                  </div>
                  <div className="template-field-list">
                    {templateEditorModel.fields.map((field, index) => (
                      <article className="template-field-card" key={`${field.name}-${index}`}>
                        <div className="template-field-header">
                          <div>
                            <strong>{field.name || `field_${index + 1}`}</strong>
                            <small>
                              {field.xMm.toFixed(1)}mm / {field.yMm.toFixed(1)}mm /{" "}
                              {field.fontSizeMm.toFixed(1)}mm
                            </small>
                          </div>
                          <div className="template-field-actions">
                            <button
                              className="button-secondary"
                              type="button"
                              onClick={() => moveTemplateField(index, -1)}
                              disabled={index === 0}
                            >
                              Up
                            </button>
                            <button
                              className="button-secondary"
                              type="button"
                              onClick={() => moveTemplateField(index, 1)}
                              disabled={index === templateEditorModel.fields.length - 1}
                            >
                              Down
                            </button>
                            <button
                              className="button-secondary"
                              type="button"
                              onClick={() => duplicateTemplateField(index)}
                            >
                              Duplicate
                            </button>
                            <button
                              className="button-secondary"
                              type="button"
                              onClick={() => removeTemplateField(index)}
                              disabled={templateEditorModel.fields.length <= 1}
                            >
                              Delete
                            </button>
                          </div>
                        </div>
                        <div className="template-control-grid">
                          <label className="field">
                            <span>Name</span>
                            <input
                              value={field.name}
                              onChange={(event) =>
                                updateTemplateFieldRow(index, "name", event.target.value)
                              }
                            />
                          </label>
                          <label className="field field-wide">
                            <span>Text template</span>
                            <input
                              value={field.template}
                              onChange={(event) =>
                                updateTemplateFieldRow(index, "template", event.target.value)
                              }
                            />
                            <small className="hint-text">
                              Rust render: {RUST_RENDER_PLACEHOLDERS.join(", ")}. Local
                              preview-only: {LOCAL_TEMPLATE_PREVIEW_ONLY_PLACEHOLDERS.join(", ")}.
                            </small>
                          </label>
                          <label className="field">
                            <span>X (mm)</span>
                            <input
                              type="number"
                              min="0"
                              step="0.1"
                              value={field.xMm}
                              onChange={(event) =>
                                updateTemplateFieldRow(
                                  index,
                                  "xMm",
                                  Number.parseFloat(event.target.value || "0"),
                                )
                              }
                            />
                          </label>
                          <label className="field">
                            <span>Y (mm)</span>
                            <input
                              type="number"
                              min="0"
                              step="0.1"
                              value={field.yMm}
                              onChange={(event) =>
                                updateTemplateFieldRow(
                                  index,
                                  "yMm",
                                  Number.parseFloat(event.target.value || "0"),
                                )
                              }
                            />
                          </label>
                          <label className="field">
                            <span>Font size (mm)</span>
                            <input
                              type="number"
                              min="0.1"
                              step="0.1"
                              value={field.fontSizeMm}
                              onChange={(event) =>
                                updateTemplateFieldRow(
                                  index,
                                  "fontSizeMm",
                                  Number.parseFloat(event.target.value || "0"),
                                )
                              }
                            />
                          </label>
                          <label className="field">
                            <span>Color</span>
                            <div className="color-row">
                              <input
                                type="color"
                                value={field.color}
                                onChange={(event) =>
                                  updateTemplateFieldRow(index, "color", event.target.value)
                                }
                              />
                              <input
                                value={field.color}
                                onChange={(event) =>
                                  updateTemplateFieldRow(index, "color", event.target.value)
                                }
                              />
                            </div>
                          </label>
                        </div>
                      </article>
                    ))}
                  </div>
                  <div className="toolbar">
                    <button className="button-primary" type="button" onClick={addTemplateField}>
                      Add text field
                    </button>
                  </div>
                </div>
              </>
            ) : (
              <div className="empty-state">
                <strong>Structured editor unavailable</strong>
                <p>Fix template JSON first. The form-based editor only works against valid JSON.</p>
              </div>
            )}

            <label className="field field-wide">
              <span>Template spec JSON</span>
              <textarea
                className="batch-text"
                value={templateSource}
                onChange={(event) => updateTemplateSource(event.target.value)}
                onBlur={validateTemplateText}
              />
              {templateParseError ? (
                <small className="error-text">{templateParseError}</small>
              ) : null}
              {templateImportError ? (
                <small className="error-text">{templateImportError}</small>
              ) : null}
              {templateMetaInfo ? (
                <small className="hint-text">
                  schema_version: {templateMetaInfo.schemaVersion} / template_version:{" "}
                  {templateMetaInfo.templateVersion} / label_name: {templateMetaInfo.labelName}
                </small>
              ) : null}
            </label>
          </div>

          <aside className="template-preview-column">
            <div className="section-heading">
              <h3>Visual template preview</h3>
              <p>
                Approximate label layout preview from the current template spec and live form
                values.
              </p>
            </div>
            {templateEditorModel ? (
              <>
                <div className="preview-summary">
                  <div>
                    <span>Label size</span>
                    <strong>
                      {templateEditorModel.page.widthMm.toFixed(1)} x{" "}
                      {templateEditorModel.page.heightMm.toFixed(1)} mm
                    </strong>
                    <small>{templateEditorModel.fields.length} fields</small>
                  </div>
                  <div>
                    <span>Template state</span>
                    <strong>{templateValidation.status}</strong>
                    <small>{templateValidation.message ?? "Schema route looks aligned."}</small>
                  </div>
                </div>
                {templateCatalogIssue ? (
                  <div className="proof-note">
                    <strong>Catalog mismatch</strong>
                    <p>{templateCatalogIssue}</p>
                  </div>
                ) : null}
                {templateReference ? (
                  <div className="proof-note">
                    <strong>Catalog source</strong>
                    <p>
                      {templateReferenceVersion} is tracked as {templateReferenceCatalogSource}.
                    </p>
                  </div>
                ) : null}
                {templateUsesPreviewOnlyPlaceholder ? (
                  <div className="proof-note">
                    <strong>Preview-only placeholder detected</strong>
                    <p>
                      <code>{LOCAL_TEMPLATE_PREVIEW_ONLY_PLACEHOLDERS[0]}</code> renders in the
                      local canvas only. Rust proof/PDF output does not substitute it.
                    </p>
                  </div>
                ) : null}
                <div className="template-canvas-shell">
                  <div className="template-canvas" style={templateCanvasStyle}>
                    {templateEditorModel.border.visible ? (
                      <div className="template-canvas-border" />
                    ) : null}
                    {templateEditorModel.fields.map((field, index) => (
                      <div
                        className="template-canvas-field"
                        key={`${field.name}-${index}-preview`}
                        style={{
                          left: `${(field.xMm / Math.max(templateEditorModel.page.widthMm, 1)) * 100}%`,
                          top: `${(field.yMm / Math.max(templateEditorModel.page.heightMm, 1)) * 100}%`,
                          fontSize: `${Math.max(field.fontSizeMm * 3.2, 10)}px`,
                          color: field.color,
                        }}
                      >
                        {renderTemplatePreviewText(field.template, templatePreviewBindings)}
                      </div>
                    ))}
                  </div>
                </div>
                <div className="proof-note">
                  <strong>Local binding sample</strong>
                  <p>
                    job_id: {templatePreviewBindings.job_id} / sku: {templatePreviewBindings.sku} /
                    jan: {templatePreviewBindings.jan}
                  </p>
                </div>
                <div className="template-render-preview">
                  <div className="data-grid-header">
                    <strong>Rust renderer preview</strong>
                    <button
                      className="button-secondary"
                      type="button"
                      onClick={() => void refreshTemplateRenderPreview()}
                      disabled={templateRenderPreview.phase === "rendering"}
                    >
                      {templateRenderPreview.phase === "rendering"
                        ? "Rendering..."
                        : "Refresh Rust preview"}
                    </button>
                  </div>
                  <p className="hint-text">{templateRenderPreview.message}</p>
                  {templateRenderPreview.phase === "ready" &&
                  templateRenderPreview.result &&
                  templateRenderPreviewSvgDataUrl ? (
                    <>
                      <div className="preview-summary">
                        <div>
                          <span>Renderer output</span>
                          <strong>{templateRenderPreview.result.labelName}</strong>
                          <small>
                            {templateRenderPreview.result.pageWidthMm.toFixed(1)} x{" "}
                            {templateRenderPreview.result.pageHeightMm.toFixed(1)} mm / JAN{" "}
                            {templateRenderPreview.result.normalizedJan}
                          </small>
                        </div>
                      </div>
                      <div className="template-render-preview-frame">
                        <img
                          className="template-render-preview-image"
                          src={templateRenderPreviewSvgDataUrl}
                          alt="Rust renderer SVG preview"
                        />
                      </div>
                    </>
                  ) : null}
                </div>
              </>
            ) : (
              <div className="empty-state">
                <strong>No visual preview</strong>
                <p>Template JSON must be valid before the preview canvas can be rendered.</p>
              </div>
            )}
          </aside>
        </div>
        <div className="toolbar">
          <button
            className="button-secondary"
            type="button"
            onClick={() => void runTemplateCatalogSave()}
            disabled={!isTauriConnected() || templateCatalogWriteState.phase === "submitting"}
          >
            {templateCatalogWriteState.phase === "submitting"
              ? "Saving..."
              : "Save template to local catalog"}
          </button>
          <label className="button-secondary fake-button">
            Import template JSON / asset
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
        {templateCatalogWriteState.message ? (
          <p
            className={
              templateCatalogWriteState.phase === "success"
                ? "status-ok"
                : templateCatalogWriteState.phase === "error"
                  ? "status-fail"
                  : "notice-text"
            }
          >
            {templateCatalogWriteState.message}
          </p>
        ) : null}
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
                          : entry.errors.join(" / ") ||
                            (entry.warnings.length > 0
                              ? `ready: ${entry.warnings.join(" / ")}`
                              : "ready")}
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
        <div className="proof-note">
          <strong>Audit maintenance</strong>
          <p>
            Export the current desktop ledger as JSON, then dry-run or apply retention with a backup
            bundle written under the desktop audit backup directory.
          </p>
          <small>
            {bridgeStatusAvailable && bridgeStatus.status
              ? `audit log dir: ${bridgeStatus.status.auditLogDir} / backup dir: ${bridgeStatus.status.auditBackupDir}`
              : "Desktop bridge unavailable. Audit maintenance needs desktop-shell."}
          </small>
          <div className="form-grid">
            <label className="field">
              <span>Scope</span>
              <select
                value={auditScope}
                onChange={(event) => setAuditScope(event.target.value as AuditLedgerScope)}
              >
                <option value="all">All ledgers</option>
                <option value="dispatch">Dispatch only</option>
                <option value="proof">Proof only</option>
              </select>
            </label>
            <label className="field">
              <span>Max age days</span>
              <input
                value={auditMaxAgeDays}
                onChange={(event) => setAuditMaxAgeDays(event.target.value)}
                placeholder="30"
              />
            </label>
            <label className="field">
              <span>Max entries</span>
              <input
                value={auditMaxEntries}
                onChange={(event) => setAuditMaxEntries(event.target.value)}
                placeholder="500"
              />
            </label>
            <label className="field checkbox-field">
              <span>Retention mode</span>
              <div className="checkbox-row">
                <input
                  id="audit-dry-run"
                  type="checkbox"
                  checked={auditDryRun}
                  onChange={(event) => setAuditDryRun(event.target.checked)}
                />
                <label htmlFor="audit-dry-run">Dry run first</label>
              </div>
            </label>
          </div>
          <div className="toolbar">
            <button
              className="button-secondary"
              type="button"
              onClick={() => {
                void runAuditExport();
              }}
              disabled={!bridgeStatusAvailable || auditExportState.phase === "submitting"}
            >
              Export audit JSON
            </button>
            <button
              className={auditDryRun ? "button-secondary" : "button-primary"}
              type="button"
              onClick={() => {
                void runAuditTrim();
              }}
              disabled={!bridgeStatusAvailable || auditTrimState.phase === "submitting"}
            >
              {auditDryRun ? "Run retention dry run" : "Apply retention trim"}
            </button>
          </div>
          {auditExportState.phase === "submitting" ? (
            <p className="notice-text">{auditExportState.message}</p>
          ) : null}
          {auditExportState.phase === "error" ? (
            <p className="status-fail">{auditExportState.message}</p>
          ) : null}
          {auditExportState.phase === "success" ? (
            <p className="status-ok">{auditExportState.message}</p>
          ) : null}
          {auditExportState.detail ? (
            <p className="data-summary">{auditExportState.detail}</p>
          ) : null}
          {auditTrimState.phase === "submitting" ? (
            <p className="notice-text">{auditTrimState.message}</p>
          ) : null}
          {auditTrimState.phase === "error" ? (
            <p className="status-fail">{auditTrimState.message}</p>
          ) : null}
          {auditTrimState.phase === "success" ? (
            <p className="status-ok">{auditTrimState.message}</p>
          ) : null}
          {auditTrimState.detail ? <p className="data-summary">{auditTrimState.detail}</p> : null}
        </div>
        <div className="proof-note">
          <strong>Legacy proof seed</strong>
          <p>
            Import proof PDFs created before the approval ledger existed. Seeded rows stay pending
            until they are approved in this inbox.
          </p>
          <small>
            Required columns: {LEGACY_PROOF_SEED_REQUIRED_COLUMNS.join(", ")}
            {bridgeStatusAvailable && bridgeStatus.status
              ? ` / proof output dir: ${bridgeStatus.status.proofOutputDir}`
              : ""}
          </small>
          <div className="toolbar">
            <label className="button-secondary fake-button">
              Upload legacy proof CSV/XLSX
              <input
                type="file"
                accept=".csv,.xlsx,.xls,text/csv"
                onChange={handleLegacyProofSeedUpload}
              />
            </label>
            {legacyProofSeedSource ? (
              <button
                className="button-secondary"
                type="button"
                onClick={() => {
                  setLegacyProofSeedSource(null);
                  setLegacyProofSeedError(null);
                  setLegacyProofSeedState({
                    phase: "idle",
                    message: "",
                    result: null,
                  });
                }}
              >
                Clear legacy proof file
              </button>
            ) : null}
            <button
              className="button-secondary"
              type="button"
              onClick={() => {
                void runLegacyProofSeed("validating");
              }}
              disabled={
                !legacyProofSeedSource ||
                legacyProofSeedState.phase === "validating" ||
                legacyProofSeedState.phase === "seeding" ||
                !bridgeStatusAvailable
              }
            >
              Validate legacy rows
            </button>
            <button
              className="button-primary"
              type="button"
              onClick={() => {
                void runLegacyProofSeed("seeding");
              }}
              disabled={
                !legacyProofSeedSource ||
                legacyProofSeedState.phase === "validating" ||
                legacyProofSeedState.phase === "seeding" ||
                !bridgeStatusAvailable
              }
            >
              Seed pending proofs
            </button>
          </div>
          <p className="data-summary">
            {legacyProofSeedError ? legacyProofSeedError : legacyProofSeedSummary}
          </p>
          {legacyProofSeedState.phase === "validating" ||
          legacyProofSeedState.phase === "seeding" ? (
            <p className="notice-text">{legacyProofSeedState.message}</p>
          ) : null}
          {legacyProofSeedState.phase === "error" ? (
            <p className="status-fail">{legacyProofSeedState.message}</p>
          ) : null}
          {legacyProofSeedState.phase === "success" ? (
            <p className="status-ok">{legacyProofSeedState.message}</p>
          ) : null}
          {legacyProofSeedPreviewRows.length > 0 ? (
            <div className="data-grid">
              <div className="data-grid-header">
                <strong>Legacy proof preview</strong>
                <span>{legacyProofSeedRequest?.rows.length ?? 0} rows</span>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Proof job</th>
                    <th>Template</th>
                    <th>JAN</th>
                    <th>Qty</th>
                    <th>Requested by</th>
                  </tr>
                </thead>
                <tbody>
                  {legacyProofSeedPreviewRows.map((row) => (
                    <tr key={`${row.proofJobId}-${row.artifactPath}`}>
                      <td>{row.proofJobId || "-"}</td>
                      <td>{row.templateVersion || "-"}</td>
                      <td>{row.matchSubject.jan || "-"}</td>
                      <td>{row.matchSubject.qty || "-"}</td>
                      <td>{row.requestedBy.displayName || row.requestedBy.userId || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
          {legacyProofSeedState.result?.rows.length ? (
            <div className="data-grid">
              <div className="data-grid-header">
                <strong>Legacy proof validation</strong>
                <span>{legacyProofSeedState.result.rows.length} rows</span>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Row</th>
                    <th>Proof job</th>
                    <th>Status</th>
                    <th>Result</th>
                  </tr>
                </thead>
                <tbody>
                  {legacyProofSeedState.result.rows.map((row) => (
                    <tr key={`${row.rowIndex}-${row.proofJobId}`}>
                      <td>{row.rowIndex + 1}</td>
                      <td>{row.proofJobId || "-"}</td>
                      <td className={row.status === "ok" ? "status-ok" : "status-fail"}>
                        {row.status}
                      </td>
                      <td>{row.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
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
