using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

internal static class TemplateCatalogPresentation
{
    public static void ConfigurePanel(
        TemplateLibraryPanelModel panel,
        IReadOnlyList<TemplateCatalogRowModel> entries,
        string preferredTemplateVersion,
        string headerDetail,
        string statusSummary)
    {
        panel.HeaderDetail = headerDetail;
        panel.StatusSummary = statusSummary;
        panel.EntrySummary = BuildEntrySummary(entries);
        panel.ReplaceSummaryCards(
            new[]
            {
                new SummaryCardModel("Effective Default", preferredTemplateVersion, "The current saved template winner is shown here."),
                new SummaryCardModel("Dispatch-Safe", entries.Count((entry) => !entry.Dispatch.Contains("blocked", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture), "Only saved catalog entries should be treated as proof-safe."),
                new SummaryCardModel("Blocked / Draft", entries.Count((entry) => entry.State == "draft").ToString(CultureInfo.InvariantCulture), "Unsafe or broken local entries stay visible instead of disappearing."),
                new SummaryCardModel("Rollback", entries.FirstOrDefault((entry) => entry.State == "fallback")?.Name ?? "none", "Packaged fallback stays explicit when a local overlay wins."),
            });
        panel.LoadEntries(entries, preferredTemplateVersion);
        panel.ReplaceGuidanceSections(
            new[]
            {
                new PropertySectionModel("Save Boundary", "Template choice is visible here, but only saved catalog state is production-meaningful.", new[] { new PropertyRowModel("Winning default", "packaged + local catalog resolution"), new PropertyRowModel("Unsafe state", "blocked or draft entries remain non-dispatchable"), new PropertyRowModel("Re-proof rule", "saved overlay changes still require fresh proof review") }),
                new PropertySectionModel("Why This Board Exists", "Operators should be able to tell which template wins, whether it is dispatch-safe, and how to back out before leaving the shell lane.", new[] { new PropertyRowModel("Dispatch-safe", "saved catalog entries only"), new PropertyRowModel("Rollback", "explicit packaged fallback"), new PropertyRowModel("Draft confusion", "kept visible so it is not mistaken for production state") }),
            });
        panel.ReplaceAlerts(
            entries
                .Where((entry) => entry.State == "draft" || entry.State == "fallback")
                .Take(3)
                .Select((entry) => new MessageRowModel(entry.State == "draft" ? "Warn" : "Info", "catalog", $"{entry.Name}: {entry.Note}"))
                .DefaultIfEmpty(new MessageRowModel("Info", "catalog", "No catalog alerts are currently surfaced."))
                .ToArray());
    }

    public static List<TemplateCatalogRowModel> BuildEntries(
        TemplateCatalogSnapshot snapshot)
    {
        var entries = new List<TemplateCatalogRowModel>();
        var catalogVersions = new HashSet<string>(snapshot.Entries.Select((entry) => entry.Version), StringComparer.OrdinalIgnoreCase);
        var packagedFallback = snapshot.Entries.FirstOrDefault(
            (entry) =>
                string.Equals(entry.Source, "packaged", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Version, snapshot.DefaultTemplateVersion, StringComparison.OrdinalIgnoreCase))
            ?.Version;

        foreach (var entry in snapshot.Entries)
        {
            var localEntry = snapshot.LocalEntries.FirstOrDefault((candidate) => string.Equals(candidate.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
            var hasBlockingIssue = HasBlockingIssue(snapshot, entry.Version, localEntry);
            var isDefault = string.Equals(entry.Version, snapshot.EffectiveDefaultTemplateVersion, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Version, snapshot.DefaultTemplateVersion, StringComparison.OrdinalIgnoreCase);
            var isFallback = !isDefault &&
                string.Equals(entry.Version, packagedFallback, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(snapshot.EffectiveDefaultSource, "local", StringComparison.OrdinalIgnoreCase);
            var issueSummary = BuildIssueSummary(snapshot, entry.Version);

            entries.Add(
                new TemplateCatalogRowModel(
                    entry.Version,
                    entry.Source ?? "unknown",
                    isDefault ? "default" : hasBlockingIssue ? "draft" : isFallback ? "fallback" : "stable",
                    string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                        ? "local catalog overlay"
                        : "packaged manifest",
                    hasBlockingIssue
                        ? "blocked until overlay repair"
                        : "proof + print after approved proof review",
                    localEntry?.ResolvedPath ?? "packaged manifest",
                    entry.Description ?? BuildTemplateNote(entry, isDefault, isFallback),
                    string.IsNullOrWhiteSpace(issueSummary)
                        ? (string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                            ? "Local saved overlay is currently visible in the workstation."
                            : "Packaged manifest entry remains available for rollback.")
                        : issueSummary,
                    isFallback
                        ? "Select this packaged entry directly or remove the winning local override."
                        : string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                            ? "Switch back to a packaged entry or save a corrected local overlay, then rerun proof."
                            : "Keep this packaged baseline visible for rollback discipline."));
        }

        foreach (var localEntry in snapshot.LocalEntries.Where((entry) => !catalogVersions.Contains(entry.Version)))
        {
            entries.Add(
                new TemplateCatalogRowModel(
                    localEntry.Version,
                    "local",
                    "draft",
                    "local catalog overlay",
                    "blocked until overlay repair",
                    localEntry.ResolvedPath,
                    "Local overlay entry is present in governance diagnostics but not currently surfaced as a dispatch-safe catalog entry.",
                    BuildIssueSummary(snapshot, localEntry.Version),
                    "Repair or remove the local overlay before treating it as production-safe."));
        }

        return entries
            .OrderByDescending((entry) => entry.State == "default")
            .ThenByDescending((entry) => entry.State == "fallback")
            .ThenBy((entry) => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildEntrySummary(IReadOnlyCollection<TemplateCatalogRowModel> entries)
    {
        var localCount = entries.Count((entry) => string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase));
        var draftCount = entries.Count((entry) => string.Equals(entry.State, "draft", StringComparison.OrdinalIgnoreCase));
        return $"{entries.Count} visible / {localCount} local / {draftCount} draft";
    }

    private static bool HasBlockingIssue(
        TemplateCatalogSnapshot snapshot,
        string version,
        TemplateCatalogLocalEntry? localEntry)
    {
        if (localEntry is not null && (!localEntry.Enabled || !localEntry.FileExists))
        {
            return true;
        }

        return snapshot.Issues.Any(
            (issue) =>
                issue.Message.Contains(version, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildIssueSummary(TemplateCatalogSnapshot snapshot, string version)
    {
        return string.Join(
            " ",
            snapshot.Issues
                .Where((issue) => issue.Message.Contains(version, StringComparison.OrdinalIgnoreCase))
                .Select((issue) => issue.Message));
    }

    private static string BuildTemplateNote(TemplateCatalogEntry entry, bool isDefault, bool isFallback)
    {
        if (isDefault)
        {
            return "Current effective default selected by the current catalog resolver.";
        }

        if (isFallback)
        {
            return "Packaged fallback kept visible so rollback is explicit.";
        }

        return string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
            ? "Saved local overlay is currently visible in the workstation."
            : "Packaged manifest entry remains available for operator selection.";
    }
}
