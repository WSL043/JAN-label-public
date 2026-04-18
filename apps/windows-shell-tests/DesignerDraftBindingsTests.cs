using JanLabel.WindowsShell.Core;
using Xunit;

namespace JanLabel.WindowsShell.Tests;

public sealed class DesignerDraftBindingsTests
{
    [Fact]
    public void CreateBaseBindings_ReturnsCanonicalDraftDefaults()
    {
        var bindings = DesignerDraftBindings.CreateBaseBindings("native-open@v1");

        Assert.Equal("native-open@v1", bindings["template_version"]);
        Assert.Equal(DesignerDraftBindings.DraftProofMode, bindings["proof_mode"]);
        Assert.Equal(DesignerDraftBindings.DraftStatus, bindings["status"]);
        Assert.Equal(DesignerDraftBindings.DefaultBrand, bindings["brand"]);
        Assert.Equal(DesignerDraftBindings.DefaultSku, bindings["sku"]);
        Assert.Equal(DesignerDraftBindings.DefaultJan, bindings["jan"]);
        Assert.Equal(DesignerDraftBindings.DefaultQty, bindings["qty"]);
        Assert.Equal(DesignerDraftBindings.DefaultJobId, bindings["job"]);
        Assert.Equal(DesignerDraftBindings.DefaultJobId, bindings["job_id"]);
    }

    [Theory]
    [InlineData("SKU", "sku")]
    [InlineData("Proof Mode", "proof_mode")]
    [InlineData("  Template Version  ", "template_version")]
    [InlineData("", "")]
    public void NormalizeBindingKey_UsesLowerSnakeCase(string input, string expected)
    {
        Assert.Equal(expected, DesignerDraftBindings.NormalizeBindingKey(input));
    }
}
