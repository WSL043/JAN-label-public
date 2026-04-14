import type { LabelTemplateRef, PrintJobDraft, PrinterProfile } from "@label/job-schema";
import { useState } from "react";
import type { FormEvent } from "react";

const corePillars = [
  "Print correctness stays in Rust, not the UI.",
  "The admin screen only assembles a draft and operator intent.",
  "Printer differences stay behind printer profiles and adapters.",
];

const operatorNotes = [
  "JAN normalization remains in Rust. This form does not invent a checksum.",
  "Template and printer profile are selected explicitly to keep output routing deterministic.",
  "The preview payload stays aligned with @label/job-schema so GitHub review can reason about it.",
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

type FormState = {
  parentSku: string;
  sku: string;
  jan: string;
  qty: string;
  brand: string;
  templateId: string;
  printerProfileId: string;
  actor: string;
};

type DraftSession = {
  jobId: string;
  requestedAt: string;
};

type FormErrors = Partial<Record<keyof FormState, string>>;

function createSession(): DraftSession {
  const requestedAt = new Date().toISOString();
  return {
    requestedAt,
    jobId: createJobId(requestedAt),
  };
}

function createJobId(requestedAt: string): string {
  const digits = requestedAt.replace(/\D/g, "");
  return `JOB-${digits.slice(0, 8)}-${digits.slice(8, 14)}`;
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
  };
}

function validateDraft(
  form: FormState,
  session: DraftSession,
): { draft: PrintJobDraft | null; errors: FormErrors } {
  const errors: FormErrors = {};
  const parentSku = form.parentSku.trim();
  const sku = form.sku.trim();
  const jan = form.jan.trim();
  const qty = form.qty.trim();
  const brand = form.brand.trim();
  const actor = form.actor.trim();
  const template = templateOptions.find((option) => option.id === form.templateId);
  const printerProfile = printerProfiles.find((option) => option.id === form.printerProfileId);

  if (!parentSku) {
    errors.parentSku = "Parent SKU is required.";
  }
  if (!sku) {
    errors.sku = "SKU is required.";
  }
  if (!brand) {
    errors.brand = "Brand is required.";
  }
  if (!actor) {
    errors.actor = "Actor is required.";
  }
  if (!/^\d+$/.test(qty) || Number.parseInt(qty, 10) < 1) {
    errors.qty = "Quantity must be an integer greater than or equal to 1.";
  }
  if (jan.length === 12 && /^\d+$/.test(jan)) {
    errors.jan =
      "12-digit JAN completion stays in Rust. Enter the canonical 13-digit value for this preview.";
  } else if (!/^\d{13}$/.test(jan)) {
    errors.jan = "JAN must be 13 digits to create a schema-aligned draft.";
  }
  if (!template) {
    errors.templateId = "Template selection is required.";
  }
  if (!printerProfile) {
    errors.printerProfileId = "Printer profile selection is required.";
  }

  if (Object.keys(errors).length > 0 || !template || !printerProfile) {
    return { draft: null, errors };
  }

  return {
    errors,
    draft: {
      jobId: session.jobId,
      parentSku,
      sku,
      jan: {
        raw: jan,
        normalized: jan,
        source: "manual",
      },
      qty: Number.parseInt(qty, 10),
      brand,
      template: {
        id: template.id,
        version: template.version,
      },
      printerProfile: {
        id: printerProfile.id,
        adapter: printerProfile.adapter,
        paperSize: printerProfile.paperSize,
        dpi: printerProfile.dpi,
        scalePolicy: printerProfile.scalePolicy,
      },
      actor,
      requestedAt: session.requestedAt,
    },
  };
}

export function App() {
  const [session, setSession] = useState(createSession);
  const [form, setForm] = useState(createInitialFormState);
  const [showErrors, setShowErrors] = useState(false);
  const [draftSnapshot, setDraftSnapshot] = useState<PrintJobDraft | null>(null);
  const { draft, errors } = validateDraft(form, session);
  const template = templateOptions.find((option) => option.id === form.templateId);
  const printerProfile = printerProfiles.find((option) => option.id === form.printerProfileId);
  const liveDraftJson = draft ? JSON.stringify(draft, null, 2) : null;
  const snapshotJson = draftSnapshot ? JSON.stringify(draftSnapshot, null, 2) : null;
  const draftIsStale =
    Boolean(snapshotJson) && Boolean(liveDraftJson) && snapshotJson !== liveDraftJson;
  const previewJson = snapshotJson ?? liveDraftJson;
  const visibleErrors = showErrors ? errors : {};

  function updateField<Key extends keyof FormState>(key: Key, value: FormState[Key]) {
    setForm((current) => ({
      ...current,
      [key]: value,
    }));
  }

  function resetForm() {
    setSession(createSession());
    setForm(createInitialFormState());
    setShowErrors(false);
    setDraftSnapshot(null);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setShowErrors(true);

    if (!draft) {
      return;
    }

    setDraftSnapshot(draft);
  }

  return (
    <main className="page">
      <section className="hero hero-grid">
        <div>
          <p className="eyebrow">Issue #4</p>
          <h1>Job Draft Builder</h1>
          <p className="lede">
            Capture operator intent in the UI, then hand canonical validation and rendering back to
            the Rust print core.
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
            <strong>Status</strong>
            <span className={`status-pill ${draft ? "ready" : "blocked"}`}>
              {draft ? "Ready to snapshot" : "Needs input"}
            </span>
          </p>
        </div>
      </section>

      <section className="workspace">
        <form className="panel form-panel" noValidate onSubmit={handleSubmit}>
          <div className="section-heading">
            <h2>Create draft</h2>
            <p>Enter the minimum fields from T-004 and keep the preview schema-aligned.</p>
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
                The form accepts input, but it does not perform 12-digit checksum completion.
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
                value={form.templateId}
                onChange={(event) => updateField("templateId", event.target.value)}
              >
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
          </div>

          <div className="toolbar">
            <button className="button-primary" type="submit">
              Create draft snapshot
            </button>
            <button className="button-secondary" onClick={resetForm} type="button">
              Reset session
            </button>
          </div>
        </form>

        <aside className="panel preview-panel">
          <div className="section-heading">
            <h2>Draft preview</h2>
            <p>
              The preview stays honest to the current schema and never guesses a normalized JAN.
            </p>
          </div>

          <div className="preview-summary">
            <div>
              <span>Template route</span>
              <strong>{template ? `${template.label} / ${template.version}` : "Missing"}</strong>
              <small>{template ? template.size : "Select a template."}</small>
            </div>
            <div>
              <span>Output route</span>
              <strong>{printerProfile ? printerProfile.label : "Missing"}</strong>
              <small>
                {printerProfile
                  ? `${printerProfile.adapter} / ${printerProfile.paperSize} / ${printerProfile.dpi} dpi`
                  : "Select a printer profile."}
              </small>
            </div>
          </div>

          {previewJson ? (
            <>
              <pre className="json-block">{previewJson}</pre>
              {draftIsStale ? (
                <p className="notice-text">
                  Live input changed after the last snapshot. Click create again to refresh the
                  saved draft.
                </p>
              ) : null}
            </>
          ) : (
            <div className="empty-state">
              <strong>No draft yet</strong>
              <p>Fill the required fields, then create a snapshot to inspect the payload.</p>
            </div>
          )}
        </aside>
      </section>

      <section className="grid">
        <article className="card">
          <span>Guardrails</span>
          <strong>Print core first</strong>
          <ul className="card-list">
            {corePillars.map((pillar) => (
              <li key={pillar}>{pillar}</li>
            ))}
          </ul>
        </article>
        <article className="card">
          <span>Operator notes</span>
          <strong>Why the form is strict</strong>
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
