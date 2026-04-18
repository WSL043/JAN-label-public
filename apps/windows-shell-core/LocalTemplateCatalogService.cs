using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Data.Sqlite;

namespace JanLabel.WindowsShell.Core;

public sealed class LocalTemplateCatalogService : ITemplateCatalogService
{
    private const string TemplateOverlayDirectoryEnvVar = "JAN_LABEL_TEMPLATE_OVERLAY_DIR";
    private const string ManifestFileName = "template-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly WindowsShellAppPaths _paths;

    public LocalTemplateCatalogService(WindowsShellAppPaths paths)
    {
        _paths = paths;
    }

    public TemplateCatalogSnapshot LoadCatalogSnapshot()
    {
        return NativeTemplateCatalogLoader.Load(ResolveCatalogDirectory());
    }

    public ValueTask<TemplateCatalogDocument> LoadTemplateAsync(string templateVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedTemplateVersion = RequireValue(templateVersion, nameof(templateVersion));
        var snapshot = LoadCatalogSnapshot();
        var entry = snapshot.Entries.FirstOrDefault(
            (candidate) => string.Equals(candidate.Version, normalizedTemplateVersion, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new InvalidOperationException($"Template '{normalizedTemplateVersion}' is not available in the native catalog snapshot.");
        }

        var sourcePath = ResolveDocumentPath(snapshot, entry, normalizedTemplateVersion);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Template '{normalizedTemplateVersion}' could not be loaded because '{sourcePath}' does not exist.", sourcePath);
        }

        var documentJson = File.ReadAllText(sourcePath);
        var document = TemplateCatalogDocumentParser.Parse(
            documentJson,
            normalizedTemplateVersion,
            entry.LabelName,
            entry.Source,
            sourcePath,
            entry.Description);
        return ValueTask.FromResult(document);
    }

    public ValueTask<TemplateCatalogSaveResult> SaveTemplateAsync(TemplateDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templateVersion = RequireValue(document.TemplateVersion, nameof(document.TemplateVersion));
        var labelName = RequireValue(document.LabelName, nameof(document.LabelName));
        var normalizedJson = NormalizeJson(document.DocumentJson);
        var overlayDirectory = ResolveCatalogDirectory();
        var manifestPath = Path.Combine(overlayDirectory, ManifestFileName);
        Directory.CreateDirectory(overlayDirectory);

        var warnings = new List<string>();
        var manifest = LoadOrCreateManifest(manifestPath, warnings);
        var relativeTemplatePath = ResolveTemplateRelativePath(manifest, templateVersion);
        var templatePath = ResolveOverlayTemplatePath(overlayDirectory, relativeTemplatePath, templateVersion);
        var updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");

        WriteTextAtomically(templatePath, normalizedJson);
        UpsertManifestEntry(manifest, templateVersion, labelName, relativeTemplatePath, document.Description);
        if (string.IsNullOrWhiteSpace(manifest.DefaultTemplateVersion))
        {
            manifest.DefaultTemplateVersion = templateVersion;
            warnings.Add("Local manifest did not declare a default template, so the saved template is now the local default.");
        }

        WriteTextAtomically(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        UpsertCatalogEntry(templateVersion, labelName, normalizedJson, manifestPath, manifest.DefaultTemplateVersion, updatedAtUtc);

        warnings.Insert(0, "Saving to the local catalog creates a new saved state and requires fresh proof review before dispatch.");
        return ValueTask.FromResult(
            new TemplateCatalogSaveResult(
                templateVersion,
                manifestPath,
                templatePath,
                RequiresProofReview: true,
                warnings));
    }

    private string ResolveDocumentPath(
        TemplateCatalogSnapshot snapshot,
        TemplateCatalogEntry entry,
        string templateVersion)
    {
        if (string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase))
        {
            var localEntry = snapshot.LocalEntries.FirstOrDefault(
                (candidate) => string.Equals(candidate.Version, templateVersion, StringComparison.OrdinalIgnoreCase));
            if (localEntry is null || !localEntry.Enabled || !localEntry.FileExists)
            {
                throw new InvalidOperationException($"Local template '{templateVersion}' is selected in the catalog snapshot but does not resolve to a readable overlay file.");
            }

            return localEntry.ResolvedPath;
        }

        return ResolvePackagedTemplatePath(templateVersion);
    }

    private string ResolveCatalogDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(TemplateOverlayDirectoryEnvVar);
        return string.IsNullOrWhiteSpace(configured)
            ? _paths.CatalogDirectory
            : Path.GetFullPath(configured);
    }

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeJson(string documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new ArgumentException("Template JSON cannot be empty.", nameof(documentJson));
        }

        var parsed = JsonNode.Parse(documentJson);
        if (parsed is null)
        {
            throw new InvalidOperationException("Template JSON parsed to null.");
        }

        return parsed.ToJsonString(JsonOptions);
    }

    private static TemplateManifestDocument LoadOrCreateManifest(string manifestPath, List<string> warnings)
    {
        if (!File.Exists(manifestPath))
        {
            warnings.Add("Local template manifest did not exist and was created during save.");
            return new TemplateManifestDocument
            {
                SchemaVersion = "template-manifest-v1",
            };
        }

        try
        {
            var contents = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<TemplateManifestDocument>(contents, JsonOptions);
            if (manifest is null)
            {
                throw new InvalidOperationException("Manifest deserialized to null.");
            }

            if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            {
                manifest.SchemaVersion = "template-manifest-v1";
                warnings.Add("Local template manifest was missing schema_version and was normalized during save.");
            }

            return manifest;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Local template manifest '{manifestPath}' could not be parsed for safe update: {ex.Message}",
                ex);
        }
    }

    private static string ResolveTemplateRelativePath(TemplateManifestDocument manifest, string templateVersion)
    {
        var existing = manifest.Templates.FirstOrDefault(
            (entry) => string.Equals(entry.Version, templateVersion, StringComparison.OrdinalIgnoreCase));

        if (existing is not null &&
            !string.IsNullOrWhiteSpace(existing.Path) &&
            !Path.IsPathRooted(existing.Path) &&
            !ContainsParentTraversal(existing.Path))
        {
            return NormalizeRelativePath(existing.Path);
        }

        return BuildDefaultTemplateFileName(templateVersion);
    }

    private static string ResolvePackagedTemplatePath(string templateVersion)
    {
        var packagedManifestPath = ResolvePackagedManifestPath();
        var packagedDirectory = Path.GetDirectoryName(packagedManifestPath)
            ?? throw new InvalidOperationException("Packaged template manifest directory could not be determined.");
        var packagedManifest = ReadManifest(packagedManifestPath);
        var entry = packagedManifest.Templates.FirstOrDefault(
            (candidate) =>
                candidate.Enabled &&
                string.Equals(candidate.Version, templateVersion, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new InvalidOperationException($"Packaged template '{templateVersion}' is not declared in '{packagedManifestPath}'.");
        }

        var normalizedPath = NormalizeRelativePath(entry.Path);
        if (Path.IsPathRooted(normalizedPath) || ContainsParentTraversal(normalizedPath))
        {
            throw new InvalidOperationException($"Packaged template '{templateVersion}' has an invalid manifest path '{entry.Path}'.");
        }

        return Path.GetFullPath(Path.Combine(packagedDirectory, normalizedPath));
    }

    private static void UpsertManifestEntry(
        TemplateManifestDocument manifest,
        string templateVersion,
        string labelName,
        string relativeTemplatePath,
        string? description)
    {
        var existing = manifest.Templates.FirstOrDefault(
            (entry) => string.Equals(entry.Version, templateVersion, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            manifest.Templates.Add(
                new TemplateManifestEntryDocument
                {
                    Version = templateVersion,
                    Path = NormalizeRelativePath(relativeTemplatePath),
                    LabelName = labelName,
                    Enabled = true,
                    Description = string.IsNullOrWhiteSpace(description) ? $"Saved from windows-shell for {templateVersion}." : description,
                });
            return;
        }

        existing.Path = NormalizeRelativePath(relativeTemplatePath);
        existing.LabelName = labelName;
        existing.Enabled = true;
        existing.Description = string.IsNullOrWhiteSpace(description) ? existing.Description : description;
    }

    private void UpsertCatalogEntry(
        string templateVersion,
        string labelName,
        string contentJson,
        string manifestPath,
        string? defaultTemplateVersion,
        string updatedAtUtc)
    {
        using var connection = WindowsShellPlatform.OpenConnection(_paths);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (!string.IsNullOrWhiteSpace(defaultTemplateVersion))
        {
            using var normalizeDefaults = connection.CreateCommand();
            normalizeDefaults.Transaction = transaction;
            normalizeDefaults.CommandText =
                """
                UPDATE template_catalog_entries
                SET is_default = CASE
                    WHEN template_version = $defaultTemplateVersion THEN 1
                    ELSE 0
                END;
                """;
            normalizeDefaults.Parameters.AddWithValue("$defaultTemplateVersion", defaultTemplateVersion);
            normalizeDefaults.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO template_catalog_entries (
                template_version,
                label_name,
                source,
                manifest_path,
                content_json,
                is_default,
                updated_at_utc)
            VALUES (
                $templateVersion,
                $labelName,
                $source,
                $manifestPath,
                $contentJson,
                $isDefault,
                $updatedAtUtc)
            ON CONFLICT(template_version) DO UPDATE SET
                label_name = excluded.label_name,
                source = excluded.source,
                manifest_path = excluded.manifest_path,
                content_json = excluded.content_json,
                is_default = excluded.is_default,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$templateVersion", templateVersion);
        command.Parameters.AddWithValue("$labelName", labelName);
        command.Parameters.AddWithValue("$source", "local");
        command.Parameters.AddWithValue("$manifestPath", manifestPath);
        command.Parameters.AddWithValue("$contentJson", contentJson);
        command.Parameters.AddWithValue(
            "$isDefault",
            string.Equals(templateVersion, defaultTemplateVersion, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void WriteTextAtomically(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, contents, Utf8NoBom);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, path);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string BuildDefaultTemplateFileName(string templateVersion)
    {
        if (templateVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            templateVersion.Contains(Path.DirectorySeparatorChar) ||
            templateVersion.Contains(Path.AltDirectorySeparatorChar) ||
            string.Equals(templateVersion, ".", StringComparison.Ordinal) ||
            string.Equals(templateVersion, "..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Template version '{templateVersion}' cannot be used as a local catalog file name.");
        }

        return $"{templateVersion}.json";
    }

    private static string ResolveOverlayTemplatePath(string overlayDirectory, string relativeTemplatePath, string templateVersion)
    {
        var normalizedPath = NormalizeRelativePath(relativeTemplatePath);
        if (Path.IsPathRooted(normalizedPath) || ContainsParentTraversal(normalizedPath))
        {
            throw new InvalidOperationException(
                $"Template '{templateVersion}' resolved to an invalid local path '{relativeTemplatePath}'.");
        }

        var overlayRoot = Path.GetFullPath(overlayDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(overlayRoot, normalizedPath));
        var rootWithSeparator = $"{overlayRoot}{Path.DirectorySeparatorChar}";
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, overlayRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Template '{templateVersion}' resolved outside the local catalog directory.");
        }

        return fullPath;
    }

    private static bool ContainsParentTraversal(string path)
    {
        var segments = NormalizeRelativePath(path).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any((segment) => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static TemplateManifestDocument ReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Packaged template manifest was not found at '{path}'.", path);
        }

        var contents = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<TemplateManifestDocument>(contents, JsonOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException($"Template manifest '{path}' could not be parsed.");
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
