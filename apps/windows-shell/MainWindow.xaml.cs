using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace JanLabel.WindowsShell;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private ModuleModel? _selectedModule;
    private DocumentTabModel? _selectedDocument;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Seed();
        SelectedModule = Modules[1];
        SelectedDocument = DocumentTabs[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModuleModel> Modules { get; } = new();

    public ObservableCollection<RibbonGroupModel> RibbonGroups { get; } = new();

    public ObservableCollection<ToolboxGroupModel> ToolboxGroups { get; } = new();

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

    public ModuleModel? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public DocumentTabModel? SelectedDocument
    {
        get => _selectedDocument;
        set => SetProperty(ref _selectedDocument, value);
    }

    public string WindowTitle => $"JAN Label Workstation - {SelectedModule?.Label ?? "Designer"}";

    public string WorkspaceTagline => "BarTender-style operator workstation baseline";

    public string CanvasMeta => "basic-50x30@v2 | pdf-proof | record 12 / 24";

    public string CanvasHint => "Select objects on the canvas, edit properties on the right, validate through proof before print.";

    public string MessageSummary => "2 warnings / 0 blockers";

    public string RecordSummary => "24 active records";

    public string StatusSummary => "desktop-shell remains the proof/print authority";

    public double CanvasWidth => 660;

    public double CanvasHeight => 410;

    private void Seed()
    {
        Modules.Add(new ModuleModel("Home", "Shell / session state", "active"));
        Modules.Add(new ModuleModel("Designer", "Format authoring and preview", "design"));
        Modules.Add(new ModuleModel("Print Console", "Working payload and proof route", "ready"));
        Modules.Add(new ModuleModel("Batch Jobs", "Workbook import and queue", "24"));
        Modules.Add(new ModuleModel("History", "Proof review and audit", "6 pending"));

        RibbonGroups.Add(new RibbonGroupModel("Clipboard", "Paste", "Duplicate", "Delete"));
        RibbonGroups.Add(new RibbonGroupModel("Insert", "Text", "Barcode", "Line", "Box"));
        RibbonGroups.Add(new RibbonGroupModel("Arrange", "Align Left", "Make Same Size", "Snap"));
        RibbonGroups.Add(new RibbonGroupModel("Data", "Record Browser", "Query Prompt", "Named Data Sources"));
        RibbonGroups.Add(new RibbonGroupModel("Validate", "Rust Preview", "Save to Catalog", "Run Proof"));

        ToolboxGroups.Add(new ToolboxGroupModel("Objects", new[]
        {
            new ToolboxItemModel("Text", "A"),
            new ToolboxItemModel("Barcode", "JAN"),
            new ToolboxItemModel("Counter", "#"),
            new ToolboxItemModel("Picture", "IMG"),
        }));
        ToolboxGroups.Add(new ToolboxGroupModel("Guides", new[]
        {
            new ToolboxItemModel("Margins", "4 mm"),
            new ToolboxItemModel("Grid", "2 mm"),
            new ToolboxItemModel("Snap", "On"),
        }));

        ObjectNodes.Add(new ObjectNodeModel("Label Format", "50 x 30 mm", new[]
        {
            new ObjectNodeModel("Static Layer", "3 objects", new[]
            {
                new ObjectNodeModel("Brand mark", "Text"),
                new ObjectNodeModel("Frame", "Box"),
                new ObjectNodeModel("Divider", "Line"),
            }),
            new ObjectNodeModel("Data Layer", "5 objects", new[]
            {
                new ObjectNodeModel("Product name", "{{sku}}"),
                new ObjectNodeModel("JAN barcode", "{{jan}}"),
                new ObjectNodeModel("JAN text", "{{jan}}"),
                new ObjectNodeModel("Quantity", "{{qty}}"),
            }),
        }));

        DataSources.Add(new DataSourceRowModel("sku", "Text", "200-145-3"));
        DataSources.Add(new DataSourceRowModel("jan", "JAN", "4901234567894"));
        DataSources.Add(new DataSourceRowModel("qty", "Number", "24"));
        DataSources.Add(new DataSourceRowModel("brand", "Text", "JAN-LAB"));
        DataSources.Add(new DataSourceRowModel("template_version", "Text", "basic-50x30@v2"));
        DataSources.Add(new DataSourceRowModel("proof_mode", "Expr", "proof"));

        DocumentTabs.Add(new DocumentTabModel("basic-50x30", "record-linked format"));
        DocumentTabs.Add(new DocumentTabModel("proof-preview", "validation surface"));

        CanvasElements.Add(new CanvasElementModel("BRAND", "JAN-LAB", 30, 22, 120, 32, 14, false));
        CanvasElements.Add(new CanvasElementModel("SKU", "200-145-3", 30, 72, 180, 36, 18, false));
        CanvasElements.Add(new CanvasElementModel("BARCODE", "| ||| || ||| | ||", 28, 132, 320, 102, 16, true));
        CanvasElements.Add(new CanvasElementModel("JAN", "4901234567894", 50, 244, 220, 26, 14, false));
        CanvasElements.Add(new CanvasElementModel("QTY", "24 PCS", 470, 38, 120, 40, 20, false));
        CanvasElements.Add(new CanvasElementModel("STATUS", "Proof lineage locked", 390, 304, 210, 32, 13, false));

        SetupRows.Add(new PropertyRowModel("Document", "basic-50x30@v2"));
        SetupRows.Add(new PropertyRowModel("Printer profile", "pdf-proof / 300 dpi"));
        SetupRows.Add(new PropertyRowModel("Catalog authority", "saved local overlay only"));
        SetupRows.Add(new PropertyRowModel("Dispatch route", "desktop-shell"));
        SetupRows.Add(new PropertyRowModel("Working record", "12 / 24"));

        PropertySections.Add(new PropertySectionModel("Selected Object", "Property-grid style editing for the focused item.", new[]
        {
            new PropertyRowModel("Name", "JAN barcode"),
            new PropertyRowModel("Binding", "{{jan}}"),
            new PropertyRowModel("Symbology", "EAN-13 / JAN"),
            new PropertyRowModel("Position", "28,132"),
            new PropertyRowModel("Size", "320 x 102"),
        }));
        PropertySections.Add(new PropertySectionModel("Layout Rules", "Output constraints remain deterministic and print-core-safe.", new[]
        {
            new PropertyRowModel("Scale", "fixed 100%"),
            new PropertyRowModel("Barcode engine", "Zint only"),
            new PropertyRowModel("Output", "SVG / PDF"),
            new PropertyRowModel("Unsaved draft", "preview only"),
        }));
        PropertySections.Add(new PropertySectionModel("Proof Gate", "Dispatch is still gated outside the shell.", new[]
        {
            new PropertyRowModel("Authority", "approved proof lineage"),
            new PropertyRowModel("Required match", "sku / brand / jan / qty / templateVersion"),
            new PropertyRowModel("Artifact", "valid non-empty PDF"),
        }));

        SelectionRows.Add(new PropertyRowModel("X", "28.0 mm"));
        SelectionRows.Add(new PropertyRowModel("Y", "13.2 mm"));
        SelectionRows.Add(new PropertyRowModel("Width", "32.0 mm"));
        SelectionRows.Add(new PropertyRowModel("Height", "10.2 mm"));
        SelectionRows.Add(new PropertyRowModel("Rotation", "0"));

        RecordRows.Add(new PropertyRowModel("SKU", "200-145-3"));
        RecordRows.Add(new PropertyRowModel("JAN", "4901234567894"));
        RecordRows.Add(new PropertyRowModel("Brand", "JAN-LAB"));
        RecordRows.Add(new PropertyRowModel("Qty", "24"));
        RecordRows.Add(new PropertyRowModel("Template", "basic-50x30@v2"));

        MessageRows.Add(new MessageRowModel("Info", "renderer", "Rust preview and canvas geometry are aligned for the selected format."));
        MessageRows.Add(new MessageRowModel("Warn", "catalog", "Local catalog override is active. Save before proof if this draft should dispatch."));
        MessageRows.Add(new MessageRowModel("Info", "proof", "Approved proof lineage will be required before print route unlocks."));

        StatusItems.Add(new StatusItemModel("Bridge", "connected", "desktop-shell / proof gate", "OK", Brushes.ForestGreen));
        StatusItems.Add(new StatusItemModel("Catalog", "overlay active", "packaged + local manifest", "WATCH", Brushes.DarkGoldenrod));
        StatusItems.Add(new StatusItemModel("Audit", "restore-ready", "backup bundles available", "OK", Brushes.ForestGreen));
        StatusItems.Add(new StatusItemModel("Printer", "pdf-proof", "physical validation deferred", "PDF", Brushes.SteelBlue));

        TopRulerMarks.Add("0");
        TopRulerMarks.Add("10");
        TopRulerMarks.Add("20");
        TopRulerMarks.Add("30");
        TopRulerMarks.Add("40");
        TopRulerMarks.Add("50");
        TopRulerMarks.Add("60");
        TopRulerMarks.Add("70");
        TopRulerMarks.Add("80");

        SideRulerMarks.Add("0");
        SideRulerMarks.Add("5");
        SideRulerMarks.Add("10");
        SideRulerMarks.Add("15");
        SideRulerMarks.Add("20");
        SideRulerMarks.Add("25");
        SideRulerMarks.Add("30");
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
    public ModuleModel(string label, string description, string badge)
    {
        Label = label;
        Description = description;
        Badge = badge;
    }

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
