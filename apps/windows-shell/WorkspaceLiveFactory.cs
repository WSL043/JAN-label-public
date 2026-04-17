using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace JanLabel.WindowsShell;

public static class WorkspaceLiveFactory
{
    public static void LoadModules(ObservableCollection<ModuleModel> modules, ShellWorkspaceSnapshot snapshot)
    {
        modules.Clear();
        modules.Add(BuildHomeModule(snapshot));
        modules.Add(BuildDesignerModule(snapshot));
        modules.Add(BuildPrintConsoleModule(snapshot));
        modules.Add(BuildBatchJobsModule(snapshot));
        modules.Add(BuildHistoryModule(snapshot));
    }

    private static ModuleModel BuildHomeModule(ShellWorkspaceSnapshot snapshot)
    {
        var workspace = BuildHomeWorkspace(snapshot);
        return new ModuleModel(
            "Home",
            "Companion-backed migration and session status",
            "live",
            "Live desktop-shell companion status for bridge, catalog, governance, and preview.",
            "desktop-shell remains the proof, audit, and catalog authority while the native shell reads and surfaces the current workstation state.",
            new[]
            {
                ShellActionModel.Enabled(ShellActions.RefreshState),
                ShellActionModel.Disabled(ShellActions.OpenHandoff, "Handoff notes stay in the repo until a native-shell document opener is wired."),
                ShellActionModel.Enabled(ShellActions.ViewPreview),
            },
            new[]
            {
                new RibbonGroupModel(
                    "Session",
                    ShellActionModel.Enabled(ShellActions.RefreshState),
                    ShellActionModel.Disabled(ShellActions.OpenHandoff, "Handoff notes stay in the repo until a native-shell document opener is wired.")),
                new RibbonGroupModel(
                    "Templates",
                    ShellActionModel.Enabled(ShellActions.OpenLibrary, "Refresh and focus the live template-library board from desktop-shell."),
                    ShellActionModel.Enabled(ShellActions.OverlayStatus, "Refresh and focus overlay winner, source, and dispatch-safety state."),
                    ShellActionModel.Enabled(ShellActions.CatalogRules, "Refresh and focus catalog-governance context from desktop-shell.")),
                new RibbonGroupModel("Preview", ShellActions.ViewPreview, ShellActions.RustPreview),
            },
            new[]
            {
                new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                new ContextBadgeModel("Mode", "read + safe ops", Brushes.ForestGreen),
                new ContextBadgeModel("Release", "v0.3.0", Brushes.DarkGoldenrod),
            },
            new[]
            {
                new StatusStripItemModel("Mode", "Companion dashboard"),
                new StatusStripItemModel("Bridge", snapshot.BridgeStatus.PrintAdapterKind),
                new StatusStripItemModel("Catalog", snapshot.TemplateCatalog.DefaultTemplateVersion),
                new StatusStripItemModel("Backend", "desktop-shell authority"),
            },
            workspace);
    }

    private static ModuleModel BuildDesignerModule(ShellWorkspaceSnapshot snapshot)
    {
        var workspace = BuildDesignerWorkspace(snapshot);
        return new ModuleModel(
            "Designer",
            "Live catalog, governance, and preview surface",
            "preview",
            "Designer chrome remains WPF-native, but preview and catalog authority now come from the desktop-shell companion.",
            "Live preview, catalog state, and proof-safe authority are visible without moving save or print ownership out of desktop-shell.",
            new[]
            {
                ShellActionModel.Disabled(ShellActions.NewFormat, "Designer authoring stays local to the shell frame in v0.3.0."),
                ShellActionModel.Enabled(ShellActions.PrintPreview),
                ShellActionModel.Disabled(ShellActions.RunProof, "Proof generation remains owned by apps/desktop-shell in v0.3.0."),
            },
            new[]
            {
                new RibbonGroupModel(
                    "Clipboard",
                    DisabledDesignerPlaceholder(ShellActions.Paste),
                    DisabledDesignerPlaceholder(ShellActions.Duplicate),
                    DisabledDesignerPlaceholder(ShellActions.Delete)),
                new RibbonGroupModel(
                    "Insert",
                    DisabledDesignerPlaceholder(ShellActions.Text),
                    DisabledDesignerPlaceholder(ShellActions.Barcode),
                    DisabledDesignerPlaceholder(ShellActions.Line),
                    DisabledDesignerPlaceholder(ShellActions.Box)),
                new RibbonGroupModel(
                    "Arrange",
                    DisabledDesignerPlaceholder(ShellActions.AlignLeft),
                    DisabledDesignerPlaceholder(ShellActions.MakeSameSize),
                    DisabledDesignerPlaceholder(ShellActions.Snap)),
                new RibbonGroupModel(
                    "Data",
                    DisabledDesignerPlaceholder(ShellActions.RecordBrowser),
                    DisabledDesignerPlaceholder(ShellActions.QueryPrompt),
                    DisabledDesignerPlaceholder(ShellActions.NamedDataSources)),
                new RibbonGroupModel(
                    "Validate",
                    ShellActionModel.Enabled(ShellActions.RustPreview),
                    ShellActionModel.Disabled(ShellActions.SaveToCatalog, "Template write-back remains owned by apps/desktop-shell in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.RunProof, "Proof generation remains owned by apps/desktop-shell in v0.3.0.")),
            },
            new[]
            {
                new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                new ContextBadgeModel("Catalog", snapshot.TemplateGovernance.EffectiveDefaultSource, Brushes.DarkGoldenrod),
                new ContextBadgeModel("Output", "SVG / PDF", Brushes.ForestGreen),
            },
            new[]
            {
                new StatusStripItemModel("Mode", "Designer"),
                new StatusStripItemModel("Template", snapshot.PreviewTemplateVersion),
                new StatusStripItemModel("Preview", snapshot.PreviewStatus),
                new StatusStripItemModel("Proof gate", "desktop-shell"),
            },
            workspace);
    }

    private static ModuleModel BuildPrintConsoleModule(ShellWorkspaceSnapshot snapshot)
    {
        var workspace = BuildPrintConsoleWorkspace(snapshot);
        var pendingProofs = snapshot.AuditSearch.Entries.Count((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase));
        return new ModuleModel(
            "Print Console",
            "Live proof and dispatch subject lane",
            pendingProofs == 0 ? "live" : $"{pendingProofs} pending",
            "This lane is now driven from live proof and audit data, not a seeded dispatch queue.",
            "The shell shows current proof state and recent dispatch history, but print dispatch itself remains in apps/desktop-shell for v0.3.0.",
            new[]
            {
                ShellActionModel.Enabled(ShellActions.RefreshSubjects),
                ShellActionModel.Enabled(ShellActions.ApproveProof),
                ShellActionModel.Disabled(ShellActions.DispatchBatch, "Direct print dispatch remains owned by apps/desktop-shell in v0.3.0."),
            },
            new[]
            {
                new RibbonGroupModel(
                    "Subjects",
                    ShellActionModel.Enabled(ShellActions.RefreshSubjects),
                    ShellActionModel.Disabled(ShellActions.Hold, "Queue mutation is not exposed directly from the native shell in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.Release, "Queue mutation is not exposed directly from the native shell in v0.3.0.")),
                new RibbonGroupModel(
                    "Proof",
                    ShellActionModel.Disabled(ShellActions.OpenPdf, "Artifact opening is not yet wired directly from the native shell."),
                    ShellActionModel.Enabled(ShellActions.Approve),
                    ShellActionModel.Enabled(ShellActions.Reject)),
                new RibbonGroupModel(
                    "Dispatch",
                    ShellActionModel.Enabled(ShellActions.RouteCheck, "Refresh and focus the current proof subject, bridge route, and print guardrails."),
                    ShellActionModel.Disabled(ShellActions.RunProof, "Proof generation remains owned by apps/desktop-shell in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.Print, "Direct print dispatch remains owned by apps/desktop-shell in v0.3.0.")),
            },
            new[]
            {
                new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                new ContextBadgeModel("Subjects", "audit-derived", Brushes.DarkGoldenrod),
                new ContextBadgeModel("Route", snapshot.BridgeStatus.PrintAdapterKind, Brushes.ForestGreen),
            },
            new[]
            {
                new StatusStripItemModel("Mode", "Print Console"),
                new StatusStripItemModel("Bridge", snapshot.BridgeStatus.PrintAdapterKind),
                new StatusStripItemModel("Queue", "audit-derived"),
                new StatusStripItemModel("Dispatch", "desktop-shell only"),
            },
            workspace);
    }

    private static ModuleModel BuildBatchJobsModule(ShellWorkspaceSnapshot snapshot)
    {
        var workspace = BuildBatchJobsWorkspace(snapshot);
        var queueRows = snapshot.BatchQueueSnapshot.Snapshot?.QueueRows ?? new List<BatchQueueRowDto>();
        var readyCount = queueRows.Count((row) => string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "ready", StringComparison.OrdinalIgnoreCase));
        var failedCount = queueRows.Count((row) => string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "failed", StringComparison.OrdinalIgnoreCase));
        var snapshotBadge = snapshot.BatchQueueSnapshot.Present
            ? $"{queueRows.Count} row{(queueRows.Count == 1 ? string.Empty : "s")}"
            : "waiting";
        return new ModuleModel(
            "Batch Jobs",
            "Shared batch staging snapshot",
            snapshotBadge,
            "Batch Jobs now reads the desktop-shell shared batch snapshot while leaving import, retry, and submit ownership outside WPF.",
            "The native shell can review live queued rows, submit state, and blockers from the shared snapshot, but direct batch mutation remains guarded in apps/admin-web and apps/desktop-shell for v0.3.0.",
            new[]
            {
                ShellActionModel.Enabled(ShellActions.QueueSnapshot, "Refresh the shared batch snapshot from desktop-shell."),
                ShellActionModel.Disabled(ShellActions.RetryFailed, "Retry still runs through admin-web even though the shared snapshot is now visible here."),
                ShellActionModel.Disabled(ShellActions.ImportWorkbook, "Batch import remains owned by the transitional admin-web path in v0.3.0."),
            },
            new[]
            {
                new RibbonGroupModel(
                    "Import",
                    ShellActionModel.Disabled(ShellActions.Csv, "Batch import authority remains transitional in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.Xlsx, "Batch import authority remains transitional in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.AliasMap, "Batch import authority remains transitional in v0.3.0.")),
                new RibbonGroupModel(
                    "Queue",
                    ShellActionModel.Disabled(ShellActions.SubmitReady, "Direct batch submission is not exposed from the native shell in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.RetryFailed, "Retry still runs through admin-web even though the shared snapshot is now visible here."),
                    ShellActionModel.Enabled(ShellActions.QueueSnapshot, "Refresh and focus the desktop-shell shared batch snapshot.")),
                new RibbonGroupModel(
                    "Validation",
                    ShellActionModel.Disabled(ShellActions.FixtureCheck, "Batch validation remains transitional in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.UnknownTemplate, "Batch validation remains transitional in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.JanWarnings, "Batch validation remains transitional in v0.3.0.")),
            },
            new[]
            {
                new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                new ContextBadgeModel("Snapshot", snapshot.BatchQueueSnapshot.Present ? "live" : "empty", snapshot.BatchQueueSnapshot.Present ? Brushes.ForestGreen : Brushes.DarkGoldenrod),
                new ContextBadgeModel("Guardrail", failedCount > 0 ? "review blockers" : "read-only", failedCount > 0 ? Brushes.Firebrick : Brushes.DarkGoldenrod),
            },
            new[]
            {
                new StatusStripItemModel("Mode", "Batch snapshot"),
                new StatusStripItemModel("Ready", readyCount.ToString(CultureInfo.InvariantCulture)),
                new StatusStripItemModel("Failed", failedCount.ToString(CultureInfo.InvariantCulture)),
                new StatusStripItemModel("Mutation", "desktop-shell only"),
            },
            workspace);
    }

    private static ModuleModel BuildHistoryModule(ShellWorkspaceSnapshot snapshot)
    {
        var workspace = BuildHistoryWorkspace(snapshot);
        var pendingProofs = snapshot.AuditSearch.Entries.Count((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase));
        return new ModuleModel(
            "History",
            "Live proof review and audit lane",
            pendingProofs == 0 ? "live" : $"{pendingProofs} pending",
            "History now consumes live audit search, export, and bundle-list data from the desktop-shell companion.",
            "Approve / reject / export now use safe companion commands, while restore and destructive retention remain explicitly out of scope in v0.3.0.",
            new[]
            {
                ShellActionModel.Enabled(ShellActions.RefreshAudit),
                ShellActionModel.Enabled(ShellActions.ExportAudit),
                ShellActionModel.Disabled(ShellActions.TrimRetention, "Destructive retention apply is not exposed from the native shell in v0.3.0."),
            },
            new[]
            {
                new RibbonGroupModel(
                    "Review",
                    ShellActionModel.Enabled(ShellActions.ApproveProof),
                    ShellActionModel.Enabled(ShellActions.RejectProof),
                    ShellActionModel.Disabled(ShellActions.PinArtifact, "Artifact pinning is not yet wired directly from the native shell.")),
                new RibbonGroupModel(
                    "Audit",
                    ShellActionModel.Enabled(ShellActions.RefreshAudit),
                    ShellActionModel.Enabled(ShellActions.ExportAudit),
                    ShellActionModel.Disabled(ShellActions.RetentionDryRun, "Retention commands remain owned by apps/desktop-shell in v0.3.0.")),
                new RibbonGroupModel(
                    "Restore",
                    ShellActionModel.Enabled(ShellActions.ListBundles, "Refresh and focus desktop-shell audit backup bundle inventory."),
                    ShellActionModel.Disabled(ShellActions.ValidateBundle, "Restore validation stays guarded in apps/desktop-shell."),
                    ShellActionModel.Disabled(ShellActions.Restore, "Audit restore remains owned by apps/desktop-shell in v0.3.0.")),
            },
            new[]
            {
                new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                new ContextBadgeModel("Export", "safe", Brushes.ForestGreen),
                new ContextBadgeModel("Restore", "guarded", Brushes.DarkGoldenrod),
            },
            new[]
            {
                new StatusStripItemModel("Mode", "History / audit"),
                new StatusStripItemModel("Proof inbox", pendingProofs.ToString(CultureInfo.InvariantCulture)),
                new StatusStripItemModel("Bundles", snapshot.AuditBackupBundles.Count.ToString(CultureInfo.InvariantCulture)),
                new StatusStripItemModel("Restore", "desktop-shell only"),
            },
            workspace);
    }

    private static HomeWorkspaceModel BuildHomeWorkspace(ShellWorkspaceSnapshot snapshot)
    {
        var activityRows = BuildHomeActivityRows(snapshot).ToArray();
        var model = new HomeWorkspaceModel
        {
            HeaderDetail = "desktop-shell companion live snapshot",
            StatusSummary = "desktop-shell companion live snapshot",
            ActivitySummary = $"latest {activityRows.Length} live event(s)",
        };

        var templateEntries = BuildTemplateEntries(snapshot);
        var warningCount = snapshot.BridgeStatus.WarningDetails.Count + snapshot.TemplateGovernance.Issues.Count;
        model.SummaryCards.Add(new SummaryCardModel("Bridge", snapshot.BridgeStatus.PrintAdapterKind, $"Adapters: {string.Join(", ", snapshot.BridgeStatus.AvailableAdapters)}"));
        model.SummaryCards.Add(new SummaryCardModel("Catalog", snapshot.TemplateCatalog.DefaultTemplateVersion, $"{snapshot.TemplateCatalog.Templates.Count} visible entries from desktop-shell."));
        model.SummaryCards.Add(new SummaryCardModel("Governance", warningCount.ToString(CultureInfo.InvariantCulture), "Bridge warnings and overlay issues surfaced from desktop-shell."));
        model.SummaryCards.Add(new SummaryCardModel("Preview", BuildPreviewHeadline(snapshot), snapshot.PreviewMessage));

        model.SessionRows.Add(new PropertyRowModel("Default template", snapshot.TemplateCatalog.DefaultTemplateVersion));
        model.SessionRows.Add(new PropertyRowModel("Adapter", snapshot.BridgeStatus.PrintAdapterKind));
        model.SessionRows.Add(new PropertyRowModel("Audit log", snapshot.BridgeStatus.AuditLogDir));
        model.SessionRows.Add(new PropertyRowModel("Backup dir", snapshot.BridgeStatus.AuditBackupDir));
        model.SessionRows.Add(new PropertyRowModel("Proof dir", snapshot.BridgeStatus.ProofOutputDir));
        model.SessionRows.Add(new PropertyRowModel("Print dir", snapshot.BridgeStatus.PrintOutputDir));
        model.SessionRows.Add(new PropertyRowModel("Zint", snapshot.BridgeStatus.ResolvedZintPath));

        ConfigureTemplatePanel(
            model.TemplateLibrary,
            templateEntries,
            snapshot.TemplateCatalog.DefaultTemplateVersion,
            "desktop-shell packaged manifest + local overlay",
            $"{templateEntries.Count((entry) => !entry.Dispatch.Contains("blocked", StringComparison.OrdinalIgnoreCase))} dispatch-safe / {templateEntries.Count((entry) => entry.State == "draft")} blocked");

        model.ActivityRows.AddRange(activityRows);
        model.ControlSections.Add(
            new PropertySectionModel(
                "Authority Boundary",
                "The Windows shell now reads live bridge, catalog, governance, preview, and audit state from apps/desktop-shell without taking ownership away from it.",
                new[]
                {
                    new PropertyRowModel("Proof / print gate", "apps/desktop-shell"),
                    new PropertyRowModel("Catalog authority", "apps/desktop-shell local overlay + packaged manifest"),
                    new PropertyRowModel("Direct write-back", "deferred after v0.3.0"),
                }));
        model.ControlSections.Add(
            new PropertySectionModel(
                "Current Live Lanes",
                "These lanes are now companion-backed in the native shell.",
                new[]
                {
                    new PropertyRowModel("Home", "bridge + catalog + governance + preview"),
                    new PropertyRowModel("Batch Jobs", "shared batch snapshot visibility"),
                    new PropertyRowModel("Print Console", "proof and audit-derived subject visibility"),
                    new PropertyRowModel("History", "proof review, export, bundle listing"),
                }));

        model.NextSteps.Add(new QueueItemModel("Refresh live state", "Re-query bridge, catalog, preview, and audit data from desktop-shell.", "live", "Use this before making release calls on v0.3.0 readiness."));
        model.NextSteps.Add(new QueueItemModel("Review pending proofs", "Move into Print Console or History and use safe approve / reject operations.", "proof", "Pending proofs are the live blocker that can actually move from this shell."));
        model.NextSteps.Add(new QueueItemModel("Review shared batch snapshot", "Open Batch Jobs to inspect the current admin-web queue snapshot without taking submit ownership away from desktop-shell.", "batch", "Batch visibility is live now, but import / retry / submit remain guarded elsewhere."));

        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", $"Current adapter: {snapshot.BridgeStatus.PrintAdapterKind}", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Catalog", snapshot.TemplateCatalog.DefaultTemplateVersion, $"Effective source: {snapshot.TemplateGovernance.EffectiveDefaultSource}", "LIVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Governance", snapshot.TemplateGovernance.ManifestStatus, $"{snapshot.TemplateGovernance.Issues.Count} surfaced issues", warningCount == 0 ? "OK" : "WATCH", warningCount == 0 ? Brushes.ForestGreen : Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Preview", snapshot.PreviewStatus, snapshot.PreviewMessage, BuildPreviewStatusCode(snapshot), ResolvePreviewAccent(snapshot)));
        return model;
    }

    private static DesignerWorkspaceModel BuildDesignerWorkspace(ShellWorkspaceSnapshot snapshot)
    {
        var toolboxGroups = new[]
        {
            new ToolboxGroupModel("Objects", new[] { new ToolboxItemModel("Text", "A"), new ToolboxItemModel("Barcode", "JAN"), new ToolboxItemModel("Counter", "#"), new ToolboxItemModel("Picture", "IMG") }),
            new ToolboxGroupModel("Guides", new[] { new ToolboxItemModel("Margins", "4 mm"), new ToolboxItemModel("Grid", "2 mm"), new ToolboxItemModel("Snap", "On") }),
        };
        var objectNodes = new[]
        {
            new ObjectNodeModel("Label Format", snapshot.PreviewTemplateVersion, new[] { new ObjectNodeModel("Static Layer", "3 objects", new[] { new ObjectNodeModel("Brand mark", "Text"), new ObjectNodeModel("Frame", "Box"), new ObjectNodeModel("Divider", "Line") }), new ObjectNodeModel("Data Layer", "4 objects", new[] { new ObjectNodeModel("Product name", "{sku}"), new ObjectNodeModel("JAN barcode", "{jan}"), new ObjectNodeModel("JAN text", "{jan}"), new ObjectNodeModel("Quantity", "{qty}") }) }),
        };
        var dataSources = new[]
        {
            new DataSourceRowModel("sku", "Text", "200-145-3"),
            new DataSourceRowModel("jan", "JAN", snapshot.Preview?.NormalizedJan ?? "4901234567894"),
            new DataSourceRowModel("qty", "Number", "24"),
            new DataSourceRowModel("brand", "Text", "JAN-LAB"),
            new DataSourceRowModel("template_version", "Text", snapshot.PreviewTemplateVersion),
            new DataSourceRowModel("proof_mode", "Expr", "preview"),
        };
        var model = new DesignerWorkspaceModel
        {
            CanvasMeta = $"{snapshot.PreviewTemplateVersion} | {snapshot.BridgeStatus.PrintAdapterKind} | {snapshot.PreviewStatus} preview",
            CanvasHint = "Canvas interaction stays local to the WPF shell; preview and authority state now come from apps/desktop-shell.",
            MessageSummary = $"{snapshot.TemplateGovernance.Issues.Count} governance issue(s) / {BuildPreviewSummary(snapshot)}",
            RecordSummary = "live sample record",
            StatusSummary = "desktop-shell companion drives preview, bridge, and catalog authority",
            CatalogSummary = $"{snapshot.TemplateGovernance.EffectiveDefaultSource} default / {snapshot.TemplateCatalog.Templates.Count} visible entries",
            ToolboxSummary = $"{toolboxGroups.Length} groups / {toolboxGroups.Sum((group) => group.Items.Count)} tools",
            ObjectBrowserSummary = $"{CountDesignerLeafNodes(objectNodes)} design objects / {CountDesignerLayerNodes(objectNodes)} layers",
            DataSourceSummary = $"{dataSources.Length} mapped fields",
            CanvasWidth = 660,
            CanvasHeight = 410,
            PrimaryDocumentTitle = snapshot.PreviewTemplateVersion,
            SecondaryDocumentTitle = snapshot.Preview?.LabelName ?? $"{snapshot.PreviewStatus}-preview",
            PreviewSvg = snapshot.Preview?.Svg ?? snapshot.PreviewError ?? "Preview was unavailable.",
        };

        foreach (var toolboxGroup in toolboxGroups)
        {
            model.ToolboxGroups.Add(toolboxGroup);
        }

        ConfigureTemplatePanel(
            model.TemplateLibrary,
            BuildTemplateEntries(snapshot),
            snapshot.TemplateCatalog.DefaultTemplateVersion,
            "desktop-shell catalog authority",
            $"{snapshot.TemplateGovernance.ManifestStatus} / {snapshot.TemplateGovernance.Issues.Count} surfaced issue(s)");

        foreach (var objectNode in objectNodes)
        {
            model.ObjectNodes.Add(objectNode);
        }

        foreach (var dataSource in dataSources)
        {
            model.DataSources.Add(dataSource);
        }

        model.DocumentTabs.Add(new DocumentTabModel(model.PrimaryDocumentTitle, "companion-backed format"));
        model.DocumentTabs.Add(new DocumentTabModel(model.SecondaryDocumentTitle, $"{snapshot.PreviewStatus} preview"));

        model.CanvasElements.Add(new CanvasElementModel("BRAND", "JAN-LAB", 30, 22, 120, 32, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("SKU", "200-145-3", 30, 72, 180, 36, 18, false));
        model.CanvasElements.Add(new CanvasElementModel("BARCODE", "| ||| || ||| | ||", 28, 132, 320, 102, 16, false));
        model.CanvasElements.Add(new CanvasElementModel("JAN", snapshot.Preview?.NormalizedJan ?? "4901234567894", 50, 244, 220, 26, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("QTY", "24 PCS", 470, 38, 120, 40, 20, false));
        model.CanvasElements.Add(new CanvasElementModel("STATUS", BuildPreviewCanvasStatus(snapshot), 360, 304, 240, 32, 13, false));

        model.SetupRows.Add(new PropertyRowModel("Document", snapshot.PreviewTemplateVersion));
        model.SetupRows.Add(new PropertyRowModel("Effective default", snapshot.TemplateCatalog.DefaultTemplateVersion));
        model.SetupRows.Add(new PropertyRowModel("Catalog source", snapshot.TemplateGovernance.EffectiveDefaultSource));
        model.SetupRows.Add(new PropertyRowModel("Preview route", "desktop-shell companion"));
        model.SetupRows.Add(new PropertyRowModel("Preview source", snapshot.PreviewSource));
        model.SetupRows.Add(new PropertyRowModel("Proof authority", "apps/desktop-shell"));
        model.SetupRows.Add(new PropertyRowModel("Adapter", snapshot.BridgeStatus.PrintAdapterKind));

        model.PropertySections.Add(new PropertySectionModel("Selected Object", "Property-grid editing remains local to the shell frame.", new[] { new PropertyRowModel("Name", "JAN barcode"), new PropertyRowModel("Binding", "{jan}"), new PropertyRowModel("Symbology", "EAN-13 / JAN"), new PropertyRowModel("Position", "28,132"), new PropertyRowModel("Size", "320 x 102") }));
        model.PropertySections.Add(new PropertySectionModel("Live Preview Contract", "This preview now comes from desktop-shell companion mode, not from a seeded mock.", new[] { new PropertyRowModel("Preview command", "preview_template_draft"), new PropertyRowModel("Output", "SVG / PDF release scope"), new PropertyRowModel("Template source", snapshot.PreviewTemplateVersion), new PropertyRowModel("Preview source", snapshot.PreviewSource), new PropertyRowModel("Preview status", snapshot.PreviewStatus) }));
        model.PropertySections.Add(new PropertySectionModel("Authority Boundary", "Saving and proof generation remain outside the native shell for v0.3.0.", new[] { new PropertyRowModel("Save to catalog", "desktop-shell only"), new PropertyRowModel("Proof generation", "desktop-shell only"), new PropertyRowModel("Print gate", "approved proof lineage") }));

        model.RecordRows.Add(new PropertyRowModel("SKU", "200-145-3"));
        model.RecordRows.Add(new PropertyRowModel("JAN", snapshot.Preview?.NormalizedJan ?? "4901234567894"));
        model.RecordRows.Add(new PropertyRowModel("Brand", "JAN-LAB"));
        model.RecordRows.Add(new PropertyRowModel("Qty", "24"));
        model.RecordRows.Add(new PropertyRowModel("Template", snapshot.PreviewTemplateVersion));

        model.PreviewRows.Add(new PropertyRowModel("Template", snapshot.Preview?.TemplateVersion ?? snapshot.PreviewTemplateVersion));
        model.PreviewRows.Add(new PropertyRowModel("Label", snapshot.Preview?.LabelName ?? "preview unavailable"));
        model.PreviewRows.Add(new PropertyRowModel("JAN", snapshot.Preview?.NormalizedJan ?? "n/a"));
        model.PreviewRows.Add(new PropertyRowModel("Page", snapshot.Preview is null ? "n/a" : $"{snapshot.Preview.PageWidthMm:0.#} x {snapshot.Preview.PageHeightMm:0.#} mm"));
        model.PreviewRows.Add(new PropertyRowModel("Fields", snapshot.Preview?.FieldCount.ToString(CultureInfo.InvariantCulture) ?? "0"));

        model.MessageRows.Add(new MessageRowModel(MapPreviewMessageLevel(snapshot), "preview", snapshot.PreviewMessage));
        foreach (var issue in snapshot.TemplateGovernance.Issues.Take(3))
        {
            model.MessageRows.Add(new MessageRowModel(MapIssueLevel(issue.Severity), "catalog", issue.Message));
        }

        if (model.MessageRows.Count == 1)
        {
            model.MessageRows.Add(new MessageRowModel("Info", "catalog", "No additional template governance issues are currently surfaced."));
        }

        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", $"Adapter: {snapshot.BridgeStatus.PrintAdapterKind}", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Catalog", snapshot.TemplateCatalog.DefaultTemplateVersion, $"Source: {snapshot.TemplateGovernance.EffectiveDefaultSource}", "LIVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Preview", snapshot.PreviewStatus, snapshot.PreviewMessage, BuildPreviewStatusCode(snapshot), ResolvePreviewAccent(snapshot)));
        model.StatusItems.Add(new StatusItemModel("Proof authority", "desktop-shell", "Save and proof remain outside the native shell in v0.3.0.", "LOCK", Brushes.Firebrick));

        AddRulers(model.TopRulerMarks, model.SideRulerMarks);
        model.SelectCanvasElement(model.CanvasElements[2]);
        return model;
    }

    private static PrintConsoleWorkspaceModel BuildPrintConsoleWorkspace(ShellWorkspaceSnapshot snapshot)
    {
        var proofQueue = BuildProofQueue(snapshot).ToArray();
        var printJobs = BuildPrintConsoleJobs(snapshot).Take(8).ToArray();
        var timelineRows = BuildAuditActivityRows(snapshot).Take(6).ToArray();
        var model = new PrintConsoleWorkspaceModel
        {
            MessageSummary = $"{snapshot.AuditSearch.Entries.Count((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase))} pending proof(s) / direct dispatch disabled",
            FooterDetail = "live proof + audit state from desktop-shell companion",
            JobSummary = $"{snapshot.AuditSearch.Entries.Select((entry) => entry.Dispatch.MatchSubject.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count()} recent subjects",
            ProofQueueSummary = $"{proofQueue.Length} companion-backed proof(s)",
            TimelineSummary = $"{timelineRows.Length} recent audit-derived event(s)",
        };

        foreach (var proof in proofQueue)
        {
            model.ProofQueue.Add(proof);
        }

        model.RouteRows.Add(new PropertyRowModel("Primary route", snapshot.BridgeStatus.PrintAdapterKind));
        model.RouteRows.Add(new PropertyRowModel("Proof authority", "apps/desktop-shell"));
        model.RouteRows.Add(new PropertyRowModel("Dispatch authority", "apps/desktop-shell"));
        model.RouteRows.Add(new PropertyRowModel("Audit source", snapshot.BridgeStatus.AuditLogDir));

        foreach (var job in printJobs)
        {
            model.JobRows.Add(job);
        }

        foreach (var activity in timelineRows)
        {
            model.TimelineRows.Add(activity);
        }

        model.ControlSections.Add(new PropertySectionModel("Scope for v0.3.0", "This lane is live for reading proof / audit state and safe proof review operations only.", new[] { new PropertyRowModel("Live data", "print_bridge_status + search_audit_log"), new PropertyRowModel("Safe actions", "approve_proof / reject_proof"), new PropertyRowModel("Direct print", "disabled in native shell") }));
        model.ControlSections.Add(new PropertySectionModel("Authority Boundary", "Recent dispatch history is visible here, but direct queue mutation is still outside this shell.", new[] { new PropertyRowModel("Subject source", "audit-derived subject view"), new PropertyRowModel("Proof gate", "approved proof lineage in desktop-shell"), new PropertyRowModel("Unsupported actions", "print, hold, release, run proof") }));

        model.MessageRows.Add(new MessageRowModel("Info", "bridge", $"Current bridge adapter is {snapshot.BridgeStatus.PrintAdapterKind}."));
        model.MessageRows.Add(new MessageRowModel("Warn", "dispatch", "Direct print dispatch stays disabled in the native shell for v0.3.0."));
        if (snapshot.AuditSearch.Entries.Any((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase)))
        {
            model.MessageRows.Add(new MessageRowModel("Warn", "proof", "Pending proofs are still blocking related dispatch subjects."));
        }
        else
        {
            model.MessageRows.Add(new MessageRowModel("Info", "proof", "No pending proofs are currently surfaced by desktop-shell."));
        }

        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", $"Warnings: {snapshot.BridgeStatus.WarningDetails.Count}", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Proof gate", "strict", "Approved proof lineage still gates print.", "LOCK", Brushes.Firebrick));
        model.StatusItems.Add(new StatusItemModel("Subject view", "audit-derived", "Recent proof and dispatch subjects only; not a live queue snapshot.", "LIVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Dispatch", "desktop-shell only", "Print / proof generation stay out of scope in v0.3.0.", "WATCH", Brushes.DarkGoldenrod));

        model.SelectedProof = model.ProofQueue.FirstOrDefault();
        if (model.SelectedProof is null)
        {
            model.SelectedJob = model.JobRows.FirstOrDefault();
        }

        return model;
    }

    private static BatchJobsWorkspaceModel BuildBatchJobsWorkspace(ShellWorkspaceSnapshot snapshot)
    {
        var batchSnapshot = snapshot.BatchQueueSnapshot.Snapshot;
        var batchSnapshotError = snapshot.BatchQueueSnapshot.ErrorMessage;
        var queueRows = batchSnapshot?.QueueRows ?? new List<BatchQueueRowDto>();
        var readyCount = queueRows.Count((row) => string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "ready", StringComparison.OrdinalIgnoreCase));
        var failedCount = queueRows.Count((row) => string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "failed", StringComparison.OrdinalIgnoreCase));
        var submittedCount = queueRows.Count((row) => string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "submitted", StringComparison.OrdinalIgnoreCase));
        var model = new BatchJobsWorkspaceModel
        {
            MessageSummary = batchSnapshot is null
                ? string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "desktop-shell shared batch snapshot is empty"
                    : "desktop-shell shared batch snapshot is temporarily unavailable"
                : $"{queueRows.Count} queued row(s) / {failedCount} blocker(s)",
            FooterDetail = batchSnapshot is null
                ? string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "waiting for admin-web to publish a shared batch snapshot"
                    : $"batch snapshot lane degraded: {batchSnapshotError}"
                : $"desktop-shell shared snapshot updated {batchSnapshot.UpdatedAt}",
            QueueSummary = batchSnapshot is null
                ? string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "no queued rows published"
                    : "batch snapshot unavailable"
                : $"{readyCount} ready / {submittedCount} submitted / {failedCount} failed",
            ImportSummary = batchSnapshot is null
                ? string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "shared snapshot not published"
                    : "shared snapshot refresh failed"
                : $"{batchSnapshot.SourceKind ?? "snapshot"} / {batchSnapshot.SourceFileName ?? "desktop-shell shared snapshot"}",
            ActivitySummary = batchSnapshot is null
                ? string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "publish a queue from admin-web to light this lane up"
                    : "live shell stayed up, but this lane could not load the shared batch snapshot"
                : batchSnapshot.SubmitMessage,
        };

        model.ColumnRows.Add(new PropertyRowModel("Snapshot route", string.IsNullOrWhiteSpace(snapshot.BatchQueueSnapshot.FilePath) ? "desktop-shell companion response" : snapshot.BatchQueueSnapshot.FilePath));
        model.ColumnRows.Add(new PropertyRowModel("Authority", "apps/admin-web via apps/desktop-shell"));
        model.ColumnRows.Add(new PropertyRowModel("Release scope", "read-only queue visibility in WPF"));
        model.ColumnRows.Add(new PropertyRowModel("Direct mutation", "disabled in native shell"));

        if (batchSnapshot is null)
        {
            model.ImportSessions.Add(new QueueItemModel(
                "shared-batch-snapshot",
                "awaiting published queue",
                string.IsNullOrWhiteSpace(batchSnapshotError) ? "empty" : "degraded",
                string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "No admin-web queue snapshot is currently published through desktop-shell."
                    : $"The shared batch snapshot could not be loaded: {batchSnapshotError}",
                "desktop-shell companion",
                "shared snapshot",
                string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "Publish a queue from admin-web before reviewing it here."
                    : "Review the snapshot file or rebuild the queue from admin-web before relying on this lane.",
                string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "Refresh this lane after admin-web snapshots a queue."
                    : "The rest of the live shell stayed up; only the batch lane is degraded."));

            model.ActivityRows.Add(new ActivityRowModel("now", "batch", string.IsNullOrWhiteSpace(batchSnapshotError) ? "No shared batch snapshot is currently published." : $"Shared batch snapshot load failed: {batchSnapshotError}", "watch"));
            model.ActivityRows.Add(new ActivityRowModel("scope", "guardrail", "Import, retry, and submit remain disabled in WPF.", "lock"));

            model.ControlSections.Add(new PropertySectionModel(
                "Shared Snapshot Contract",
                string.IsNullOrWhiteSpace(batchSnapshotError)
                    ? "This lane now waits on a desktop-shell-owned snapshot instead of seeded mock rows."
                    : "This lane reads a desktop-shell-owned snapshot, but the current refresh could not load that file.",
                new[]
                {
                    new PropertyRowModel("Published by", "apps/admin-web via desktop-shell"),
                    new PropertyRowModel("Read by", "apps/windows-shell companion"),
                    new PropertyRowModel("Direct submit", "not exposed in WPF"),
                }));
            model.ControlSections.Add(new PropertySectionModel(
                "Operator Guidance",
                "When this lane is empty, refresh after admin-web builds a batch snapshot.",
                new[]
                {
                    new PropertyRowModel("Use this lane for", "reviewing live queued rows and blockers"),
                    new PropertyRowModel("Do not use this lane for", "import, retry, or submit"),
                    new PropertyRowModel("Real mutation path", "apps/admin-web"),
                }));

            model.MessageRows.Add(new MessageRowModel("Info", "batch", "Batch Jobs now expects a shared snapshot from admin-web through desktop-shell."));
            model.MessageRows.Add(new MessageRowModel("Warn", "scope", "Import, submission, and retry remain disabled in the native shell for v0.3.0."));
            if (!string.IsNullOrWhiteSpace(batchSnapshotError))
            {
                model.MessageRows.Add(new MessageRowModel("Warn", "snapshot", $"Shared batch snapshot load failed: {batchSnapshotError}"));
            }

            model.StatusItems.Add(new StatusItemModel("Snapshot", string.IsNullOrWhiteSpace(batchSnapshotError) ? "empty" : "degraded", string.IsNullOrWhiteSpace(batchSnapshotError) ? "No shared batch snapshot is available yet." : "Shared batch snapshot refresh failed; the rest of the live shell remains available.", string.IsNullOrWhiteSpace(batchSnapshotError) ? "WAIT" : "WATCH", Brushes.DarkGoldenrod));
            model.StatusItems.Add(new StatusItemModel("Authority", "admin-web via desktop-shell", "Queue mutation stays in admin-web; desktop-shell remains the authority path for shared state.", "LOCK", Brushes.Firebrick));
            model.StatusItems.Add(new StatusItemModel("Mode", "read-only", "Review shared queued rows after admin-web publishes them.", "LIVE", Brushes.SteelBlue));

            model.SelectedImportSession = model.ImportSessions.FirstOrDefault();
            return model;
        }

        model.ImportSessions.Add(new QueueItemModel(
            batchSnapshot.SourceFileName ?? "shared-batch-snapshot",
            $"{batchSnapshot.SourceKind ?? "snapshot"} / {batchSnapshot.CapturedAt}",
            batchSnapshot.SubmitPhase,
            string.IsNullOrWhiteSpace(batchSnapshot.SubmitMessage)
                ? $"{queueRows.Count} queued row(s) are published through desktop-shell."
                : batchSnapshot.SubmitMessage,
            batchSnapshot.Actor,
            "desktop-shell shared snapshot",
            failedCount > 0 ? $"{failedCount} row(s) are blocked or failed." : "No blockers are currently surfaced.",
            readyCount > 0
                ? "Review ready rows here, then submit from admin-web when appropriate."
                : "Review submitted/failed rows and continue in admin-web for any mutation."));

        foreach (var row in queueRows.Select(BuildBatchRow))
        {
            model.BatchRows.Add(row);
        }

        model.ActivityRows.Add(new ActivityRowModel(batchSnapshot.CapturedAt, "snapshot", $"{queueRows.Count} queued row(s) captured from {batchSnapshot.SourceFileName ?? "desktop-shell"}.", "live"));
        model.ActivityRows.Add(new ActivityRowModel(batchSnapshot.UpdatedAt, "submit", string.IsNullOrWhiteSpace(batchSnapshot.SubmitMessage) ? "Submit state is idle." : batchSnapshot.SubmitMessage, batchSnapshot.SubmitPhase));
        if (failedCount > 0)
        {
            model.ActivityRows.Add(new ActivityRowModel("watch", "retry", $"{failedCount} queued row(s) need admin-web retry handling.", "watch"));
        }

        model.ControlSections.Add(new PropertySectionModel(
            "Shared Snapshot Contract",
            "This lane now reflects a live queue snapshot saved by admin-web through desktop-shell.",
            new[]
            {
                new PropertyRowModel("Snapshot id", batchSnapshot.SnapshotId),
                new PropertyRowModel("Captured", batchSnapshot.CapturedAt),
                new PropertyRowModel("Updated", batchSnapshot.UpdatedAt),
            }));
        model.ControlSections.Add(new PropertySectionModel(
            "Authority Boundary",
            "Queued rows are live here, but import, retry, and submit still happen outside WPF.",
            new[]
            {
                new PropertyRowModel("Import / rebuild", "apps/admin-web"),
                new PropertyRowModel("Retry / submit", "apps/admin-web"),
                new PropertyRowModel("Proof / print gate", "apps/desktop-shell"),
            }));

        model.MessageRows.Add(new MessageRowModel("Info", "batch", $"Shared snapshot route is {snapshot.BatchQueueSnapshot.FilePath}."));
        model.MessageRows.Add(new MessageRowModel("Warn", "scope", "Direct import, retry, and submit remain intentionally disabled in the native shell."));
        if (failedCount > 0)
        {
            model.MessageRows.Add(new MessageRowModel("Warn", "queue", $"{failedCount} queued row(s) need review before retry in admin-web."));
        }
        else if (readyCount > 0)
        {
            model.MessageRows.Add(new MessageRowModel("Info", "queue", $"{readyCount} queued row(s) are ready for admin-web submit."));
        }

        model.StatusItems.Add(new StatusItemModel("Snapshot", queueRows.Count.ToString(CultureInfo.InvariantCulture), "Shared queue rows currently published through desktop-shell.", "LIVE", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Submit state", batchSnapshot.SubmitPhase, string.IsNullOrWhiteSpace(batchSnapshot.SubmitMessage) ? "No submit message is currently recorded." : batchSnapshot.SubmitMessage, batchSnapshot.SubmitPhase == "error" ? "WATCH" : "INFO", batchSnapshot.SubmitPhase == "error" ? Brushes.DarkGoldenrod : Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Authority", "admin-web via desktop-shell", "Queued rows are visible here; import, retry, and submit remain outside WPF.", "LOCK", Brushes.Firebrick));

        model.SelectedBatch = model.BatchRows.FirstOrDefault((row) => string.Equals(row.CanonicalStatus, "failed", StringComparison.OrdinalIgnoreCase))
            ?? model.BatchRows.FirstOrDefault((row) => string.Equals(row.CanonicalStatus, "ready", StringComparison.OrdinalIgnoreCase))
            ?? model.BatchRows.FirstOrDefault();
        return model;
    }

    private static BatchRowModel BuildBatchRow(BatchQueueRowDto row)
    {
        var canonicalStatus = NormalizeBatchStatus(row.SubmissionStatus);
        var draft = row.Draft;
        var templateVersion = string.IsNullOrWhiteSpace(draft.Template.Id) || string.IsNullOrWhiteSpace(draft.Template.Version)
            ? "unknown-template"
            : $"{draft.Template.Id}@{draft.Template.Version}";
        var route = row.DispatchResult?.Submission.AdapterKind
            ?? (string.IsNullOrWhiteSpace(draft.PrinterProfile.Adapter) ? "desktop-shell" : draft.PrinterProfile.Adapter);
        var note = BuildBatchRowNote(row);
        return new BatchRowModel(
            draft.JobId,
            templateVersion,
            "1 row",
            canonicalStatus,
            note,
            canonicalStatus == "ready" ? "1" : "0",
            canonicalStatus == "failed" ? "1" : "0",
            route,
            row.DispatchError ?? (canonicalStatus == "failed" ? "Review and retry from admin-web." : "No blocker surfaced."),
            BuildBatchRetryRule(row, canonicalStatus),
            canonicalStatus);
    }

    private static string BuildBatchRowNote(BatchQueueRowDto row)
    {
        if (!string.IsNullOrWhiteSpace(row.DispatchError))
        {
            return row.DispatchError!;
        }

        if (row.DispatchResult is not null)
        {
            return $"Submitted as {row.DispatchResult.Mode} via {row.DispatchResult.Submission.ExternalJobId}.";
        }

        if (string.Equals(NormalizeBatchStatus(row.SubmissionStatus), "submitting", StringComparison.OrdinalIgnoreCase))
        {
            return "Submit is in flight through admin-web.";
        }

        return $"Queued row {row.Draft.Sku} is ready for admin-web submit.";
    }

    private static string BuildBatchRetryRule(BatchQueueRowDto row, string canonicalStatus)
    {
        if (canonicalStatus == "failed")
        {
            return !string.IsNullOrWhiteSpace(row.RetryLineageJobId)
                ? $"Retry from admin-web with lineage {row.RetryLineageJobId}."
                : "Retry from admin-web after reviewing the blocker.";
        }

        if (canonicalStatus == "submitted")
        {
            return "No retry needed unless admin-web records a later failure.";
        }

        return "Mutation stays outside WPF in v0.3.0.";
    }

    private static string NormalizeBatchStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "draft";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "queued" or "ready" or "pending-submit" => "ready",
            "submitting" or "running" => "submitting",
            "submitted" or "completed" or "printed" or "success" => "submitted",
            "failed" or "error" or "blocked" or "partial" => "failed",
            _ => "draft",
        };
    }

    private static HistoryWorkspaceModel BuildHistoryWorkspace(ShellWorkspaceSnapshot snapshot)
    {
        var pendingProofs = BuildHistoryPendingProofs(snapshot).ToArray();
        var bundleRows = BuildBundleRows(snapshot).ToArray();
        var auditRows = BuildHistoryAuditRows(snapshot).Take(10).ToArray();
        var model = new HistoryWorkspaceModel
        {
            MessageSummary = $"{snapshot.AuditSearch.Entries.Count((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase))} pending proof(s) / {snapshot.AuditBackupBundles.Count} bundle(s)",
            FooterDetail = "approve, reject, export, and bundle listing are companion-backed",
            LedgerSummary = $"{snapshot.AuditSearch.Entries.Count} recent audit entries",
            PendingProofSummary = $"{pendingProofs.Length} review item(s)",
            BundleSummary = $"{bundleRows.Length} guarded bundle(s)",
            FilterSummary = "desktop-shell companion filter set",
        };

        foreach (var proof in pendingProofs)
        {
            model.PendingProofs.Add(proof);
        }

        foreach (var bundle in bundleRows)
        {
            model.BundleRows.Add(bundle);
        }

        foreach (var auditRow in auditRows)
        {
            model.AuditRows.Add(auditRow);
        }

        model.FilterRows.Add(new PropertyRowModel("Query source", "desktop-shell companion"));
        model.FilterRows.Add(new PropertyRowModel("Loaded entries", snapshot.AuditSearch.Entries.Count.ToString(CultureInfo.InvariantCulture)));
        model.FilterRows.Add(new PropertyRowModel("Default template", snapshot.TemplateCatalog.DefaultTemplateVersion));
        model.FilterRows.Add(new PropertyRowModel("Restore authority", "desktop-shell only"));

        model.ControlSections.Add(new PropertySectionModel("Safe Actions", "Only read + safe operations are exposed from this lane in v0.3.0.", new[] { new PropertyRowModel("Approve proof", "enabled"), new PropertyRowModel("Reject proof", "enabled"), new PropertyRowModel("Export audit", "enabled") }));
        model.ControlSections.Add(new PropertySectionModel("Deferred Actions", "Destructive or authority-shifting operations stay outside the native shell for this release.", new[] { new PropertyRowModel("Retention apply", "disabled"), new PropertyRowModel("Restore bundle", "disabled"), new PropertyRowModel("Artifact pin / repair", "deferred") }));

        model.MessageRows.Add(new MessageRowModel("Info", "audit", "Audit search and export are now backed by the desktop-shell companion."));
        model.MessageRows.Add(new MessageRowModel("Warn", "restore", "Restore stays guarded in apps/desktop-shell for v0.3.0."));
        foreach (var issue in snapshot.TemplateGovernance.Issues.Take(2))
        {
            model.MessageRows.Add(new MessageRowModel(MapIssueLevel(issue.Severity), "catalog", issue.Message));
        }

        model.StatusItems.Add(new StatusItemModel("Pending proofs", model.PendingProofs.Count.ToString(CultureInfo.InvariantCulture), "Safe approve / reject is available from this shell.", model.PendingProofs.Count == 0 ? "OK" : "WATCH", model.PendingProofs.Count == 0 ? Brushes.ForestGreen : Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Audit export", "ready", "Export returns the current audit snapshot from desktop-shell.", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Bundle listing", snapshot.AuditBackupBundles.Count.ToString(CultureInfo.InvariantCulture), "Bundle list is live; restore remains guarded elsewhere.", "LIVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Retention", "deferred", "Retention commands are not exposed from WPF in v0.3.0.", "LOCK", Brushes.Firebrick));

        model.SelectedPendingProof = model.PendingProofs.FirstOrDefault();
        if (model.SelectedPendingProof is null)
        {
            model.SelectedBundle = model.BundleRows.FirstOrDefault();
        }

        if (model.SelectedPendingProof is null && model.SelectedBundle is null)
        {
            model.SelectedAuditRow = model.AuditRows.FirstOrDefault();
        }

        return model;
    }

    private static void ConfigureTemplatePanel(
        TemplateLibraryPanelModel panel,
        IReadOnlyList<TemplateCatalogRowModel> entries,
        string preferredTemplateVersion,
        string headerDetail,
        string statusSummary)
    {
        panel.HeaderDetail = headerDetail;
        panel.StatusSummary = statusSummary;
        panel.EntrySummary = $"{entries.Count} visible / {entries.Count((entry) => string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase))} local / {entries.Count((entry) => string.Equals(entry.State, "draft", StringComparison.OrdinalIgnoreCase))} draft";
        panel.ReplaceSummaryCards(
            new[]
            {
                new SummaryCardModel("Effective Default", preferredTemplateVersion, "Desktop-shell still decides which saved template version wins."),
                new SummaryCardModel("Dispatch-Safe", entries.Count((entry) => !entry.Dispatch.Contains("blocked", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture), "Only saved catalog entries should be treated as proof-safe."),
                new SummaryCardModel("Blocked / Draft", entries.Count((entry) => entry.State == "draft").ToString(CultureInfo.InvariantCulture), "Unsafe or broken local entries stay visible instead of disappearing."),
                new SummaryCardModel("Rollback", entries.FirstOrDefault((entry) => entry.State == "fallback")?.Name ?? "none", "Packaged fallback stays explicit when a local overlay wins."),
            });
        panel.LoadEntries(entries, preferredTemplateVersion);
        panel.ReplaceGuidanceSections(
            new[]
            {
                new PropertySectionModel("Authority Boundary", "Template choice is visible here, but catalog authority still belongs to desktop-shell.", new[] { new PropertyRowModel("Winning default", "desktop-shell packaged + local catalog resolution"), new PropertyRowModel("Unsafe state", "blocked or draft entries remain non-dispatchable"), new PropertyRowModel("Re-proof rule", "saved overlay changes still require fresh proof review") }),
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

    private static List<TemplateCatalogRowModel> BuildTemplateEntries(ShellWorkspaceSnapshot snapshot)
    {
        var entries = new List<TemplateCatalogRowModel>();
        var catalogVersions = new HashSet<string>(snapshot.TemplateCatalog.Templates.Select((entry) => entry.Version), StringComparer.OrdinalIgnoreCase);
        var packagedFallback = snapshot.TemplateCatalog.Templates.FirstOrDefault(
            (entry) =>
                string.Equals(entry.Source, "packaged", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Version, snapshot.TemplateCatalog.DefaultTemplateVersion, StringComparison.OrdinalIgnoreCase))
            ?.Version;

        foreach (var entry in snapshot.TemplateCatalog.Templates)
        {
            var localEntry = snapshot.TemplateGovernance.LocalEntries.FirstOrDefault((candidate) => string.Equals(candidate.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
            var hasBlockingIssue = HasBlockingIssue(snapshot.TemplateGovernance, entry.Version, localEntry);
            var isDefault = string.Equals(entry.Version, snapshot.TemplateGovernance.EffectiveDefaultTemplateVersion, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Version, snapshot.TemplateCatalog.DefaultTemplateVersion, StringComparison.OrdinalIgnoreCase);
            var isFallback = !isDefault &&
                string.Equals(entry.Version, packagedFallback, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(snapshot.TemplateGovernance.EffectiveDefaultSource, "local", StringComparison.OrdinalIgnoreCase);
            var issueSummary = BuildIssueSummary(snapshot.TemplateGovernance, entry.Version);

            entries.Add(
                new TemplateCatalogRowModel(
                    entry.Version,
                    entry.Source ?? "unknown",
                    isDefault ? "default" : hasBlockingIssue ? "draft" : isFallback ? "fallback" : "stable",
                    string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                        ? "desktop-shell local catalog overlay"
                        : "desktop-shell packaged manifest",
                    hasBlockingIssue
                        ? "blocked until overlay repair"
                        : "proof + print after approved proof in desktop-shell",
                    localEntry?.ResolvedPath ?? "packaged manifest",
                    entry.Description ?? BuildTemplateNote(entry, isDefault, isFallback),
                    string.IsNullOrWhiteSpace(issueSummary)
                        ? (string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                            ? "Local saved overlay is currently visible to desktop-shell."
                            : "Packaged manifest entry remains available for rollback.")
                        : issueSummary,
                    isFallback
                        ? "Select this packaged entry directly or remove the winning local override."
                        : string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
                            ? "Switch back to a packaged entry or save a corrected local overlay, then rerun proof."
                            : "Keep this packaged baseline visible for rollback discipline."));
        }

        foreach (var localEntry in snapshot.TemplateGovernance.LocalEntries.Where((entry) => !catalogVersions.Contains(entry.Version)))
        {
            entries.Add(
                new TemplateCatalogRowModel(
                    localEntry.Version,
                    "local",
                    "draft",
                    "desktop-shell local overlay",
                    "blocked until overlay repair",
                    localEntry.ResolvedPath,
                    "Local overlay entry is present in governance diagnostics but not currently surfaced as a dispatch-safe catalog entry.",
                    BuildIssueSummary(snapshot.TemplateGovernance, localEntry.Version),
                    "Repair or remove the local overlay before treating it as production-safe."));
        }

        return entries
            .OrderByDescending((entry) => entry.State == "default")
            .ThenByDescending((entry) => entry.State == "fallback")
            .ThenBy((entry) => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<ActivityRowModel> BuildHomeActivityRows(ShellWorkspaceSnapshot snapshot)
    {
        foreach (var warning in snapshot.BridgeStatus.WarningDetails.Take(2))
        {
            yield return new ActivityRowModel("live", "bridge", warning.Message, warning.Severity);
        }

        foreach (var issue in snapshot.TemplateGovernance.Issues.Take(2))
        {
            yield return new ActivityRowModel("live", "catalog", issue.Message, issue.Severity);
        }

        foreach (var entry in snapshot.AuditSearch.Entries.Take(2))
        {
            yield return new ActivityRowModel(
                FormatTimestamp(entry.Dispatch.Audit.OccurredAt),
                entry.Dispatch.Mode,
                $"{entry.Dispatch.MatchSubject.Sku} | {entry.Dispatch.TemplateVersion}",
                entry.Proof?.Status ?? entry.Dispatch.Audit.Event);
        }
    }

    private static ShellActionModel DisabledDesignerPlaceholder(string label)
    {
        return ShellActionModel.Disabled(label, "Designer editing commands are not yet wired in the native shell.");
    }

    private static int CountDesignerLeafNodes(IEnumerable<ObjectNodeModel> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.Children.Count == 0)
            {
                count += 1;
                continue;
            }

            count += CountDesignerLeafNodes(node.Children);
        }

        return count;
    }

    private static int CountDesignerLayerNodes(IEnumerable<ObjectNodeModel> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
            {
                count += 1;
                count += CountDesignerLayerNodes(node.Children);
            }
        }

        return count;
    }

    private static string BuildPreviewHeadline(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "fallback" => "inline fallback",
            "degraded" => "degraded",
            _ => snapshot.Preview?.LabelName ?? snapshot.PreviewTemplateVersion,
        };
    }

    private static string BuildPreviewSummary(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "live" => "live companion preview",
            "cached" => "cached companion preview",
            "fallback" => "inline fallback preview",
            _ => "preview degraded",
        };
    }

    private static string BuildPreviewCanvasStatus(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "live" => "Companion preview live",
            "cached" => "Companion preview cached",
            "fallback" => "Inline fallback preview",
            _ => "Preview degraded",
        };
    }

    private static string MapPreviewMessageLevel(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "live" => "Info",
            "cached" => "Info",
            "fallback" => "Warn",
            _ => "Warn",
        };
    }

    private static string BuildPreviewStatusCode(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "live" => "LIVE",
            "cached" => "WATCH",
            "fallback" => "WARN",
            _ => "DEGRADED",
        };
    }

    private static Brush ResolvePreviewAccent(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.PreviewStatus switch
        {
            "live" => Brushes.ForestGreen,
            "cached" => Brushes.SteelBlue,
            "fallback" => Brushes.DarkGoldenrod,
            _ => Brushes.Firebrick,
        };
    }

    private static IEnumerable<QueueItemModel> BuildProofQueue(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditSearch.Entries
            .Where((entry) => entry.Proof is not null)
            .OrderByDescending((entry) => entry.Proof!.RequestedAt, StringComparer.Ordinal)
            .Select(
                (entry) =>
                {
                    var proof = entry.Proof!;
                    return new QueueItemModel(
                        proof.ProofJobId,
                        $"{entry.Dispatch.TemplateVersion} | {entry.Dispatch.MatchSubject.JanNormalized}",
                        proof.Status,
                        BuildProofNote(proof.Status),
                        "desktop-shell proof review",
                        proof.ArtifactPath,
                        BuildProofBlocker(proof.Status),
                        BuildProofNextAction(proof.Status));
                });
    }

    private static IEnumerable<JobRowModel> BuildPrintConsoleJobs(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditSearch.Entries
            .GroupBy(
                (entry) => new
                {
                    entry.Dispatch.MatchSubject.Sku,
                    entry.Dispatch.TemplateVersion,
                    entry.Dispatch.MatchSubject.JanNormalized,
                    entry.Dispatch.MatchSubject.Qty,
                })
            .Select((group) => group.OrderByDescending((entry) => entry.Dispatch.Audit.OccurredAt, StringComparer.Ordinal).First())
            .OrderByDescending((entry) => entry.Dispatch.Audit.OccurredAt, StringComparer.Ordinal)
            .Select(
                (entry) =>
                {
                    var proofStatus = entry.Proof?.Status ?? "missing";
                    var routeStatus = MapPrintSubjectStatus(entry);
                    return new JobRowModel(
                        entry.Dispatch.MatchSubject.Sku,
                        proofStatus,
                        entry.Dispatch.SubmissionAdapterKind,
                        routeStatus,
                        BuildPrintSubjectNote(entry, routeStatus),
                        entry.Dispatch.TemplateVersion,
                        entry.Dispatch.MatchSubject.JanNormalized,
                        entry.Dispatch.MatchSubject.Qty.ToString(CultureInfo.InvariantCulture),
                        entry.Dispatch.Audit.JobLineageId,
                        BuildPrintSubjectBlocker(entry, routeStatus),
                        BuildPrintSubjectNextAction(routeStatus));
                });
    }

    private static IEnumerable<ActivityRowModel> BuildAuditActivityRows(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditSearch.Entries
            .OrderByDescending((entry) => entry.Dispatch.Audit.OccurredAt, StringComparer.Ordinal)
            .Select(
                (entry) => new ActivityRowModel(
                    FormatTimestamp(entry.Dispatch.Audit.OccurredAt),
                    entry.Dispatch.Mode,
                    $"{entry.Dispatch.MatchSubject.Sku} | {entry.Dispatch.Audit.Event}",
                    entry.Proof?.Status ?? entry.Dispatch.Audit.Event));
    }

    private static IEnumerable<QueueItemModel> BuildHistoryPendingProofs(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditSearch.Entries
            .Where((entry) => string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending((entry) => entry.Proof!.RequestedAt, StringComparer.Ordinal)
            .Select(
                (entry) =>
                {
                    var proof = entry.Proof!;
                    return new QueueItemModel(
                        proof.ProofJobId,
                        $"{entry.Dispatch.TemplateVersion} | {entry.Dispatch.MatchSubject.Sku}",
                        proof.Status,
                        "Pending proof requires operator review before the related dispatch path can be considered safe.",
                        "desktop-shell proof review",
                        proof.ArtifactPath,
                        "Pending proof still blocks the related dispatch subject.",
                        "Approve or reject from History or Print Console.");
                });
    }

    private static IEnumerable<QueueItemModel> BuildBundleRows(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditBackupBundles
            .OrderByDescending((bundle) => bundle.CreatedAtUtc, StringComparer.Ordinal)
            .Select(
                (bundle) => new QueueItemModel(
                    bundle.FileName,
                    $"{bundle.SizeBytes} bytes",
                    "listed",
                    "Bundle listing is live, but restore remains guarded in apps/desktop-shell for v0.3.0.",
                    "desktop-shell restore authority",
                    bundle.FilePath,
                    "Restore is intentionally disabled from the native shell.",
                    "Use desktop-shell if explicit restore review is required."));
    }

    private static IEnumerable<AuditRowModel> BuildHistoryAuditRows(ShellWorkspaceSnapshot snapshot)
    {
        return snapshot.AuditSearch.Entries
            .OrderByDescending((entry) => entry.Dispatch.Audit.OccurredAt, StringComparer.Ordinal)
            .Select(
                (entry) => new AuditRowModel(
                    FormatTimestamp(entry.Dispatch.Audit.OccurredAt),
                    entry.Dispatch.Mode,
                    entry.Dispatch.MatchSubject.Sku,
                    entry.Proof?.Status ?? entry.Dispatch.Audit.Event,
                    entry.Dispatch.Audit.JobLineageId,
                    entry.Dispatch.TemplateVersion,
                    entry.Proof?.ArtifactPath ?? entry.Dispatch.ArtifactMediaType,
                    entry.Proof is null
                        ? "Review dispatch history only"
                        : BuildProofNextAction(entry.Proof.Status),
                    BuildPrintSubjectNote(entry, MapPrintSubjectStatus(entry))));
    }

    private static bool HasBlockingIssue(
        TemplateCatalogGovernanceResultDto governance,
        string version,
        TemplateCatalogGovernanceEntryDto? localEntry)
    {
        if (localEntry is not null && (!localEntry.Enabled || !localEntry.FileExists))
        {
            return true;
        }

        return governance.Issues.Any(
            (issue) =>
                issue.Message.Contains(version, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildIssueSummary(TemplateCatalogGovernanceResultDto governance, string version)
    {
        return string.Join(
            " ",
            governance.Issues
                .Where((issue) => issue.Message.Contains(version, StringComparison.OrdinalIgnoreCase))
                .Select((issue) => issue.Message));
    }

    private static string BuildTemplateNote(TemplateCatalogEntryDto entry, bool isDefault, bool isFallback)
    {
        if (isDefault)
        {
            return "Current effective default selected by desktop-shell.";
        }

        if (isFallback)
        {
            return "Packaged fallback kept visible so rollback is explicit.";
        }

        return string.Equals(entry.Source, "local", StringComparison.OrdinalIgnoreCase)
            ? "Saved local overlay currently visible to desktop-shell."
            : "Packaged manifest entry remains available for operator selection.";
    }

    private static string BuildProofNote(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "approved" => "Proof is approved and can act as live lineage context for desktop-shell.",
            "rejected" => "Proof was rejected and cannot unlock print until a corrected proof is reviewed.",
            "pending" => "Proof is still awaiting operator review in desktop-shell.",
            _ => "Proof state is visible from desktop-shell audit data.",
        };
    }

    private static string BuildProofBlocker(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "approved" => "No review blocker remains on this proof.",
            "rejected" => "Rejected proof keeps the related dispatch path closed.",
            "pending" => "Pending review still blocks the related dispatch path.",
            _ => "Review the proof state before relying on it for production decisions.",
        };
    }

    private static string BuildProofNextAction(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "approved" => "Use as live proof context; direct print still runs from desktop-shell.",
            "rejected" => "Correct the issue, save the catalog state, and rerun proof in desktop-shell.",
            "pending" => "Approve or reject from the native shell safe-op path.",
            _ => "Refresh audit state from desktop-shell.",
        };
    }

    private static string MapPrintSubjectStatus(AuditSearchEntryDto entry)
    {
        if (string.Equals(entry.Proof?.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return "held";
        }

        if (string.Equals(entry.Proof?.Status, "rejected", StringComparison.OrdinalIgnoreCase) ||
            entry.Proof is null)
        {
            return "blocked";
        }

        if (string.Equals(entry.Proof?.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return "ready";
        }

        return entry.Dispatch.Audit.Event;
    }

    private static string BuildPrintSubjectNote(AuditSearchEntryDto entry, string routeStatus)
    {
        return routeStatus switch
        {
            "ready" => $"Approved proof context is visible for {entry.Dispatch.MatchSubject.Sku}; direct print still routes through desktop-shell.",
            "held" => $"Pending proof keeps {entry.Dispatch.MatchSubject.Sku} out of a dispatch-safe state.",
            "blocked" => $"No approved proof context is currently visible for {entry.Dispatch.MatchSubject.Sku}.",
            _ => $"Recent {entry.Dispatch.Audit.Event} event is visible for {entry.Dispatch.MatchSubject.Sku}.",
        };
    }

    private static string BuildPrintSubjectBlocker(AuditSearchEntryDto entry, string routeStatus)
    {
        return routeStatus switch
        {
            "ready" => "Direct dispatch still stays in desktop-shell for v0.3.0.",
            "held" => "A pending proof decision is still blocking this subject.",
            "blocked" => entry.Proof is null ? "No proof record is visible for this subject." : "Rejected proof keeps this subject blocked.",
            _ => "Review current audit context before treating this subject as production-safe.",
        };
    }

    private static string BuildPrintSubjectNextAction(string routeStatus)
    {
        return routeStatus switch
        {
            "ready" => "Use desktop-shell for actual print dispatch when the operator is ready.",
            "held" => "Approve or reject the pending proof first.",
            "blocked" => "Create or repair proof state in desktop-shell before dispatch.",
            _ => "Refresh live state before acting.",
        };
    }

    private static string MapIssueLevel(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => "Error",
            "warning" => "Warn",
            _ => "Info",
        };
    }

    private static string FormatTimestamp(string timestamp)
    {
        if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return timestamp;
    }

    private static void AddRulers(ObservableCollection<string> topMarks, ObservableCollection<string> sideMarks)
    {
        topMarks.Add("0");
        topMarks.Add("10");
        topMarks.Add("20");
        topMarks.Add("30");
        topMarks.Add("40");
        topMarks.Add("50");
        topMarks.Add("60");
        topMarks.Add("70");
        topMarks.Add("80");
        sideMarks.Add("0");
        sideMarks.Add("5");
        sideMarks.Add("10");
        sideMarks.Add("15");
        sideMarks.Add("20");
        sideMarks.Add("25");
        sideMarks.Add("30");
    }

    private static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
