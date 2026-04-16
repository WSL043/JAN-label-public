namespace JanLabel.WindowsShell;

public static class TemplateLibraryCatalog
{
    public static void SeedHomePanel(TemplateLibraryPanelModel panel)
    {
        panel.HeaderDetail = "packaged manifest + desktop local overlay";
        panel.StatusSummary = "2 dispatch-safe / 1 draft / 1 rollback path";
        panel.ReplaceSummaryCards(
            new[]
            {
                new SummaryCardModel("Effective Default", "basic-50x30@v2", "Desktop local catalog currently wins over the packaged manifest."),
                new SummaryCardModel("Dispatch-Safe", "2", "Two saved entries are modeled as valid proof/print targets."),
                new SummaryCardModel("Save Required", "1", "One draft still needs a catalog save before any proof route."),
                new SummaryCardModel("Rollback", "packaged v1", "Fallback remains explicit instead of hidden behind the overlay."),
            });
        panel.LoadEntries(BuildEntries(), "basic-50x30@v2");
        panel.ReplaceGuidanceSections(
            new[]
            {
                new PropertySectionModel(
                    "Selection Rule",
                    "Operators should be able to answer which template wins and whether it can dispatch before opening the designer.",
                    new[]
                    {
                        new PropertyRowModel("Preferred default", "Saved local overlay when it intentionally replaces packaged state"),
                        new PropertyRowModel("Unsafe choice", "Draft-only entries that have not been saved into the local catalog"),
                        new PropertyRowModel("Rollback choice", "Packaged manifest default kept as an explicit operator escape hatch"),
                    }),
                new PropertySectionModel(
                    "Authority Boundary",
                    "Template reasoning belongs in the shell, but proof/print authority still belongs to desktop-shell.",
                    new[]
                    {
                        new PropertyRowModel("Catalog authority", "Saved local catalog + packaged manifest"),
                        new PropertyRowModel("Dispatch authority", "desktop-shell proof and print gate"),
                        new PropertyRowModel("Required proof step", "Any saved overlay change still expects a fresh proof review"),
                    }),
            });
        panel.ReplaceAlerts(
            new[]
            {
                new MessageRowModel("Warn", "catalog", "proof-ticket@v1 is still draft-only and must not be treated as a dispatch target."),
                new MessageRowModel("Info", "default", "basic-50x30@v2 currently overrides the packaged manifest default on this workstation."),
                new MessageRowModel("Info", "rollback", "basic-50x30@v1 remains the explicit packaged rollback path if the overlay must be backed out."),
            });
    }

    public static void SeedDesignerPanel(TemplateLibraryPanelModel panel)
    {
        panel.HeaderDetail = "selection must stay proof-safe";
        panel.StatusSummary = "overlay delta, save boundary, rollback all visible";
        panel.ReplaceSummaryCards(
            new[]
            {
                new SummaryCardModel("Working Format", "basic-50x30@v2", "Current document is tied to the local dispatch-safe overlay."),
                new SummaryCardModel("Draft Only", "proof-ticket@v1", "Draft experiments remain visible but blocked from dispatch."),
                new SummaryCardModel("Proof Impact", "re-proof", "Any saved overlay change still expects a fresh proof lineage."),
                new SummaryCardModel("Rollback", "basic-50x30@v1", "Packaged baseline is one explicit selection away."),
            });
        panel.LoadEntries(BuildEntries(), "basic-50x30@v2");
        panel.ReplaceGuidanceSections(
            new[]
            {
                new PropertySectionModel(
                    "Save Boundary",
                    "Live editor state is not production state until it becomes a saved catalog entry.",
                    new[]
                    {
                        new PropertyRowModel("Live draft", "Designer-only preview and property editing"),
                        new PropertyRowModel("Saved catalog", "Dispatch-safe candidate that can enter proof flow"),
                        new PropertyRowModel("Unsaved risk", "Operators may believe they changed dispatch when they only changed the draft"),
                    }),
                new PropertySectionModel(
                    "Proof Eligibility",
                    "Template choice is only production-usable when its route and rollback path are explicit.",
                    new[]
                    {
                        new PropertyRowModel("Proof route", "Saved local or packaged entry with clear authority"),
                        new PropertyRowModel("Print route", "Still gated by approved proof lineage in desktop-shell"),
                        new PropertyRowModel("Rollback discipline", "Switch default or select packaged fallback before re-running proof"),
                    }),
            });
        panel.ReplaceAlerts(
            new[]
            {
                new MessageRowModel("Warn", "designer", "Local overrides should trigger a new proof even when the visual diff looks small."),
                new MessageRowModel("Info", "catalog", "Selected template detail now shows authority owner, dispatch route, and rollback path in one place."),
                new MessageRowModel("Info", "default", "basic-50x30@v2 is currently modeled as the default winner for this operator shell."),
            });
    }

    private static TemplateCatalogRowModel[] BuildEntries()
    {
        return new[]
        {
            new TemplateCatalogRowModel(
                "basic-50x30@v2",
                "local",
                "default",
                "desktop local catalog overlay",
                "proof + print after proof approval",
                "2026-04-16 14:24",
                "Current handheld label default for manual and batch work.",
                "Overrides the packaged basic-50x30@v1 baseline on this workstation.",
                "Switch default back to packaged basic-50x30@v1, then rerun proof."),
            new TemplateCatalogRowModel(
                "basic-50x30@v1",
                "packaged",
                "fallback",
                "repo manifest default",
                "proof + print after proof approval",
                "shipped baseline",
                "Regression baseline and rollback target kept visible to operators.",
                "Still matches the packaged template manifest committed in the repository.",
                "Select this entry directly or remove the local v2 override."),
            new TemplateCatalogRowModel(
                "proof-ticket@v1",
                "local",
                "draft",
                "designer draft only",
                "blocked until save",
                "2026-04-16 14:11",
                "Draft proof format for review experiments; not dispatch-safe yet.",
                "Visible in the shell to prevent operators from confusing drafts with saved catalog authority.",
                "Save into the local catalog or discard the draft before routing proof."),
            new TemplateCatalogRowModel(
                "shipper-70x50@v1",
                "packaged",
                "stable",
                "packaged library",
                "proof candidate / batch-ready after selection",
                "release baseline",
                "Larger carton label kept available for batch operator scenarios.",
                "No local overlay is winning for this version, so packaged behavior remains authoritative.",
                "Continue using the packaged entry or introduce a saved local override with a fresh proof."),
        };
    }
}
