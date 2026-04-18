using System.IO;

using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class LocalTemplateCatalogServiceTests
{
    [Fact]
    public async Task SaveTemplateAsync_CreatesManifestAndIndexesCatalogEntry_WhenManifestIsMissing()
    {
        var context = InitializePlatform();
        var service = context.TemplateCatalogService;

        var result = await service.SaveTemplateAsync(
            new TemplateDocument(
                "native-save@v1",
                "native-save",
                """
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "native-save@v1",
                  "label_name": "native-save",
                  "page": { "width_mm": 50, "height_mm": 30 },
                  "fields": []
                }
                """));

        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.TemplatePath));
        Assert.True(result.RequiresProofReview);
        Assert.Contains(result.Warnings, (warning) => warning.Contains("proof review", StringComparison.OrdinalIgnoreCase));

        var snapshot = service.LoadCatalogSnapshot();
        Assert.Equal("native-save@v1", snapshot.DefaultTemplateVersion);
        Assert.Equal("local", snapshot.EffectiveDefaultSource);
        Assert.Contains(snapshot.Entries, (entry) => entry.Version == "native-save@v1" && entry.Source == "local");

        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT label_name, manifest_path, is_default FROM template_catalog_entries WHERE template_version = $templateVersion;";
        command.Parameters.AddWithValue("$templateVersion", "native-save@v1");
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("native-save", reader.GetString(0));
        Assert.Equal(context.Paths.CatalogManifestPath, reader.GetString(1));
        Assert.Equal(1L, reader.GetInt64(2));
    }

    [Fact]
    public async Task SaveTemplateAsync_PreservesExistingManifestEntriesAndDefault()
    {
        var context = InitializePlatform();
        Directory.CreateDirectory(context.Paths.CatalogDirectory);
        File.WriteAllText(
            context.Paths.CatalogManifestPath,
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "existing@v1",
              "templates": [
                {
                  "version": "existing@v1",
                  "path": "existing@v1.json",
                  "label_name": "existing",
                  "enabled": true,
                  "description": "Existing local entry"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(context.Paths.CatalogDirectory, "existing@v1.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "existing@v1",
              "label_name": "existing",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": []
            }
            """);

        var result = await context.TemplateCatalogService.SaveTemplateAsync(
            new TemplateDocument(
                "new-local@v1",
                "new-local",
                """
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "new-local@v1",
                  "label_name": "new-local",
                  "page": { "width_mm": 50, "height_mm": 30 },
                  "fields": []
                }
                """));

        Assert.True(File.Exists(result.TemplatePath));
        var snapshot = context.TemplateCatalogService.LoadCatalogSnapshot();
        Assert.Equal("existing@v1", snapshot.DefaultTemplateVersion);
        Assert.Contains(snapshot.Entries, (entry) => entry.Version == "existing@v1" && entry.Source == "local");
        Assert.Contains(snapshot.Entries, (entry) => entry.Version == "new-local@v1" && entry.Source == "local");
    }

    [Fact]
    public async Task SaveTemplateAsync_RejectsMalformedManifestToAvoidSilentOverwrite()
    {
        var context = InitializePlatform();
        Directory.CreateDirectory(context.Paths.CatalogDirectory);
        File.WriteAllText(context.Paths.CatalogManifestPath, "{ not-valid-json");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.TemplateCatalogService.SaveTemplateAsync(
                new TemplateDocument(
                    "blocked-save@v1",
                    "blocked-save",
                    """
                    {
                      "schema_version": "template-spec-v1",
                      "template_version": "blocked-save@v1",
                      "label_name": "blocked-save",
                      "page": { "width_mm": 50, "height_mm": 30 },
                      "fields": []
                    }
                    """)));

        Assert.Contains("could not be parsed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(context.Paths.CatalogDirectory, "blocked-save@v1.json")));
    }

    [Fact]
    public async Task LoadTemplateAsync_LoadsPackagedTemplate_WhenNoLocalOverrideExists()
    {
        var context = InitializePlatform();

        var document = await context.TemplateCatalogService.LoadTemplateAsync("basic-50x30@v1");

        Assert.Equal("basic-50x30@v1", document.TemplateVersion);
        Assert.Equal("packaged", document.Source);
        Assert.EndsWith("packages\\templates\\basic-50x30@v1.json", document.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(document.Fields);
        Assert.Contains("\"template_version\": \"basic-50x30@v1\"", document.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadTemplateAsync_PrefersReadableLocalOverlay_WhenTemplateExistsLocally()
    {
        var context = InitializePlatform();
        Directory.CreateDirectory(context.Paths.CatalogDirectory);
        File.WriteAllText(
            context.Paths.CatalogManifestPath,
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "basic-50x30@v1",
              "templates": [
                {
                  "version": "basic-50x30@v1",
                  "path": "basic-50x30@v1.json",
                  "label_name": "Local Basic",
                  "enabled": true,
                  "description": "Local override"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(context.Paths.CatalogDirectory, "basic-50x30@v1.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "basic-50x30@v1",
              "label_name": "Local Basic",
              "description": "Local override",
              "page": { "width_mm": 60, "height_mm": 40 },
              "fields": [
                { "name": "barcode", "x_mm": 2, "y_mm": 8, "font_size_mm": 4, "template": "{jan}" }
              ]
            }
            """);

        var document = await context.TemplateCatalogService.LoadTemplateAsync("basic-50x30@v1");

        Assert.Equal("local", document.Source);
        Assert.Equal("Local Basic", document.LabelName);
        Assert.Equal(60, document.PageWidthMm);
        Assert.Single(document.Fields);
        Assert.Equal("barcode", document.Fields[0].Name);
    }

    [Fact]
    public async Task SaveTemplateAsync_RejectsTemplateVersionTraversalOutsideCatalogRoot()
    {
        var context = InitializePlatform();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.TemplateCatalogService.SaveTemplateAsync(
                new TemplateDocument(
                    "..\\..\\outside\\pwn",
                    "escape",
                    """
                    {
                      "schema_version": "template-spec-v1",
                      "template_version": "..\\..\\outside\\pwn",
                      "label_name": "escape",
                      "page": { "width_mm": 50, "height_mm": 30 },
                      "fields": []
                    }
                    """)));

        Assert.Contains("cannot be used as a local catalog file name", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(context.Paths.CatalogDirectory, "..\\..\\outside\\pwn.json")));
    }

    [Fact]
    public async Task LoadTemplateAsync_RejectsMismatchedEmbeddedTemplateVersion()
    {
        var context = InitializePlatform();
        Directory.CreateDirectory(context.Paths.CatalogDirectory);
        File.WriteAllText(
            context.Paths.CatalogManifestPath,
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "basic-50x30@v1",
              "templates": [
                {
                  "version": "basic-50x30@v1",
                  "path": "basic-50x30@v1.json",
                  "label_name": "Broken Basic",
                  "enabled": true
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(context.Paths.CatalogDirectory, "basic-50x30@v1.json"),
            """
            {
              "schema_version": "template-spec-v1",
              "template_version": "other-template@v1",
              "label_name": "Broken Basic",
              "page": { "width_mm": 50, "height_mm": 30 },
              "fields": []
            }
            """);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.TemplateCatalogService.LoadTemplateAsync("basic-50x30@v1"));

        Assert.Contains("identity mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveTemplateAsync_ClearsPreviousDefaultRowsWhenManifestDefaultChanges()
    {
        var context = InitializePlatform();
        await context.TemplateCatalogService.SaveTemplateAsync(
            new TemplateDocument(
                "existing@v1",
                "existing",
                """
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "existing@v1",
                  "label_name": "existing",
                  "page": { "width_mm": 50, "height_mm": 30 },
                  "fields": []
                }
                """));
        File.WriteAllText(
            context.Paths.CatalogManifestPath,
            """
            {
              "schema_version": "template-manifest-v1",
              "default_template_version": "new-default@v1",
              "templates": [
                {
                  "version": "existing@v1",
                  "path": "existing@v1.json",
                  "label_name": "existing",
                  "enabled": true
                },
                {
                  "version": "new-default@v1",
                  "path": "new-default@v1.json",
                  "label_name": "new-default",
                  "enabled": true
                }
              ]
            }
            """);

        await context.TemplateCatalogService.SaveTemplateAsync(
            new TemplateDocument(
                "new-default@v1",
                "new-default",
                """
                {
                  "schema_version": "template-spec-v1",
                  "template_version": "new-default@v1",
                  "label_name": "new-default",
                  "page": { "width_mm": 50, "height_mm": 30 },
                  "fields": []
                }
                """));

        using var connection = WindowsShellPlatform.OpenConnection(context.Paths);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT template_version, is_default
            FROM template_catalog_entries
            ORDER BY template_version;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<(string TemplateVersion, long IsDefault)>();
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        Assert.Contains(rows, (row) => row.TemplateVersion == "existing@v1" && row.IsDefault == 0);
        Assert.Contains(rows, (row) => row.TemplateVersion == "new-default@v1" && row.IsDefault == 1);
        Assert.Equal(1, rows.Count((row) => row.IsDefault == 1));
    }

    private static WindowsShellPlatformContext InitializePlatform()
    {
        return WindowsShellPlatform.Initialize(
            WindowsShellAppPaths.FromRootDirectory(CreateTempPath()),
            LegacyRuntimePaths.FromRootDirectory(CreateTempPath()));
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "jan-label-tests", Path.GetRandomFileName());
    }
}
