using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class TemplateCatalogPreviewBuilderTests
{
    [Fact]
    public void BuildSvg_RendersExpandedTextFieldsAndEscapesXml()
    {
        var document = new TemplateCatalogDocument(
            "preview-basic@v1",
            "preview-basic",
            "local",
            @"C:\catalog\preview-basic@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("brand", 2, 4, 3, "brand:{brand} <ok>"),
                new TemplateCatalogDocumentField("sku", 2, 10, 4, "sku:{sku}"),
            },
            "Preview test");

        var svg = TemplateCatalogPreviewBuilder.BuildSvg(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["brand"] = "JAN&LAB",
                ["sku"] = "200-145-3",
            });

        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("brand:JAN&amp;LAB &lt;ok&gt;", svg, StringComparison.Ordinal);
        Assert.Contains("sku:200-145-3", svg, StringComparison.Ordinal);
        Assert.Contains("Native draft preview only", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSvg_RendersBarcodePlaceholderBarsAndExpandedValue()
    {
        var document = new TemplateCatalogDocument(
            "preview-barcode@v1",
            "preview-barcode",
            "packaged",
            @"C:\catalog\preview-barcode@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("barcode", 2, 8, 4, "{jan}"),
            },
            "Preview test");

        var svg = TemplateCatalogPreviewBuilder.BuildSvg(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["jan"] = "4901234567894",
            });

        Assert.Contains("4901234567894", svg, StringComparison.Ordinal);
        Assert.Contains("stroke=\"#2563eb\"", svg, StringComparison.Ordinal);
        Assert.Contains("<rect", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSvg_FromTemplateDocument_ParsesTemplateJsonIntoDraftPreview()
    {
        var document = new TemplateDocument(
            "preview-draft@v1",
            "preview-draft",
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "preview-draft@v1",
              "label_name": "preview-draft",
              "page": { "width_mm": 60, "height_mm": 40 },
              "fields": [
                { "name": "brand", "x_mm": 2, "y_mm": 4, "font_size_mm": 3, "template": "brand:{brand}" }
              ]
            }
            """);

        var svg = TemplateCatalogPreviewBuilder.BuildSvg(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["brand"] = "JAN-LAB",
            });

        Assert.Contains("width=\"60mm\"", svg, StringComparison.Ordinal);
        Assert.Contains("brand:JAN-LAB", svg, StringComparison.Ordinal);
        Assert.Contains("Native draft preview only", svg, StringComparison.Ordinal);
    }
}
