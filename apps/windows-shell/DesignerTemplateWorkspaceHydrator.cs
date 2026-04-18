using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;

using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

internal static class DesignerTemplateWorkspaceHydrator
{
    private const double CanvasScale = 12.0;
    private const double CanvasMarginWidth = 60.0;
    private const double CanvasMarginHeight = 50.0;

    public static void Apply(DesignerWorkspaceModel workspace, TemplateCatalogDocument document)
    {
        var preferredCanvasCaption = workspace.SelectedCanvasElement?.Caption;
        var sampleBindings = BuildSampleBindings(document.TemplateVersion);
        var canvasElements = document.Fields
            .Select(BuildCanvasElement)
            .ToList();
        var dataSources = BuildDataSources(sampleBindings, document.TemplateVersion);

        workspace.CanvasMeta = $"{document.TemplateVersion} | {document.Source} | native catalog document";
        workspace.CanvasHint = "Designer surface now opens directly from the native catalog, and the preview pane refreshes through the local render service. Proof, audit, and dispatch still remain on the hybrid path until native render fully replaces them.";
        workspace.MessageSummary = $"{document.Fields.Count} field(s) loaded from the native {document.Source} catalog.";
        workspace.RecordSummary = "native sample record";
        workspace.StatusSummary = "native design surface loaded; native draft preview refresh pending";
        workspace.CatalogSummary = $"{document.Source} open / {document.Fields.Count} field(s)";
        workspace.ObjectBrowserSummary = $"{canvasElements.Count} design object(s) / 2 layer(s)";
        workspace.DataSourceSummary = $"{dataSources.Count} mapped field(s)";
        workspace.CanvasWidth = Math.Max(660.0, Math.Round((document.PageWidthMm * CanvasScale) + CanvasMarginWidth, 1));
        workspace.CanvasHeight = Math.Max(410.0, Math.Round((document.PageHeightMm * CanvasScale) + CanvasMarginHeight, 1));
        workspace.PrimaryDocumentTitle = document.TemplateVersion;
        workspace.SecondaryDocumentTitle = $"{document.LabelName} draft SVG / PDF";
        workspace.PreviewSvg = string.Empty;

        ReplaceCollection(
            workspace.DocumentTabs,
            new[]
            {
                new DocumentTabModel(workspace.PrimaryDocumentTitle, "native catalog document"),
                new DocumentTabModel(workspace.SecondaryDocumentTitle, "native draft svg/pdf"),
            });
        ReplaceCollection(
            workspace.ObjectNodes,
            new[]
            {
                new ObjectNodeModel(
                    "Label Format",
                    document.TemplateVersion,
                    new[]
                    {
                        new ObjectNodeModel("Static Layer", "border + layout"),
                        new ObjectNodeModel(
                            "Data Layer",
                            $"{canvasElements.Count} objects",
                            canvasElements.Select((element) => new ObjectNodeModel(element.Caption, element.Value)).ToArray()),
                    }),
            });
        ReplaceCollection(workspace.DataSources, dataSources);
        ReplaceCollection(workspace.CanvasElements, canvasElements);
        ReplaceCollection(
            workspace.SetupRows,
            new[]
            {
                new PropertyRowModel("Document", document.TemplateVersion),
                new PropertyRowModel("Label", document.LabelName),
                new PropertyRowModel("Catalog source", document.Source),
                new PropertyRowModel("Template path", document.SourcePath),
                new PropertyRowModel("Page", $"{document.PageWidthMm:0.#} x {document.PageHeightMm:0.#} mm"),
                new PropertyRowModel("Proof review", "required after save"),
                new PropertyRowModel("Preview route", "local render service draft"),
            });
        ReplaceCollection(
            workspace.PropertySections,
            new[]
            {
                new PropertySectionModel(
                    "Selected Object",
                    "The property grid edits the currently selected native catalog object.",
                    new[]
                    {
                        new PropertyRowModel("Source", "native catalog document"),
                        new PropertyRowModel("Object count", canvasElements.Count.ToString(CultureInfo.InvariantCulture)),
                        new PropertyRowModel("Coordinate scale", "12 px per mm"),
                    }),
                new PropertySectionModel(
                    "Preview Boundary",
                    "The design surface and the preview pane are now fed by the selected native catalog document, but this preview is still only a draft and not the final proof/print authority.",
                    new[]
                    {
                        new PropertyRowModel("Current preview", "local render service draft svg/pdf"),
                        new PropertyRowModel("Proof preview authority", "service only"),
                        new PropertyRowModel("Release targets", "SVG / PDF"),
                        new PropertyRowModel("Proof generation", "service only"),
                    }),
                new PropertySectionModel(
                    "Save and Proof",
                    "Saved local catalog state is still the only template state this shell treats as proof-safe.",
                    new[]
                    {
                        new PropertyRowModel("Save to catalog", "native local catalog"),
                        new PropertyRowModel("Dispatch gate", "approved proof lineage"),
                        new PropertyRowModel("Unsaved edits", "never dispatch-safe"),
                    }),
            });
        ReplaceCollection(
            workspace.RecordRows,
            new[]
            {
                new PropertyRowModel("SKU", sampleBindings["sku"]),
                new PropertyRowModel("JAN", sampleBindings["jan"]),
                new PropertyRowModel("Brand", sampleBindings["brand"]),
                new PropertyRowModel("Qty", sampleBindings["qty"]),
                new PropertyRowModel("Template", document.TemplateVersion),
            });
        ReplaceCollection(
            workspace.PreviewRows,
            new[]
            {
                new PropertyRowModel("Template", document.TemplateVersion),
                new PropertyRowModel("Label", document.LabelName),
                new PropertyRowModel("Source", document.Source),
                new PropertyRowModel("Mode", "native draft render"),
                new PropertyRowModel("Authority", "local render service draft"),
                new PropertyRowModel("Draft PDF", "pending render"),
                new PropertyRowModel("Page", $"{document.PageWidthMm:0.#} x {document.PageHeightMm:0.#} mm"),
                new PropertyRowModel("Fields", document.Fields.Count.ToString(CultureInfo.InvariantCulture)),
            });
        ReplaceCollection(
            workspace.MessageRows,
            new[]
            {
                new MessageRowModel("Info", "catalog", $"Loaded {document.TemplateVersion} directly from the native {document.Source} catalog path."),
                new MessageRowModel("Info", "designer", "Canvas objects, object browser, and document setup now come from the selected template document."),
                new MessageRowModel("Info", "preview", "Preview pane and draft PDF metadata will refresh through the local render service for the selected catalog document."),
                new MessageRowModel("Warn", "proof", "This native draft preview is not yet the final proof/print authority."),
            });
        ReplaceCollection(
            workspace.StatusItems,
            new[]
            {
                new StatusItemModel("Catalog", document.Source, $"Opened from {document.SourcePath}", "LIVE", Brushes.ForestGreen),
                new StatusItemModel("Preview", "pending render", "Preview pane refreshes through the local render service after the native template opens, and the same draft render pass also stages PDF metadata for the Designer lane; proof/print authority stays elsewhere.", "PENDING", Brushes.SteelBlue),
                new StatusItemModel("Designer source", "native", $"{document.Fields.Count} field(s) loaded into the design surface.", "OPEN", Brushes.SteelBlue),
                new StatusItemModel("Save path", "native local catalog", "Saved templates persist directly into the local catalog; proof review is still required.", "LIVE", Brushes.ForestGreen),
            });

        if (!workspace.TopRulerMarks.Any())
        {
            workspace.TopRulerMarks.Add("0");
            workspace.TopRulerMarks.Add("10");
            workspace.TopRulerMarks.Add("20");
            workspace.TopRulerMarks.Add("30");
            workspace.TopRulerMarks.Add("40");
            workspace.TopRulerMarks.Add("50");
            workspace.TopRulerMarks.Add("60");
            workspace.TopRulerMarks.Add("70");
            workspace.TopRulerMarks.Add("80");
        }

        if (!workspace.SideRulerMarks.Any())
        {
            workspace.SideRulerMarks.Add("0");
            workspace.SideRulerMarks.Add("5");
            workspace.SideRulerMarks.Add("10");
            workspace.SideRulerMarks.Add("15");
            workspace.SideRulerMarks.Add("20");
            workspace.SideRulerMarks.Add("25");
            workspace.SideRulerMarks.Add("30");
        }

        workspace.SelectCanvasElement(
            canvasElements.FirstOrDefault((element) => string.Equals(element.Caption, preferredCanvasCaption, StringComparison.OrdinalIgnoreCase))
            ?? canvasElements.FirstOrDefault((element) => string.Equals(element.Caption, "BARCODE", StringComparison.OrdinalIgnoreCase))
            ?? canvasElements.FirstOrDefault());
    }

    private static Dictionary<string, string> BuildSampleBindings(string templateVersion)
    {
        return DesignerDraftBindings.CreateBaseBindings(templateVersion);
    }

    private static List<DataSourceRowModel> BuildDataSources(
        IReadOnlyDictionary<string, string> sampleBindings,
        string templateVersion)
    {
        return new List<DataSourceRowModel>
        {
            new("sku", "Text", sampleBindings["sku"]),
            new("jan", "JAN", sampleBindings["jan"]),
            new("qty", "Text", sampleBindings["qty"]),
            new("brand", "Text", sampleBindings["brand"]),
            new("template_version", "Text", templateVersion),
            new("proof_mode", "Expr", sampleBindings["proof_mode"]),
        };
    }

    private static CanvasElementModel BuildCanvasElement(
        TemplateCatalogDocumentField field)
    {
        var caption = NormalizeCaption(field.Name);
        var fontSize = Math.Max(12.0, Math.Round(field.FontSizeMm * 4.5, 1));
        var isBarcode = string.Equals(caption, "BARCODE", StringComparison.OrdinalIgnoreCase);
        var width = isBarcode
            ? 320.0
            : Math.Clamp(Math.Round((field.Template.Length * Math.Max(fontSize, 14.0)) * 0.62, 1), 120.0, 340.0);
        var height = isBarcode
            ? 102.0
            : Math.Max(26.0, Math.Round(fontSize * 1.8, 1));

        return new CanvasElementModel(
            caption,
            field.Template,
            Math.Round(field.Xmm * CanvasScale, 1),
            Math.Round(field.Ymm * CanvasScale, 1),
            width,
            height,
            fontSize,
            false);
    }

    private static string NormalizeCaption(string fieldName)
    {
        return string.Equals(fieldName, "barcode", StringComparison.OrdinalIgnoreCase)
            ? "BARCODE"
            : fieldName.Trim().Replace('_', ' ').ToUpperInvariant();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
