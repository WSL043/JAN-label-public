import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

function parseArgs(argv) {
  const args = new Map();
  for (let index = 0; index < argv.length; index += 1) {
    const current = argv[index];
    if (!current.startsWith("--")) continue;
    const key = current.slice(2);
    const next = argv[index + 1];
    if (!next || next.startsWith("--")) {
      args.set(key, "true");
      continue;
    }
    args.set(key, next);
    index += 1;
  }
  return args;
}

function normalizeVersion(input) {
  if (!input) {
    throw new Error("release readiness generation requires --version <vX.Y.Z>.");
  }
  return input.startsWith("v") ? input : `v${input}`;
}

function runCommand(command, { retryOnWindowsOsError5 = false } = {}) {
  const attempt = () =>
    spawnSync(command, {
      cwd: process.cwd(),
      encoding: "utf8",
      shell: true,
      maxBuffer: 16 * 1024 * 1024,
    });

  let result = attempt();
  const combinedOutput = `${result.stdout ?? ""}\n${result.stderr ?? ""}`.trim();
  if (
    retryOnWindowsOsError5 &&
    result.status !== 0 &&
    process.platform === "win32" &&
    /os error 5/i.test(combinedOutput)
  ) {
    result = attempt();
    result.retryAttempted = true;
  }

  return {
    command,
    ok: result.status === 0,
    code: result.status ?? 1,
    output: `${result.stdout ?? ""}\n${result.stderr ?? ""}`.trim(),
    retryAttempted: Boolean(result.retryAttempted),
  };
}

function sectionNowRows(markdown) {
  const match = markdown.match(/## Now\r?\n([\s\S]*?)(?=\r?\n## |$)/);
  if (!match) {
    return [];
  }
  const rowLines = match[1]
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.startsWith("|"));
  return rowLines.slice(2).filter((line) => !/^\|\s*None\s*\|/i.test(line));
}

const args = parseArgs(process.argv.slice(2));
const version = normalizeVersion(args.get("version"));
const readinessOutputDir = path.join(process.cwd(), "artifacts");
fs.mkdirSync(readinessOutputDir, { recursive: true });

const validations = [
  "pnpm fixture:validate",
  "pnpm format:check",
  "pnpm lint",
  "pnpm typecheck",
  "pnpm --filter @label/admin-web build",
  "cargo fmt --all --check",
  "cargo clippy --workspace --all-targets -- -D warnings",
  "cargo test --workspace",
  "cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml",
].map((command) =>
  runCommand(command, {
    retryOnWindowsOsError5: command === "cargo test --workspace",
  }),
);

const desktopBuild =
  process.platform === "win32"
    ? runCommand("pnpm --filter @label/desktop-shell build --ci --no-sign")
    : {
        command: "pnpm --filter @label/desktop-shell build --ci --no-sign",
        ok: false,
        code: null,
        output: "Windows desktop build check is only available on Windows hosts.",
        skipped: true,
      };

const notesPath = path.join(process.cwd(), "docs", "release", `${version}.md`);
const releaseNotesDraftExists = fs.existsSync(notesPath);
const activeTodo = fs.readFileSync(path.join(process.cwd(), "docs", "todo", "active.md"), "utf8");
const nowRows = sectionNowRows(activeTodo);
const nowTasksEmpty = nowRows.length === 0;
const validationPassed = validations.every((item) => item.ok);
const desktopBuildPassed = desktopBuild.ok;
const overallStatus =
  validationPassed && releaseNotesDraftExists && nowTasksEmpty && desktopBuildPassed
    ? "pass"
    : process.platform !== "win32" && !desktopBuildPassed
      ? "blocked"
      : "fail";

const report = {
  version,
  generatedAt: new Date().toISOString(),
  nowTasksEmpty,
  nowTaskCount: nowRows.length,
  validationPassed,
  releaseNotesDraft: {
    exists: releaseNotesDraftExists,
    path: notesPath,
  },
  validations: validations.map((item) => ({
    command: item.command,
    ok: item.ok,
    retryAttempted: item.retryAttempted,
    output: item.output,
  })),
  windowsDesktopBuild: {
    command: desktopBuild.command,
    ok: Boolean(desktopBuild.ok),
    skipped: Boolean(desktopBuild.skipped),
    output: desktopBuild.output,
  },
  overallStatus,
};

const markdown = `# Release Readiness ${version}

- Generated: ${report.generatedAt}
- Overall status: **${report.overallStatus}**
- Now tasks empty: ${report.nowTasksEmpty ? "yes" : `no (${report.nowTaskCount})`}
- Release notes draft: ${releaseNotesDraftExists ? notesPath : "missing"}
- Windows desktop build: ${desktopBuildPassed ? "pass" : desktopBuild.skipped ? "blocked" : "fail"}

## Validation Commands

${report.validations
  .map(
    (item) =>
      `- ${item.ok ? "[pass]" : "[fail]"} \`${item.command}\`${item.retryAttempted ? " (retried once)" : ""}`,
  )
  .join("\n")}

## Desktop Build

- Command: \`${desktopBuild.command}\`
- Result: ${desktopBuildPassed ? "pass" : desktopBuild.skipped ? "blocked" : "fail"}

## Remaining Now Tasks

${nowRows.length > 0 ? nowRows.join("\n") : "- None"}
`;

const jsonPath = path.join(readinessOutputDir, "release-readiness.json");
const markdownPath = path.join(readinessOutputDir, "release-readiness.md");
fs.writeFileSync(jsonPath, `${JSON.stringify(report, null, 2)}\n`);
fs.writeFileSync(markdownPath, `${markdown}\n`);

process.stdout.write(`${jsonPath}\n${markdownPath}\n`);
if (overallStatus !== "pass") {
  process.exitCode = 1;
}
