using System.Text.Json;
using System.Text.Json.Nodes;

namespace JanLabel.WindowsShell.Core;

public sealed class LocalRenderService : IRenderService
{
    public ValueTask<RenderArtifact> RenderAsync(DesignerDocument document, RenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var bindings = ParseBindings(string.IsNullOrWhiteSpace(request.SampleJson) ? document.BindingJson : request.SampleJson);
        if (!string.IsNullOrWhiteSpace(request.NormalizedJan))
        {
            bindings["jan"] = request.NormalizedJan!;
        }

        var parsedDocument = TemplateCatalogDocumentParser.Parse(
            document.CanvasJson,
            document.TemplateVersion,
            document.LabelName,
            "draft",
            string.IsNullOrWhiteSpace(document.SourcePath) ? "in-memory designer draft" : document.SourcePath);
        var includeSvg = IncludesOutput(request.OutputTargets, "SVG");
        var includePdf = IncludesOutput(request.OutputTargets, "PDF");
        if (!includeSvg && !includePdf)
        {
            throw new InvalidOperationException($"Unsupported render targets '{request.OutputTargets}'. Expected SVG and/or PDF.");
        }

        var warnings = new List<string>();
        var svg = includeSvg ? TemplateCatalogPreviewBuilder.BuildSvg(parsedDocument, bindings) : string.Empty;
        var pdfBytes = Array.Empty<byte>();
        var pdfRendered = false;
        if (includePdf)
        {
            try
            {
                pdfBytes = TemplateCatalogPdfBuilder.BuildPdf(parsedDocument, bindings);
                pdfRendered = true;
            }
            catch (DraftPdfUnsupportedTextException ex) when (includeSvg)
            {
                warnings.Add($"Draft PDF artifact was skipped: {ex.Message}");
            }
        }

        var normalizedJan = bindings.TryGetValue("jan", out var janValue) ? janValue : request.NormalizedJan;

        return ValueTask.FromResult(
            new RenderArtifact(
                parsedDocument.TemplateVersion,
                svg,
                pdfBytes,
                ResolveOutputMediaType(includeSvg, pdfRendered),
                normalizedJan,
                warnings));
    }

    private static bool IncludesOutput(string? outputTargets, string target)
    {
        if (string.IsNullOrWhiteSpace(outputTargets))
        {
            return false;
        }

        var parts = outputTargets
            .Split(new[] { ',', '+', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any((part) => string.Equals(part, target, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveOutputMediaType(bool includeSvg, bool includePdf)
    {
        return (includeSvg, includePdf) switch
        {
            (true, true) => "image/svg+xml,application/pdf",
            (true, false) => "image/svg+xml",
            (false, true) => "application/pdf",
            _ => "application/octet-stream",
        };
    }

    private static Dictionary<string, string> ParseBindings(string? bindingJson)
    {
        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(bindingJson))
        {
            return bindings;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(bindingJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Render binding JSON could not be parsed: {ex.Message}", ex);
        }

        if (parsed is not JsonObject jsonObject)
        {
            throw new InvalidOperationException("Render binding JSON must be an object.");
        }

        foreach (var property in jsonObject)
        {
            if (property.Value is null)
            {
                continue;
            }

            if (property.Value is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<string>(out var stringValue))
                {
                    bindings[property.Key] = stringValue ?? string.Empty;
                    continue;
                }

                if (jsonValue.TryGetValue<bool>(out var boolValue))
                {
                    bindings[property.Key] = boolValue ? "true" : "false";
                    continue;
                }

                if (jsonValue.TryGetValue<double>(out var doubleValue))
                {
                    bindings[property.Key] = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }

                if (jsonValue.TryGetValue<long>(out var longValue))
                {
                    bindings[property.Key] = longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }
            }

            bindings[property.Key] = property.Value.ToJsonString();
        }

        return bindings;
    }
}
