import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const fixtureRoot = join(process.cwd(), "packages", "fixtures");
const validJobPath = join(fixtureRoot, "label-jobs", "valid-minimal.json");
const spoolerJobPath = join(fixtureRoot, "label-jobs", "valid-windows-spooler.json");
const invalidJobPath = join(fixtureRoot, "label-jobs", "invalid-missing-brand.json");
const goldenPath = join(fixtureRoot, "golden", "basic-label.svg");
const goldenPdfPath = join(fixtureRoot, "golden", "basic-label.pdf");
const csvPath = join(fixtureRoot, "importer", "catalog-valid.csv");
const invalidCsvPath = join(fixtureRoot, "importer", "catalog-invalid-values.csv");

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

function validateRequiredFields(job, path) {
  for (const field of requiredTopLevel) {
    assert(field in job, `${path}: missing required field "${field}"`);
  }
}

function validateJan(job, path) {
  assert(/^\d{12,13}$/.test(job.jan.raw), `${path}: jan.raw must be 12 or 13 digits`);
  assert(/^\d{13}$/.test(job.jan.normalized), `${path}: jan.normalized must be 13 digits`);
}

const validJob = readJson(validJobPath);
validateRequiredFields(validJob, validJobPath);
validateJan(validJob, validJobPath);
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
validateRequiredFields(spoolerJob, spoolerJobPath);
validateJan(spoolerJob, spoolerJobPath);
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

const svg = readFileSync(goldenPath, "utf8");
assert(svg.includes("JOB-20260414-0001"), `${goldenPath}: expected golden job id missing`);

const pdf = readFileSync(goldenPdfPath, "utf8");
assert(pdf.startsWith("%PDF-1.4"), `${goldenPdfPath}: expected pdf header missing`);
assert(pdf.includes("(job:JOB-20260414-0001)"), `${goldenPdfPath}: expected golden job id missing`);

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
