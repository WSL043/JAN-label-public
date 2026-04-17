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

function detectToolchainBlocker(output) {
  if (process.platform !== "win32") {
    return null;
  }

  const blockerPatterns = [
    {
      pattern:
        /makensis\.exe[\s\S]*(os error 2|not recognized|指定されたファイルが見つかりません)/i,
      reason: "NSIS tooling is missing on the local Windows host.",
    },
    {
      pattern:
        /ISCC\.exe[\s\S]*(CommandNotFoundException|not recognized|cannot find|could not|指定されたファイルが見つかりません)/i,
      reason: "Inno Setup is missing on the local Windows host.",
    },
    {
      pattern: /link\.exe[\s\S]*(not found|cannot find|could not|is not recognized)/i,
      reason: "MSVC linker tools are missing on the local Windows host.",
    },
    {
      pattern:
        /(The term 'dotnet' is not recognized|No executable found matching command \"dotnet\"|'dotnet' is not recognized as an internal or external command)/i,
      reason: ".NET SDK is missing on the local Windows host.",
    },
  ];

  for (const entry of blockerPatterns) {
    if (entry.pattern.test(output)) {
      return entry.reason;
    }
  }

  return null;
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
  let output = `${result.stdout ?? ""}\n${result.stderr ?? ""}`.trim();
  if (
    retryOnWindowsOsError5 &&
    result.status !== 0 &&
    process.platform === "win32" &&
    /os error 5/i.test(output)
  ) {
    result = attempt();
    result.retryAttempted = true;
    output = `${result.stdout ?? ""}\n${result.stderr ?? ""}`.trim();
  }

  return {
    command,
    ok: result.status === 0,
    code: result.status ?? 1,
    output,
    retryAttempted: Boolean(result.retryAttempted),
    blocked: result.status !== 0 ? detectToolchainBlocker(output) : null,
  };
}

function runCommandWithArtifact(command, artifactPath, options) {
  const result = runCommand(command, options);
  if (result.ok && !fs.existsSync(artifactPath)) {
    return {
      ...result,
      ok: false,
      output: `${result.output}\nExpected artifact was not created: ${artifactPath}`.trim(),
    };
  }

  return {
    ...result,
    artifactPath,
  };
}

function requireArtifact(result, artifactPath, missingMessage) {
  if (result.ok && !fs.existsSync(artifactPath)) {
    return {
      ...result,
      ok: false,
      output: `${result.output}\n${missingMessage}: ${artifactPath}`.trim(),
    };
  }

  return {
    ...result,
    artifactPath,
  };
}

function skippedCheck(command, output) {
  return {
    command,
    ok: false,
    code: null,
    output,
    retryAttempted: false,
    blocked: null,
    skipped: true,
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

function releaseNotesStatus(notesPath, version) {
  if (!fs.existsSync(notesPath)) {
    return {
      exists: false,
      ok: false,
      path: notesPath,
      output: "Release notes draft is missing.",
    };
  }

  const content = fs.readFileSync(notesPath, "utf8");
  const checks = [
    content.startsWith(`# ${version}`),
    /^- Generated: /m.test(content),
    /^- Previous tag: /m.test(content),
    /^- Commit range: /m.test(content),
  ];

  return {
    exists: true,
    ok: checks.every(Boolean),
    path: notesPath,
    output: checks.every(Boolean)
      ? "Release notes draft has the expected generated structure."
      : "Release notes draft exists but is missing generated metadata.",
  };
}

function versionCheck(command, ok, output) {
  return {
    command,
    ok,
    code: ok ? 0 : 1,
    output,
    retryAttempted: false,
    blocked: null,
  };
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function desktopShellVersionAlignment(expectedVersion) {
  const normalizedExpectedVersion = expectedVersion.startsWith("v")
    ? expectedVersion.slice(1)
    : expectedVersion;
  const packageJsonPath = path.join(process.cwd(), "apps", "desktop-shell", "package.json");
  const tauriConfigPath = path.join(
    process.cwd(),
    "apps",
    "desktop-shell",
    "src-tauri",
    "tauri.conf.json",
  );
  const cargoTomlPath = path.join(
    process.cwd(),
    "apps",
    "desktop-shell",
    "src-tauri",
    "Cargo.toml",
  );

  const packageVersion = readJson(packageJsonPath).version;
  const tauriVersion = readJson(tauriConfigPath).version;
  const cargoToml = fs.readFileSync(cargoTomlPath, "utf8");
  const cargoVersionMatch = cargoToml.match(/^version = "([^"]+)"$/m);
  const cargoVersion = cargoVersionMatch?.[1] ?? "<missing>";
  const ok =
    packageVersion === normalizedExpectedVersion &&
    tauriVersion === normalizedExpectedVersion &&
    cargoVersion === normalizedExpectedVersion;

  return versionCheck(
    `desktop-shell version metadata matches ${expectedVersion}`,
    ok,
    [
      `package.json: ${packageVersion}`,
      `tauri.conf.json: ${tauriVersion}`,
      `Cargo.toml: ${cargoVersion}`,
    ].join("\n"),
  );
}

function statusLabel(check) {
  if (check.ok) {
    return "pass";
  }
  if (check.skipped || check.blocked) {
    return "blocked";
  }
  return "fail";
}

function powershellSingleQuote(input) {
  return input.replace(/'/g, "''");
}

const args = parseArgs(process.argv.slice(2));
const version = normalizeVersion(args.get("version"));
const readinessOutputDir = path.join(process.cwd(), "artifacts");
fs.mkdirSync(readinessOutputDir, { recursive: true });
const desktopShellVersionStatus = desktopShellVersionAlignment(version);

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
    retryOnWindowsOsError5: command.startsWith("cargo test"),
  }),
);

const nativeShellTests =
  process.platform === "win32"
    ? runCommand(
        "dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release",
      )
    : skippedCheck(
        "dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release",
        "Windows native shell tests are only available on Windows hosts.",
      );

const desktopBuild =
  process.platform === "win32"
    ? runCommand("pnpm --filter @label/desktop-shell build --ci --no-sign")
    : skippedCheck(
        "pnpm --filter @label/desktop-shell build --ci --no-sign",
        "Windows desktop build check is only available on Windows hosts.",
      );

const nativeShellBuildCommand =
  "dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release";
const nativeShellPublishCommand =
  "dotnet publish apps/windows-shell/JanLabel.WindowsShell.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true";
const nativeShellPublishOutputDir = path.join(
  process.cwd(),
  "apps",
  "windows-shell",
  "bin",
  "Release",
  "net8.0-windows",
  "win-x64",
  "publish",
);
const nativeShellCompanionArtifactPath = path.join(
  nativeShellPublishOutputDir,
  "desktop-shell.exe",
);
const nativeShellInstallerOutputDir = path.join(process.cwd(), "apps", "windows-shell", "dist");
const nativeShellInstallerBaseFilename = `JAN-Label_windows-native-shell_${version}`;
const nativeShellInstallerArtifactPath = path.join(
  nativeShellInstallerOutputDir,
  `${nativeShellInstallerBaseFilename}.exe`,
);
const nativeShellInstallerCommand = `powershell -NoProfile -Command "New-Item -ItemType Directory -Force -Path '${powershellSingleQuote(nativeShellInstallerOutputDir)}' | Out-Null; & 'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe' '/DMyAppVersion=${version}' '/DMyOutputDir=${powershellSingleQuote(nativeShellInstallerOutputDir)}' '/DMyOutputBaseFilename=${nativeShellInstallerBaseFilename}' 'apps/windows-shell/installer/JanLabel.WindowsShell.iss'"`;

const nativeShellBuild =
  process.platform === "win32"
    ? runCommand(nativeShellBuildCommand)
    : skippedCheck(
        nativeShellBuildCommand,
        "Windows native shell build check is only available on Windows hosts.",
      );

const nativeShellPublish =
  process.platform === "win32"
    ? requireArtifact(
        runCommand(nativeShellPublishCommand),
        nativeShellCompanionArtifactPath,
        "Expected companion binary was not staged into the native-shell publish output",
      )
    : skippedCheck(
        nativeShellPublishCommand,
        "Windows native shell publish check is only available on Windows hosts.",
      );

const nativeShellInstaller =
  process.platform !== "win32"
    ? skippedCheck(
        nativeShellInstallerCommand,
        "Windows native shell installer check is only available on Windows hosts.",
      )
    : !nativeShellPublish.ok
      ? skippedCheck(
          nativeShellInstallerCommand,
          "Native shell installer check was skipped because self-contained publish did not pass.",
        )
      : runCommandWithArtifact(nativeShellInstallerCommand, nativeShellInstallerArtifactPath);

const notesPath = path.join(process.cwd(), "docs", "release", `${version}.md`);
const notes = releaseNotesStatus(notesPath, version);
const activeTodo = fs.readFileSync(path.join(process.cwd(), "docs", "todo", "active.md"), "utf8");
const nowRows = sectionNowRows(activeTodo);
const nowTasksEmpty = nowRows.length === 0;

const requiredChecks = [
  desktopShellVersionStatus,
  ...validations,
  nativeShellTests,
  desktopBuild,
  nativeShellBuild,
  nativeShellPublish,
  nativeShellInstaller,
];

const blockingChecks = requiredChecks.filter((item) => !item.ok && (item.blocked || item.skipped));
const failingChecks = requiredChecks.filter((item) => !item.ok && !item.blocked && !item.skipped);
const overallStatus =
  notes.ok && nowTasksEmpty && failingChecks.length === 0 && blockingChecks.length === 0
    ? "pass"
    : failingChecks.length === 0 && blockingChecks.length > 0 && notes.ok && nowTasksEmpty
      ? "blocked"
      : "fail";

const report = {
  version,
  generatedAt: new Date().toISOString(),
  nowTasksEmpty,
  nowTaskCount: nowRows.length,
  releaseNotesDraft: notes,
  desktopShellVersionAlignment: {
    command: desktopShellVersionStatus.command,
    ok: desktopShellVersionStatus.ok,
    output: desktopShellVersionStatus.output,
  },
  validations: validations.map((item) => ({
    command: item.command,
    ok: item.ok,
    blocked: item.blocked,
    retryAttempted: item.retryAttempted,
    output: item.output,
  })),
  windowsNativeShellTests: {
    command: nativeShellTests.command,
    ok: Boolean(nativeShellTests.ok),
    blocked: nativeShellTests.blocked,
    skipped: Boolean(nativeShellTests.skipped),
    output: nativeShellTests.output,
  },
  windowsDesktopBuild: {
    command: desktopBuild.command,
    ok: Boolean(desktopBuild.ok),
    blocked: desktopBuild.blocked,
    skipped: Boolean(desktopBuild.skipped),
    output: desktopBuild.output,
  },
  windowsNativeShell: {
    build: {
      command: nativeShellBuild.command,
      ok: Boolean(nativeShellBuild.ok),
      blocked: nativeShellBuild.blocked,
      skipped: Boolean(nativeShellBuild.skipped),
      output: nativeShellBuild.output,
    },
    publish: {
      command: nativeShellPublish.command,
      ok: Boolean(nativeShellPublish.ok),
      blocked: nativeShellPublish.blocked,
      skipped: Boolean(nativeShellPublish.skipped),
      artifactPath: nativeShellPublish.artifactPath ?? nativeShellCompanionArtifactPath,
      output: nativeShellPublish.output,
    },
    installer: {
      command: nativeShellInstaller.command,
      ok: Boolean(nativeShellInstaller.ok),
      blocked: nativeShellInstaller.blocked,
      skipped: Boolean(nativeShellInstaller.skipped),
      artifactPath: nativeShellInstaller.artifactPath ?? nativeShellInstallerArtifactPath,
      output: nativeShellInstaller.output,
    },
  },
  overallStatus,
};

const markdown = `# Release Readiness ${version}

- Generated: ${report.generatedAt}
- Overall status: **${report.overallStatus}**
- Now tasks empty: ${report.nowTasksEmpty ? "yes" : `no (${report.nowTaskCount})`}
- Release notes draft: ${notes.ok ? notes.path : notes.exists ? `${notes.path} (stale)` : "missing"}
- Windows desktop build: ${statusLabel(desktopBuild)}
- Windows native shell build: ${statusLabel(nativeShellBuild)}
- Windows native shell publish: ${statusLabel(nativeShellPublish)}
- Windows native shell installer: ${statusLabel(nativeShellInstaller)}

## Validation Commands

- ${desktopShellVersionStatus.ok ? "[pass]" : "[fail]"} \`${desktopShellVersionStatus.command}\`
${report.validations
  .map(
    (item) =>
      `- ${item.ok ? "[pass]" : item.blocked ? "[blocked]" : "[fail]"} \`${item.command}\`${item.retryAttempted ? " (retried once)" : ""}${item.blocked ? ` - ${item.blocked}` : ""}`,
  )
  .join("\n")}
- ${nativeShellTests.ok ? "[pass]" : nativeShellTests.blocked ? "[blocked]" : "[fail]"} \`${nativeShellTests.command}\`${nativeShellTests.blocked ? ` - ${nativeShellTests.blocked}` : ""}

## Release Notes Draft

- Path: ${notes.path}
- Result: ${notes.ok ? "pass" : "fail"}

## Desktop Build

- Command: \`${desktopBuild.command}\`
- Result: ${statusLabel(desktopBuild)}${desktopBuild.blocked ? ` (${desktopBuild.blocked})` : ""}

## Native Shell Checks

- Build: ${statusLabel(nativeShellBuild)}${nativeShellBuild.blocked ? ` (${nativeShellBuild.blocked})` : ""}
- Publish: ${statusLabel(nativeShellPublish)}${nativeShellPublish.blocked ? ` (${nativeShellPublish.blocked})` : ""}
- Companion binary in publish output: ${nativeShellPublish.ok ? "pass" : "fail"}
- Installer: ${statusLabel(nativeShellInstaller)}${nativeShellInstaller.blocked ? ` (${nativeShellInstaller.blocked})` : ""}
- Companion artifact: ${nativeShellPublish.artifactPath ?? nativeShellCompanionArtifactPath}
- Installer artifact: ${nativeShellInstaller.artifactPath ?? nativeShellInstallerArtifactPath}

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
