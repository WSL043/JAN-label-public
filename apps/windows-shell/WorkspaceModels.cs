using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    {
        Label = label;
        Description = description;
        Badge = badge;
        Tagline = tagline;
        Lead = lead;
        HeaderActions = new ObservableCollection<string>(headerActions);
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

    public ObservableCollection<string> HeaderActions { get; }

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

    public ObservableCollection<TemplateCatalogRowModel> TemplateRows { get; } = new();

    public ObservableCollection<ActivityRowModel> ActivityRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<QueueItemModel> NextSteps { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public string HeaderDetail { get; init; } = string.Empty;
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

    public ObservableCollection<TemplateCatalogRowModel> TemplateRows { get; } = new();

    public ObservableCollection<ObjectNodeModel> ObjectNodes { get; } = new();

    public ObservableCollection<DataSourceRowModel> DataSources { get; } = new();

    public ObservableCollection<DocumentTabModel> DocumentTabs { get; } = new();

    public ObservableCollection<CanvasElementModel> CanvasElements { get; } = new();

    public ObservableCollection<PropertySectionModel> PropertySections { get; } = new();

    public ObservableCollection<PropertyRowModel> SetupRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectionRows { get; } = new();

    public ObservableCollection<PropertyRowModel> RecordRows { get; } = new();

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

    public double CanvasWidth { get; init; }

    public double CanvasHeight { get; init; }

    public string PrimaryDocumentTitle { get; init; } = "Format Surface";

    public string SecondaryDocumentTitle { get; init; } = "Proof Preview";

    public void SelectCanvasElement(CanvasElementModel? element)
    {
        SelectedCanvasElement = element;
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
    public ObservableCollection<QueueItemModel> ProofQueue { get; } = new();

    public ObservableCollection<PropertyRowModel> RouteRows { get; } = new();

    public ObservableCollection<JobRowModel> JobRows { get; } = new();

    public ObservableCollection<ActivityRowModel> TimelineRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectedJobRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string JobSummary { get; init; } = string.Empty;
}

public sealed class BatchJobsWorkspaceModel : WorkspaceModel
{
    public ObservableCollection<QueueItemModel> ImportSessions { get; } = new();

    public ObservableCollection<PropertyRowModel> ColumnRows { get; } = new();

    public ObservableCollection<BatchRowModel> BatchRows { get; } = new();

    public ObservableCollection<ActivityRowModel> ActivityRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SessionDetailRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string QueueSummary { get; init; } = string.Empty;
}

public sealed class HistoryWorkspaceModel : WorkspaceModel
{
    public ObservableCollection<QueueItemModel> PendingProofs { get; } = new();

    public ObservableCollection<QueueItemModel> BundleRows { get; } = new();

    public ObservableCollection<AuditRowModel> AuditRows { get; } = new();

    public ObservableCollection<PropertyRowModel> FilterRows { get; } = new();

    public ObservableCollection<PropertyRowModel> SelectedEntryRows { get; } = new();

    public ObservableCollection<PropertySectionModel> ControlSections { get; } = new();

    public ObservableCollection<MessageRowModel> MessageRows { get; } = new();

    public ObservableCollection<StatusItemModel> StatusItems { get; } = new();

    public string MessageSummary { get; init; } = string.Empty;

    public string FooterDetail { get; init; } = string.Empty;

    public string LedgerSummary { get; init; } = string.Empty;
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
    public QueueItemModel(string label, string subtext, string badge, string note)
    {
        Label = label;
        Subtext = subtext;
        Badge = badge;
        Note = note;
    }

    public string Label { get; }

    public string Subtext { get; }

    public string Badge { get; }

    public string Note { get; }
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

public sealed class TemplateCatalogRowModel
{
    public TemplateCatalogRowModel(string name, string source, string state, string note)
    {
        Name = name;
        Source = source;
        State = state;
        Note = note;
    }

    public string Name { get; }

    public string Source { get; }

    public string State { get; }

    public string Note { get; }
}

public sealed class JobRowModel
{
    public JobRowModel(string subject, string proof, string route, string status, string note)
    {
        Subject = subject;
        Proof = proof;
        Route = route;
        Status = status;
        Note = note;
    }

    public string Subject { get; }

    public string Proof { get; }

    public string Route { get; }

    public string Status { get; }

    public string Note { get; }
}

public sealed class BatchRowModel
{
    public BatchRowModel(string batchId, string template, string records, string status, string note)
    {
        BatchId = batchId;
        Template = template;
        Records = records;
        Status = status;
        Note = note;
    }

    public string BatchId { get; }

    public string Template { get; }

    public string Records { get; }

    public string Status { get; }

    public string Note { get; }
}

public sealed class AuditRowModel
{
    public AuditRowModel(string time, string lane, string subject, string status, string lineage)
    {
        Time = time;
        Lane = lane;
        Subject = subject;
        Status = status;
        Lineage = lineage;
    }

    public string Time { get; }

    public string Lane { get; }

    public string Subject { get; }

    public string Status { get; }

    public string Lineage { get; }
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
