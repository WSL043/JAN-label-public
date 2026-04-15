using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace JanLabel.WindowsShell;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private ModuleModel? _selectedModule;
    private string _toolPaneTitle = "Tasks";
    private string _workspaceTitle = "Job Document";
    private string _workspaceSummary = "Operator input, execution intent, and the current working payload.";
    private string _workspaceFocus = "Working copy drives submit. Frozen review copy stays reference-only.";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SeedStaticState();
        SelectedModule = Modules[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModuleModel> Modules { get; } = new();

    public ObservableCollection<RibbonGroupModel> RibbonGroups { get; } = new();

    public ObservableCollection<ToolItemModel> ToolPaneItems { get; } = new();

    public ObservableCollection<PropertyRowModel> DocumentProperties { get; } = new();

    public ObservableCollection<WorkbenchPanelModel> WorkbenchPanels { get; } = new();

    public ObservableCollection<InspectorSectionModel> InspectorSections { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public ModuleModel? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                LoadModuleState(value);
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ActiveDocumentMeta));
            }
        }
    }

    public string ToolPaneTitle
    {
        get => _toolPaneTitle;
        set => SetProperty(ref _toolPaneTitle, value);
    }

    public string WorkspaceTitle
    {
        get => _workspaceTitle;
        set => SetProperty(ref _workspaceTitle, value);
    }

    public string WorkspaceSummary
    {
        get => _workspaceSummary;
        set => SetProperty(ref _workspaceSummary, value);
    }

    public string WorkspaceFocus
    {
        get => _workspaceFocus;
        set => SetProperty(ref _workspaceFocus, value);
    }

    public string ActiveDocumentName => "JOB-20260416-001";

    public string ActiveDocumentMeta => $"{SelectedModule?.Label ?? "Operator Workstation"} | basic-50x30@v1";

    public string WindowTitle => $"JAN Label Operator Console - {SelectedModule?.Label ?? "Workstation"}";

    private void SeedStaticState()
    {
        Modules.Add(new ModuleModel("compose", "Job Setup", "Working copy, operator data, and proof intent.", "ready"));
        Modules.Add(new ModuleModel("templates", "Designer", "Canvas, objects, renderer, and local library.", "valid"));
        Modules.Add(new ModuleModel("queue", "Batch Manager", "Import session, validation, and dispatch queue.", "42"));
        Modules.Add(new ModuleModel("audit", "History", "Proof approvals, audit ledger, and recovery.", "6 pending"));

        DocumentProperties.Add(new PropertyRowModel("Job", "JOB-20260416-001"));
        DocumentProperties.Add(new PropertyRowModel("Template", "basic-50x30@v1"));
        DocumentProperties.Add(new PropertyRowModel("Mode", "proof"));
        DocumentProperties.Add(new PropertyRowModel("Bridge", "desktop-shell"));

        StatusItems.Add(new StatusItemModel("Bridge", "ready", "desktop-shell backend"));
        StatusItems.Add(new StatusItemModel("Payload", "armed", "live working copy"));
        StatusItems.Add(new StatusItemModel("Queue", "42 rows", "31 ready / 1 failed"));
        StatusItems.Add(new StatusItemModel("Audit", "6 pending", "restore capable"));
    }

    private void LoadModuleState(ModuleModel? module)
    {
        RibbonGroups.Clear();
        ToolPaneItems.Clear();
        WorkbenchPanels.Clear();
        InspectorSections.Clear();

        if (module is null)
        {
            return;
        }

        switch (module.Key)
        {
            case "compose":
                LoadComposeState();
                break;
            case "templates":
                LoadTemplateState();
                break;
            case "queue":
                LoadQueueState();
                break;
            default:
                LoadAuditState();
                break;
        }
    }

    private void LoadComposeState()
    {
        ToolPaneTitle = "Tasks";
        WorkspaceTitle = "Job Document";
        WorkspaceSummary = "Operator input, execution intent, and the current working payload.";
        WorkspaceFocus = "Working copy drives submit. Frozen review copy stays reference-only.";
        RibbonGroups.Add(new RibbonGroupModel("File", "Refresh bridge", "Load workspace", "Clear workspace"));
        RibbonGroups.Add(new RibbonGroupModel("Document", "Freeze review copy", "New job"));
        RibbonGroups.Add(new RibbonGroupModel("Proof / Print", "Submit live job"));
        ToolPaneItems.Add(new ToolItemModel("Working copy", "live"));
        ToolPaneItems.Add(new ToolItemModel("Review copy", "frozen"));
        ToolPaneItems.Add(new ToolItemModel("Authority", "proof-gated"));
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Payload",
                "Current working copy",
                new[]
                {
                    "SKU 200-145-3 / Parent SKU 200-145",
                    "JAN 4901234567894 / Qty 24 / Brand JAN-LAB",
                    "Execution intent proof / requested by operator-1",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Proof Boundary",
                "Submit authority",
                new[]
                {
                    "Live payload is authoritative.",
                    "Frozen review copy is export/reference only.",
                    "desktop-shell enforces lineage and approved-proof checks.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Preview",
                "Document verification",
                new[]
                {
                    "Template route basic-50x30@v1",
                    "Adapter PDF proof",
                    "Local catalog remains the dispatch source of truth.",
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Document",
                "Operator context",
                new[]
                {
                    new PropertyRowModel("Requested by", "operator-1"),
                    new PropertyRowModel("Requested at", "2026-04-16 14:20"),
                    new PropertyRowModel("Execution", "proof / review-first"),
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Authority",
                "Dispatch rules",
                new[]
                {
                    new PropertyRowModel("Template authority", "saved local catalog only"),
                    new PropertyRowModel("Proof gate", "approved lineage + subject match"),
                    new PropertyRowModel("Fallback", "browser mode remains submit-disabled"),
                }
            )
        );
    }

    private void LoadTemplateState()
    {
        ToolPaneTitle = "Toolbox";
        WorkspaceTitle = "Template Designer";
        WorkspaceSummary = "Canvas authoring, object properties, library save, and renderer parity.";
        WorkspaceFocus = "Local draft, preview, and library authority stay separate so unsaved edits never dispatch.";
        RibbonGroups.Add(new RibbonGroupModel("Authoring", "Check template", "Reset designer", "Refresh renderer"));
        RibbonGroups.Add(new RibbonGroupModel("Library", "Publish local copy", "Inspect manifest"));
        RibbonGroups.Add(new RibbonGroupModel("Selection", "Page setup", "Objects", "Preview", "Library"));
        ToolPaneItems.Add(new ToolItemModel("Page Setup", "valid"));
        ToolPaneItems.Add(new ToolItemModel("Objects", "8 fields"));
        ToolPaneItems.Add(new ToolItemModel("Preview", "rust-ready"));
        ToolPaneItems.Add(new ToolItemModel("Library", "local"));
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Canvas",
                "Layout surface",
                new[]
                {
                    "50mm x 30mm media / border on",
                    "Object list: SKU, JAN, brand, qty",
                    "Selection model and property grid are native-shell targets",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Renderer",
                "Rust parity review",
                new[]
                {
                    "Rust preview stays authoritative for output parity.",
                    "Local canvas is for editing speed and geometry review.",
                    "Saved library state remains the dispatch boundary.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Library",
                "Catalog governance",
                new[]
                {
                    "Packaged + local overlay resolution shown together.",
                    "Manifest health and orphaned JSON need dedicated operator UX.",
                    "Single-writer repair guidance remains visible.",
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Selection",
                "Current object",
                new[]
                {
                    new PropertyRowModel("Object", "jan"),
                    new PropertyRowModel("Binding", "{{jan}}"),
                    new PropertyRowModel("Font", "Segoe UI 10pt"),
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Authority",
                "Catalog rules",
                new[]
                {
                    new PropertyRowModel("Draft", "not authoritative"),
                    new PropertyRowModel("Library", "local overlay wins when saved"),
                    new PropertyRowModel("Repair", "manifest + backup guidance required"),
                }
            )
        );
    }

    private void LoadQueueState()
    {
        ToolPaneTitle = "Views";
        WorkspaceTitle = "Batch Manager";
        WorkspaceSummary = "Spreadsheet intake, row validation, and controlled batch dispatch.";
        WorkspaceFocus = "Batch mutation lock remains active until the current dispatch session completes.";
        RibbonGroups.Add(new RibbonGroupModel("Batch", "Build batch", "Clear batch", "Reset import"));
        RibbonGroups.Add(new RibbonGroupModel("Dispatch", "Run batch", "Retry failed"));
        RibbonGroups.Add(new RibbonGroupModel("View", "All rows", "Ready rows", "Failed rows"));
        ToolPaneItems.Add(new ToolItemModel("All rows", "42"));
        ToolPaneItems.Add(new ToolItemModel("Ready rows", "31"));
        ToolPaneItems.Add(new ToolItemModel("Failed rows", "1"));
        ToolPaneItems.Add(new ToolItemModel("Submitted rows", "10"));
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Import Session",
                "Workbook intake",
                new[]
                {
                    "CSV/XLSX mapping remains operator-driven.",
                    "Risky numeric JAN cells remain blocked or warned.",
                    "Queue snapshot is built from validated rows only.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Grid",
                "Dispatch ledger view",
                new[]
                {
                    "Sticky headers, sort, filter, and paging stay dense.",
                    "Rows show ready / failed / submitted state.",
                    "Mutation controls lock while batch dispatch is active.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Recovery",
                "Retry scope",
                new[]
                {
                    "Only ready and failed rows can re-enter dispatch.",
                    "Submitted rows remain immutable.",
                    "Lineage and proof gate still resolve in desktop-shell.",
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Selected Row",
                "Current queue selection",
                new[]
                {
                    new PropertyRowModel("Row", "18"),
                    new PropertyRowModel("Status", "failed"),
                    new PropertyRowModel("Reason", "template version not found"),
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Session",
                "Batch boundaries",
                new[]
                {
                    new PropertyRowModel("Snapshot", "queue-20260416-01"),
                    new PropertyRowModel("Mutation lock", "active during submit"),
                    new PropertyRowModel("Retry policy", "ready + failed only"),
                }
            )
        );
    }

    private void LoadAuditState()
    {
        ToolPaneTitle = "Views";
        WorkspaceTitle = "Audit Ledger";
        WorkspaceSummary = "Proof review, export, retention, bundle inventory, and recovery.";
        WorkspaceFocus = "History combines proof review, export, retention, bundle inventory, and restore.";
        RibbonGroups.Add(new RibbonGroupModel("Audit", "Refresh ledger", "Refresh bundles", "Export records"));
        RibbonGroups.Add(new RibbonGroupModel("Recovery", "Restore bundle", "Inspect bundle"));
        RibbonGroups.Add(new RibbonGroupModel("Proof", "Approve", "Reject", "Pin proof"));
        ToolPaneItems.Add(new ToolItemModel("All entries", "198"));
        ToolPaneItems.Add(new ToolItemModel("Proof only", "34"));
        ToolPaneItems.Add(new ToolItemModel("Print only", "164"));
        ToolPaneItems.Add(new ToolItemModel("Pending proof", "6"));
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Ledger",
                "Operational history",
                new[]
                {
                    "Proof and print events stay in the same lineage family.",
                    "Retention and export operate on the persisted local ledger.",
                    "Bundle inventory is part of the audit workstation, not a side list.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Restore",
                "Bundle recovery",
                new[]
                {
                    "Restore remains all-or-nothing on conflict or invalid input.",
                    "Success refreshes ledger search and backup inventory.",
                    "UI must keep restore confirmation explicit.",
                }
            )
        );
        WorkbenchPanels.Add(
            new WorkbenchPanelModel(
                "Proof Review",
                "Inbox discipline",
                new[]
                {
                    "Legacy proofs seed only as pending.",
                    "Approval is always operator-driven.",
                    "Approved artifact path and PDF validity remain enforced.",
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Selected Entry",
                "Focused ledger record",
                new[]
                {
                    new PropertyRowModel("Job ID", "proof-20260416-004"),
                    new PropertyRowModel("Mode", "proof"),
                    new PropertyRowModel("Status", "pending"),
                }
            )
        );
        InspectorSections.Add(
            new InspectorSectionModel(
                "Recovery",
                "Bundle restore state",
                new[]
                {
                    new PropertyRowModel("Bundle", "audit-backup-20260416.json"),
                    new PropertyRowModel("Confirmation", "required"),
                    new PropertyRowModel("Apply mode", "merge restore / fail on conflict"),
                }
            )
        );
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ModuleModel
{
    public ModuleModel(string key, string label, string description, string badge)
    {
        Key = key;
        Label = label;
        Description = description;
        Badge = badge;
    }

    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public string Badge { get; }
}

public sealed class RibbonGroupModel
{
    public RibbonGroupModel(string title, params string[] commands)
    {
        Title = title;
        Commands = new ObservableCollection<string>(commands);
    }

    public string Title { get; }

    public ObservableCollection<string> Commands { get; }
}

public sealed class ToolItemModel
{
    public ToolItemModel(string label, string badge)
    {
        Label = label;
        Badge = badge;
    }

    public string Label { get; }

    public string Badge { get; }
}

public sealed class PropertyRowModel
{
    public PropertyRowModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string Value { get; }
}

public sealed class WorkbenchPanelModel
{
    public WorkbenchPanelModel(string title, string summary, IEnumerable<string> lines)
    {
        Title = title;
        Summary = summary;
        Lines = new ObservableCollection<string>(lines);
    }

    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<string> Lines { get; }
}

public sealed class InspectorSectionModel
{
    public InspectorSectionModel(string title, string summary, IEnumerable<PropertyRowModel> rows)
    {
        Title = title;
        Summary = summary;
        Rows = new ObservableCollection<PropertyRowModel>(rows);
    }

    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<PropertyRowModel> Rows { get; }
}

public sealed class StatusItemModel
{
    public StatusItemModel(string label, string value, string detail)
    {
        Label = label;
        Value = value;
        Detail = detail;
    }

    public string Label { get; }

    public string Value { get; }

    public string Detail { get; }
}
