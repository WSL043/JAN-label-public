using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class DesignerTemplateWorkspaceHydratorTests
{
    [Fact]
    public void Apply_PreservesFieldTemplateExpressionsForAuthoring()
    {
        var workspace = new DesignerWorkspaceModel();
        var document = new TemplateCatalogDocument(
            "native-open@v1",
            "native-open",
            "local",
            @"C:\catalog\native-open@v1.json",
            "{}",
            50,
            30,
            new[]
            {
                new TemplateCatalogDocumentField("sku", 2, 4, 3, "sku:{sku}"),
            });

        DesignerTemplateWorkspaceHydrator.Apply(workspace, document);

        var element = Assert.Single(workspace.CanvasElements);
        Assert.Equal("sku:{sku}", element.Value);
        Assert.Equal("sku:{sku}", workspace.SelectedElementProperties.Binding);
    }

    [Fact]
    public void DesignerSelectionModel_UsesTwelvePixelsPerMillimeterWhenEditingGeometry()
    {
        var workspace = new DesignerWorkspaceModel();
        var element = new CanvasElementModel("SKU", "{sku}", 24, 36, 120, 60, 14, false);
        workspace.CanvasElements.Add(element);

        workspace.SelectCanvasElement(element);

        Assert.Equal(2.0, workspace.SelectedElementProperties.Xmm);
        Assert.Equal(3.0, workspace.SelectedElementProperties.Ymm);
        Assert.Equal(10.0, workspace.SelectedElementProperties.WidthMm);
        Assert.Equal(5.0, workspace.SelectedElementProperties.HeightMm);

        workspace.SelectedElementProperties.Xmm = 3.5;
        workspace.SelectedElementProperties.Ymm = 4.5;
        workspace.SelectedElementProperties.WidthMm = 11.0;
        workspace.SelectedElementProperties.HeightMm = 6.0;

        Assert.Equal(42.0, element.X);
        Assert.Equal(54.0, element.Y);
        Assert.Equal(132.0, element.Width);
        Assert.Equal(72.0, element.Height);
    }
}
