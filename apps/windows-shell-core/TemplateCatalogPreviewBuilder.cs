using System.Text;

namespace JanLabel.WindowsShell.Core;

public static class TemplateCatalogPreviewBuilder
{
    public static string BuildSvg(TemplateDocument document, IReadOnlyDictionary<string, string> sampleBindings)
    {
        ArgumentNullException.ThrowIfNull(document);

        var parsed = TemplateCatalogDocumentParser.Parse(
            document.DocumentJson,
            document.TemplateVersion,
            document.LabelName,
            "draft",
            "in-memory designer draft",
            document.Description);
        return BuildSvg(parsed, sampleBindings);
    }

    public static string BuildSvg(TemplateCatalogDocument document, IReadOnlyDictionary<string, string> sampleBindings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sampleBindings);

        var scene = TemplateCatalogDraftSceneBuilder.Build(document, sampleBindings);
        var builder = new StringBuilder();
        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{TemplateCatalogRenderUtilities.FormatNumber(scene.PageWidthMm)}mm" height="{TemplateCatalogRenderUtilities.FormatNumber(scene.PageHeightMm)}mm" viewBox="0 0 {TemplateCatalogRenderUtilities.FormatNumber(scene.PageWidthMm)} {TemplateCatalogRenderUtilities.FormatNumber(scene.PageHeightMm)}">""");
        builder.AppendLine($"""  <rect x="0" y="0" width="{TemplateCatalogRenderUtilities.FormatNumber(scene.PageWidthMm)}" height="{TemplateCatalogRenderUtilities.FormatNumber(scene.PageHeightMm)}" fill="#ffffff" />""");
        builder.AppendLine($"""  <rect x="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.BorderInsetMm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.BorderInsetMm)}" width="{TemplateCatalogRenderUtilities.FormatNumber(Math.Max(0.0, scene.PageWidthMm - (scene.Frame.BorderInsetMm * 2.0)))}" height="{TemplateCatalogRenderUtilities.FormatNumber(Math.Max(0.0, scene.PageHeightMm - (scene.Frame.BorderInsetMm * 2.0)))}" fill="none" stroke="#111827" stroke-width="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.BorderStrokeMm)}" />""");
        builder.AppendLine($"""  <text x="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.FooterXmm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.FooterTopYmm)}" fill="#6b7280" font-size="{TemplateCatalogRenderUtilities.FormatNumber(scene.Frame.FooterFontSizeMm)}" font-family="Segoe UI" dominant-baseline="hanging">{TemplateCatalogRenderUtilities.EscapeXml(scene.Frame.FooterText)}</text>""");

        foreach (var element in scene.Elements)
        {
            switch (element)
            {
                case DraftBarcodeSceneElement barcode:
                    AppendBarcode(builder, barcode);
                    break;
                case DraftTextSceneElement text:
                    AppendText(builder, text);
                    break;
            }
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static void AppendText(StringBuilder builder, DraftTextSceneElement text)
    {
        builder.AppendLine($"""  <text x="{TemplateCatalogRenderUtilities.FormatNumber(text.Xmm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(text.TopYmm)}" fill="#111827" font-size="{TemplateCatalogRenderUtilities.FormatNumber(text.FontSizeMm)}" font-family="{text.FontFamily}" dominant-baseline="hanging">{TemplateCatalogRenderUtilities.EscapeXml(text.Text)}</text>""");
    }

    private static void AppendBarcode(StringBuilder builder, DraftBarcodeSceneElement barcode)
    {
        builder.AppendLine($"""  <rect x="{TemplateCatalogRenderUtilities.FormatNumber(barcode.Xmm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(barcode.Ymm)}" width="{TemplateCatalogRenderUtilities.FormatNumber(barcode.FrameWidthMm)}" height="{TemplateCatalogRenderUtilities.FormatNumber(barcode.FrameHeightMm)}" fill="#f8fafc" stroke="#2563eb" stroke-width="0.25" rx="{TemplateCatalogRenderUtilities.FormatNumber(barcode.CornerRadiusMm)}" />""");
        foreach (var bar in barcode.Bars)
        {
            builder.AppendLine($"""  <rect x="{TemplateCatalogRenderUtilities.FormatNumber(barcode.Xmm + bar.OffsetXmm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(barcode.Ymm + barcode.BarInsetMm)}" width="{TemplateCatalogRenderUtilities.FormatNumber(bar.WidthMm)}" height="{TemplateCatalogRenderUtilities.FormatNumber(barcode.BarHeightMm)}" fill="#111827" />""");
        }

        builder.AppendLine($"""  <text x="{TemplateCatalogRenderUtilities.FormatNumber(barcode.LabelXmm)}" y="{TemplateCatalogRenderUtilities.FormatNumber(barcode.LabelTopYmm)}" fill="#111827" font-size="{TemplateCatalogRenderUtilities.FormatNumber(barcode.LabelFontSizeMm)}" font-family="Consolas" dominant-baseline="hanging">{TemplateCatalogRenderUtilities.EscapeXml(barcode.Value)}</text>""");
    }
}
