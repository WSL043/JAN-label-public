import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
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

function runFile(file, args, options = {}) {
  return execFileSync(file, args, {
    cwd: options.cwd ?? process.cwd(),
    encoding: "utf8",
    maxBuffer: 16 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function tryRunFile(file, args, options = {}) {
  try {
    return runFile(file, args, options);
  } catch {
    return null;
  }
}

function boolArg(value, fallback = false) {
  if (value === undefined) {
    return fallback;
  }
  return value === "true";
}

function parseRemoteRepo(remote) {
  const httpsMatch = remote.match(/github\.com[/:]([^/]+)\/(.+?)(?:\.git)?$/);
  if (!httpsMatch) {
    return null;
  }
  return `${httpsMatch[1]}/${httpsMatch[2]}`;
}

function loadConfig(repoRoot) {
  const configPath = path.join(repoRoot, "public-mirror.config.json");
  if (!fs.existsSync(configPath)) {
    return {};
  }
  return JSON.parse(fs.readFileSync(configPath, "utf8"));
}

function ensureCleanWorkingTree(repoRoot, sourceRef) {
  if (sourceRef !== "HEAD") {
    return;
  }

  const status = runFile("git", ["status", "--porcelain"], { cwd: repoRoot });
  if (status.length > 0) {
    throw new Error(
      "Working tree is not clean. Commit or stash changes before exporting HEAD to the public mirror.",
    );
  }
}

function ensureMirrorRepository(repository, createIfMissing) {
  const existing = tryRunFile("gh", [
    "repo",
    "view",
    repository,
    "--json",
    "nameWithOwner,isPrivate,url",
  ]);
  if (existing) {
    const parsed = JSON.parse(existing);
    if (parsed.isPrivate) {
      throw new Error(
        `Configured public mirror ${repository} already exists but is still private.`,
      );
    }
    return parsed;
  }

  if (!createIfMissing) {
    throw new Error(
      `Public mirror repository ${repository} does not exist. Create it first or re-run with --create true.`,
    );
  }

  runFile("gh", [
    "repo",
    "create",
    repository,
    "--public",
    "--confirm",
    "--disable-issues",
    "--disable-wiki",
    "--description",
    "Public packaging mirror for JAN-label releases and free GitHub-hosted runner builds.",
  ]);

  const created = runFile("gh", [
    "repo",
    "view",
    repository,
    "--json",
    "nameWithOwner,isPrivate,url",
  ]);
  const parsed = JSON.parse(created);
  if (parsed.isPrivate) {
    throw new Error(`Public mirror repository ${repository} was created but is still private.`);
  }
  return parsed;
}

function exportSnapshot(repoRoot, sourceRef, outputDirectory) {
  const archivePath = path.join(outputDirectory, "mirror-snapshot.tar");
  const snapshotDirectory = path.join(outputDirectory, "snapshot");
  fs.mkdirSync(snapshotDirectory, { recursive: true });
  runFile("git", ["archive", "--format=tar", "--output", archivePath, sourceRef], {
    cwd: repoRoot,
  });
  runFile("tar", ["-xf", archivePath, "-C", snapshotDirectory], { cwd: repoRoot });
  return snapshotDirectory;
}

function writeMirrorMetadata(snapshotDirectory, metadataPath, metadata) {
  const outputPath = path.join(snapshotDirectory, metadataPath);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(metadata, null, 2)}\n`);
}

function initializeSnapshotRepository(snapshotDirectory, branch, commitMessage, tag) {
  runFile("git", ["init", `--initial-branch=${branch}`], { cwd: snapshotDirectory });
  runFile("git", ["config", "user.name", "Codex Mirror Bot"], { cwd: snapshotDirectory });
  runFile("git", ["config", "user.email", "codex-mirror-bot@users.noreply.github.com"], {
    cwd: snapshotDirectory,
  });
  runFile("git", ["add", "-A"], { cwd: snapshotDirectory });
  runFile("git", ["commit", "-m", commitMessage], { cwd: snapshotDirectory });

  if (tag) {
    runFile("git", ["tag", "-a", tag, "-m", `Public mirror release snapshot for ${tag}`], {
      cwd: snapshotDirectory,
    });
  }
}

function pushSnapshot(snapshotDirectory, repository, branch, tag) {
  const remoteUrl = `https://github.com/${repository}.git`;
  runFile("git", ["remote", "add", "origin", remoteUrl], { cwd: snapshotDirectory });
  runFile("git", ["push", "--force", "origin", `HEAD:refs/heads/${branch}`], {
    cwd: snapshotDirectory,
  });
  if (tag) {
    runFile("git", ["push", "origin", `refs/tags/${tag}`], { cwd: snapshotDirectory });
  }
}

function normalizeTag(tag) {
  if (!tag) {
    return null;
  }
  return tag.startsWith("v") ? tag : `v${tag}`;
}

const repoRoot = runFile("git", ["rev-parse", "--show-toplevel"]);
const args = parseArgs(process.argv.slice(2));
const config = loadConfig(repoRoot);
const repository = args.get("repo") ?? config.repository;
const branch = args.get("branch") ?? config.branch ?? "main";
const sourceRef = args.get("ref") ?? "HEAD";
const createIfMissing = boolArg(args.get("create"), Boolean(config.createIfMissing));
const snapshotMetadataPath =
  args.get("metadata-path") ?? config.snapshotMetadataPath ?? ".github/public-mirror-source.json";
const tag = normalizeTag(args.get("tag"));

if (!repository) {
  throw new Error(
    "Public mirror repository is not configured. Set it in public-mirror.config.json or pass --repo owner/name.",
  );
}

runFile("gh", ["auth", "status"]);
ensureCleanWorkingTree(repoRoot, sourceRef);
const mirrorRepo = ensureMirrorRepository(repository, createIfMissing);

const sourceSha = runFile("git", ["rev-parse", sourceRef], { cwd: repoRoot });
const sourceRemote = tryRunFile("git", ["remote", "get-url", "origin"], { cwd: repoRoot });
const sourceRepository = sourceRemote ? parseRemoteRepo(sourceRemote) : null;

const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "jan-label-public-mirror-"));

try {
  const snapshotDirectory = exportSnapshot(repoRoot, sourceRef, tempRoot);
  writeMirrorMetadata(snapshotDirectory, snapshotMetadataPath, {
    sourceRepository,
    sourceRef,
    sourceSha,
    exportedAt: new Date().toISOString(),
    exportedBy: "scripts/publish-public-mirror.mjs",
    releaseTag: tag,
    note: "Snapshot-only export for the public packaging mirror. Private git history is intentionally not included.",
  });

  const commitMessage = tag
    ? `chore(mirror): release snapshot ${tag} from ${sourceSha.slice(0, 7)}`
    : `chore(mirror): sync snapshot ${sourceSha.slice(0, 7)}`;
  initializeSnapshotRepository(snapshotDirectory, branch, commitMessage, tag);
  pushSnapshot(snapshotDirectory, repository, branch, tag);

  process.stdout.write(
    `${mirrorRepo.url}\nbranch=${branch}\nsourceSha=${sourceSha}\n${tag ? `tag=${tag}\n` : ""}`,
  );
} finally {
  fs.rmSync(tempRoot, { recursive: true, force: true });
}
