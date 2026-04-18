using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class LocalRenderServiceTests
{
    [Fact]
    public async Task RenderAsync_UsesDesignerDocumentAndBindingsToProduceDraftSvg()
    {
        var service = new LocalRenderService();
        var document = new DesignerDocument(
            "doc-1",
            "render-basic@v1",
            "render-basic",
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "render-basic@v1",
              "label_name": "render-basic",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": [
                { "name": "brand", "x_mm": 2, "y_mm": 4, "font_size_mm": 3, "template": "brand:{brand}" }
              ]
            }
            """,
            """
            {
              "brand": "JAN-LAB"
            }
            """);

        var artifact = await service.RenderAsync(document, new RenderRequest("SVG"));

        Assert.Equal("render-basic@v1", artifact.TemplateVersion);
        Assert.Equal("image/svg+xml", artifact.OutputMediaType);
        Assert.Contains("brand:JAN-LAB", artifact.Svg, StringComparison.Ordinal);
        Assert.Contains("Native draft preview only", artifact.Svg, StringComparison.Ordinal);
        Assert.Empty(artifact.PdfBytes);
    }

    [Fact]
    public async Task RenderAsync_PrefersNormalizedJanFromRequest()
    {
        var service = new LocalRenderService();
        var document = new DesignerDocument(
            "doc-2",
            "render-jan@v1",
            "render-jan",
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "render-jan@v1",
              "label_name": "render-jan",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": [
                { "name": "barcode", "x_mm": 2, "y_mm": 8, "font_size_mm": 4, "template": "{jan}" }
              ]
            }
            """,
            """
            {
              "jan": "1111111111111"
            }
            """);

        var artifact = await service.RenderAsync(
            document,
            new RenderRequest("SVG", SampleJson: null, NormalizedJan: "4901234567894"));

        Assert.Equal("4901234567894", artifact.NormalizedJan);
        Assert.Contains("4901234567894", artifact.Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("1111111111111", artifact.Svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_ReturnsSvgAndPdfWhenBothTargetsAreRequested()
    {
        var service = new LocalRenderService();
        var document = new DesignerDocument(
            "doc-3",
            "render-both@v1",
            "render-both",
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "render-both@v1",
              "label_name": "render-both",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": [
                { "name": "brand", "x_mm": 2, "y_mm": 4, "font_size_mm": 3, "template": "brand:{brand}" },
                { "name": "barcode", "x_mm": 2, "y_mm": 10, "font_size_mm": 4, "template": "{jan}" }
              ]
            }
            """,
            """
            {
              "brand": "JAN-LAB",
              "jan": "4901234567894"
            }
            """);

        var artifact = await service.RenderAsync(document, new RenderRequest("SVG,PDF"));

        Assert.Equal("image/svg+xml,application/pdf", artifact.OutputMediaType);
        Assert.Contains("brand:JAN-LAB", artifact.Svg, StringComparison.Ordinal);
        Assert.NotEmpty(artifact.PdfBytes);
        Assert.StartsWith("%PDF-1.4", System.Text.Encoding.ASCII.GetString(artifact.PdfBytes), StringComparison.Ordinal);
        Assert.Empty(artifact.Warnings ?? Array.Empty<string>());
    }

    [Fact]
    public async Task RenderAsync_DegradesToSvgOnlyWhenDraftPdfCannotEncodeNonAsciiText()
    {
        var service = new LocalRenderService();
        var document = new DesignerDocument(
            "doc-4",
            "render-unicode@v1",
            "render-unicode",
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "render-unicode@v1",
              "label_name": "render-unicode",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": [
                { "name": "brand", "x_mm": 2, "y_mm": 4, "font_size_mm": 3, "template": "\u54C1\u724C:{brand}" }
              ]
            }
            """,
            """
            {
              "brand": "\u6D4B\u8BD5"
            }
            """);

        var artifact = await service.RenderAsync(document, new RenderRequest("SVG,PDF"));

        Assert.Equal("image/svg+xml", artifact.OutputMediaType);
        Assert.Contains("\u54C1\u724C:\u6D4B\u8BD5", artifact.Svg, StringComparison.Ordinal);
        Assert.Empty(artifact.PdfBytes);
        Assert.Contains(artifact.Warnings ?? Array.Empty<string>(), (warning) => warning.Contains("Draft PDF artifact was skipped", StringComparison.Ordinal));
    }
}
