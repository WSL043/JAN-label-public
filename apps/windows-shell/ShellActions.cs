namespace JanLabel.WindowsShell;

internal static class ShellActions
{
    public const string RefreshState = "Refresh State";
    public const string RefreshSubjects = "Refresh Subjects";
    public const string RefreshQueue = "Refresh Queue";
    public const string ViewPreview = "View Preview";
    public const string RustPreview = "Rust Preview";
    public const string PrintPreview = "Print Preview";
    public const string RouteCheck = "Route Check";
    public const string RefreshAudit = "Refresh Audit";
    public const string OpenLibrary = "Open Library";
    public const string OverlayStatus = "Overlay Status";
    public const string CatalogRules = "Catalog Rules";
    public const string ListBundles = "List Bundles";
    public const string QueueSnapshot = "Queue Snapshot";
    public const string ApproveProof = "Approve Proof";
    public const string Approve = "Approve";
    public const string RejectProof = "Reject Proof";
    public const string Reject = "Reject";
    public const string ExportAudit = "Export Audit";
    public const string OpenHandoff = "Open Handoff";
    public const string CurrentState = "Current State";
    public const string ReleaseNotes = "Release Notes";
    public const string PreviewPackage = "Preview Package";
    public const string PinWorkspace = "Pin Workspace";
    public const string ExportState = "Export State";
    public const string NewFormat = "New Format";
    public const string SaveToCatalog = "Save to Catalog";
    public const string Hold = "Hold";
    public const string Release = "Release";
    public const string OpenPdf = "Open PDF";
    public const string DispatchBatch = "Dispatch Batch";
    public const string RunProof = "Run Proof";
    public const string Print = "Print";
    public const string Proof = "Proof";
    public const string ImportWorkbook = "Import Workbook";
    public const string RetryFailed = "Retry Failed";
    public const string Csv = "CSV";
    public const string Xlsx = "XLSX";
    public const string AliasMap = "Alias Map";
    public const string SubmitReady = "Submit Ready";
    public const string FreezeRow = "Freeze Row";
    public const string FixtureCheck = "Fixture Check";
    public const string UnknownTemplate = "Unknown Template";
    public const string JanWarnings = "JAN Warnings";
    public const string TrimRetention = "Trim Retention";
    public const string PinArtifact = "Pin Artifact";
    public const string RetentionDryRun = "Retention Dry Run";
    public const string ValidateBundle = "Validate Bundle";
    public const string Restore = "Restore";
    public const string Paste = "Paste";
    public const string Duplicate = "Duplicate";
    public const string Delete = "Delete";
    public const string Text = "Text";
    public const string Barcode = "Barcode";
    public const string Line = "Line";
    public const string Box = "Box";
    public const string AlignLeft = "Align Left";
    public const string MakeSameSize = "Make Same Size";
    public const string Snap = "Snap";
    public const string RecordBrowser = "Record Browser";
    public const string QueryPrompt = "Query Prompt";
    public const string NamedDataSources = "Named Data Sources";

    public static readonly string[] RefreshSnapshotActions =
    {
        RefreshState,
        RefreshSubjects,
        RefreshQueue,
        ViewPreview,
        RustPreview,
        PrintPreview,
        RouteCheck,
        RefreshAudit,
        OpenLibrary,
        OverlayStatus,
        CatalogRules,
        ListBundles,
        QueueSnapshot,
    };

    public static readonly string[] TemplateCatalogRefreshActions = { OpenLibrary, OverlayStatus, CatalogRules };
    public static readonly string[] PreviewRefreshActions = { ViewPreview, RustPreview, PrintPreview };
    public static readonly string[] PrintSubjectRefreshActions = { RefreshSubjects, RefreshQueue, RouteCheck };
    public static readonly string[] AuditRefreshActions = { RefreshAudit };
    public static readonly string[] AuditBundleRefreshActions = { ListBundles };
    public static readonly string[] BatchSnapshotRefreshActions = { QueueSnapshot };
    public static readonly string[] ApproveActions = { ApproveProof, Approve };
    public static readonly string[] RejectActions = { RejectProof, Reject };
    public static readonly string[] ReviewActions = { ApproveProof, Approve, RejectProof, Reject };

    public static readonly string[] HomeRouteActions =
    {
        RefreshState,
        ViewPreview,
        CurrentState,
        ReleaseNotes,
        PreviewPackage,
        PinWorkspace,
        ExportState,
    };

    public static readonly string[] DesignerRouteActions =
    {
        OpenLibrary,
        OverlayStatus,
        CatalogRules,
        PrintPreview,
        RustPreview,
    };

    public static readonly string[] PrintConsoleRouteActions =
    {
        RefreshSubjects,
        RefreshQueue,
        Hold,
        Release,
        OpenPdf,
        DispatchBatch,
        RouteCheck,
        RunProof,
        Print,
        Proof,
    };

    public static readonly string[] BatchRouteActions =
    {
        ImportWorkbook,
        RetryFailed,
        QueueSnapshot,
        Csv,
        Xlsx,
        AliasMap,
        SubmitReady,
        FreezeRow,
        FixtureCheck,
        UnknownTemplate,
        JanWarnings,
    };

    public static readonly string[] HistoryRouteActions =
    {
        RefreshAudit,
        ExportAudit,
        TrimRetention,
        PinArtifact,
        RetentionDryRun,
        ListBundles,
        ValidateBundle,
        Restore,
    };

    public static readonly string[] HomeDefaultFocusActions = { RefreshState, CurrentState, ViewPreview };
    public static readonly string[] HomeFallbackFocusActions = { PreviewPackage, ReleaseNotes, OpenHandoff };
    public static readonly string[] DesignerTemplateFocusActions = { OpenLibrary, OverlayStatus, SaveToCatalog };
    public static readonly string[] DesignerDraftFocusActions = { CatalogRules };
    public static readonly string[] DesignerPreviewFocusActions = { PrintPreview, RustPreview, Barcode };
    public static readonly string[] PrintProofFocusActions = { OpenPdf, Approve, Reject, RunProof, Proof };
    public static readonly string[] PrintHoldFocusActions = { Hold, Release };
    public static readonly string[] PrintSubjectFocusActions = { RefreshSubjects, RefreshQueue, RouteCheck, DispatchBatch, Print };
    public static readonly string[] BatchImportFocusActions = { ImportWorkbook, Csv, Xlsx, AliasMap };
    public static readonly string[] BatchRetryFocusActions = { RetryFailed };
    public static readonly string[] BatchReadyFocusActions = { SubmitReady, QueueSnapshot, FixtureCheck };
    public static readonly string[] BatchFreezeFocusActions = { FreezeRow };
    public static readonly string[] BatchDraftFocusActions = { UnknownTemplate };
    public static readonly string[] BatchWarningFocusActions = { JanWarnings };
    public static readonly string[] HistoryAuditFocusActions = { PinArtifact, RefreshAudit, ExportAudit };
    public static readonly string[] HistoryRetentionFocusActions = { TrimRetention, RetentionDryRun };
    public static readonly string[] HistoryRestoreFocusActions = { ListBundles, ValidateBundle, Restore };
}
