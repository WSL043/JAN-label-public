using System.Globalization;
using System.Text;

namespace JanLabel.WindowsShell.Core;

public static class TemplateCatalogPdfBuilder
{
    private const double PointsPerMillimeter = 72.0 / 25.4;

    public static byte[] BuildPdf(TemplateDocument document, IReadOnlyDictionary<string, string> bindings)
    {
        ArgumentNullException.ThrowIfNull(document);

        var parsed = TemplateCatalogDocumentParser.Parse(
            document.DocumentJson,
            document.TemplateVersion,
            document.LabelName,
            "draft",
            "in-memory designer draft",
            document.Description);
        return BuildPdf(parsed, bindings);
    }

    public static byte[] BuildPdf(TemplateCatalogDocument document, IReadOnlyDictionary<string, string> bindings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bindings);

        var scene = TemplateCatalogDraftSceneBuilder.Build(document, bindings);
        var widthPt = Math.Max(1.0, ToPoints(scene.PageWidthMm));
        var heightPt = Math.Max(1.0, ToPoints(scene.PageHeightMm));

        var streamBuilder = new StringBuilder();
        streamBuilder.AppendLine("0.95 w");
        streamBuilder.AppendLine("0 0 0 RG");
        AppendBorder(streamBuilder, scene);
        AppendFooter(streamBuilder, scene);

        foreach (var element in scene.Elements)
        {
            switch (element)
            {
                case DraftBarcodeSceneElement barcode:
                    AppendBarcode(streamBuilder, barcode, scene.PageHeightMm);
                    break;
                case DraftTextSceneElement text:
                    AppendText(streamBuilder, text, scene.PageHeightMm);
                    break;
            }
        }

        return BuildPdfBytes(widthPt, heightPt, streamBuilder.ToString());
    }

    private static void AppendBorder(StringBuilder builder, DraftRenderScene scene)
    {
        var borderWidthMm = Math.Max(0.0, scene.PageWidthMm - (scene.Frame.BorderInsetMm * 2.0));
        var borderHeightMm = Math.Max(0.0, scene.PageHeightMm - (scene.Frame.BorderInsetMm * 2.0));
        var x = ToPoints(scene.Frame.BorderInsetMm);
        var y = ToPoints(scene.PageHeightMm - scene.Frame.BorderInsetMm - borderHeightMm);
        var width = ToPoints(borderWidthMm);
        var height = ToPoints(borderHeightMm);
        builder.AppendLine(
            $"""{TemplateCatalogRenderUtilities.FormatNumber(x)} {TemplateCatalogRenderUtilities.FormatNumber(y)} {TemplateCatalogRenderUtilities.FormatNumber(width)} {TemplateCatalogRenderUtilities.FormatNumber(height)} re S""");
    }

    private static void AppendFooter(StringBuilder builder, DraftRenderScene scene)
    {
        AppendText(
            builder,
            new DraftTextSceneElement(
                scene.Frame.FooterXmm,
                scene.Frame.FooterTopYmm,
                scene.Frame.FooterFontSizeMm,
                scene.Frame.FooterText,
                "Segoe UI"),
            scene.PageHeightMm,
            "0.45 0.49 0.56");
    }

    private static void AppendText(
        StringBuilder builder,
        DraftTextSceneElement text,
        double pageHeightMm,
        string colorRgb = "0 0 0")
    {
        var x = ToPoints(text.Xmm);
        var y = ToPoints(pageHeightMm - text.TopYmm - TemplateCatalogDraftSceneBuilder.ResolvePdfBaselineOffsetMm(text.FontSizeMm));
        var fontSize = ToPoints(text.FontSizeMm);
        builder.AppendLine(
            $"""BT /F1 {TemplateCatalogRenderUtilities.FormatNumber(fontSize)} Tf {colorRgb} rg {TemplateCatalogRenderUtilities.FormatNumber(x)} {TemplateCatalogRenderUtilities.FormatNumber(y)} Td ({TemplateCatalogRenderUtilities.EscapePdfText(text.Text)}) Tj ET""");
    }

    private static void AppendBarcode(
        StringBuilder builder,
        DraftBarcodeSceneElement barcode,
        double pageHeightMm)
    {
        var x = ToPoints(barcode.Xmm);
        var y = ToPoints(pageHeightMm - barcode.Ymm - barcode.FrameHeightMm);
        var width = ToPoints(barcode.FrameWidthMm);
        var height = ToPoints(barcode.FrameHeightMm);

        builder.AppendLine("0.15 0.39 0.92 rg");
        builder.AppendLine("0.15 0.39 0.92 RG");
        builder.AppendLine(
            $"""{TemplateCatalogRenderUtilities.FormatNumber(x)} {TemplateCatalogRenderUtilities.FormatNumber(y)} {TemplateCatalogRenderUtilities.FormatNumber(width)} {TemplateCatalogRenderUtilities.FormatNumber(height)} re S""");
        builder.AppendLine("0 0 0 rg");
        builder.AppendLine("0 0 0 RG");
        foreach (var bar in barcode.Bars)
        {
            builder.AppendLine(
                $"""{TemplateCatalogRenderUtilities.FormatNumber(ToPoints(barcode.Xmm + bar.OffsetXmm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(pageHeightMm - barcode.Ymm - barcode.BarInsetMm - barcode.BarHeightMm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(bar.WidthMm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(barcode.BarHeightMm))} re f""");
        }

        AppendText(
            builder,
            new DraftTextSceneElement(
                barcode.LabelXmm,
                barcode.LabelTopYmm,
                barcode.LabelFontSizeMm,
                barcode.Value,
                "Consolas"),
            pageHeightMm);
    }

    private static double ToPoints(double millimeters)
    {
        return Math.Round(millimeters * PointsPerMillimeter, 2);
    }

    private static byte[] BuildPdfBytes(double widthPt, double heightPt, string contentStream)
    {
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {widthPt.ToString("0.##", CultureInfo.InvariantCulture)} {heightPt.ToString("0.##", CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream\nendobj\n",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.AppendLine("xref");
        builder.AppendLine($"0 {objects.Length + 1}");
        builder.AppendLine("0000000000 65535 f ");
        for (var index = 1; index < offsets.Count; index += 1)
        {
            builder.AppendLine($"{offsets[index]:D10} 00000 n ");
        }

        builder.AppendLine("trailer");
        builder.AppendLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
        builder.AppendLine("startxref");
        builder.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        builder.Append("%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
