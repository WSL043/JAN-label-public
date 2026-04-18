using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JanLabel.WindowsShell.Core;

public static class TemplateCatalogDocumentParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    public static TemplateCatalogDocument Parse(
        string documentJson,
        string templateVersion,
        string labelName,
        string source,
        string sourcePath,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new ArgumentException("Template JSON cannot be empty.", nameof(documentJson));
        }

        var normalizedJson = NormalizeJson(documentJson);

        try
        {
            var document = JsonSerializer.Deserialize<TemplateSpecDocument>(normalizedJson, JsonOptions);
            if (document is null)
            {
                throw new InvalidOperationException("Template document deserialized to null.");
            }

            if (string.IsNullOrWhiteSpace(document.SchemaVersion))
            {
                throw new InvalidOperationException("Template document is missing schema_version.");
            }

            if (!string.IsNullOrWhiteSpace(document.TemplateVersion) &&
                !string.Equals(document.TemplateVersion.Trim(), templateVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Template document identity mismatch: expected '{templateVersion}' but file declares '{document.TemplateVersion.Trim()}'.");
            }

            var resolvedTemplateVersion = string.IsNullOrWhiteSpace(document.TemplateVersion)
                ? templateVersion
                : document.TemplateVersion.Trim();
            var resolvedLabelName = string.IsNullOrWhiteSpace(document.LabelName)
                ? labelName
                : document.LabelName.Trim();

            return new TemplateCatalogDocument(
                resolvedTemplateVersion,
                resolvedLabelName,
                source,
                sourcePath,
                normalizedJson,
                document.Page.WidthMm,
                document.Page.HeightMm,
                document.Fields
                    .Select(
                        (field) => new TemplateCatalogDocumentField(
                            field.Name,
                            field.Xmm,
                            field.Ymm,
                            field.FontSizeMm,
                            field.Template))
                    .ToList(),
                string.IsNullOrWhiteSpace(document.Description) ? description : document.Description);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Template document could not be parsed: {ex.Message}", ex);
        }
    }

    private static string NormalizeJson(string documentJson)
    {
        var parsed = JsonNode.Parse(documentJson);
        if (parsed is null)
        {
            throw new InvalidOperationException("Template JSON parsed to null.");
        }

        return parsed.ToJsonString(JsonOptions);
    }

    private sealed class TemplateSpecDocument
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;

        [JsonPropertyName("template_version")]
        public string TemplateVersion { get; set; } = string.Empty;

        [JsonPropertyName("label_name")]
        public string LabelName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("page")]
        public TemplateSpecPageDocument Page { get; set; } = new();

        [JsonPropertyName("fields")]
        public List<TemplateSpecFieldDocument> Fields { get; set; } = new();
    }

    private sealed class TemplateSpecPageDocument
    {
        [JsonPropertyName("width_mm")]
        public double WidthMm { get; set; }

        [JsonPropertyName("height_mm")]
        public double HeightMm { get; set; }
    }

    private sealed class TemplateSpecFieldDocument
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("x_mm")]
        public double Xmm { get; set; }

        [JsonPropertyName("y_mm")]
        public double Ymm { get; set; }

        [JsonPropertyName("font_size_mm")]
        public double FontSizeMm { get; set; }

        [JsonPropertyName("template")]
        public string Template { get; set; } = string.Empty;
    }
}
