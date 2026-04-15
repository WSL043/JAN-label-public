import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const fixtureRoot = join(process.cwd(), "packages", "fixtures");
const schemaPath = join(process.cwd(), "packages", "job-schema", "schema", "print-job.schema.json");
const dispatchRequestSchemaPath = join(
  process.cwd(),
  "packages",
  "job-schema",
  "schema",
  "print-dispatch-request.schema.json",
);
const dispatchResultSchemaPath = join(
  process.cwd(),
  "packages",
  "job-schema",
  "schema",
  "print-dispatch-result.schema.json",
);
const templateRoot = join(process.cwd(), "packages", "templates");
const templateSchemaRoot = join(templateRoot, "schema");
const templateManifestPath = join(templateRoot, "template-manifest.json");
const templateSpecSchemaPath = join(templateSchemaRoot, "template-spec-v1.schema.json");
const templateManifestSchemaPath = join(templateSchemaRoot, "template-manifest-v1.schema.json");
const validJobPath = join(fixtureRoot, "label-jobs", "valid-minimal.json");
const spoolerJobPath = join(fixtureRoot, "label-jobs", "valid-windows-spooler.json");
const invalidJobPath = join(fixtureRoot, "label-jobs", "invalid-missing-brand.json");
const goldenPath = join(fixtureRoot, "golden", "basic-label.svg");
const goldenPdfPath = join(fixtureRoot, "golden", "basic-label.pdf");
const csvPath = join(fixtureRoot, "importer", "catalog-valid.csv");
const invalidCsvPath = join(fixtureRoot, "importer", "catalog-invalid-values.csv");
const businessCsvPath = join(fixtureRoot, "importer", "catalog-valid-business-headers.csv");
const ambiguousHeaderCsvPath = join(
  fixtureRoot,
  "importer",
  "catalog-invalid-ambiguous-headers.csv",
);

const requiredTopLevel = [
  "jobId",
  "parentSku",
  "sku",
  "jan",
  "qty",
  "brand",
  "template",
  "printerProfile",
  "actor",
  "requestedAt",
];

function readJson(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function assertString(value, path, field) {
  assert(typeof value === "string", `${path}: ${field} must be a string`);
}

function assertNonEmptyString(value, path, field) {
  assertString(value, path, field);
  assert(value.trim().length > 0, `${path}: ${field} must not be empty`);
}

function checksum(body) {
  const total = [...body]
    .reverse()
    .reduce((sum, digit, index) => sum + Number.parseInt(digit, 10) * (index % 2 === 0 ? 3 : 1), 0);
  return String((10 - (total % 10)) % 10);
}

function validateRequiredFields(job, path, errors) {
  for (const field of requiredTopLevel) {
    if (!(field in job)) {
      errors.push(`${path}: missing required field "${field}"`);
    }
  }
}

function validateJan(job, path, errors) {
  const jan = job.jan;
  if (typeof jan !== "object" || jan === null || Array.isArray(jan)) {
    errors.push(`${path}: jan must be an object`);
    return;
  }

  if (!/^\d{12,13}$/.test(jan.raw)) {
    errors.push(`${path}: jan.raw must be 12 or 13 digits`);
  }
  if (!/^\d{13}$/.test(jan.normalized)) {
    errors.push(`${path}: jan.normalized must be 13 digits`);
  }
  if (!["manual", "import"].includes(jan.source)) {
    errors.push(`${path}: jan.source must be manual or import`);
  }
  if (/^\d{13}$/.test(jan.raw) && jan.raw !== jan.normalized) {
    errors.push(`${path}: jan.normalized must match jan.raw when raw already has 13 digits`);
  }
  if (/^\d{12}$/.test(jan.raw) && jan.normalized !== `${jan.raw}${checksum(jan.raw)}`) {
    errors.push(`${path}: jan.normalized must equal jan.raw plus the computed checksum`);
  }
  if (/^\d{13}$/.test(jan.normalized)) {
    const expected = checksum(jan.normalized.slice(0, 12));
    const actual = jan.normalized.slice(-1);
    if (expected !== actual) {
      errors.push(`${path}: jan.normalized checksum is invalid`);
    }
  }
}

function validateTemplate(job, path, errors) {
  const template = job.template;
  if (typeof template !== "object" || template === null || Array.isArray(template)) {
    errors.push(`${path}: template must be an object`);
    return;
  }

  try {
    assertNonEmptyString(template.id, path, "template.id");
    assertNonEmptyString(template.version, path, "template.version");
  } catch (error) {
    errors.push(error.message);
  }
}

function validatePrinterProfile(job, path, errors) {
  const profile = job.printerProfile;
  if (typeof profile !== "object" || profile === null || Array.isArray(profile)) {
    errors.push(`${path}: printerProfile must be an object`);
    return;
  }

  try {
    assertNonEmptyString(profile.id, path, "printerProfile.id");
    assert(
      ["pdf", "windows-spooler", "zpl", "tspl", "qz"].includes(profile.adapter),
      `${path}: printerProfile.adapter is invalid`,
    );
    assertNonEmptyString(profile.paperSize, path, "printerProfile.paperSize");
    assert(
      Number.isInteger(profile.dpi) && profile.dpi >= 150,
      `${path}: printerProfile.dpi must be an integer >= 150`,
    );
    assert(
      profile.scalePolicy === "fixed-100",
      `${path}: printerProfile.scalePolicy must be fixed-100`,
    );
  } catch (error) {
    errors.push(error.message);
  }
}

function validateJob(job, path) {
  const errors = [];
  assert(
    typeof job === "object" && job !== null && !Array.isArray(job),
    `${path}: fixture root must be an object`,
  );
  validateRequiredFields(job, path, errors);

  if (errors.length > 0) {
    return errors;
  }

  try {
    assertNonEmptyString(job.jobId, path, "jobId");
    assertNonEmptyString(job.parentSku, path, "parentSku");
    assertNonEmptyString(job.sku, path, "sku");
    assert(Number.isInteger(job.qty) && job.qty >= 1, `${path}: qty must be an integer >= 1`);
    assertNonEmptyString(job.brand, path, "brand");
    assertNonEmptyString(job.actor, path, "actor");
    assertString(job.requestedAt, path, "requestedAt");
    assert(
      !Number.isNaN(Date.parse(job.requestedAt)),
      `${path}: requestedAt must be a valid date-time`,
    );
  } catch (error) {
    errors.push(error.message);
  }

  validateJan(job, path, errors);
  validateTemplate(job, path, errors);
  validatePrinterProfile(job, path, errors);

  return errors;
}

function validatePdfProof(path) {
  const pdf = readFileSync(path, "utf8");
  assert(pdf.startsWith("%PDF-1.4"), `${path}: expected pdf header missing`);
  assert(pdf.includes("/Type /Page"), `${path}: page object missing`);
  assert(pdf.includes("/Type /Font"), `${path}: font object missing`);
  assert(pdf.includes("(job:JOB-20260414-0001)"), `${path}: expected golden job id missing`);
  assert(pdf.includes("(brand:Acme)"), `${path}: expected brand text missing`);
  assert(pdf.includes("(jan:4006381333931)"), `${path}: expected JAN text missing`);

  const mediaBoxMatch = pdf.match(/\/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]/);
  assert(mediaBoxMatch, `${path}: MediaBox not found`);
  const widthPt = Number.parseFloat(mediaBoxMatch[1]);
  const heightPt = Number.parseFloat(mediaBoxMatch[2]);
  assert(
    Math.abs(widthPt - 141.732) < 0.001 && Math.abs(heightPt - 85.039) < 0.001,
    `${path}: MediaBox must stay aligned with 50mm x 30mm proof size`,
  );
}

function validateTemplateManifest(manifestPath) {
  const manifest = readJson(manifestPath);
  assert(
    manifest.schema_version === "template-manifest-v1",
    `${manifestPath}: schema_version must be template-manifest-v1`,
  );
  assertNonEmptyString(manifest.default_template_version, manifestPath, "default_template_version");
  assert(Array.isArray(manifest.templates), `${manifestPath}: templates must be an array`);
  assert(manifest.templates.length >= 1, `${manifestPath}: templates must not be empty`);

  const seenVersions = new Set();
  for (const entry of manifest.templates) {
    assertNonEmptyString(entry.version, manifestPath, "templates[].version");
    assertNonEmptyString(entry.path, manifestPath, "templates[].path");
    assertNonEmptyString(entry.label_name, manifestPath, "templates[].label_name");
    assert(
      typeof entry.enabled === "boolean",
      `${manifestPath}: templates[].enabled must be boolean`,
    );
    assert(
      !seenVersions.has(entry.version),
      `${manifestPath}: duplicate template version '${entry.version}'`,
    );
    seenVersions.add(entry.version);

    const templatePath = join(templateRoot, entry.path);
    assert(readFileSync(templatePath, "utf8"), `${templatePath}: template file must be readable`);
    validateTemplateSpec(templatePath, entry.version);
  }

  assert(
    seenVersions.has(manifest.default_template_version),
    `${manifestPath}: default_template_version must exist in templates[]`,
  );
}

function validateTemplateSpec(path, expectedVersion) {
  const template = readJson(path);
  assert(
    template.schema_version === "template-spec-v1",
    `${path}: schema_version must be template-spec-v1`,
  );
  assert(
    template.template_version === expectedVersion,
    `${path}: template_version must match manifest version '${expectedVersion}'`,
  );
  assertNonEmptyString(template.label_name, path, "label_name");
  assert(
    typeof template.page === "object" && template.page !== null,
    `${path}: page must be an object`,
  );
  assert(
    typeof template.page.width_mm === "number" && template.page.width_mm > 0,
    `${path}: page.width_mm must be a positive number`,
  );
  assert(
    typeof template.page.height_mm === "number" && template.page.height_mm > 0,
    `${path}: page.height_mm must be a positive number`,
  );
  assertNonEmptyString(template.page.background_fill, path, "page.background_fill");
  assert(
    typeof template.border === "object" && template.border !== null,
    `${path}: border must be an object`,
  );
  assert(typeof template.border.visible === "boolean", `${path}: border.visible must be boolean`);
  assertNonEmptyString(template.border.color, path, "border.color");
  assert(
    typeof template.border.width_mm === "number" && template.border.width_mm >= 0,
    `${path}: border.width_mm must be a non-negative number`,
  );
  assert(Array.isArray(template.fields), `${path}: fields must be an array`);
  assert(template.fields.length >= 1, `${path}: fields must not be empty`);

  const seenNames = new Set();
  for (const field of template.fields) {
    assertNonEmptyString(field.name, path, "fields[].name");
    assert(!seenNames.has(field.name), `${path}: duplicate field name '${field.name}'`);
    seenNames.add(field.name);
    assert(
      typeof field.x_mm === "number" && field.x_mm >= 0 && field.x_mm <= template.page.width_mm,
      `${path}: fields[].x_mm must stay within page width`,
    );
    assert(
      typeof field.y_mm === "number" && field.y_mm >= 0 && field.y_mm <= template.page.height_mm,
      `${path}: fields[].y_mm must stay within page height`,
    );
    assert(
      typeof field.font_size_mm === "number" && field.font_size_mm > 0,
      `${path}: fields[].font_size_mm must be positive`,
    );
    assertNonEmptyString(field.template, path, "fields[].template");
  }
}

const schema = readJson(schemaPath);
assert(schema.title === "PrintJobDraft", `${schemaPath}: expected title PrintJobDraft`);
assert(schema.type === "object", `${schemaPath}: root type must stay object`);
assert(
  schema.additionalProperties === false,
  `${schemaPath}: additionalProperties must stay false`,
);
assert(
  JSON.stringify(schema.required) === JSON.stringify(requiredTopLevel),
  `${schemaPath}: required top-level properties drifted from fixture expectations`,
);

const dispatchRequestSchema = readJson(dispatchRequestSchemaPath);
assert(
  dispatchRequestSchema.title === "DispatchRequest",
  `${dispatchRequestSchemaPath}: expected title DispatchRequest`,
);
assert(
  dispatchRequestSchema.type === "object",
  `${dispatchRequestSchemaPath}: root type must stay object`,
);
assert(
  dispatchRequestSchema.additionalProperties === false,
  `${dispatchRequestSchemaPath}: additionalProperties must stay false`,
);
assert(
  Array.isArray(dispatchRequestSchema.oneOf) && dispatchRequestSchema.oneOf.length === 2,
  `${dispatchRequestSchemaPath}: expected templateVersion/template fallback oneOf`,
);

const dispatchResultSchema = readJson(dispatchResultSchemaPath);
assert(
  dispatchResultSchema.title === "PrintDispatchResult",
  `${dispatchResultSchemaPath}: expected title PrintDispatchResult`,
);
assert(
  dispatchResultSchema.type === "object",
  `${dispatchResultSchemaPath}: root type must stay object`,
);

const templateSpecSchema = readJson(templateSpecSchemaPath);
assert(
  templateSpecSchema.title === "Template Specification",
  `${templateSpecSchemaPath}: expected title Template Specification`,
);
assert(
  templateSpecSchema.type === "object",
  `${templateSpecSchemaPath}: root type must stay object`,
);
assert(
  templateSpecSchema.properties?.schema_version?.pattern === "^template-spec-v[0-9]+$",
  `${templateSpecSchemaPath}: schema_version pattern must track template-spec versions`,
);

const templateManifestSchema = readJson(templateManifestSchemaPath);
assert(
  templateManifestSchema.title === "Template Manifest",
  `${templateManifestSchemaPath}: expected title Template Manifest`,
);
assert(
  templateManifestSchema.type === "object",
  `${templateManifestSchemaPath}: root type must stay object`,
);
assert(
  templateManifestSchema.properties?.schema_version?.pattern === "^template-manifest-v[0-9]+$",
  `${templateManifestSchemaPath}: schema_version pattern must track template-manifest versions`,
);

validateTemplateManifest(templateManifestPath);

const validJob = readJson(validJobPath);
assert(
  validateJob(validJob, validJobPath).length === 0,
  validateJob(validJob, validJobPath).join("\n"),
);
assert(
  validJob.printerProfile.id === "pdf-a4-proof",
  `${validJobPath}: expected proof printer profile id to stay pdf-a4-proof`,
);
assert(
  validJob.printerProfile.adapter === "pdf",
  `${validJobPath}: expected proof printer profile adapter to stay pdf`,
);
assert(
  validJob.printerProfile.scalePolicy === "fixed-100",
  `${validJobPath}: expected proof printer profile to keep fixed-100 scaling`,
);

const spoolerJob = readJson(spoolerJobPath);
assert(
  validateJob(spoolerJob, spoolerJobPath).length === 0,
  validateJob(spoolerJob, spoolerJobPath).join("\n"),
);
assert(
  spoolerJob.printerProfile.adapter === "windows-spooler",
  `${spoolerJobPath}: expected spooler printer profile adapter to stay windows-spooler`,
);
assert(
  spoolerJob.printerProfile.id === "winspool-zd421-203dpi",
  `${spoolerJobPath}: expected spooler printer profile id to stay winspool-zd421-203dpi`,
);
assert(
  spoolerJob.printerProfile.scalePolicy === "fixed-100",
  `${spoolerJobPath}: expected spooler printer profile to keep fixed-100 scaling`,
);

const invalidJob = readJson(invalidJobPath);
assert(!("brand" in invalidJob), `${invalidJobPath}: invalid fixture should omit brand`);
const invalidErrors = validateJob(invalidJob, invalidJobPath);
assert(
  invalidErrors.some((message) => message.includes('missing required field "brand"')),
  `${invalidJobPath}: invalid fixture should fail because brand is missing`,
);

const svg = readFileSync(goldenPath, "utf8");
assert(svg.includes("JOB-20260414-0001"), `${goldenPath}: expected golden job id missing`);

validatePdfProof(goldenPdfPath);

const csv = readFileSync(csvPath, "utf8").trim().split(/\r?\n/);
assert(csv.length >= 2, `${csvPath}: expected header and at least one data row`);
assert(
  csv[0] === "parent_sku,sku,jan,qty,brand,template,printer_profile,enabled",
  `${csvPath}: header row does not match canonical column order`,
);

const invalidCsv = readFileSync(invalidCsvPath, "utf8").trim().split(/\r?\n/);
assert(invalidCsv.length >= 2, `${invalidCsvPath}: expected header and at least one data row`);
assert(
  invalidCsv[0] === "parent_sku,sku,jan,qty,brand,template,printer_profile,enabled",
  `${invalidCsvPath}: header row does not match canonical column order`,
);

const businessCsv = readFileSync(businessCsvPath, "utf8").trim().split(/\r?\n/);
assert(businessCsv.length >= 2, `${businessCsvPath}: expected header and at least one data row`);
assert(
  businessCsv[0] === "親SKU,商品コード,JANコード,入数,ブランド,ラベル名,プリンター,有効,商品名",
  `${businessCsvPath}: header row does not match the business alias fixture`,
);

const ambiguousHeaderCsv = readFileSync(ambiguousHeaderCsvPath, "utf8").trim().split(/\r?\n/);
assert(
  ambiguousHeaderCsv.length >= 2,
  `${ambiguousHeaderCsvPath}: expected header and at least one data row`,
);
assert(
  ambiguousHeaderCsv[0] ===
    "親SKU,商品コード,品番,JANコード,入数,ブランド,テンプレート名,プリンター名,有効",
  `${ambiguousHeaderCsvPath}: header row does not match the ambiguous-header fixture`,
);

const fixtureDirs = readdirSync(fixtureRoot, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name);
assert(
  fixtureDirs.includes("golden") &&
    fixtureDirs.includes("importer") &&
    fixtureDirs.includes("label-jobs"),
  `${fixtureRoot}: expected fixture directories are missing`,
);

const goldenFiles = readdirSync(join(fixtureRoot, "golden"));
assert(
  goldenFiles.includes("basic-label.svg") && goldenFiles.includes("basic-label.pdf"),
  `${join(fixtureRoot, "golden")}: expected svg/pdf golden fixtures are missing`,
);

console.log("Fixture validation passed.");
