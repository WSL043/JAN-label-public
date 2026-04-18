using System.Text.Json;
using System.Text.Json.Serialization;

namespace JanLabel.WindowsShell.Core;

public static class NativeTemplateCatalogLoader
{
    private const string TemplateOverlayDirectoryEnvVar = "JAN_LABEL_TEMPLATE_OVERLAY_DIR";
    private const string ManifestFileName = "template-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryLoad(out TemplateCatalogSnapshot? snapshot, out string? error)
    {
        try
        {
            snapshot = Load();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            error = ex.Message;
            return false;
        }
    }

    public static bool TryLoad(string overlayDirectory, out TemplateCatalogSnapshot? snapshot, out string? error)
    {
        try
        {
            snapshot = Load(overlayDirectory);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            error = ex.Message;
            return false;
        }
    }

    public static TemplateCatalogSnapshot Load()
    {
        return Load(ResolveTemplateOverlayDirectory());
    }

    public static TemplateCatalogSnapshot Load(string overlayDirectory)
    {
        var packagedManifestPath = ResolvePackagedManifestPath();
        var packagedManifest = ReadManifest(packagedManifestPath, "packaged template manifest");
        return BuildSnapshot(packagedManifest, Path.GetFullPath(overlayDirectory));
    }

    private static TemplateCatalogSnapshot BuildSnapshot(
        TemplateManifestDocument packagedManifest,
        string overlayDirectory)
    {
        var manifestPath = Path.Combine(overlayDirectory, ManifestFileName);
        var manifestExists = File.Exists(manifestPath);
        var overlayJsonFiles = EnumerateOverlayJsonFiles(overlayDirectory);
        var manifestStatus = manifestExists ? "ready" : "missing";
        var effectiveDefaultTemplateVersion = packagedManifest.DefaultTemplateVersion;
        var effectiveDefaultSource = "packaged";
        var localEntryCount = 0;
        var localEntries = new List<TemplateCatalogLocalEntry>();
        var issues = new List<TemplateCatalogIssue>();
        var referencedLocalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TemplateManifestDocument? localManifest = null;

        if (!manifestExists)
        {
            issues.Add(InfoIssue("manifest_missing", "The desktop local template manifest does not exist yet. Saving a template from the Windows shell will create it."));
        }
        else
        {
            string manifestContents;
            try
            {
                manifestContents = File.ReadAllText(manifestPath);
            }
            catch (Exception ex)
            {
                manifestStatus = "error";
                issues.Add(ErrorIssue("manifest_read_failed", $"Template manifest '{manifestPath}' could not be read: {ex.Message}"));
                manifestContents = string.Empty;
            }

            if (!string.Equals(manifestStatus, "error", StringComparison.Ordinal))
            {
                try
                {
                    localManifest = JsonSerializer.Deserialize<TemplateManifestDocument>(manifestContents, JsonOptions);
                    if (localManifest is null)
                    {
                        throw new InvalidOperationException("manifest deserialized to null.");
                    }
                }
                catch (Exception ex)
                {
                    manifestStatus = "error";
                    issues.Add(ErrorIssue("manifest_parse_failed", $"Template manifest '{manifestPath}' is not valid JSON: {ex.Message}"));
                }
            }

            if (localManifest is not null)
            {
                localEntryCount = localManifest.Templates.Count;
                effectiveDefaultTemplateVersion = string.IsNullOrWhiteSpace(localManifest.DefaultTemplateVersion)
                    ? packagedManifest.DefaultTemplateVersion
                    : localManifest.DefaultTemplateVersion;

                if (string.IsNullOrWhiteSpace(localManifest.SchemaVersion))
                {
                    issues.Add(WarningIssue("manifest_schema_missing", "The local template manifest is missing schema_version. The packaged schema will be used until the manifest is rewritten."));
                }

                if (string.IsNullOrWhiteSpace(localManifest.DefaultTemplateVersion))
                {
                    issues.Add(WarningIssue("manifest_default_missing", "The local template manifest is missing default_template_version. The packaged default will be used until the manifest is rewritten."));
                }

                foreach (var duplicate in localManifest.Templates
                             .GroupBy((entry) => entry.Version, StringComparer.OrdinalIgnoreCase)
                             .Where((group) => group.Count() > 1))
                {
                    issues.Add(WarningIssue("duplicate_local_version", $"Local template manifest contains {duplicate.Count()} entries for template_version '{duplicate.Key}'. Keep a single entry per template_version."));
                }

                foreach (var entry in localManifest.Templates)
                {
                    var normalizedPath = NormalizeRelativePath(entry.Path);
                    var pathIsAbsolute = Path.IsPathRooted(normalizedPath);
                    var pathHasParentTraversal = ContainsParentTraversal(normalizedPath);
                    var resolvedPath = pathIsAbsolute
                        ? normalizedPath
                        : Path.GetFullPath(Path.Combine(overlayDirectory, normalizedPath));
                    var fileExists = !pathIsAbsolute && !pathHasParentTraversal && File.Exists(resolvedPath);

                    if (!pathIsAbsolute && !pathHasParentTraversal)
                    {
                        referencedLocalPaths.Add(normalizedPath);
                    }

                    if (pathIsAbsolute)
                    {
                        issues.Add(ErrorIssue("absolute_template_path", $"Local template entry '{entry.Version}' points to an absolute path '{entry.Path}'. Local overlay paths must stay relative to the overlay directory."));
                    }
                    else if (pathHasParentTraversal)
                    {
                        issues.Add(ErrorIssue("template_path_traversal", $"Local template entry '{entry.Version}' contains parent traversal in '{entry.Path}'. Parent traversal is rejected for overlay templates."));
                    }
                    else if (!fileExists)
                    {
                        issues.Add(ErrorIssue("missing_template_file", $"Local template entry '{entry.Version}' references '{entry.Path}' but the file is missing from the overlay directory."));
                    }

                    localEntries.Add(
                        new TemplateCatalogLocalEntry(
                            entry.Version,
                            entry.LabelName,
                            entry.Path,
                            resolvedPath,
                            entry.Enabled,
                            fileExists));
                }
            }
        }

        foreach (var overlayJsonFile in overlayJsonFiles.Where((path) => !referencedLocalPaths.Contains(path)))
        {
            issues.Add(WarningIssue("unreferenced_overlay_file", $"Overlay file '{overlayJsonFile}' exists in the local template directory but is not referenced by the local manifest."));
        }

        var entries = BuildMergedEntries(packagedManifest, localManifest, localEntries);
        var mergedVersions = entries.Select((entry) => entry.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (mergedVersions.Contains(effectiveDefaultTemplateVersion))
        {
            effectiveDefaultSource = localManifest?.Templates.Any(
                (entry) => entry.Enabled && string.Equals(entry.Version, effectiveDefaultTemplateVersion, StringComparison.OrdinalIgnoreCase))
                == true
                ? "local"
                : "packaged";
        }
        else
        {
            effectiveDefaultSource = "unknown";
            issues.Add(ErrorIssue("default_template_missing", $"Effective default template_version '{effectiveDefaultTemplateVersion}' is not available in the merged packaged/local catalog."));
        }

        return new TemplateCatalogSnapshot(
            localManifest is not null && !string.IsNullOrWhiteSpace(localManifest.DefaultTemplateVersion)
                ? localManifest.DefaultTemplateVersion
                : packagedManifest.DefaultTemplateVersion,
            effectiveDefaultTemplateVersion,
            effectiveDefaultSource,
            manifestStatus,
            overlayDirectory,
            manifestPath,
            manifestExists,
            localEntryCount,
            overlayJsonFiles.Count,
            entries,
            localEntries,
            issues,
            new[]
            {
                "Back up the whole overlay directory as a unit before repair or migration.",
                "Keep packaged templates immutable; only local overlay files should change here.",
            },
            new[]
            {
                "Rewrite malformed local manifest JSON before treating the overlay as authoritative.",
                "Remove broken or unreferenced overlay entries after confirming the packaged fallback path.",
            },
            new[]
            {
                "Treat the local template catalog as single-writer state.",
                "Do not let multiple tools rewrite the overlay manifest concurrently.",
            });
    }

    private static IReadOnlyList<TemplateCatalogEntry> BuildMergedEntries(
        TemplateManifestDocument packagedManifest,
        TemplateManifestDocument? localManifest,
        IReadOnlyCollection<TemplateCatalogLocalEntry> localEntries)
    {
        var mergedEntries = packagedManifest.Templates
            .Where((entry) => entry.Enabled)
            .Select((entry) => new TemplateCatalogEntry(entry.Version, entry.LabelName, entry.Description, "packaged"))
            .ToList();
        var indexByVersion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < mergedEntries.Count; index += 1)
        {
            indexByVersion[mergedEntries[index].Version] = index;
        }

        if (localManifest is null)
        {
            return mergedEntries;
        }

        foreach (var entry in localManifest.Templates)
        {
            if (!entry.Enabled)
            {
                continue;
            }

            var localEntry = localEntries.FirstOrDefault((candidate) => string.Equals(candidate.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
            if (localEntry is null || !localEntry.Enabled || !localEntry.FileExists)
            {
                continue;
            }

            var mergedEntry = new TemplateCatalogEntry(entry.Version, entry.LabelName, entry.Description, "local");
            if (indexByVersion.TryGetValue(entry.Version, out var existingIndex))
            {
                mergedEntries[existingIndex] = mergedEntry;
            }
            else
            {
                indexByVersion[entry.Version] = mergedEntries.Count;
                mergedEntries.Add(mergedEntry);
            }
        }

        return mergedEntries;
    }

    private static TemplateManifestDocument ReadManifest(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{description} was not found at '{path}'.", path);
        }

        var contents = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<TemplateManifestDocument>(contents, JsonOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException($"{description} could not be parsed.");
        }

        return manifest;
    }

    private static string ResolvePackagedManifestPath()
    {
        foreach (var candidate in EnumeratePackagedManifestCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Packaged template manifest was not found. Expected packages/templates/template-manifest.json.");
    }

    private static IEnumerable<string> EnumeratePackagedManifestCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;

        yield return Path.Combine(currentDirectory, "packages", "templates", ManifestFileName);
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "packages", "templates", ManifestFileName));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "packages", "templates", ManifestFileName));
    }

    private static string ResolveTemplateOverlayDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(TemplateOverlayDirectoryEnvVar);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return WindowsShellAppPaths.Resolve().CatalogDirectory;
    }

    private static List<string> EnumerateOverlayJsonFiles(string overlayDirectory)
    {
        if (!Directory.Exists(overlayDirectory))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(overlayDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where((fileName) => !string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .Select((fileName) => fileName!.Replace('\\', '/'))
            .OrderBy((fileName) => fileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeRelativePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static bool ContainsParentTraversal(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any((segment) => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static TemplateCatalogIssue InfoIssue(string code, string message)
    {
        return new("info", code, message);
    }

    private static TemplateCatalogIssue WarningIssue(string code, string message)
    {
        return new("warning", code, message);
    }

    private static TemplateCatalogIssue ErrorIssue(string code, string message)
    {
        return new("error", code, message);
    }

    private sealed class TemplateManifestDocument
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;

        [JsonPropertyName("default_template_version")]
        public string DefaultTemplateVersion { get; set; } = string.Empty;

        [JsonPropertyName("templates")]
        public List<TemplateManifestEntryDocument> Templates { get; set; } = new();
    }

    private sealed class TemplateManifestEntryDocument
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("label_name")]
        public string LabelName { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
