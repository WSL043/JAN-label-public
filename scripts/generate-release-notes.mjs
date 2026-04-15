import { execFileSync, execSync } from "node:child_process";
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

function run(command) {
  return execSync(command, {
    cwd: process.cwd(),
    encoding: "utf8",
    maxBuffer: 16 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function tryRun(command) {
  try {
    return run(command);
  } catch {
    return null;
  }
}

function runFile(file, args) {
  return execFileSync(file, args, {
    cwd: process.cwd(),
    encoding: "utf8",
    maxBuffer: 16 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function tryRunFile(file, args) {
  try {
    return runFile(file, args);
  } catch {
    return null;
  }
}

function normalizeVersion(input) {
  if (!input) {
    throw new Error("release notes generation requires --version <vX.Y.Z>.");
  }
  return input.startsWith("v") ? input : `v${input}`;
}

function sectionBody(markdown, heading) {
  const pattern = new RegExp(`## ${heading}\\r?\\n([\\s\\S]*?)(?=\\r?\\n## |$)`);
  const match = markdown.match(pattern);
  return match?.[1].trim() ?? "";
}

function takeLines(block, limit = 16) {
  return block
    .split(/\r?\n/)
    .map((line) => line.trimEnd())
    .filter((line) => line.trim().length > 0)
    .slice(0, limit)
    .join("\n");
}

function previousTag(currentVersion) {
  const tags = run("git tag --sort=-creatordate")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  return tags.find((tag) => tag !== currentVersion) ?? null;
}

function commitSummary(range) {
  const raw = tryRun(`git log ${range} --no-merges --pretty=format:"- %s (%h)"`);
  if (!raw) {
    return "- No commit summary available.";
  }
  return raw.split(/\r?\n/).slice(0, 24).join("\n");
}

function parseRemoteRepo(remote) {
  const httpsMatch = remote.match(/github\.com[/:]([^/]+)\/(.+?)(?:\.git)?$/);
  if (!httpsMatch) {
    return null;
  }
  return `${httpsMatch[1]}/${httpsMatch[2]}`;
}

function loadMaintenanceLedger(repo) {
  if (!repo) {
    return {
      source: "No GitHub remote detected.",
      body: "Maintenance Ledger source unavailable.",
    };
  }

  const issueListRaw = tryRunFile("gh", [
    "issue",
    "list",
    "--repo",
    repo,
    "--state",
    "all",
    "--search",
    '"Maintenance Ledger" in:title',
    "--limit",
    "10",
    "--json",
    "number,title,updatedAt,url",
  ]);
  if (issueListRaw) {
    try {
      const issues = JSON.parse(issueListRaw);
      const issue = [...issues].sort((left, right) =>
        right.updatedAt.localeCompare(left.updatedAt),
      )[0];
      if (issue) {
        const issueViewRaw = tryRunFile("gh", [
          "issue",
          "view",
          String(issue.number),
          "--repo",
          repo,
          "--comments",
          "--json",
          "body,comments,title,url",
        ]);
        if (issueViewRaw) {
          const issueView = JSON.parse(issueViewRaw);
          const latestComment = issueView.comments?.at(-1)?.body?.trim();
          const issueBody = issueView.body?.trim();
          const body = latestComment || issueBody || "Maintenance Ledger issue is empty.";
          return {
            source: `GitHub issue ${issueView.url}`,
            body,
          };
        }
      }
    } catch {
      // Fall through to file fallback.
    }
  }

  const fallbackPath = path.join(process.cwd(), ".codex-maintenance-ledger.md");
  if (fs.existsSync(fallbackPath)) {
    return {
      source: "Local .codex-maintenance-ledger.md fallback",
      body: fs.readFileSync(fallbackPath, "utf8").trim(),
    };
  }

  return {
    source: "No Maintenance Ledger issue/comment or local fallback available.",
    body: "Maintenance Ledger source unavailable.",
  };
}

const args = parseArgs(process.argv.slice(2));
const version = normalizeVersion(args.get("version"));
const generatedAt = new Date().toISOString();
const currentStatePath = path.join(process.cwd(), "docs", "handoff", "current-state.md");
const currentState = fs.readFileSync(currentStatePath, "utf8");
const remote = tryRun("git remote get-url origin");
const repo = remote ? parseRemoteRepo(remote) : null;
const ledger = loadMaintenanceLedger(repo);
const priorTag = previousTag(version);
const commitRange = priorTag ? `${priorTag}..HEAD` : "HEAD";
const shippingNow = takeLines(sectionBody(currentState, "Shipping Now"));
const landedThisBatch = takeLines(sectionBody(currentState, "Landed In This Batch"));
const nextTasks = takeLines(sectionBody(currentState, "Next Main Tasks"));
const notes = `# ${version}

- Generated: ${generatedAt}
- Previous tag: ${priorTag ?? "none"}
- Commit range: \`${commitRange}\`

## Release Focus

${shippingNow || "- Shipping focus unavailable from current-state handoff."}

## Landed Since ${priorTag ?? "Repo Start"}

${landedThisBatch || "- No landed batch summary available."}

## Maintenance Ledger

- Source: ${ledger.source}

\`\`\`md
${takeLines(ledger.body, 40)}
\`\`\`

## Commit Summary

${commitSummary(commitRange)}

## Remaining Release Gate

${nextTasks || "- No remaining release gate tasks recorded in current-state handoff."}
`;

const outputPath = path.join(process.cwd(), "docs", "release", `${version}.md`);
fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, notes);
process.stdout.write(`${outputPath}\n`);
