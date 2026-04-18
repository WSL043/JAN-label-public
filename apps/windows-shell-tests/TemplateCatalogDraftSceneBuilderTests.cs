using System.Text;
using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class TemplateCatalogDraftSceneBuilderTests
{
    [Fact]
    public void Build_CreatesSharedMillimeterLayoutForTextAndBarcode()
    {
        var document = new TemplateCatalogDocument(
            "scene-basic@v1",
            "scene-basic",
            "local",
            @"C:\catalog\scene-basic@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("brand", 2, 4, 3, "brand:{brand}"),
                new TemplateCatalogDocumentField("barcode", 2, 8, 4, "{jan}"),
            },
            "Scene builder test");

        var scene = TemplateCatalogDraftSceneBuilder.Build(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["brand"] = "JAN-LAB",
                ["jan"] = "4901234567894",
            });

        var text = Assert.IsType<DraftTextSceneElement>(scene.Elements[0]);
        var barcode = Assert.IsType<DraftBarcodeSceneElement>(scene.Elements[1]);

        Assert.Equal(50, scene.PageWidthMm);
        Assert.Equal(30, scene.PageHeightMm);
        Assert.Equal("brand:JAN-LAB", text.Text);
        Assert.Equal(2, text.Xmm);
        Assert.Equal(4, text.TopYmm);
        Assert.Equal(3.15, text.FontSizeMm, 2);
        Assert.Equal(35, barcode.FrameWidthMm, 2);
        Assert.Equal(7.25, barcode.FrameHeightMm, 2);
        Assert.True(barcode.LabelTopYmm > barcode.Ymm + barcode.FrameHeightMm);
        Assert.NotEmpty(barcode.Bars);
    }

    [Fact]
    public void BuildSvgAndPdf_ConsumeSharedBarcodeFrameGeometry()
    {
        var document = new TemplateCatalogDocument(
            "scene-render@v1",
            "scene-render",
            "local",
            @"C:\catalog\scene-render@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("barcode", 2, 8, 4, "{jan}"),
            },
            "Scene render parity test");

        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["jan"] = "4901234567894",
        };

        var scene = TemplateCatalogDraftSceneBuilder.Build(document, bindings);
        var barcode = Assert.IsType<DraftBarcodeSceneElement>(scene.Elements[0]);
        var svg = TemplateCatalogPreviewBuilder.BuildSvg(document, bindings);
        var pdfText = Encoding.ASCII.GetString(TemplateCatalogPdfBuilder.BuildPdf(document, bindings));

        var expectedSvgFrame =
            $"<rect x=\"{TemplateCatalogRenderUtilities.FormatNumber(barcode.Xmm)}\" y=\"{TemplateCatalogRenderUtilities.FormatNumber(barcode.Ymm)}\" width=\"{TemplateCatalogRenderUtilities.FormatNumber(barcode.FrameWidthMm)}\" height=\"{TemplateCatalogRenderUtilities.FormatNumber(barcode.FrameHeightMm)}\"";
        var expectedPdfFrame =
            $"""{TemplateCatalogRenderUtilities.FormatNumber(ToPoints(barcode.Xmm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(scene.PageHeightMm - barcode.Ymm - barcode.FrameHeightMm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(barcode.FrameWidthMm))} {TemplateCatalogRenderUtilities.FormatNumber(ToPoints(barcode.FrameHeightMm))} re S""";

        Assert.Contains(expectedSvgFrame, svg, StringComparison.Ordinal);
        Assert.Contains(expectedPdfFrame, pdfText, StringComparison.Ordinal);
    }

    private static double ToPoints(double millimeters)
    {
        return Math.Round(millimeters * (72.0 / 25.4), 2);
    }
}
