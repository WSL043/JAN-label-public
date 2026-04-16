using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public abstract class WorkspaceModel;

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

    public string CanvasMeta { get; init; } = string.Empty;

    public string CanvasHint { get; init; } = string.Empty;

    public string MessageSummary { get; init; } = string.Empty;

    public string RecordSummary { get; init; } = string.Empty;

    public string StatusSummary { get; init; } = string.Empty;

    public string CatalogSummary { get; init; } = string.Empty;

    public double CanvasWidth { get; init; }

    public double CanvasHeight { get; init; }
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

public sealed class CanvasElementModel
{
    public CanvasElementModel(string caption, string value, double x, double y, double width, double height, double fontSize, bool isHighlighted)
    {
        Caption = caption;
        Value = value;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        FontSize = fontSize;
        IsHighlighted = isHighlighted;
    }

    public string Caption { get; }

    public string Value { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double FontSize { get; }

    public bool IsHighlighted { get; }
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
