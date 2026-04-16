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
                new[] { "Refresh State", "Open Handoff", "View Preview" },
                new[]
                {
                    new RibbonGroupModel("Session", "Refresh", "Pin Workspace", "Export State"),
                    new RibbonGroupModel("Templates", "Open Library", "Overlay Status", "Catalog Rules"),
                    new RibbonGroupModel("Migration", "Current State", "Release Notes", "Preview Package"),
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
                new[] { "New Format", "Print Preview", "Run Proof" },
                new[]
                {
                    new RibbonGroupModel("Clipboard", "Paste", "Duplicate", "Delete"),
                    new RibbonGroupModel("Insert", "Text", "Barcode", "Line", "Box"),
                    new RibbonGroupModel("Arrange", "Align Left", "Make Same Size", "Snap"),
                    new RibbonGroupModel("Data", "Record Browser", "Query Prompt", "Named Data Sources"),
                    new RibbonGroupModel("Validate", "Rust Preview", "Save to Catalog", "Run Proof"),
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
                new[] { "Refresh Queue", "Approve Proof", "Dispatch Batch" },
                new[]
                {
                    new RibbonGroupModel("Queue", "Refresh", "Hold", "Release"),
                    new RibbonGroupModel("Proof", "Open PDF", "Approve", "Reject"),
                    new RibbonGroupModel("Dispatch", "Route Check", "Run Proof", "Print"),
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
                "Operator batch staging with queue mutation visibility and retry rules.",
                "Batch tooling is only usable in production when import assumptions, retry eligibility, and blocked submit reasons are visible on the same screen.",
                new[] { "Import Workbook", "Retry Failed", "Queue Snapshot" },
                new[]
                {
                    new RibbonGroupModel("Import", "CSV", "XLSX", "Alias Map"),
                    new RibbonGroupModel("Queue", "Submit Ready", "Retry Failed", "Freeze Row"),
                    new RibbonGroupModel("Validation", "Fixture Check", "Unknown Template", "JAN Warnings"),
                },
                new[]
                {
                    new ContextBadgeModel("Import", "csv / xlsx", Brushes.SteelBlue),
                    new ContextBadgeModel("Retry", "ready / failed only", Brushes.Firebrick),
                    new ContextBadgeModel("Queue", "186 staged", Brushes.ForestGreen),
                },
                new[]
                {
                    new StatusStripItemModel("Mode", "Batch staging"),
                    new StatusStripItemModel("Session lock", "visible"),
                    new StatusStripItemModel("Template blocker", "enforced"),
                    new StatusStripItemModel("Rows staged", "186"),
                },
                BuildBatchJobsWorkspace()));

        modules.Add(
            new ModuleModel(
                "History",
                "Proof review and audit",
                "6 pending",
                "Audit and proof-review lane with retention and restore visibility.",
                "Operational history must let an operator answer three questions fast: what happened, what still needs review, and whether recovery is safe.",
                new[] { "Search Ledger", "Export Audit", "Trim Retention" },
                new[]
                {
                    new RibbonGroupModel("Review", "Approve Proof", "Reject Proof", "Pin Artifact"),
                    new RibbonGroupModel("Audit", "Search", "Export", "Retention Dry Run"),
                    new RibbonGroupModel("Restore", "List Bundles", "Validate Bundle", "Restore"),
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
        var model = new HomeWorkspaceModel { HeaderDetail = "native shell absorbing operator lanes" };
        model.SummaryCards.Add(new SummaryCardModel("Modules", "5", "Home, Designer, Print Console, Batch Jobs, History"));
        model.SummaryCards.Add(new SummaryCardModel("Preview", "installer", "CI artifacts now produce an installer prerelease"));
        model.SummaryCards.Add(new SummaryCardModel("Authority", "desktop-shell", "Proof, audit, and catalog resolution stay authoritative there"));
        model.SummaryCards.Add(new SummaryCardModel("Next", "T-049", "Absorb current operational lanes into the native shell frame"));
        model.SessionRows.Add(new PropertyRowModel("Branch", "main"));
        model.SessionRows.Add(new PropertyRowModel("Release base", "v0.2.0"));
        model.SessionRows.Add(new PropertyRowModel("Latest preview", "preview-t049-windows-native-shell-main-20260416"));
        model.SessionRows.Add(new PropertyRowModel("Validation", "GitHub Windows runner authoritative"));
        model.SessionRows.Add(new PropertyRowModel("Primary shell", "apps/windows-shell"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("basic-50x30@v2", "local", "default", "Dispatch-safe overlay currently selected"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("basic-50x30@v1", "packaged", "fallback", "Reference version kept for rollback checks"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("shipper-70x50@v1", "packaged", "stable", "Batch import route baseline"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("proof-ticket@v1", "local", "draft", "Save before any proof dispatch"));
        model.ActivityRows.Add(new ActivityRowModel("14:22", "release", "Installer prerelease refreshed from successful Windows CI", "done"));
        model.ActivityRows.Add(new ActivityRowModel("14:10", "designer", "Native shell designer frame aligned to practical workstation shape", "done"));
        model.ActivityRows.Add(new ActivityRowModel("13:58", "catalog", "Overlay authority and single-writer rules remain visible", "watch"));
        model.ActivityRows.Add(new ActivityRowModel("13:47", "audit", "Desktop-shell restore path remains available for backup recovery", "ok"));
        model.ActivityRows.Add(new ActivityRowModel("13:35", "proof", "Approved lineage still blocks print unlock in native shell", "ok"));
        model.ControlSections.Add(new PropertySectionModel("Shell Ownership", "The shell frame moves to WPF first; backend proof and dispatch authority stays unchanged.", new[] { new PropertyRowModel("Shell language", "native Windows workstation"), new PropertyRowModel("Proof / print gate", "desktop-shell"), new PropertyRowModel("Template save authority", "local catalog overlay") }));
        model.ControlSections.Add(new PropertySectionModel("Immediate Migration Fronts", "These are the next practical lanes to absorb after the designer frame.", new[] { new PropertyRowModel("Print Console", "proof queue and dispatch checklist"), new PropertyRowModel("Batch Jobs", "import queue and retry guardrails"), new PropertyRowModel("History", "audit search, proof review, retention") }));
        model.NextSteps.Add(new QueueItemModel("Exercise the installer preview", "Confirm module switching and pane density on Windows", "preview", "Use the prerelease installer rather than the loose publish output."));
        model.NextSteps.Add(new QueueItemModel("Map current admin-web lanes", "Check every current operational surface against a native shell lane", "migration", "Keep admin-web usable, but stop inventing shell language there."));
        model.NextSteps.Add(new QueueItemModel("Wire backend commands later", "Keep Rust/Tauri authority untouched until an explicit backend migration ADR exists", "backend", "This batch stays at practical UI framing and operator flow modeling."));
        model.StatusItems.Add(new StatusItemModel("Preview packaging", "installer published", "GitHub prerelease can be downloaded directly", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Migration lane", "T-049 active", "shell now carries multi-lane operator views", "MOVE", Brushes.SteelBlue));
        model.StatusItems.Add(new StatusItemModel("Catalog authority", "overlay preserved", "packaged vs local split remains explicit", "WATCH", Brushes.DarkGoldenrod));
        return model;
    }

    private static DesignerWorkspaceModel BuildDesignerWorkspace()
    {
        var model = new DesignerWorkspaceModel
        {
            CanvasMeta = "basic-50x30@v2 | pdf-proof | record 12 / 24",
            CanvasHint = "Select objects on the canvas, adjust properties on the right, then save to catalog before proof.",
            MessageSummary = "2 warnings / 0 blockers",
            RecordSummary = "24 active records",
            StatusSummary = "desktop-shell remains the proof/print authority",
            CatalogSummary = "local override active",
            CanvasWidth = 660,
            CanvasHeight = 410,
            PrimaryDocumentTitle = "basic-50x30",
            SecondaryDocumentTitle = "proof-preview",
        };

        model.ToolboxGroups.Add(new ToolboxGroupModel("Objects", new[] { new ToolboxItemModel("Text", "A"), new ToolboxItemModel("Barcode", "JAN"), new ToolboxItemModel("Counter", "#"), new ToolboxItemModel("Picture", "IMG") }));
        model.ToolboxGroups.Add(new ToolboxGroupModel("Guides", new[] { new ToolboxItemModel("Margins", "4 mm"), new ToolboxItemModel("Grid", "2 mm"), new ToolboxItemModel("Snap", "On") }));
        model.TemplateRows.Add(new TemplateCatalogRowModel("basic-50x30@v2", "local", "default", "Dispatch target for current workbench"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("basic-50x30@v1", "packaged", "stable", "Fallback for regression checks"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("shipper-70x50@v1", "packaged", "stable", "Available in library browser"));
        model.TemplateRows.Add(new TemplateCatalogRowModel("proof-ticket@v1", "local", "draft", "Review-only until saved"));
        model.ObjectNodes.Add(new ObjectNodeModel("Label Format", "50 x 30 mm", new[] { new ObjectNodeModel("Static Layer", "3 objects", new[] { new ObjectNodeModel("Brand mark", "Text"), new ObjectNodeModel("Frame", "Box"), new ObjectNodeModel("Divider", "Line") }), new ObjectNodeModel("Data Layer", "5 objects", new[] { new ObjectNodeModel("Product name", "{{sku}}"), new ObjectNodeModel("JAN barcode", "{{jan}}"), new ObjectNodeModel("JAN text", "{{jan}}"), new ObjectNodeModel("Quantity", "{{qty}}") }) }));
        model.DataSources.Add(new DataSourceRowModel("sku", "Text", "200-145-3"));
        model.DataSources.Add(new DataSourceRowModel("jan", "JAN", "4901234567894"));
        model.DataSources.Add(new DataSourceRowModel("qty", "Number", "24"));
        model.DataSources.Add(new DataSourceRowModel("brand", "Text", "JAN-LAB"));
        model.DataSources.Add(new DataSourceRowModel("template_version", "Text", "basic-50x30@v2"));
        model.DataSources.Add(new DataSourceRowModel("proof_mode", "Expr", "proof"));
        model.DocumentTabs.Add(new DocumentTabModel(model.PrimaryDocumentTitle, "record-linked format"));
        model.DocumentTabs.Add(new DocumentTabModel(model.SecondaryDocumentTitle, "validation surface"));
        model.CanvasElements.Add(new CanvasElementModel("BRAND", "JAN-LAB", 30, 22, 120, 32, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("SKU", "200-145-3", 30, 72, 180, 36, 18, false));
        model.CanvasElements.Add(new CanvasElementModel("BARCODE", "| ||| || ||| | ||", 28, 132, 320, 102, 16, false));
        model.CanvasElements.Add(new CanvasElementModel("JAN", "4901234567894", 50, 244, 220, 26, 14, false));
        model.CanvasElements.Add(new CanvasElementModel("QTY", "24 PCS", 470, 38, 120, 40, 20, false));
        model.CanvasElements.Add(new CanvasElementModel("STATUS", "Proof lineage locked", 390, 304, 210, 32, 13, false));
        model.SetupRows.Add(new PropertyRowModel("Document", "basic-50x30@v2"));
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
        var model = new PrintConsoleWorkspaceModel { MessageSummary = "1 blocker / 2 informative checks", FooterDetail = "proof lineage and bridge route visible", JobSummary = "3 ready / 1 held / 0 misrouted" };
        model.ProofQueue.Add(new QueueItemModel("proof-240416-017", "basic-50x30@v2 | 4901234567894", "pending", "Needs operator approval before dispatch lane unlocks."));
        model.ProofQueue.Add(new QueueItemModel("proof-240416-018", "shipper-70x50@v1 | 4901234567801", "approved", "Current approved lineage available for print."));
        model.ProofQueue.Add(new QueueItemModel("proof-240416-019", "basic-50x30@v2 | 4901234567818", "rejected", "Mismatch between saved overlay and proof subject."));
        model.RouteRows.Add(new PropertyRowModel("Primary route", "desktop-shell pdf-proof"));
        model.RouteRows.Add(new PropertyRowModel("Fallback route", "blocked until proof match"));
        model.RouteRows.Add(new PropertyRowModel("Printer profile", "win-spool deferred"));
        model.RouteRows.Add(new PropertyRowModel("Batch gate", "approved lineage required"));
        model.JobRows.Add(new JobRowModel("200-145-3", "approved", "pdf-proof", "ready", "Lineage locked to proof-240416-018"));
        model.JobRows.Add(new JobRowModel("200-145-4", "pending", "pdf-proof", "held", "Awaiting operator review"));
        model.JobRows.Add(new JobRowModel("200-145-5", "approved", "pdf-proof", "ready", "Local overlay saved and matched"));
        model.JobRows.Add(new JobRowModel("200-145-6", "missing", "n/a", "blocked", "No proof artifact on record"));
        model.TimelineRows.Add(new ActivityRowModel("14:26", "proof", "proof-240416-018 approved and pinned", "done"));
        model.TimelineRows.Add(new ActivityRowModel("14:24", "dispatch", "route check completed for 200-145-3", "ok"));
        model.TimelineRows.Add(new ActivityRowModel("14:21", "catalog", "saved overlay confirmed for basic-50x30@v2", "ok"));
        model.TimelineRows.Add(new ActivityRowModel("14:19", "proof", "proof-240416-019 rejected due to subject mismatch", "watch"));
        model.SelectedJobRows.Add(new PropertyRowModel("SKU", "200-145-3"));
        model.SelectedJobRows.Add(new PropertyRowModel("JAN", "4901234567894"));
        model.SelectedJobRows.Add(new PropertyRowModel("Qty", "24"));
        model.SelectedJobRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));
        model.SelectedJobRows.Add(new PropertyRowModel("Approved proof", "proof-240416-018"));
        model.SelectedJobRows.Add(new PropertyRowModel("Lineage", "lineage-7ba1a3"));
        model.ControlSections.Add(new PropertySectionModel("Proof Match", "Print stays locked until the approved proof subject and lineage match exactly.", new[] { new PropertyRowModel("Required fields", "sku / brand / jan / qty / templateVersion"), new PropertyRowModel("Artifact rule", "readable, non-empty PDF"), new PropertyRowModel("Current status", "approved for 200-145-3 only") }));
        model.ControlSections.Add(new PropertySectionModel("Operator Actions", "The native shell is modeling the lane, not replacing the Rust authority yet.", new[] { new PropertyRowModel("Proof authority", "desktop-shell"), new PropertyRowModel("Dispatch authority", "desktop-shell"), new PropertyRowModel("Preview purpose", "operator UX evaluation") }));
        model.MessageRows.Add(new MessageRowModel("Warn", "dispatch", "Job 200-145-4 is held because proof-240416-017 is still pending review."));
        model.MessageRows.Add(new MessageRowModel("Info", "proof", "Approved proof artifacts are pinned and readable for ready jobs."));
        model.MessageRows.Add(new MessageRowModel("Info", "route", "Current route is PDF-only until physical validation resumes."));
        model.StatusItems.Add(new StatusItemModel("Bridge", "connected", "desktop-shell route checks responding", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Proof gate", "strict", "subject and lineage must match", "LOCK", Brushes.Firebrick));
        model.StatusItems.Add(new StatusItemModel("Printer route", "pdf-proof", "physical printer matrix deferred", "PDF", Brushes.SteelBlue));
        return model;
    }

    private static BatchJobsWorkspaceModel BuildBatchJobsWorkspace()
    {
        var model = new BatchJobsWorkspaceModel { MessageSummary = "3 warnings / 0 fatal", FooterDetail = "retry only for ready or failed rows", QueueSummary = "4 sessions / 186 staged records" };
        model.ImportSessions.Add(new QueueItemModel("import-240416-a", "sales-apr16.xlsx", "42 rows", "Alias map resolved; 2 JAN warnings surfaced before queueing."));
        model.ImportSessions.Add(new QueueItemModel("import-240416-b", "proof-fixes.csv", "18 rows", "Template version verified against local overlay."));
        model.ImportSessions.Add(new QueueItemModel("import-240416-c", "legacy-seed.xlsx", "126 rows", "Pending proof seed only; cannot auto-approve."));
        model.ColumnRows.Add(new PropertyRowModel("sku", "Column B -> product_code"));
        model.ColumnRows.Add(new PropertyRowModel("jan", "Column D -> jan13"));
        model.ColumnRows.Add(new PropertyRowModel("qty", "Column F -> pack_qty"));
        model.ColumnRows.Add(new PropertyRowModel("template_version", "Derived from operator selection"));
        model.ColumnRows.Add(new PropertyRowModel("brand", "Fallback to workbook constant"));
        model.BatchRows.Add(new BatchRowModel("batch-014", "basic-50x30@v2", "42", "ready", "Two rows require JAN review before submit"));
        model.BatchRows.Add(new BatchRowModel("batch-015", "shipper-70x50@v1", "18", "submitted", "Locked against retry while in flight"));
        model.BatchRows.Add(new BatchRowModel("batch-016", "basic-50x30@v2", "126", "failed", "Pending-proof seed rows blocked from dispatch"));
        model.BatchRows.Add(new BatchRowModel("batch-017", "proof-ticket@v1", "0", "draft", "Catalog save still missing"));
        model.ActivityRows.Add(new ActivityRowModel("14:28", "queue", "batch-014 revalidated after alias map adjustment", "ok"));
        model.ActivityRows.Add(new ActivityRowModel("14:24", "retry", "batch-016 kept eligible because rows remain in failed state", "watch"));
        model.ActivityRows.Add(new ActivityRowModel("14:20", "submit", "batch-015 locked while submitted rows are active", "done"));
        model.ActivityRows.Add(new ActivityRowModel("14:17", "validation", "12-digit numeric JAN blocked before staging", "done"));
        model.SessionDetailRows.Add(new PropertyRowModel("Session", "import-240416-a"));
        model.SessionDetailRows.Add(new PropertyRowModel("Workbook", "sales-apr16.xlsx"));
        model.SessionDetailRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));
        model.SessionDetailRows.Add(new PropertyRowModel("Rows ready", "40"));
        model.SessionDetailRows.Add(new PropertyRowModel("Rows warned", "2"));
        model.SessionDetailRows.Add(new PropertyRowModel("Submit route", "desktop-shell"));
        model.ControlSections.Add(new PropertySectionModel("Retry Rule", "Retry is intentionally narrow to avoid mutating rows that are already submitted.", new[] { new PropertyRowModel("Eligible states", "ready / failed"), new PropertyRowModel("Locked state", "submitted"), new PropertyRowModel("Current held batch", "batch-015") }));
        model.ControlSections.Add(new PropertySectionModel("Workbook Safety", "Import remains usable without a strict external database schema.", new[] { new PropertyRowModel("JAN hardening", "12-digit numeric blocked"), new PropertyRowModel("Template mismatch", "unknown template blocks queue"), new PropertyRowModel("Legacy proof seed", "pending only") }));
        model.MessageRows.Add(new MessageRowModel("Warn", "import", "sales-apr16.xlsx contains two rows with ambiguous numeric JAN values."));
        model.MessageRows.Add(new MessageRowModel("Warn", "queue", "batch-017 cannot submit because proof-ticket@v1 is still unsaved in the local catalog."));
        model.MessageRows.Add(new MessageRowModel("Info", "retry", "Failed-only rows remain eligible for controlled retry."));
        model.StatusItems.Add(new StatusItemModel("Queue session lock", "visible", "submit-time mutation guard active", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Template mismatch blocker", "enforced", "unknown template_version is stopped", "LOCK", Brushes.Firebrick));
        model.StatusItems.Add(new StatusItemModel("Import mode", "csv/xlsx", "external schema remains optional", "OPEN", Brushes.SteelBlue));
        return model;
    }

    private static HistoryWorkspaceModel BuildHistoryWorkspace()
    {
        var model = new HistoryWorkspaceModel { MessageSummary = "1 attention item / 2 clean", FooterDetail = "restore stays conflict-safe", LedgerSummary = "last 5 ledger events" };
        model.PendingProofs.Add(new QueueItemModel("proof-240416-017", "basic-50x30@v2 | 200-145-4", "pending", "Operator needs to confirm overlay save and subject match."));
        model.PendingProofs.Add(new QueueItemModel("proof-240416-020", "shipper-70x50@v1 | 200-145-9", "pending", "PDF artifact validated; waiting for manual approval."));
        model.PendingProofs.Add(new QueueItemModel("proof-240416-019", "basic-50x30@v2 | 200-145-5", "rejected", "Retained for audit visibility and retry follow-up."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-15-1800", "audit backup / 241 entries", "latest", "Validated bundle; safe candidate for explicit restore review."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-14-1800", "audit backup / 229 entries", "kept", "Retention baseline before workstation redesign release."));
        model.BundleRows.Add(new QueueItemModel("bundle-2026-04-12-1800", "audit backup / 201 entries", "archive", "Older bundle kept until trim policy is applied."));
        model.AuditRows.Add(new AuditRowModel("14:27", "proof", "200-145-3", "approved", "lineage-7ba1a3"));
        model.AuditRows.Add(new AuditRowModel("14:24", "dispatch", "200-145-3", "printed", "lineage-7ba1a3"));
        model.AuditRows.Add(new AuditRowModel("14:19", "proof", "200-145-5", "rejected", "lineage-45de90"));
        model.AuditRows.Add(new AuditRowModel("14:12", "backup", "bundle-2026-04-15-1800", "listed", "restore-safe"));
        model.AuditRows.Add(new AuditRowModel("14:08", "retention", "trim-dry-run", "clean", "0 destructive ops"));
        model.FilterRows.Add(new PropertyRowModel("Time window", "today / current shift"));
        model.FilterRows.Add(new PropertyRowModel("Lane filter", "proof + dispatch + retention"));
        model.FilterRows.Add(new PropertyRowModel("Subject search", "200-145-3"));
        model.FilterRows.Add(new PropertyRowModel("Export scope", "filtered current view"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Entry", "dispatch print job"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Subject", "200-145-3"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Approved proof", "proof-240416-018"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Artifact", "valid PDF"));
        model.SelectedEntryRows.Add(new PropertyRowModel("Retention bundle", "bundle-2026-04-15-1800"));
        model.ControlSections.Add(new PropertySectionModel("Proof Review", "Legacy proof seeds stay pending; approval still flows through operator review.", new[] { new PropertyRowModel("Approve path", "manual review only"), new PropertyRowModel("Reject path", "captured in audit"), new PropertyRowModel("Pin artifact", "allowed after approval") }));
        model.ControlSections.Add(new PropertySectionModel("Audit Recovery", "Restore must fail before merge if the bundle is invalid or conflicts.", new[] { new PropertyRowModel("Restore policy", "conflict-safe"), new PropertyRowModel("Bundle validation", "explicit pre-check"), new PropertyRowModel("Trim mode", "dry-run before apply") }));
        model.MessageRows.Add(new MessageRowModel("Warn", "proof", "proof-240416-017 is still pending and blocks the related dispatch lane."));
        model.MessageRows.Add(new MessageRowModel("Info", "audit", "Filtered export is ready for the current shift view."));
        model.MessageRows.Add(new MessageRowModel("Info", "restore", "Latest audit backup bundle validated with no merge conflict warnings."));
        model.StatusItems.Add(new StatusItemModel("Pending proofs", "2 active", "review queue still requires operator action", "WATCH", Brushes.DarkGoldenrod));
        model.StatusItems.Add(new StatusItemModel("Audit export", "ready", "filtered ledger export available", "OK", Brushes.ForestGreen));
        model.StatusItems.Add(new StatusItemModel("Restore path", "guarded", "bundle validation before merge", "SAFE", Brushes.SteelBlue));
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
}
