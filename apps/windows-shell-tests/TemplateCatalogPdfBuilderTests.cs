using System.Text;
using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class TemplateCatalogPdfBuilderTests
{
    [Fact]
    public void BuildPdf_UsesExactMediaBoxForDocumentSize()
    {
        var document = new TemplateCatalogDocument(
            "pdf-size@v1",
            "pdf-size",
            "local",
            @"C:\catalog\pdf-size@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("brand", 2, 4, 3, "brand:{brand}"),
            },
            "PDF size test");

        var pdf = TemplateCatalogPdfBuilder.BuildPdf(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["brand"] = "JAN-LAB",
            });

        var pdfText = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", pdfText, StringComparison.Ordinal);
        Assert.Contains("/MediaBox [0 0 141.73 85.04]", pdfText, StringComparison.Ordinal);
        Assert.Contains("Native draft PDF only", pdfText, StringComparison.Ordinal);
        Assert.Contains("brand:JAN-LAB", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPdf_RendersExpandedBarcodeValueInsideDraftStream()
    {
        var document = new TemplateCatalogDocument(
            "pdf-barcode@v1",
            "pdf-barcode",
            "packaged",
            @"C:\catalog\pdf-barcode@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("barcode", 2, 8, 4, "{jan}"),
            },
            "PDF barcode test");

        var pdf = TemplateCatalogPdfBuilder.BuildPdf(
            document,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["jan"] = "4901234567894",
            });

        var pdfText = Encoding.ASCII.GetString(pdf);
        Assert.Contains("4901234567894", pdfText, StringComparison.Ordinal);
        Assert.Contains(" re f", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPdf_RejectsNonAsciiTextInsteadOfSilentlyCorruptingDraftOutput()
    {
        var document = new TemplateCatalogDocument(
            "pdf-unicode@v1",
            "pdf-unicode",
            "local",
            @"C:\catalog\pdf-unicode@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("brand", 2, 4, 3, "\u54C1\u724C:{brand}"),
            },
            "PDF unicode test");

        var error = Assert.Throws<DraftPdfUnsupportedTextException>(
            () => TemplateCatalogPdfBuilder.BuildPdf(
                document,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brand"] = "\u6D4B\u8BD5",
                }));

        Assert.Contains("ASCII-only", error.Message, StringComparison.Ordinal);
    }
}
