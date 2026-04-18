namespace JanLabel.WindowsShell.Core;

public sealed record TemplateCatalogSnapshot(
    string DefaultTemplateVersion,
    string EffectiveDefaultTemplateVersion,
    string EffectiveDefaultSource,
    string ManifestStatus,
    string OverlayDirectoryPath,
    string ManifestPath,
    bool ManifestExists,
    int LocalEntryCount,
    int OverlayJsonFileCount,
    IReadOnlyList<TemplateCatalogEntry> Entries,
    IReadOnlyList<TemplateCatalogLocalEntry> LocalEntries,
    IReadOnlyList<TemplateCatalogIssue> Issues,
    IReadOnlyList<string> BackupGuidance,
    IReadOnlyList<string> RepairGuidance,
    IReadOnlyList<string> SingleWriterGuidance);

public sealed record TemplateCatalogEntry(
    string Version,
    string LabelName,
    string? Description,
    string Source);

public sealed record TemplateCatalogLocalEntry(
    string Version,
    string LabelName,
    string Path,
    string ResolvedPath,
    bool Enabled,
    bool FileExists);

public sealed record TemplateCatalogIssue(
    string Severity,
    string Code,
    string Message);
