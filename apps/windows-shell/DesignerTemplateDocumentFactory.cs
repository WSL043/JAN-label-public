using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

internal static class DesignerTemplateDocumentFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static TemplateDocument Create(DesignerWorkspaceModel workspace)
    {
        var templateVersion = ResolveTemplateVersion(workspace);
        var labelName = ResolveLabelName(workspace, templateVersion);
        var (pageWidthMm, pageHeightMm) = ResolvePageSize(workspace);

        var document = new TemplateSpecDocument
        {
            SchemaVersion = "template-spec-v1",
            TemplateVersion = templateVersion,
            LabelName = labelName,
            Description = $"Saved from windows-shell on {DateTimeOffset.UtcNow:O}.",
            Page = new TemplatePageDocument
            {
                WidthMm = pageWidthMm,
                HeightMm = pageHeightMm,
                BackgroundFill = "#ffffff",
            },
            Border = new TemplateBorderDocument
            {
                Visible = true,
                Color = "#000000",
                WidthMm = 0.2,
            },
            Fields = workspace.CanvasElements
                .Select((element) => ToFieldDocument(element))
                .ToList(),
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        return new TemplateDocument(templateVersion, labelName, json, document.Description);
    }

    public static DesignerDocument CreateDesignerDocument(DesignerWorkspaceModel workspace, string bindingJson)
    {
        var templateDocument = Create(workspace);
        return new DesignerDocument(
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            templateDocument.TemplateVersion,
            templateDocument.LabelName,
            templateDocument.DocumentJson,
            string.IsNullOrWhiteSpace(bindingJson) ? "{}" : bindingJson,
            SourcePath: "in-memory designer draft");
    }

    private static string ResolveTemplateVersion(DesignerWorkspaceModel workspace)
    {
        var selected = workspace.TemplateLibrary.SelectedTemplate?.Name;
        if (!string.IsNullOrWhiteSpace(selected))
        {
            return selected;
        }

        var documentRow = workspace.SetupRows.FirstOrDefault((row) => string.Equals(row.Name, "Document", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(documentRow?.Value))
        {
            return documentRow.Value;
        }

        if (!string.IsNullOrWhiteSpace(workspace.PrimaryDocumentTitle))
        {
            return workspace.PrimaryDocumentTitle;
        }

        throw new InvalidOperationException("Designer workspace does not have a template version to save.");
    }

    private static string ResolveLabelName(DesignerWorkspaceModel workspace, string templateVersion)
    {
        var previewLabel = workspace.PreviewRows.FirstOrDefault((row) => string.Equals(row.Name, "Label", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(previewLabel) &&
            !string.Equals(previewLabel, "preview unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return previewLabel;
        }

        var trimmedVersion = templateVersion.Split('@', 2, StringSplitOptions.TrimEntries)[0];
        return string.IsNullOrWhiteSpace(trimmedVersion)
            ? templateVersion
            : trimmedVersion;
    }

    private static (double WidthMm, double HeightMm) ResolvePageSize(DesignerWorkspaceModel workspace)
    {
        var pageText = workspace.PreviewRows.FirstOrDefault((row) => string.Equals(row.Name, "Page", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(pageText))
        {
            var match = Regex.Match(pageText, @"(?<width>\d+(\.\d+)?)\s*x\s*(?<height>\d+(\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success &&
                double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var widthMm) &&
                double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var heightMm))
            {
                return (widthMm, heightMm);
            }
        }

        return (50.0, 30.0);
    }

    private static TemplateFieldDocument ToFieldDocument(CanvasElementModel element)
    {
        var normalizedName = NormalizeFieldName(element.Caption);
        return new TemplateFieldDocument
        {
            Name = normalizedName,
            Xmm = Math.Round(element.X / 12.0, 1),
            Ymm = Math.Round(element.Y / 12.0, 1),
            FontSizeMm = Math.Max(1.0, Math.Round(element.FontSize / 4.5, 1)),
            Template = ResolveTemplateValue(element, normalizedName),
        };
    }

    private static string NormalizeFieldName(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return "field";
        }

        return caption.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string ResolveTemplateValue(CanvasElementModel element, string normalizedName)
    {
        if (!string.IsNullOrWhiteSpace(element.Value) &&
            element.Value.Contains('{', StringComparison.Ordinal) &&
            element.Value.Contains('}', StringComparison.Ordinal))
        {
            return element.Value;
        }

        return normalizedName switch
        {
            "brand" => "{brand}",
            "sku" => "{sku}",
            "barcode" => "{jan}",
            "jan" => "{jan}",
            "qty" => "{qty}",
            "job" => "{job_id}",
            "status" => element.Value,
            _ => string.IsNullOrWhiteSpace(element.Value) ? $"{{{normalizedName}}}" : element.Value,
        };
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
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("page")]
        public TemplatePageDocument Page { get; set; } = new();

        [JsonPropertyName("border")]
        public TemplateBorderDocument Border { get; set; } = new();

        [JsonPropertyName("fields")]
        public List<TemplateFieldDocument> Fields { get; set; } = new();
    }

    private sealed class TemplatePageDocument
    {
        [JsonPropertyName("width_mm")]
        public double WidthMm { get; set; }

        [JsonPropertyName("height_mm")]
        public double HeightMm { get; set; }

        [JsonPropertyName("background_fill")]
        public string BackgroundFill { get; set; } = "#ffffff";
    }

    private sealed class TemplateBorderDocument
    {
        [JsonPropertyName("visible")]
        public bool Visible { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#000000";

        [JsonPropertyName("width_mm")]
        public double WidthMm { get; set; }
    }

    private sealed class TemplateFieldDocument
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
