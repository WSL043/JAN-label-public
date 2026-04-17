using System.Collections.ObjectModel;
using System.Windows.Media;

namespace JanLabel.WindowsShell;

public static class WorkspaceFactory
{
    public static void SeedModules(ObservableCollection<ModuleModel> modules)
    {
        modules.Add(
            new ModuleModel(
                "Home",
                "Migration baseline and session status",
                "overview",
                "Operational readiness and migration scope for the native shell.",
                "Native shell migration should be judged by operator comprehension first: authority, current blocker, and next practical move must all be visible without opening another lane.",
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
                        ShellActionModel.Disabled(ShellActions.PinWorkspace, "Workspace pinning is not yet implemented in the native shell."),
                        ShellActionModel.Disabled(ShellActions.ExportState, "State export is not yet implemented in the native shell.")),
                    new RibbonGroupModel(
                        "Templates",
                        ShellActionModel.Enabled(ShellActions.OpenLibrary, "Refresh and focus the template-library board in the current shell lane."),
                        ShellActionModel.Enabled(ShellActions.OverlayStatus, "Refresh and focus overlay winner, source, and dispatch-safety state."),
                        ShellActionModel.Enabled(ShellActions.CatalogRules, "Refresh and focus catalog-governance context from desktop-shell.")),
                    new RibbonGroupModel(
                        "Migration",
                        ShellActionModel.Disabled(ShellActions.CurrentState, "This migration readout is not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.ReleaseNotes, "Release-note viewing is not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.PreviewPackage, "Preview package inspection is not yet wired in the native shell.")),
                },
                new[]
                {
                    new ContextBadgeModel("Authority", "desktop-shell", Brushes.SteelBlue),
                    new ContextBadgeModel("Preview", "installer live", Brushes.ForestGreen),
                    new ContextBadgeModel("Priority", "T-049", Brushes.DarkGoldenrod),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "Migration dashboard"),
                    new StatusStripItemModel("Branch", "main"),
                    new StatusStripItemModel("Preview", "t049 installer"),
                    new StatusStripItemModel("Backend", "desktop-shell authority"),
                },
                BuildHomeWorkspace()));

        modules.Add(
            new ModuleModel(
                "Designer",
                "Format authoring and preview",
                "design",
                "BarTender-style designer with canvas, toolbox, and property-grid workflow.",
                "Authoring is only production-safe when saved catalog state, preview output, and proof gating are easy to distinguish from the live draft at a glance.",
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
                        ShellActionModel.Disabled(ShellActions.Paste, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Duplicate, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Delete, "Designer editing commands are not yet wired in the native shell.")),
                    new RibbonGroupModel(
                        "Insert",
                        ShellActionModel.Disabled(ShellActions.Text, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Barcode, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Line, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Box, "Designer editing commands are not yet wired in the native shell.")),
                    new RibbonGroupModel(
                        "Arrange",
                        ShellActionModel.Disabled(ShellActions.AlignLeft, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.MakeSameSize, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.Snap, "Designer editing commands are not yet wired in the native shell.")),
                    new RibbonGroupModel(
                        "Data",
                        ShellActionModel.Disabled(ShellActions.RecordBrowser, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.QueryPrompt, "Designer editing commands are not yet wired in the native shell."),
                        ShellActionModel.Disabled(ShellActions.NamedDataSources, "Designer editing commands are not yet wired in the native shell.")),
                    new RibbonGroupModel(
                        "Validate",
                        ShellActionModel.Enabled(ShellActions.RustPreview),
                        ShellActionModel.Disabled(ShellActions.SaveToCatalog, "Template write-back remains owned by apps/desktop-shell in v0.3.0."),
                        ShellActionModel.Disabled(ShellActions.RunProof, "Proof generation remains owned by apps/desktop-shell in v0.3.0.")),
                },
                new[]
                {
                    new ContextBadgeModel("Draft state", "live preview only", Brushes.Firebrick),
                    new ContextBadgeModel("Catalog", "local overlay", Brushes.DarkGoldenrod),
                    new ContextBadgeModel("Output", "SVG / PDF", Brushes.SteelBlue),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "Designer"),
                    new StatusStripItemModel("Template", "basic-50x30@v2"),
                    new StatusStripItemModel("Catalog", "overlay active"),
                    new StatusStripItemModel("Proof gate", "external authority"),
                },
                BuildDesignerWorkspace()));

        modules.Add(
            new ModuleModel(
                "Print Console",
                "Working payload and proof route",
                "ready",
                "Proof and dispatch lane with explicit guardrails before print unlock.",
                "A production print lane must surface the approved proof, blocked jobs, and current route before the operator even thinks about pressing print.",
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
                    new ContextBadgeModel("Proof gate", "strict match", Brushes.Firebrick),
                    new ContextBadgeModel("Route", "pdf-proof", Brushes.SteelBlue),
                    new ContextBadgeModel("Queue", "3 ready / 1 held", Brushes.ForestGreen),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "Print Console"),
                    new StatusStripItemModel("Bridge", "connected"),
                    new StatusStripItemModel("Printer route", "pdf-proof"),
                    new StatusStripItemModel("Blocked jobs", "1"),
                },
                BuildPrintConsoleWorkspace()));

        modules.Add(
            new ModuleModel(
                "Batch Jobs",
                "Workbook import and queue",
                "24",
                "Seeded shared batch snapshot fallback",
                "Seeded fallback keeps batch-shell language visible until the desktop-shell shared snapshot is available.",
                new[]
                {
                    ShellActionModel.Disabled(ShellActions.ImportWorkbook, "Batch submission remains transitional in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.RetryFailed, "Retry remains outside the native shell in v0.3.0."),
                    ShellActionModel.Disabled(ShellActions.QueueSnapshot, "Shared batch snapshot refresh requires the desktop-shell companion."),
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
                        ShellActionModel.Disabled(ShellActions.RetryFailed, "Retry remains outside the native shell in v0.3.0."),
                        ShellActionModel.Disabled(ShellActions.FreezeRow, "Batch queue mutation remains transitional in v0.3.0.")),
                    new RibbonGroupModel(
                        "Validation",
                        ShellActionModel.Disabled(ShellActions.FixtureCheck, "Batch validation remains transitional in v0.3.0."),
                        ShellActionModel.Disabled(ShellActions.UnknownTemplate, "Batch validation remains transitional in v0.3.0."),
                        ShellActionModel.Disabled(ShellActions.JanWarnings, "Batch validation remains transitional in v0.3.0.")),
                },
                new[]
                {
                    new ContextBadgeModel("Import", "csv / xlsx", Brushes.SteelBlue),
                    new ContextBadgeModel("Snapshot", "seeded fallback", Brushes.DarkGoldenrod),
                    new ContextBadgeModel("Queue", "seeded", Brushes.ForestGreen),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "Batch fallback"),
                    new StatusStripItemModel("Snapshot", "seeded"),
                    new StatusStripItemModel("Authority", "desktop-shell"),
                    new StatusStripItemModel("Mutation", "disabled"),
                },
                BuildBatchJobsWorkspace()));

        modules.Add(
            new ModuleModel(
                "History",
                "Proof review and audit",
                "6 pending",
                "Audit and proof-review lane with retention and restore visibility.",
                "Operational history must let an operator answer three questions fast: what happened, what still needs review, and whether recovery is safe.",
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
                    new ContextBadgeModel("Pending review", "2 proofs", Brushes.DarkGoldenrod),
                    new ContextBadgeModel("Audit export", "ready", Brushes.ForestGreen),
                    new ContextBadgeModel("Restore", "guarded", Brushes.SteelBlue),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "History / audit"),
                    new StatusStripItemModel("Proof inbox", "2 active"),
                    new StatusStripItemModel("Export", "filtered view"),
                    new StatusStripItemModel("Restore", "pre-check required"),
                },
                BuildHistoryWorkspace()));
    }

    private static HomeWorkspaceModel BuildHomeWorkspace()
    {
        var model = new HomeWorkspaceModel
        {
            HeaderDetail = "native shell absorbing operator lanes",
            StatusSummary = "seeded workstation baseline",
            ActivitySummary = "latest 5 seeded events",
        };
        model.SummaryCards.Add(new SummaryCardModel("Modules", "5", "Home, Designer, Print Console, Batch Jobs, History"));
        model.SummaryCards.Add(new SummaryCardModel("Preview", "installer", "CI artifacts now produce an installer prerelease"));
        model.SummaryCards.Add(new SummaryCardModel("Authority", "desktop-shell", "Proof, audit, and catalog resolution stay authoritative there"));
        model.SummaryCards.Add(new SummaryCardModel("Next", "T-049", "Absorb current operational lanes into the native shell frame"));
        model.SessionRows.Add(new PropertyRowModel("Branch", "main"));
        model.SessionRows.Add(new PropertyRowModel("Release base", "v0.2.0"));
        model.SessionRows.Add(new PropertyRowModel("Latest preview", "preview-t049-windows-native-shell-main-20260416"));
        model.SessionRows.Add(new PropertyRowModel("Validation", "GitHub Windows runner authoritative"));
        model.SessionRows.Add(new PropertyRowModel("Primary shell", "apps/windows-shell"));
        model.SessionRows.Add(new PropertyRowModel("Effective default", "basic-50x30@v2"));
        TemplateLibraryCatalog.SeedHomePanel(model.TemplateLibrary);
        model.ActivityRows.Add(new ActivityRowModel("14:22", "release", "Installer prerelease refreshed from successful Windows CI", "done"));
        model.ActivityRows.Add(new ActivityRowModel("14:10", "designer", "Native shell designer frame aligned to practical workstation shape", "done"));
        model.ActivityRows.Add(new ActivityRowModel("13:58", "catalog", "Overlay authority and rollback path are now visible without opening another lane", "done"));
        model.ActivityRows.Add(new ActivityRowModel("13:47", "audit", "Desktop-shell restore path remains available for backup recovery", "ok"));
        model.ActivityRows.Add(new ActivityRowModel("13:35", "proof", "Approved lineage still blocks print unlock in native shell", "ok"));
        model.ControlSections.Add(new PropertySectionModel("Shell Ownership", "The shell frame moves to WPF first; backend proof and dispatch authority stays unchanged.", new[] { new PropertyRowModel("Shell language", "native Windows workstation"), new PropertyRowModel("Proof / print gate", "desktop-shell"), new PropertyRowModel("Template save authority", "local catalog overlay") }));
        model.ControlSections.Add(new PropertySectionModel("Immediate Migration Fronts", "These are the next practical lanes to absorb after the designer frame.", new[] { new PropertyRowModel("Print Console", "proof queue and dispatch checklist"), new PropertyRowModel("Batch Jobs", "import queue and retry guardrails"), new PropertyRowModel("History", "audit search, proof review, retention") }));
        model.NextSteps.Add(new QueueItemModel("Exercise the template library board", "Confirm default / draft / rollback reasoning without leaving Home", "t-042", "Operators should be able to tell which entry wins and which entry is unsafe before entering Designer."));
        model.NextSteps.Add(new QueueItemModel("Exercise the installer preview", "Confirm module switching and pane density on Windows", "preview", "Use the prerelease installer rather than the loose publish output."));
        model.NextSteps.Add(new QueueItemModel("Wire backend commands later", "Keep Rust/Tauri authority untouched until an explicit backend migration ADR exists", "backend", "This batch keeps authority text and operator flow aligned while backend parity lands separately."));
        model.StatusItems.Add(new StatusItemModel("Preview packaging", "installer published", "GitHub prerelease can be downloaded directly", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Migration lane", "T-049 active", "shell now carries multi-lane operator views", "MOVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Catalog authority", "overlay preserved", "packaged vs local split remains explicit", "WATCH", Brushes.DarkGoldenrod));
        return model;
    }

    private static DesignerWorkspaceModel BuildDesignerWorkspace()
    {
        var toolboxGroups = new[]
        {
            new ToolboxGroupModel("Objects", new[] { new ToolboxItemModel("Text", "A"), new ToolboxItemModel("Barcode", "JAN"), new ToolboxItemModel("Counter", "#"), new ToolboxItemModel("Picture", "IMG") }),
            new ToolboxGroupModel("Guides", new[] { new ToolboxItemModel("Margins", "4 mm"), new ToolboxItemModel("Grid", "2 mm"), new ToolboxItemModel("Snap", "On") }),
        };
        var objectNodes = new[]
        {
            new ObjectNodeModel("Label Format", "50 x 30 mm", new[] { new ObjectNodeModel("Static Layer", "3 objects", new[] { new ObjectNodeModel("Brand mark", "Text"), new ObjectNodeModel("Frame", "Box"), new ObjectNodeModel("Divider", "Line") }), new ObjectNodeModel("Data Layer", "4 objects", new[] { new ObjectNodeModel("Product name", "{{sku}}"), new ObjectNodeModel("JAN barcode", "{{jan}}"), new ObjectNodeModel("JAN text", "{{jan}}"), new ObjectNodeModel("Quantity", "{{qty}}") }) }),
        };
        var dataSources = new[]
        {
            new DataSourceRowModel("sku", "Text", "200-145-3"),
            new DataSourceRowModel("jan", "JAN", "4901234567894"),
            new DataSourceRowModel("qty", "Number", "24"),
            new DataSourceRowModel("brand", "Text", "JAN-LAB"),
            new DataSourceRowModel("template_version", "Text", "basic-50x30@v2"),
            new DataSourceRowModel("proof_mode", "Expr", "proof"),
        };
        var model = new DesignerWorkspaceModel
        {
            CanvasMeta = "basic-50x30@v2 | pdf-proof | record 12 / 24",
            CanvasHint = "Select objects on the canvas, adjust properties on the right, then save to catalog before proof.",
            MessageSummary = "1 warning / 2 informative checks",
            RecordSummary = "24 active records",
            StatusSummary = "desktop-shell remains the proof/print authority",
            CatalogSummary = "authority, route, and rollback visible",
            ToolboxSummary = $"{toolboxGroups.Length} groups / {toolboxGroups.Sum((group) => group.Items.Count)} tools",
            ObjectBrowserSummary = $"{CountDesignerLeafNodes(objectNodes)} design objects / {CountDesignerLayerNodes(objectNodes)} layers",
            DataSourceSummary = $"{dataSources.Length} mapped fields",
            CanvasWidth = 660,
            CanvasHeight = 410,
            PrimaryDocumentTitle = "basic-50x30",
            SecondaryDocumentTitle = "proof-preview",
            PreviewSvg = "<svg><!-- mock preview --></svg>",
        };

        foreach (var toolboxGroup in toolboxGroups)
        {
            model.ToolboxGroups.Add(toolboxGroup);
        }
        TemplateLibraryCatalog.SeedDesignerPanel(model.TemplateLibrary);
        foreach (var objectNode in objectNodes)
        {
            model.ObjectNodes.Add(objectNode);
        }

        foreach (var dataSource in dataSources)
        {
            model.DataSources.Add(dataSource);
        }
        model.DocumentTabs.Add(new DocumentTabModel(model.PrimaryDocumentTitle, "record-linked format"));
        model.DocumentTabs.Add(new DocumentTabModel(model.SecondaryDocumentTitle, "validation surface"));
        model.CanvasElements.Add(new CanvasElementModel("BRAND", "JAN-LAB", 30, 22, 120, 32, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("SKU", "200-145-3", 30, 72, 180, 36, 18, false));
        model.CanvasElements.Add(new CanvasElementModel("BARCODE", "| ||| || ||| | ||", 28, 132, 320, 102, 16, false));
        model.CanvasElements.Add(new CanvasElementModel("JAN", "4901234567894", 50, 244, 220, 26, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("QTY", "24 PCS", 470, 38, 120, 40, 20, false));
        model.CanvasElements.Add(new CanvasElementModel("STATUS", "Proof lineage locked", 390, 304, 210, 32, 13, false));
        model.SetupRows.Add(new PropertyRowModel("Document", "basic-50x30@v2"));
        model.SetupRows.Add(new PropertyRowModel("Effective default", "basic-50x30@v2"));
        model.SetupRows.Add(new PropertyRowModel("Printer profile", "pdf-proof / 300 dpi"));
        model.SetupRows.Add(new PropertyRowModel("Catalog authority", "saved local overlay only"));
        model.SetupRows.Add(new PropertyRowModel("Dispatch route", "desktop-shell"));
        model.SetupRows.Add(new PropertyRowModel("Working record", "12 / 24"));
        model.PropertySections.Add(new PropertySectionModel("Selected Object", "Property-grid style editing for the focused item.", new[] { new PropertyRowModel("Name", "JAN barcode"), new PropertyRowModel("Binding", "{{jan}}"), new PropertyRowModel("Symbology", "EAN-13 / JAN"), new PropertyRowModel("Position", "28,132"), new PropertyRowModel("Size", "320 x 102") }));
        model.PropertySections.Add(new PropertySectionModel("Layout Rules", "Output constraints remain deterministic and print-core-safe.", new[] { new PropertyRowModel("Scale", "fixed 100%"), new PropertyRowModel("Barcode engine", "Zint only"), new PropertyRowModel("Output", "SVG / PDF"), new PropertyRowModel("Unsaved draft", "preview only") }));
        model.PropertySections.Add(new PropertySectionModel("Proof Gate", "Dispatch is still gated outside the shell.", new[] { new PropertyRowModel("Authority", "approved proof lineage"), new PropertyRowModel("Required match", "sku / brand / jan / qty / templateVersion"), new PropertyRowModel("Artifact", "valid non-empty PDF") }));
        model.RecordRows.Add(new PropertyRowModel("SKU", "200-145-3"));
        model.RecordRows.Add(new PropertyRowModel("JAN", "4901234567894"));
        model.RecordRows.Add(new PropertyRowModel("Brand", "JAN-LAB"));
        model.RecordRows.Add(new PropertyRowModel("Qty", "24"));
        model.RecordRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));
        model.PreviewRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));
        model.PreviewRows.Add(new PropertyRowModel("Label", "proof-preview"));
        model.PreviewRows.Add(new PropertyRowModel("JAN", "4901234567894"));
        model.PreviewRows.Add(new PropertyRowModel("Page", "50 x 30 mm"));
        model.PreviewRows.Add(new PropertyRowModel("Fields", "6"));
        model.MessageRows.Add(new MessageRowModel("Info", "renderer", "Rust preview and canvas geometry are aligned for the selected format."));
        model.MessageRows.Add(new MessageRowModel("Warn", "catalog", "Local catalog override is active. Save before proof if this draft should dispatch."));
        model.MessageRows.Add(new MessageRowModel("Info", "proof", "Approved proof lineage will be required before print route unlocks."));
        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", "desktop-shell / proof gate", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Catalog", "overlay active", "packaged + local manifest", "WATCH", Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Audit", "restore-ready", "backup bundles available", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Printer", "pdf-proof", "physical validation deferred", "PDF", Brushes.SteelBlue));
        AddRulers(model.TopRulerMarks, model.SideRulerMarks);
        model.SelectCanvasElement(model.CanvasElements[2]);
        return model;
    }

    private static PrintConsoleWorkspaceModel BuildPrintConsoleWorkspace()
    {
        var model = new PrintConsoleWorkspaceModel
        {
            MessageSummary = "1 blocker / 2 informative checks",
            FooterDetail = "proof lineage and bridge route visible",
            JobSummary = "3 ready / 1 held / 0 misrouted",
            ProofQueueSummary = "seeded desktop-shell review lane",
            TimelineSummary = "seeded audit-derived timeline",
        };
        model.ProofQueue.Add(new QueueItemModel("proof-240416-017", "basic-50x30@v2 | 4901234567894", "pending", "Needs operator approval before dispatch lane unlocks.", "desktop-shell proof review", "artifact valid / lineage pending", "Pending review blocks 200-145-4 dispatch.", "Approve or reject after confirming saved overlay and subject."));
        model.ProofQueue.Add(new QueueItemModel("proof-240416-018", "shipper-70x50@v1 | 4901234567801", "approved", "Current approved lineage available for print.", "desktop-shell approved lineage", "artifact pinned / route open", "No blocker. Ready jobs can use this proof.", "Use as the approved reference before dispatch."));
        model.ProofQueue.Add(new QueueItemModel("proof-240416-019", "basic-50x30@v2 | 4901234567818", "rejected", "Mismatch between saved overlay and proof subject.", "desktop-shell review archive", "artifact retained / route closed", "Rejected proof cannot unlock print.", "Correct the subject mismatch and re-run proof."));
        model.RouteRows.Add(new PropertyRowModel("Primary route", "desktop-shell pdf-proof"));
        model.RouteRows.Add(new PropertyRowModel("Fallback route", "blocked until proof match"));
        model.RouteRows.Add(new PropertyRowModel("Printer profile", "win-spool deferred"));
        model.RouteRows.Add(new PropertyRowModel("Batch gate", "approved lineage required"));
        model.JobRows.Add(new JobRowModel("200-145-3", "approved", "pdf-proof", "ready", "Lineage locked to proof-240416-018", "basic-50x30@v2", "4901234567894", "24", "lineage-7ba1a3", "No blocker. Route is open.", "Dispatch when the queue window is clear."));
        model.JobRows.Add(new JobRowModel("200-145-4", "pending", "pdf-proof", "held", "Awaiting operator review", "basic-50x30@v2", "4901234567895", "24", "lineage-pending", "proof-240416-017 is still pending review.", "Hold until a reviewer approves or rejects the proof."));
        model.JobRows.Add(new JobRowModel("200-145-5", "approved", "pdf-proof", "ready", "Local overlay saved and matched", "basic-50x30@v2", "4901234567818", "12", "lineage-45de90", "No blocker. Overlay and proof match.", "Dispatch after current ready jobs finish."));
        model.JobRows.Add(new JobRowModel("200-145-6", "missing", "n/a", "blocked", "No proof artifact on record", "basic-50x30@v2", "4901234567800", "8", "none", "Missing proof artifact keeps route closed.", "Generate proof and obtain approval before queueing."));
        model.TimelineRows.Add(new ActivityRowModel("14:26", "proof", "proof-240416-018 approved and pinned", "done"));
        model.TimelineRows.Add(new ActivityRowModel("14:24", "dispatch", "route check completed for 200-145-3", "ok"));
        model.TimelineRows.Add(new ActivityRowModel("14:21", "catalog", "saved overlay confirmed for basic-50x30@v2", "ok"));
        model.TimelineRows.Add(new ActivityRowModel("14:19", "proof", "proof-240416-019 rejected due to subject mismatch", "watch"));
        model.ControlSections.Add(new PropertySectionModel("Proof Match", "Print stays locked until the approved proof subject and lineage match exactly.", new[] { new PropertyRowModel("Required fields", "sku / brand / jan / qty / templateVersion"), new PropertyRowModel("Artifact rule", "readable, non-empty PDF"), new PropertyRowModel("Current status", "approved for 200-145-3 only") }));
        model.ControlSections.Add(new PropertySectionModel("Operator Actions", "The native shell is modeling the lane, not replacing the Rust authority yet.", new[] { new PropertyRowModel("Proof authority", "desktop-shell"), new PropertyRowModel("Dispatch authority", "desktop-shell"), new PropertyRowModel("Preview purpose", "operator UX evaluation") }));
        model.MessageRows.Add(new MessageRowModel("Warn", "dispatch", "Job 200-145-4 is held because proof-240416-017 is still pending review."));
        model.MessageRows.Add(new MessageRowModel("Info", "proof", "Approved proof artifacts are pinned and readable for ready jobs."));
        model.MessageRows.Add(new MessageRowModel("Info", "route", "Current route is PDF-only until physical validation resumes."));
        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", "desktop-shell route checks responding", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Proof gate", "strict", "subject and lineage must match", "LOCK", Brushes.Firebrick));
        model.StatusItems.Add(new StatusItemModel("Printer route", "pdf-proof", "physical printer matrix deferred", "PDF", Brushes.SteelBlue));
        model.SelectedJob = model.JobRows[0];
        return model;
    }

    private static BatchJobsWorkspaceModel BuildBatchJobsWorkspace()
    {
        var model = new BatchJobsWorkspaceModel
        {
            MessageSummary = "seeded fallback / shared snapshot unavailable",
            FooterDetail = "desktop-shell shared batch snapshot is not loaded in seeded mode",
            QueueSummary = "seeded batch examples only",
            ImportSummary = "shared snapshot fallback",
            ActivitySummary = "batch shell language preview",
        };
        model.ImportSessions.Add(new QueueItemModel("shared-batch-snapshot", "desktop-shell companion required", "seeded", "Seeded fallback keeps the Batch Jobs lane readable even when the shared snapshot is unavailable.", "windows-shell fallback", "desktop-shell shared snapshot", "No live snapshot is currently loaded.", "Refresh after desktop-shell publishes a batch snapshot."));
        model.ColumnRows.Add(new PropertyRowModel("Snapshot route", "desktop-shell shared batch snapshot"));
        model.ColumnRows.Add(new PropertyRowModel("Authority", "apps/admin-web + apps/desktop-shell"));
        model.ColumnRows.Add(new PropertyRowModel("Seeded mode", "no live queue rows loaded"));
        model.ColumnRows.Add(new PropertyRowModel("Direct mutation", "disabled in WPF"));
        model.BatchRows.Add(new BatchRowModel("seeded-batch-ready", "basic-50x30@v2", "1 row", "ready", "Seeded example row for workstation layout review.", "1", "0", "desktop-shell shared snapshot", "Live row not loaded yet.", "Refresh after admin-web publishes a queue."));
        model.BatchRows.Add(new BatchRowModel("seeded-batch-failed", "proof-ticket@v1", "1 row", "failed", "Seeded blocker example so the focus pane keeps realistic copy in fallback mode.", "0", "1", "desktop-shell shared snapshot", "Live row not loaded yet.", "Handle the real retry from admin-web once the shared snapshot is available."));
        model.ActivityRows.Add(new ActivityRowModel("seeded", "batch", "Batch Jobs is waiting on the desktop-shell shared snapshot.", "watch"));
        model.ActivityRows.Add(new ActivityRowModel("scope", "guardrail", "Import, retry, and submit remain disabled in WPF.", "lock"));
        model.ControlSections.Add(new PropertySectionModel("Shared Snapshot Contract", "Batch Jobs now expects a desktop-shell-owned shared snapshot instead of hardcoded queue authority.", new[] { new PropertyRowModel("Published by", "apps/admin-web"), new PropertyRowModel("Read by", "apps/windows-shell"), new PropertyRowModel("Mutation path", "apps/admin-web") }));
        model.ControlSections.Add(new PropertySectionModel("Seeded Fallback", "This fallback keeps the lane readable until the companion can load the real shared snapshot.", new[] { new PropertyRowModel("Use this lane for", "layout review and shell wording"), new PropertyRowModel("Do not use this lane for", "actual import or submit"), new PropertyRowModel("Refresh source", "desktop-shell companion") }));
        model.MessageRows.Add(new MessageRowModel("Info", "batch", "Batch Jobs now expects a shared snapshot published by admin-web through desktop-shell."));
        model.MessageRows.Add(new MessageRowModel("Warn", "scope", "Seeded fallback is visible because no live shared batch snapshot is loaded."));
        model.StatusItems.Add(new StatusItemModel("Snapshot", "seeded", "No live shared batch snapshot is currently loaded.", "WAIT", Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Authority", "desktop-shell", "Batch mutation remains outside the native shell.", "LOCK", Brushes.Firebrick));
        model.StatusItems.Add(new StatusItemModel("Mode", "fallback", "Refresh from desktop-shell once a queue snapshot exists.", "INFO", Brushes.SteelBlue));
        model.SelectedImportSession = model.ImportSessions[0];
        return model;
    }

    private static HistoryWorkspaceModel BuildHistoryWorkspace()
    {
        var model = new HistoryWorkspaceModel
        {
            MessageSummary = "1 attention item / 2 clean",
            FooterDetail = "restore stays conflict-safe",
            LedgerSummary = "last 5 ledger events",
            PendingProofSummary = "approve / reject review lane",
            BundleSummary = "restore-safe bundle inventory",
            FilterSummary = "current operator filter set",
        };
        model.PendingProofs.Add(new QueueItemModel("proof-240416-017", "basic-50x30@v2 | 200-145-4", "pending", "Operator needs to confirm overlay save and subject match.", "desktop-shell proof review", "PDF artifact valid / approval pending", "Pending proof blocks related dispatch lane.", "Approve or reject after checking subject and saved overlay."));
        model.PendingProofs.Add(new QueueItemModel("proof-240416-020", "shipper-70x50@v1 | 200-145-9", "pending", "PDF artifact validated; waiting for manual approval.", "desktop-shell proof review", "PDF artifact valid / approval pending", "No technical blocker. Awaiting human decision.", "Approve if the subject and artifact still match the request."));
        model.PendingProofs.Add(new QueueItemModel("proof-240416-019", "basic-50x30@v2 | 200-145-5", "rejected", "Retained for audit visibility and retry follow-up.", "audit history", "artifact retained / route closed", "Rejected proofs cannot unlock print.", "Use as audit reference when preparing a corrected retry."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-15-1800", "audit backup / 241 entries", "latest", "Validated bundle; safe candidate for explicit restore review.", "restore-safe bundle", "pre-check clean / merge guarded", "None. Bundle is ready for explicit restore review.", "Validate once more immediately before restore."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-14-1800", "audit backup / 229 entries", "kept", "Retention baseline before workstation redesign release.", "retention baseline", "kept / restore-capable", "No blocker. Kept for comparison.", "Retain until the next formal release is stable."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-12-1800", "audit backup / 201 entries", "archive", "Older bundle kept until trim policy is applied.", "archive bundle", "eligible for trim review", "Trim policy not yet applied.", "Run retention dry-run before deleting archival bundles."));
        model.AuditRows.Add(new AuditRowModel("14:27", "proof", "200-145-3", "approved", "lineage-7ba1a3", "basic-50x30@v2", "valid PDF", "Ready for dispatch audit follow-through", "Approved proof with matching lineage for the current subject."));
        model.AuditRows.Add(new AuditRowModel("14:24", "dispatch", "200-145-3", "printed", "lineage-7ba1a3", "basic-50x30@v2", "dispatch audit entry", "Use as the print reference for export or investigation", "Dispatch completed with the same approved lineage."));
        model.AuditRows.Add(new AuditRowModel("14:19", "proof", "200-145-5", "rejected", "lineage-45de90", "basic-50x30@v2", "rejected PDF", "Correct subject mismatch before retry", "Rejected proof retained for audit and retry context."));
        model.AuditRows.Add(new AuditRowModel("14:12", "backup", "bundle-2026-04-15-1800", "listed", "restore-safe", "audit backup", "validated bundle", "Eligible for guarded restore review", "Latest backup bundle has already passed validation checks."));
        model.AuditRows.Add(new AuditRowModel("14:08", "retention", "trim-dry-run", "clean", "0 destructive ops", "retention policy", "dry-run report", "Safe to inspect before apply", "Dry-run confirmed there are no destructive operations queued yet."));
        model.FilterRows.Add(new PropertyRowModel("Time window", "today / current shift"));
        model.FilterRows.Add(new PropertyRowModel("Lane filter", "proof + dispatch + retention"));
        model.FilterRows.Add(new PropertyRowModel("Subject search", "200-145-3"));
        model.FilterRows.Add(new PropertyRowModel("Export scope", "filtered current view"));
        model.ControlSections.Add(new PropertySectionModel("Proof Review", "Legacy proof seeds stay pending; approval still flows through operator review.", new[] { new PropertyRowModel("Approve path", "manual review only"), new PropertyRowModel("Reject path", "captured in audit"), new PropertyRowModel("Pin artifact", "allowed after approval") }));
        model.ControlSections.Add(new PropertySectionModel("Audit Recovery", "Restore must fail before merge if the bundle is invalid or conflicts.", new[] { new PropertyRowModel("Restore policy", "conflict-safe"), new PropertyRowModel("Bundle validation", "explicit pre-check"), new PropertyRowModel("Trim mode", "dry-run before apply") }));
        model.MessageRows.Add(new MessageRowModel("Warn", "proof", "proof-240416-017 is still pending and blocks the related dispatch lane."));
        model.MessageRows.Add(new MessageRowModel("Info", "audit", "Filtered export is ready for the current shift view."));
        model.MessageRows.Add(new MessageRowModel("Info", "restore", "Latest audit backup bundle validated with no merge conflict warnings."));
        model.StatusItems.Add(new StatusItemModel("Pending proofs", "2 active", "review queue still requires operator action", "WATCH", Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Audit export", "ready", "filtered ledger export available", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Restore path", "guarded", "bundle validation before merge", "SAFE", Brushes.SteelBlue));
        model.SelectedPendingProof = model.PendingProofs[0];
        return model;
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
}
