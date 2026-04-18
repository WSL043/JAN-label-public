using System.IO;

using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class NativeTemplateCatalogLoaderTests
{
    private const string TemplateOverlayDirectoryEnvVar = "JAN_LABEL_TEMPLATE_OVERLAY_DIR";

    [Fact]
    public void Load_UsesPackagedManifest_WhenNoLocalManifestExists()
    {
        var overlayDirectory = CreateTempDirectory();
        using var _ = new EnvironmentVariableScope(TemplateOverlayDirectoryEnvVar, overlayDirectory);

        var state = NativeTemplateCatalogLoader.Load();

        Assert.Equal("basic-50x30@v1", state.DefaultTemplateVersion);
        Assert.Contains(state.Entries, (entry) => entry.Version == "basic-50x30@v1" && entry.Source == "packaged");
        Assert.Equal("missing", state.ManifestStatus);
        Assert.Equal("packaged", state.EffectiveDefaultSource);
        Assert.Contains(state.Issues, (issue) => issue.Code == "manifest_missing");
        Assert.Equal(Path.GetFullPath(overlayDirectory), state.OverlayDirectoryPath);
    }

    [Fact]
    public void Load_PrefersValidLocalOverlay_AndReportsBrokenEntries()
    {
        var overlayDirectory = CreateTempDirectory();
        using var _ = new EnvironmentVariableScope(TemplateOverlayDirectoryEnvVar, overlayDirectory);

        File.WriteAllText(
            Path.Combine(overlayDirectory, "template-manifest.json"),
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "overlay-basic@v1",
              "templates": [
                {
                  "version": "overlay-basic@v1",
                  "path": "overlay-basic@v1.json",
                  "label_name": "Overlay Basic",
                  "enabled": true,
                  "description": "Overlay winner"
                },
                {
                  "version": "broken-local@v1",
                  "path": "missing.json",
                  "label_name": "Broken Local",
                  "enabled": true,
                  "description": "Broken entry"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(overlayDirectory, "overlay-basic@v1.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "overlay-basic@v1",
              "label_name": "Overlay Basic",
              "description": "Overlay winner",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": []
            }
            """);
        File.WriteAllText(
            Path.Combine(overlayDirectory, "orphan.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "orphan@v1",
              "label_name": "Orphan",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": []
            }
            """);

        var state = NativeTemplateCatalogLoader.Load();

        Assert.Equal("overlay-basic@v1", state.DefaultTemplateVersion);
        Assert.Contains(state.Entries, (entry) => entry.Version == "overlay-basic@v1" && entry.Source == "local");
        Assert.DoesNotContain(state.Entries, (entry) => entry.Version == "broken-local@v1");
        Assert.Equal("local", state.EffectiveDefaultSource);
        Assert.Contains(state.LocalEntries, (entry) => entry.Version == "overlay-basic@v1" && entry.FileExists);
        Assert.Contains(state.Issues, (issue) => issue.Code == "missing_template_file");
        Assert.Contains(state.Issues, (issue) => issue.Code == "unreferenced_overlay_file");
    }

    [Fact]
    public void Load_FallsBackToPackagedCatalog_WhenLocalManifestIsMalformed()
    {
        var overlayDirectory = CreateTempDirectory();
        using var _ = new EnvironmentVariableScope(TemplateOverlayDirectoryEnvVar, overlayDirectory);

        File.WriteAllText(Path.Combine(overlayDirectory, "template-manifest.json"), "{ not-valid-json");

        var state = NativeTemplateCatalogLoader.Load();

        Assert.Equal("basic-50x30@v1", state.DefaultTemplateVersion);
        Assert.Contains(state.Entries, (entry) => entry.Version == "basic-50x30@v1" && entry.Source == "packaged");
        Assert.Equal("error", state.ManifestStatus);
        Assert.Contains(state.Issues, (issue) => issue.Code == "manifest_parse_failed");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "jan-label-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
