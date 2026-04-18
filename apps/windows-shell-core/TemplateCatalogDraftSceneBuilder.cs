namespace JanLabel.WindowsShell.Core;

public sealed record DraftRenderScene(
    double PageWidthMm,
    double PageHeightMm,
    DraftRenderFrame Frame,
    IReadOnlyList<DraftRenderSceneElement> Elements);

public sealed record DraftRenderFrame(
    double BorderInsetMm,
    double BorderStrokeMm,
    string FooterText,
    double FooterXmm,
    double FooterTopYmm,
    double FooterFontSizeMm);

public abstract record DraftRenderSceneElement;

public sealed record DraftTextSceneElement(
    double Xmm,
    double TopYmm,
    double FontSizeMm,
    string Text,
    string FontFamily) : DraftRenderSceneElement;

public sealed record DraftBarcodeSceneElement(
    double Xmm,
    double Ymm,
    double FrameWidthMm,
    double FrameHeightMm,
    double CornerRadiusMm,
    double BarInsetMm,
    double BarHeightMm,
    double LabelXmm,
    double LabelTopYmm,
    double LabelFontSizeMm,
    string Value,
    IReadOnlyList<DraftBarcodeBarSceneElement> Bars) : DraftRenderSceneElement;

public sealed record DraftBarcodeBarSceneElement(
    double OffsetXmm,
    double WidthMm);

public static class TemplateCatalogDraftSceneBuilder
{
    private const string FooterText = "Native draft preview only; proof and print remain gated elsewhere.";
    private const double BorderInsetMm = 0.35;
    private const double BorderStrokeMm = 0.25;
    private const double FooterInsetMm = 1.0;
    private const double FooterFontSizeMm = 1.2;
    private const double FooterBottomMarginMm = 0.6;
    private const double TextMinimumFontSizeMm = 1.5;
    private const double TextScaleMultiplier = 1.05;
    private const double PdfBaselineFactor = 0.82;
    private const double BarcodeFrameMinWidthMm = 14.0;
    private const double BarcodeFrameMaxWidthMm = 35.0;
    private const double BarcodeFrameMinAvailableWidthMm = 6.0;
    private const double BarcodeFrameTrailingMarginMm = 2.0;
    private const double BarcodeWidthPerCharacterMm = 2.7;
    private const double BarcodeFrameHeightMm = 7.25;
    private const double BarcodeCornerRadiusMm = 0.3;
    private const double BarcodeBarInsetMm = 0.5;
    private const double BarcodeBarHeightMm = 6.0;
    private const double BarcodeLabelGapMm = 0.55;
    private const double BarcodeLabelFontSizeMm = 1.5;
    private const double BarcodeSlotMinimumMm = 0.28;
    private const double BarcodeSlotMaximumMm = 0.85;
    private const double BarcodeSlotDivisorFactor = 1.8;

    public static DraftRenderScene Build(TemplateCatalogDocument document, IReadOnlyDictionary<string, string> bindings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bindings);

        var frame = new DraftRenderFrame(
            BorderInsetMm,
            BorderStrokeMm,
            FooterText,
            FooterInsetMm,
            Math.Max(0.0, document.PageHeightMm - FooterFontSizeMm - FooterBottomMarginMm),
            FooterFontSizeMm);

        var elements = new List<DraftRenderSceneElement>(document.Fields.Count);
        foreach (var field in document.Fields)
        {
            var expanded = TemplateCatalogRenderUtilities.ExpandTemplate(field.Template, bindings);
            if (TemplateCatalogRenderUtilities.IsBarcodeField(field))
            {
                elements.Add(BuildBarcodeElement(document, field, expanded));
                continue;
            }

            elements.Add(BuildTextElement(field, expanded));
        }

        return new DraftRenderScene(
            document.PageWidthMm,
            document.PageHeightMm,
            frame,
            elements);
    }

    public static double ResolvePdfBaselineOffsetMm(double fontSizeMm)
    {
        return Math.Max(0.0, fontSizeMm * PdfBaselineFactor);
    }

    private static DraftTextSceneElement BuildTextElement(TemplateCatalogDocumentField field, string expanded)
    {
        return new DraftTextSceneElement(
            Math.Round(field.Xmm, 2),
            Math.Round(field.Ymm, 2),
            Math.Round(Math.Max(TextMinimumFontSizeMm, field.FontSizeMm * TextScaleMultiplier), 2),
            expanded,
            "Segoe UI");
    }

    private static DraftBarcodeSceneElement BuildBarcodeElement(
        TemplateCatalogDocument document,
        TemplateCatalogDocumentField field,
        string expanded)
    {
        var normalized = TemplateCatalogRenderUtilities.NormalizeBarcodeValue(expanded);
        var x = Math.Round(field.Xmm, 2);
        var y = Math.Round(field.Ymm, 2);
        var availableWidthMm = Math.Max(
            BarcodeFrameMinAvailableWidthMm,
            Math.Round(document.PageWidthMm - x - BarcodeFrameTrailingMarginMm, 2));
        var desiredWidthMm = Math.Max(
            BarcodeFrameMinWidthMm,
            Math.Round(normalized.Length * BarcodeWidthPerCharacterMm, 2));
        var frameWidthMm = Math.Round(
            Math.Min(BarcodeFrameMaxWidthMm, Math.Min(availableWidthMm, desiredWidthMm)),
            2);
        var slotWidthMm = Math.Round(
            Math.Clamp(
                frameWidthMm / Math.Max(18.0, normalized.Length * BarcodeSlotDivisorFactor),
                BarcodeSlotMinimumMm,
                BarcodeSlotMaximumMm),
            2);

        var bars = new List<DraftBarcodeBarSceneElement>(normalized.Length);
        var cursorMm = BarcodeBarInsetMm;
        foreach (var character in normalized)
        {
            var weight = 1 + (character % 3);
            var barWidthMm = Math.Round(slotWidthMm * (0.45 + (weight * 0.18)), 2);
            bars.Add(new DraftBarcodeBarSceneElement(Math.Round(cursorMm, 2), barWidthMm));
            cursorMm += slotWidthMm;
            if (cursorMm >= frameWidthMm - slotWidthMm)
            {
                break;
            }
        }

        return new DraftBarcodeSceneElement(
            x,
            y,
            frameWidthMm,
            BarcodeFrameHeightMm,
            BarcodeCornerRadiusMm,
            BarcodeBarInsetMm,
            BarcodeBarHeightMm,
            Math.Round(x + (BarcodeBarInsetMm * 2.0), 2),
            Math.Round(y + BarcodeFrameHeightMm + BarcodeLabelGapMm, 2),
            BarcodeLabelFontSizeMm,
            expanded,
            bars);
    }
}
