using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace JanLabel.WindowsShell;

public sealed class ModuleModel
{
    public ModuleModel(
        string label,
        string description,
        string badge,
        string tagline,
        string lead,
        IEnumerable<string> headerActions,
        IEnumerable<RibbonGroupModel> ribbonGroups,
        IEnumerable<ContextBadgeModel> contextBadges,
        IEnumerable<StatusStripItemModel> statusStripItems,
        WorkspaceModel workspace)
        : this(
            label,
            description,
            badge,
            tagline,
            lead,
            headerActions.Select(ShellActionModel.Enabled),
            ribbonGroups,
            contextBadges,
            statusStripItems,
            workspace)
    {
    }

    public ModuleModel(
        string label,
        string description,
        string badge,
        string tagline,
        string lead,
        IEnumerable<ShellActionModel> headerActions,
        IEnumerable<RibbonGroupModel> ribbonGroups,
        IEnumerable<ContextBadgeModel> contextBadges,
        IEnumerable<StatusStripItemModel> statusStripItems,
        WorkspaceModel workspace)
    {
        Label = label;
        Description = description;
        Badge = badge;
        Tagline = tagline;
        Lead = lead;
        HeaderActions = new ObservableCollection<ShellActionModel>(headerActions);
        RibbonGroups = new ObservableCollection<RibbonGroupModel>(ribbonGroups);
        ContextBadges = new ObservableCollection<ContextBadgeModel>(contextBadges);
        StatusStripItems = new ObservableCollection<StatusStripItemModel>(statusStripItems);
        Workspace = workspace;
    }

    public string Label { get; }

    public string Description { get; }

    public string Badge { get; }

    public string Tagline { get; }

    public string Lead { get; }

    public ObservableCollection<ShellActionModel> HeaderActions { get; }

    public ObservableCollection<RibbonGroupModel> RibbonGroups { get; }

    public ObservableCollection<ContextBadgeModel> ContextBadges { get; }

    public ObservableCollection<StatusStripItemModel> StatusStripItems { get; }

    public WorkspaceModel Workspace { get; }
}

public abstract class BindableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public abstract class WorkspaceModel : BindableModel;

public sealed class HomeWorkspaceModel : WorkspaceModel
{
    public ObservableCollection<SummaryCardModel> SummaryCards { get; } = new();

    public ObservableCollection<PropertyRowModel> SessionRows { get; } = new();

    public TemplateLibraryPanelModel TemplateLibrary { get; } = new();

    public ObservableCollection<ActivityRowModel> ActivityRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<QueueItemModel> NextSteps { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public string HeaderDetail { get; init; } = string.Empty;

    public string StatusSummary { get; init; } = string.Empty;

    public string ActivitySummary { get; init; } = string.Empty;

    public bool FocusTemplateEntry(string templateName)
    {
        return TemplateLibrary.TrySelectTemplate(templateName);
    }

    public bool FocusTemplateByState(string state)
    {
        return TemplateLibrary.TrySelectFirst((entry) => string.Equals(entry.State, state, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class DesignerWorkspaceModel : WorkspaceModel
{
    private CanvasElementModel? _selectedCanvasElement;
    private DesignerSelectionModel _selectedElementProperties = new();

    public DesignerWorkspaceModel()
    {
        _selectedElementProperties.PropertyChanged += SelectedElementPropertiesChanged;
    }

    public ObservableCollection<ToolboxGroupModel> ToolboxGroups { get; } = new();

    public TemplateLibraryPanelModel TemplateLibrary { get; } = new();

    public ObservableCollection<ObjectNodeModel> ObjectNodes { get; } = new();

    public ObservableCollection<DataSourceRowModel> DataSources { get; } = new();

    public ObservableCollection<DocumentTabModel> DocumentTabs { get; } = new();

    public ObservableCollection<CanvasElementModel> CanvasElements { get; } = new();

    public ObservableCollection<PropertySectionModel> PropertySections { get; } = new();

    public ObservableCollection<PropertyRowModel> SetupRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectionRows { get; } = new();

    public ObservableCollection<PropertyRowModel> RecordRows { get; } = new();

    public ObservableCollection<PropertyRowModel> PreviewRows { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public ObservableCollection<string> TopRulerMarks { get; } = new();

    public ObservableCollection<string> SideRulerMarks { get; } = new();

    public CanvasElementModel? SelectedCanvasElement
    {
        get => _selectedCanvasElement;
        set
        {
            if (ReferenceEquals(_selectedCanvasElement, value))
            {
                return;
            }

            if (_selectedCanvasElement is not null)
            {
                _selectedCanvasElement.IsHighlighted = false;
            }

            _selectedCanvasElement = value;
            if (_selectedCanvasElement is not null)
            {
                _selectedCanvasElement.IsHighlighted = true;
            }

            SyncSelectionFromCanvasElement(_selectedCanvasElement);
            RefreshSelectionRows();
            OnPropertyChanged();
        }
    }

    public DesignerSelectionModel SelectedElementProperties
    {
        get => _selectedElementProperties;
        private set
        {
            if (ReferenceEquals(_selectedElementProperties, value))
            {
                return;
            }

            _selectedElementProperties.PropertyChanged -= SelectedElementPropertiesChanged;
            _selectedElementProperties = value;
            _selectedElementProperties.PropertyChanged += SelectedElementPropertiesChanged;
            OnPropertyChanged();
        }
    }

    public string CanvasMeta { get; init; } = string.Empty;

    public string CanvasHint { get; init; } = string.Empty;

    public string MessageSummary { get; init; } = string.Empty;

    public string RecordSummary { get; init; } = string.Empty;

    public string StatusSummary { get; init; } = string.Empty;

    public string CatalogSummary { get; init; } = string.Empty;

    public string ToolboxSummary { get; init; } = string.Empty;

    public string ObjectBrowserSummary { get; init; } = string.Empty;

    public string DataSourceSummary { get; init; } = string.Empty;

    public double CanvasWidth { get; init; }

    public double CanvasHeight { get; init; }

    public string PrimaryDocumentTitle { get; init; } = "Format Surface";

    public string SecondaryDocumentTitle { get; init; } = "Proof Preview";

    public string PreviewSvg { get; init; } = string.Empty;

    public void SelectCanvasElement(CanvasElementModel? element)
    {
        SelectedCanvasElement = element;
    }

    public bool FocusTemplateEntry(string templateName)
    {
        return TemplateLibrary.TrySelectTemplate(templateName);
    }

    public bool FocusTemplateByState(string state)
    {
        return TemplateLibrary.TrySelectFirst((entry) => string.Equals(entry.State, state, StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusCanvasElement(string caption)
    {
        var match = CanvasElements.FirstOrDefault((element) => string.Equals(element.Caption, caption, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        SelectCanvasElement(match);
        return true;
    }

    private void SelectedElementPropertiesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (SelectedCanvasElement is null)
        {
            return;
        }

        SelectedCanvasElement.Caption = SelectedElementProperties.Name;
        SelectedCanvasElement.Value = SelectedElementProperties.Binding;
        SelectedCanvasElement.X = Math.Round(SelectedElementProperties.Xmm * 10, 1);
        SelectedCanvasElement.Y = Math.Round(SelectedElementProperties.Ymm * 10, 1);
        SelectedCanvasElement.Width = Math.Round(SelectedElementProperties.WidthMm * 10, 1);
        SelectedCanvasElement.Height = Math.Round(SelectedElementProperties.HeightMm * 10, 1);
        RefreshSelectionRows();
    }

    private void SyncSelectionFromCanvasElement(CanvasElementModel? element)
    {
        SelectedElementProperties = element is null
            ? new DesignerSelectionModel()
            : new DesignerSelectionModel
            {
                Name = element.Caption,
                Binding = element.Value,
                Symbology = element.Caption == "BARCODE" ? "EAN-13 / JAN" : "Text",
                Xmm = Math.Round(element.X / 10, 1),
                Ymm = Math.Round(element.Y / 10, 1),
                WidthMm = Math.Round(element.Width / 10, 1),
                HeightMm = Math.Round(element.Height / 10, 1),
                BarcodeEngine = element.Caption == "BARCODE" ? "Zint" : "n/a",
                OutputTargets = "SVG / PDF",
                DispatchAuthority = "desktop-shell approved proof lineage",
            };
    }

    private void RefreshSelectionRows()
    {
        SelectionRows.Clear();
        SelectionRows.Add(new PropertyRowModel("X", $"{SelectedElementProperties.Xmm:0.0} mm"));
        SelectionRows.Add(new PropertyRowModel("Y", $"{SelectedElementProperties.Ymm:0.0} mm"));
        SelectionRows.Add(new PropertyRowModel("Width", $"{SelectedElementProperties.WidthMm:0.0} mm"));
        SelectionRows.Add(new PropertyRowModel("Height", $"{SelectedElementProperties.HeightMm:0.0} mm"));
        SelectionRows.Add(new PropertyRowModel("Object", SelectedElementProperties.Name));
    }
}

public sealed class DesignerSelectionModel : BindableModel
{
    private string _name = string.Empty;
    private string _binding = string.Empty;
    private string _symbology = string.Empty;
    private double _xmm;
    private double _ymm;
    private double _widthMm;
    private double _heightMm;
    private string _barcodeEngine = string.Empty;
    private string _outputTargets = string.Empty;
    private string _dispatchAuthority = string.Empty;

    [Category("Identity")]
    [DisplayName("Object")]
    [Description("Display name for the selected design object.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    [Category("Identity")]
    [Description("Current binding or literal content used by the object.")]
    public string Binding
    {
        get => _binding;
        set => SetProperty(ref _binding, value);
    }

    [Category("Barcode")]
    [Description("Barcode or object implementation used for deterministic output.")]
    public string Symbology
    {
        get => _symbology;
        set => SetProperty(ref _symbology, value);
    }

    [Category("Position")]
    [DisplayName("X (mm)")]
    public double Xmm
    {
        get => _xmm;
        set => SetProperty(ref _xmm, value);
    }

    [Category("Position")]
    [DisplayName("Y (mm)")]
    public double Ymm
    {
        get => _ymm;
        set => SetProperty(ref _ymm, value);
    }

    [Category("Size")]
    [DisplayName("Width (mm)")]
    public double WidthMm
    {
        get => _widthMm;
        set => SetProperty(ref _widthMm, value);
    }

    [Category("Size")]
    [DisplayName("Height (mm)")]
    public double HeightMm
    {
        get => _heightMm;
        set => SetProperty(ref _heightMm, value);
    }

    [Category("Output")]
    [Description("Rendering engine used for production-safe barcode output.")]
    public string BarcodeEngine
    {
        get => _barcodeEngine;
        set => SetProperty(ref _barcodeEngine, value);
    }

    [Category("Output")]
    [Description("Current release scope for render targets.")]
    public string OutputTargets
    {
        get => _outputTargets;
        set => SetProperty(ref _outputTargets, value);
    }

    [Category("Authority")]
    [Description("Current production authority for proof and print dispatch.")]
    public string DispatchAuthority
    {
        get => _dispatchAuthority;
        set => SetProperty(ref _dispatchAuthority, value);
    }
}

public sealed class PrintConsoleWorkspaceModel : WorkspaceModel
{
    private QueueItemModel? _selectedProof;
    private JobRowModel? _selectedJob;
    private string _selectionHeading = "Select a proof or job";
    private string _selectionSummary = "Review route, blocker, and next action before dispatch.";

    public ObservableCollection<QueueItemModel> ProofQueue { get; } = new();

    public ObservableCollection<PropertyRowModel> RouteRows { get; } = new();

    public ObservableCollection<JobRowModel> JobRows { get; } = new();

    public ObservableCollection<ActivityRowModel> TimelineRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectedJobRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public QueueItemModel? SelectedProof
    {
        get => _selectedProof;
        set
        {
            if (ReferenceEquals(_selectedProof, value))
            {
                return;
            }

            _selectedProof = value;
            if (value is not null)
            {
                _selectedJob = null;
                OnPropertyChanged(nameof(SelectedJob));
            }

            RefreshSelectionFromProof(value);
            OnPropertyChanged();
        }
    }

    public JobRowModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (ReferenceEquals(_selectedJob, value))
            {
                return;
            }

            _selectedJob = value;
            if (value is not null)
            {
                _selectedProof = null;
                OnPropertyChanged(nameof(SelectedProof));
            }

            RefreshSelectionFromJob(value);
            OnPropertyChanged();
        }
    }

    public string SelectionHeading
    {
        get => _selectionHeading;
        private set => SetProperty(ref _selectionHeading, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string JobSummary { get; init; } = string.Empty;

    public string ProofQueueSummary { get; init; } = string.Empty;

    public string TimelineSummary { get; init; } = string.Empty;

    public bool FocusPendingProof()
    {
        return TrySelectProof((proof) => string.Equals(proof.Badge, "pending", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusApprovedProof()
    {
        return TrySelectProof((proof) => string.Equals(proof.Badge, "approved", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusReadyJob()
    {
        return TrySelectJob((job) => string.Equals(job.Status, "ready", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusHeldJob()
    {
        return TrySelectJob((job) => string.Equals(job.Status, "held", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusBlockedJob()
    {
        return TrySelectJob((job) => string.Equals(job.Status, "blocked", StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshSelectionFromProof(QueueItemModel? proof)
    {
        SelectedJobRows.Clear();

        if (proof is null)
        {
            SelectionHeading = "Select a proof or job";
            SelectionSummary = "Review route, blocker, and next action before dispatch.";
            return;
        }

        SelectionHeading = proof.Label;
        SelectionSummary = proof.Note;
        SelectedJobRows.Add(new PropertyRowModel("Subject", proof.Subtext));
        SelectedJobRows.Add(new PropertyRowModel("State", proof.Badge));
        SelectedJobRows.Add(new PropertyRowModel("Authority", proof.Owner));
        SelectedJobRows.Add(new PropertyRowModel("Route", proof.Route));
        SelectedJobRows.Add(new PropertyRowModel("Blocker", proof.Blocker));
        SelectedJobRows.Add(new PropertyRowModel("Next action", proof.NextAction));
    }

    private void RefreshSelectionFromJob(JobRowModel? job)
    {
        SelectedJobRows.Clear();

        if (job is null)
        {
            SelectionHeading = "Select a proof or job";
            SelectionSummary = "Review route, blocker, and next action before dispatch.";
            return;
        }

        SelectionHeading = job.Subject;
        SelectionSummary = job.Note;
        SelectedJobRows.Add(new PropertyRowModel("Template", job.Template));
        SelectedJobRows.Add(new PropertyRowModel("Proof", job.Proof));
        SelectedJobRows.Add(new PropertyRowModel("Route", job.Route));
        SelectedJobRows.Add(new PropertyRowModel("Status", job.Status));
        SelectedJobRows.Add(new PropertyRowModel("JAN", job.Jan));
        SelectedJobRows.Add(new PropertyRowModel("Qty", job.Qty));
        SelectedJobRows.Add(new PropertyRowModel("Lineage", job.Lineage));
        SelectedJobRows.Add(new PropertyRowModel("Blocker", job.Blocker));
        SelectedJobRows.Add(new PropertyRowModel("Next action", job.NextAction));
    }

    private bool TrySelectProof(Func<QueueItemModel, bool> predicate)
    {
        var match = ProofQueue.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedProof = match;
        return true;
    }

    private bool TrySelectJob(Func<JobRowModel, bool> predicate)
    {
        var match = JobRows.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedJob = match;
        return true;
    }
}

public sealed class BatchJobsWorkspaceModel : WorkspaceModel
{
    private QueueItemModel? _selectedImportSession;
    private BatchRowModel? _selectedBatch;
    private string _selectionHeading = "Select an import session or batch";
    private string _selectionSummary = "Review queue readiness, blockers, and retry rules before submit.";

    public ObservableCollection<QueueItemModel> ImportSessions { get; } = new();

    public ObservableCollection<PropertyRowModel> ColumnRows { get; } = new();

    public ObservableCollection<BatchRowModel> BatchRows { get; } = new();

    public ObservableCollection<ActivityRowModel> ActivityRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SessionDetailRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public QueueItemModel? SelectedImportSession
    {
        get => _selectedImportSession;
        set
        {
            if (ReferenceEquals(_selectedImportSession, value))
            {
                return;
            }

            _selectedImportSession = value;
            if (value is not null)
            {
                _selectedBatch = null;
                OnPropertyChanged(nameof(SelectedBatch));
            }

            RefreshSelectionFromImportSession(value);
            OnPropertyChanged();
        }
    }

    public BatchRowModel? SelectedBatch
    {
        get => _selectedBatch;
        set
        {
            if (ReferenceEquals(_selectedBatch, value))
            {
                return;
            }

            _selectedBatch = value;
            if (value is not null)
            {
                _selectedImportSession = null;
                OnPropertyChanged(nameof(SelectedImportSession));
            }

            RefreshSelectionFromBatch(value);
            OnPropertyChanged();
        }
    }

    public string SelectionHeading
    {
        get => _selectionHeading;
        private set => SetProperty(ref _selectionHeading, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string QueueSummary { get; init; } = string.Empty;

    public string ImportSummary { get; init; } = string.Empty;

    public string ActivitySummary { get; init; } = string.Empty;

    public bool FocusImportSessionWithWarnings()
    {
        return TrySelectImportSession(
            (session) =>
                session.Note.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                session.Blocker.Contains("JAN", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusImportSession(string sessionLabel)
    {
        return TrySelectImportSession((session) => string.Equals(session.Label, sessionLabel, StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusFirstImportSession()
    {
        return TrySelectImportSession((_) => true);
    }

    public bool FocusReadyBatch()
    {
        return TrySelectBatch((batch) => string.Equals(batch.CanonicalStatus, "ready", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusSubmittedBatch()
    {
        return TrySelectBatch((batch) => string.Equals(batch.CanonicalStatus, "submitted", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusFailedBatch()
    {
        return TrySelectBatch((batch) => string.Equals(batch.CanonicalStatus, "failed", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusDraftBatch()
    {
        return TrySelectBatch((batch) => string.Equals(batch.CanonicalStatus, "draft", StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshSelectionFromImportSession(QueueItemModel? session)
    {
        SessionDetailRows.Clear();

        if (session is null)
        {
            SelectionHeading = "Select an import session or batch";
            SelectionSummary = "Review queue readiness, blockers, and retry rules before submit.";
            return;
        }

        SelectionHeading = session.Label;
        SelectionSummary = session.Note;
        SessionDetailRows.Add(new PropertyRowModel("Workbook", session.Subtext));
        SessionDetailRows.Add(new PropertyRowModel("State", session.Badge));
        SessionDetailRows.Add(new PropertyRowModel("Owner", session.Owner));
        SessionDetailRows.Add(new PropertyRowModel("Submit route", session.Route));
        SessionDetailRows.Add(new PropertyRowModel("Blocker", session.Blocker));
        SessionDetailRows.Add(new PropertyRowModel("Next action", session.NextAction));
    }

    private void RefreshSelectionFromBatch(BatchRowModel? batch)
    {
        SessionDetailRows.Clear();

        if (batch is null)
        {
            SelectionHeading = "Select an import session or batch";
            SelectionSummary = "Review queue readiness, blockers, and retry rules before submit.";
            return;
        }

        SelectionHeading = batch.BatchId;
        SelectionSummary = batch.Note;
        SessionDetailRows.Add(new PropertyRowModel("Template", batch.Template));
        SessionDetailRows.Add(new PropertyRowModel("Records", batch.Records));
        SessionDetailRows.Add(new PropertyRowModel("Status", batch.Status));
        SessionDetailRows.Add(new PropertyRowModel("Ready rows", batch.ReadyRows));
        SessionDetailRows.Add(new PropertyRowModel("Warn rows", batch.WarnRows));
        SessionDetailRows.Add(new PropertyRowModel("Submit route", batch.SubmitRoute));
        SessionDetailRows.Add(new PropertyRowModel("Blocker", batch.Blocker));
        SessionDetailRows.Add(new PropertyRowModel("Retry rule", batch.RetryRule));
    }

    private bool TrySelectImportSession(Func<QueueItemModel, bool> predicate)
    {
        var match = ImportSessions.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedImportSession = match;
        return true;
    }

    private bool TrySelectBatch(Func<BatchRowModel, bool> predicate)
    {
        var match = BatchRows.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedBatch = match;
        return true;
    }
}

public sealed class HistoryWorkspaceModel : WorkspaceModel
{
    private QueueItemModel? _selectedPendingProof;
    private QueueItemModel? _selectedBundle;
    private AuditRowModel? _selectedAuditRow;
    private string _selectionHeading = "Select a proof, bundle, or ledger entry";
    private string _selectionSummary = "Review audit impact and recovery safety before acting.";

    public ObservableCollection<QueueItemModel> PendingProofs { get; } = new();

    public ObservableCollection<QueueItemModel> BundleRows { get; } = new();

    public ObservableCollection<AuditRowModel> AuditRows { get; } = new();

    public ObservableCollection<PropertyRowModel> FilterRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectedEntryRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public QueueItemModel? SelectedPendingProof
    {
        get => _selectedPendingProof;
        set
        {
            if (ReferenceEquals(_selectedPendingProof, value))
            {
                return;
            }

            _selectedPendingProof = value;
            if (value is not null)
            {
                _selectedBundle = null;
                _selectedAuditRow = null;
                OnPropertyChanged(nameof(SelectedBundle));
                OnPropertyChanged(nameof(SelectedAuditRow));
            }

            RefreshSelectionFromPendingProof(value);
            OnPropertyChanged();
        }
    }

    public QueueItemModel? SelectedBundle
    {
        get => _selectedBundle;
        set
        {
            if (ReferenceEquals(_selectedBundle, value))
            {
                return;
            }

            _selectedBundle = value;
            if (value is not null)
            {
                _selectedPendingProof = null;
                _selectedAuditRow = null;
                OnPropertyChanged(nameof(SelectedPendingProof));
                OnPropertyChanged(nameof(SelectedAuditRow));
            }

            RefreshSelectionFromBundle(value);
            OnPropertyChanged();
        }
    }

    public AuditRowModel? SelectedAuditRow
    {
        get => _selectedAuditRow;
        set
        {
            if (ReferenceEquals(_selectedAuditRow, value))
            {
                return;
            }

            _selectedAuditRow = value;
            if (value is not null)
            {
                _selectedPendingProof = null;
                _selectedBundle = null;
                OnPropertyChanged(nameof(SelectedPendingProof));
                OnPropertyChanged(nameof(SelectedBundle));
            }

            RefreshSelectionFromAuditRow(value);
            OnPropertyChanged();
        }
    }

    public string SelectionHeading
    {
        get => _selectionHeading;
        private set => SetProperty(ref _selectionHeading, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string LedgerSummary { get; init; } = string.Empty;

    public string PendingProofSummary { get; init; } = string.Empty;

    public string BundleSummary { get; init; } = string.Empty;

    public string FilterSummary { get; init; } = string.Empty;

    public bool FocusPendingProof()
    {
        return TrySelectPendingProof((proof) => string.Equals(proof.Badge, "pending", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusRejectedProof()
    {
        return TrySelectPendingProof((proof) => string.Equals(proof.Badge, "rejected", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusLatestBundle()
    {
        return TrySelectBundle((bundle) => true);
    }

    public bool FocusLatestAudit()
    {
        return TrySelectAudit((audit) => true);
    }

    public bool FocusApprovedAudit()
    {
        return TrySelectAudit((audit) => string.Equals(audit.Status, "approved", StringComparison.OrdinalIgnoreCase));
    }

    public bool FocusRetentionAudit()
    {
        return TrySelectAudit(
            (audit) =>
                string.Equals(audit.Lane, "retention", StringComparison.OrdinalIgnoreCase) ||
                audit.Subject.Contains("trim", StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshSelectionFromPendingProof(QueueItemModel? proof)
    {
        SelectedEntryRows.Clear();

        if (proof is null)
        {
            SelectionHeading = "Select a proof, bundle, or ledger entry";
            SelectionSummary = "Review audit impact and recovery safety before acting.";
            return;
        }

        SelectionHeading = proof.Label;
        SelectionSummary = proof.Note;
        SelectedEntryRows.Add(new PropertyRowModel("Subject", proof.Subtext));
        SelectedEntryRows.Add(new PropertyRowModel("State", proof.Badge));
        SelectedEntryRows.Add(new PropertyRowModel("Authority", proof.Owner));
        SelectedEntryRows.Add(new PropertyRowModel("Artifact", proof.Route));
        SelectedEntryRows.Add(new PropertyRowModel("Blocker", proof.Blocker));
        SelectedEntryRows.Add(new PropertyRowModel("Next action", proof.NextAction));
    }

    private void RefreshSelectionFromBundle(QueueItemModel? bundle)
    {
        SelectedEntryRows.Clear();

        if (bundle is null)
        {
            SelectionHeading = "Select a proof, bundle, or ledger entry";
            SelectionSummary = "Review audit impact and recovery safety before acting.";
            return;
        }

        SelectionHeading = bundle.Label;
        SelectionSummary = bundle.Note;
        SelectedEntryRows.Add(new PropertyRowModel("Bundle", bundle.Subtext));
        SelectedEntryRows.Add(new PropertyRowModel("State", bundle.Badge));
        SelectedEntryRows.Add(new PropertyRowModel("Restore policy", bundle.Owner));
        SelectedEntryRows.Add(new PropertyRowModel("Validation", bundle.Route));
        SelectedEntryRows.Add(new PropertyRowModel("Blocker", bundle.Blocker));
        SelectedEntryRows.Add(new PropertyRowModel("Next action", bundle.NextAction));
    }

    private void RefreshSelectionFromAuditRow(AuditRowModel? auditRow)
    {
        SelectedEntryRows.Clear();

        if (auditRow is null)
        {
            SelectionHeading = "Select a proof, bundle, or ledger entry";
            SelectionSummary = "Review audit impact and recovery safety before acting.";
            return;
        }

        SelectionHeading = $"{auditRow.Lane} | {auditRow.Subject}";
        SelectionSummary = auditRow.Detail;
        SelectedEntryRows.Add(new PropertyRowModel("Status", auditRow.Status));
        SelectedEntryRows.Add(new PropertyRowModel("Lineage", auditRow.Lineage));
        SelectedEntryRows.Add(new PropertyRowModel("Template", auditRow.Template));
        SelectedEntryRows.Add(new PropertyRowModel("Artifact", auditRow.Artifact));
        SelectedEntryRows.Add(new PropertyRowModel("Action", auditRow.ActionRequired));
        SelectedEntryRows.Add(new PropertyRowModel("When", auditRow.Time));
    }

    private bool TrySelectPendingProof(Func<QueueItemModel, bool> predicate)
    {
        var match = PendingProofs.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedPendingProof = match;
        return true;
    }

    private bool TrySelectBundle(Func<QueueItemModel, bool> predicate)
    {
        var match = BundleRows.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedBundle = match;
        return true;
    }

    private bool TrySelectAudit(Func<AuditRowModel, bool> predicate)
    {
        var match = AuditRows.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedAuditRow = match;
        return true;
    }
}

public sealed class RibbonGroupModel
{
    public RibbonGroupModel(string title, params string[] commands)
        : this(title, commands.Select(ShellActionModel.Enabled))
    {
    }

    public RibbonGroupModel(string title, params ShellActionModel[] commands)
        : this(title, (IEnumerable<ShellActionModel>)commands)
    {
    }

    public RibbonGroupModel(string title, IEnumerable<ShellActionModel> commands)
    {
        Title = title;
        Commands = new ObservableCollection<ShellActionModel>(commands);
    }

    public string Title { get; }

    public ObservableCollection<ShellActionModel> Commands { get; }
}

public sealed class ShellActionModel
{
    public ShellActionModel(string label, bool isEnabled, string summary = "")
    {
        Label = label;
        IsEnabled = isEnabled;
        Summary = summary;
    }

    public string Label { get; }

    public bool IsEnabled { get; }

    public string Summary { get; }

    public static ShellActionModel Enabled(string label)
    {
        return new(label, true);
    }

    public static ShellActionModel Enabled(string label, string summary)
    {
        return new(label, true, summary);
    }

    public static ShellActionModel Disabled(string label, string summary)
    {
        return new(label, false, summary);
    }
}

public sealed class SummaryCardModel
{
    public SummaryCardModel(string title, string value, string detail)
    {
        Title = title;
        Value = value;
        Detail = detail;
    }

    public string Title { get; }

    public string Value { get; }

    public string Detail { get; }
}

public sealed class ContextBadgeModel
{
    public ContextBadgeModel(string label, string value, Brush accent)
    {
        Label = label;
        Value = value;
        Accent = accent;
    }

    public string Label { get; }

    public string Value { get; }

    public Brush Accent { get; }
}

public sealed class StatusStripItemModel
{
    public StatusStripItemModel(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}

public sealed class QueueItemModel
{
    public QueueItemModel(
        string label,
        string subtext,
        string badge,
        string note,
        string owner = "",
        string route = "",
        string blocker = "",
        string nextAction = "")
    {
        Label = label;
        Subtext = subtext;
        Badge = badge;
        Note = note;
        Owner = owner;
        Route = route;
        Blocker = blocker;
        NextAction = nextAction;
    }

    public string Label { get; }

    public string Subtext { get; }

    public string Badge { get; }

    public string Note { get; }

    public string Owner { get; }

    public string Route { get; }

    public string Blocker { get; }

    public string NextAction { get; }
}

public sealed class ActivityRowModel
{
    public ActivityRowModel(string time, string lane, string summary, string status)
    {
        Time = time;
        Lane = lane;
        Summary = summary;
        Status = status;
    }

    public string Time { get; }

    public string Lane { get; }

    public string Summary { get; }

    public string Status { get; }
}

public sealed class TemplateLibraryPanelModel : BindableModel
{
    private TemplateCatalogRowModel? _selectedTemplate;
    private string _headerDetail = string.Empty;
    private string _statusSummary = string.Empty;
    private string _entrySummary = string.Empty;
    private string _selectionHeading = "Select a template";
    private string _selectionSummary = "Choose a template to inspect authority, dispatch safety, and rollback intent.";

    public ObservableCollection<SummaryCardModel> SummaryCards { get; } = new();

    public ObservableCollection<TemplateCatalogRowModel> Entries { get; } = new();

    public ObservableCollection<PropertyRowModel> DetailRows { get; } = new();

    public ObservableCollection<PropertySectionModel> GuidanceSections { get; } = new();

    public ObservableCollection<MessageRowModel> AlertRows { get; } = new();

    public string HeaderDetail
    {
        get => _headerDetail;
        set => SetProperty(ref _headerDetail, value);
    }

    public string StatusSummary
    {
        get => _statusSummary;
        set => SetProperty(ref _statusSummary, value);
    }

    public string EntrySummary
    {
        get => _entrySummary;
        set => SetProperty(ref _entrySummary, value);
    }

    public string SelectionHeading
    {
        get => _selectionHeading;
        private set => SetProperty(ref _selectionHeading, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public TemplateCatalogRowModel? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (ReferenceEquals(_selectedTemplate, value))
            {
                return;
            }

            _selectedTemplate = value;
            RefreshDetailRows();
            OnPropertyChanged();
        }
    }

    public void LoadEntries(IEnumerable<TemplateCatalogRowModel> entries, string? preferredTemplateName = null)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        TemplateCatalogRowModel? preferred = null;
        if (!string.IsNullOrWhiteSpace(preferredTemplateName))
        {
            foreach (var entry in Entries)
            {
                if (entry.Name == preferredTemplateName)
                {
                    preferred = entry;
                    break;
                }
            }
        }

        SelectedTemplate = preferred ?? (Entries.Count > 0 ? Entries[0] : null);
    }

    public void ReplaceSummaryCards(IEnumerable<SummaryCardModel> cards)
    {
        SummaryCards.Clear();
        foreach (var card in cards)
        {
            SummaryCards.Add(card);
        }
    }

    public void ReplaceGuidanceSections(IEnumerable<PropertySectionModel> sections)
    {
        GuidanceSections.Clear();
        foreach (var section in sections)
        {
            GuidanceSections.Add(section);
        }
    }

    public void ReplaceAlerts(IEnumerable<MessageRowModel> alerts)
    {
        AlertRows.Clear();
        foreach (var alert in alerts)
        {
            AlertRows.Add(alert);
        }
    }

    public bool TrySelectTemplate(string templateName)
    {
        return TrySelectFirst((entry) => string.Equals(entry.Name, templateName, StringComparison.OrdinalIgnoreCase));
    }

    public bool TrySelectFirst(Func<TemplateCatalogRowModel, bool> predicate)
    {
        var match = Entries.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        SelectedTemplate = match;
        return true;
    }

    private void RefreshDetailRows()
    {
        DetailRows.Clear();

        if (SelectedTemplate is null)
        {
            SelectionHeading = "Select a template";
            SelectionSummary = "Choose a template to inspect authority, dispatch safety, and rollback intent.";
            return;
        }

        SelectionHeading = SelectedTemplate.Name;
        SelectionSummary = SelectedTemplate.Note;
        DetailRows.Add(new PropertyRowModel("Source", SelectedTemplate.Source));
        DetailRows.Add(new PropertyRowModel("State", SelectedTemplate.State));
        DetailRows.Add(new PropertyRowModel("Authority", SelectedTemplate.Authority));
        DetailRows.Add(new PropertyRowModel("Dispatch", SelectedTemplate.Dispatch));
        DetailRows.Add(new PropertyRowModel("Updated", SelectedTemplate.Updated));
        DetailRows.Add(new PropertyRowModel("Current use", SelectedTemplate.Note));
        DetailRows.Add(new PropertyRowModel("Overlay impact", SelectedTemplate.ChangeSummary));
        DetailRows.Add(new PropertyRowModel("Rollback", SelectedTemplate.Rollback));
    }
}

public sealed class TemplateCatalogRowModel
{
    public TemplateCatalogRowModel(
        string name,
        string source,
        string state,
        string authority,
        string dispatch,
        string updated,
        string note,
        string changeSummary,
        string rollback)
    {
        Name = name;
        Source = source;
        State = state;
        Authority = authority;
        Dispatch = dispatch;
        Updated = updated;
        Note = note;
        ChangeSummary = changeSummary;
        Rollback = rollback;
    }

    public string Name { get; }

    public string Source { get; }

    public string State { get; }

    public string Authority { get; }

    public string Dispatch { get; }

    public string Updated { get; }

    public string Note { get; }

    public string ChangeSummary { get; }

    public string Rollback { get; }
}

public sealed class JobRowModel
{
    public JobRowModel(
        string subject,
        string proof,
        string route,
        string status,
        string note,
        string template = "",
        string jan = "",
        string qty = "",
        string lineage = "",
        string blocker = "",
        string nextAction = "")
    {
        Subject = subject;
        Proof = proof;
        Route = route;
        Status = status;
        Note = note;
        Template = template;
        Jan = jan;
        Qty = qty;
        Lineage = lineage;
        Blocker = blocker;
        NextAction = nextAction;
    }

    public string Subject { get; }

    public string Proof { get; }

    public string Route { get; }

    public string Status { get; }

    public string Note { get; }

    public string Template { get; }

    public string Jan { get; }

    public string Qty { get; }

    public string Lineage { get; }

    public string Blocker { get; }

    public string NextAction { get; }
}

public sealed class BatchRowModel
{
    public BatchRowModel(
        string batchId,
        string template,
        string records,
        string status,
        string note,
        string readyRows = "",
        string warnRows = "",
        string submitRoute = "",
        string blocker = "",
        string retryRule = "",
        string? canonicalStatus = null)
    {
        BatchId = batchId;
        Template = template;
        Records = records;
        Status = status;
        Note = note;
        ReadyRows = readyRows;
        WarnRows = warnRows;
        SubmitRoute = submitRoute;
        Blocker = blocker;
        RetryRule = retryRule;
        CanonicalStatus = canonicalStatus ?? status;
    }

    public string BatchId { get; }

    public string Template { get; }

    public string Records { get; }

    public string Status { get; }

    public string Note { get; }

    public string ReadyRows { get; }

    public string WarnRows { get; }

    public string SubmitRoute { get; }

    public string Blocker { get; }

    public string RetryRule { get; }

    public string CanonicalStatus { get; }
}

public sealed class AuditRowModel
{
    public AuditRowModel(
        string time,
        string lane,
        string subject,
        string status,
        string lineage,
        string template = "",
        string artifact = "",
        string actionRequired = "",
        string detail = "")
    {
        Time = time;
        Lane = lane;
        Subject = subject;
        Status = status;
        Lineage = lineage;
        Template = template;
        Artifact = artifact;
        ActionRequired = actionRequired;
        Detail = detail;
    }

    public string Time { get; }

    public string Lane { get; }

    public string Subject { get; }

    public string Status { get; }

    public string Lineage { get; }

    public string Template { get; }

    public string Artifact { get; }

    public string ActionRequired { get; }

    public string Detail { get; }
}

public sealed class ToolboxGroupModel
{
    public ToolboxGroupModel(string title, IEnumerable<ToolboxItemModel> items)
    {
        Title = title;
        Items = new ObservableCollection<ToolboxItemModel>(items);
    }

    public string Title { get; }

    public ObservableCollection<ToolboxItemModel> Items { get; }
}

public sealed class ToolboxItemModel
{
    public ToolboxItemModel(string label, string badge)
    {
        Label = label;
        Badge = badge;
    }

    public string Label { get; }

    public string Badge { get; }
}

public sealed class ObjectNodeModel
{
    public ObjectNodeModel(string label, string meta, IEnumerable<ObjectNodeModel>? children = null)
    {
        Label = label;
        Meta = meta;
        Children = children is null ? new ObservableCollection<ObjectNodeModel>() : new ObservableCollection<ObjectNodeModel>(children);
    }

    public string Label { get; }

    public string Meta { get; }

    public ObservableCollection<ObjectNodeModel> Children { get; }
}

public sealed class DataSourceRowModel
{
    public DataSourceRowModel(string name, string type, string sample)
    {
        Name = name;
        Type = type;
        Sample = sample;
    }

    public string Name { get; }

    public string Type { get; }

    public string Sample { get; }
}

public sealed class DocumentTabModel
{
    public DocumentTabModel(string name, string meta)
    {
        Name = name;
        Meta = meta;
    }

    public string Name { get; }

    public string Meta { get; }
}

public sealed class CanvasElementModel : BindableModel
{
    private string _caption;
    private string _value;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private double _fontSize;
    private bool _isHighlighted;

    public CanvasElementModel(string caption, string value, double x, double y, double width, double height, double fontSize, bool isHighlighted)
    {
        _caption = caption;
        _value = value;
        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _fontSize = fontSize;
        _isHighlighted = isHighlighted;
    }

    public string Caption
    {
        get => _caption;
        set => SetProperty(ref _caption, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }
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

public sealed class PropertySectionModel
{
    public PropertySectionModel(string title, string summary, IEnumerable<PropertyRowModel> rows)
    {
        Title = title;
        Summary = summary;
        Rows = new ObservableCollection<PropertyRowModel>(rows);
    }

    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<PropertyRowModel> Rows { get; }
}

public sealed class MessageRowModel
{
    public MessageRowModel(string level, string source, string message)
    {
        Level = level;
        Source = source;
        Message = message;
    }

    public string Level { get; }

    public string Source { get; }

    public string Message { get; }
}

public sealed class StatusItemModel
{
    public StatusItemModel(string label, string value, string detail, string tone, Brush accent)
    {
        Label = label;
        Value = value;
        Detail = detail;
        Tone = tone;
        Accent = accent;
    }

    public string Label { get; }

    public string Value { get; }

    public string Detail { get; }

    public string Tone { get; }

    public Brush Accent { get; }
}
