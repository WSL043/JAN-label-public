namespace JanLabel.WindowsShell.Core;

public static class DesignerDraftBindings
{
    public const string DraftProofMode = "native-draft";
    public const string DraftStatus = "Native draft preview";
    public const string DefaultBrand = "JAN-LAB";
    public const string DefaultSku = "200-145-3";
    public const string DefaultJan = "4901234567894";
    public const string DefaultQty = "24 PCS";
    public const string DefaultJobId = "JOB-001";

    public static Dictionary<string, string> CreateBaseBindings(string templateVersion)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["template_version"] = string.IsNullOrWhiteSpace(templateVersion) ? "designer-draft" : templateVersion,
            ["proof_mode"] = DraftProofMode,
            ["status"] = DraftStatus,
            ["job"] = DefaultJobId,
            ["job_id"] = DefaultJobId,
            ["brand"] = DefaultBrand,
            ["sku"] = DefaultSku,
            ["jan"] = DefaultJan,
            ["qty"] = DefaultQty,
        };
    }

    public static string NormalizeBindingKey(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : key.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
