using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

internal static class TemplateCatalogSnapshotAdapter
{
    public static TemplateCatalogSnapshot FromCompanion(
        TemplateCatalogResultDto catalog,
        TemplateCatalogGovernanceResultDto governance)
    {
        return new TemplateCatalogSnapshot(
            catalog.DefaultTemplateVersion,
            governance.EffectiveDefaultTemplateVersion,
            governance.EffectiveDefaultSource,
            governance.ManifestStatus,
            governance.OverlayDirectoryPath,
            governance.ManifestPath,
            governance.ManifestExists,
            governance.LocalEntryCount,
            governance.OverlayJsonFileCount,
            catalog.Templates
                .Select((entry) => new TemplateCatalogEntry(entry.Version, entry.LabelName, entry.Description, entry.Source ?? "unknown"))
                .ToArray(),
            governance.LocalEntries
                .Select((entry) => new TemplateCatalogLocalEntry(entry.Version, entry.LabelName, entry.Path, entry.ResolvedPath, entry.Enabled, entry.FileExists))
                .ToArray(),
            governance.Issues
                .Select((issue) => new TemplateCatalogIssue(issue.Severity, issue.Code, issue.Message))
                .ToArray(),
            governance.BackupGuidance.ToArray(),
            governance.RepairGuidance.ToArray(),
            governance.SingleWriterGuidance.ToArray());
    }
}
