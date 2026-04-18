namespace JanLabel.WindowsShell.Core;

public sealed record LegacyRuntimePaths
{
    public const string PrintOutputDirectoryEnvVar = "JAN_LABEL_PRINT_OUTPUT_DIR";
    public const string AuditLogDirectoryEnvVar = "JAN_LABEL_AUDIT_LOG_DIR";
    public const string TemplateOverlayDirectoryEnvVar = "JAN_LABEL_TEMPLATE_OVERLAY_DIR";
    public const string BatchQueueSnapshotPathEnvVar = "JAN_LABEL_BATCH_QUEUE_SNAPSHOT_PATH";

    private LegacyRuntimePaths(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        TemplateOverlayDirectory = ResolvePath(TemplateOverlayDirectoryEnvVar, Path.Combine(RootDirectory, "template-overlays"));
        AuditDirectory = ResolvePath(AuditLogDirectoryEnvVar, Path.Combine(RootDirectory, "audit"));
        BatchQueueSnapshotPath = ResolvePath(BatchQueueSnapshotPathEnvVar, Path.Combine(RootDirectory, "batch-queue-snapshot.json"));
        ProofOutputDirectory = ResolvePath(PrintOutputDirectoryEnvVar, Path.Combine(RootDirectory, "proofs"));
        PrintOutputDirectory = ResolvePath(PrintOutputDirectoryEnvVar, Path.Combine(RootDirectory, "proofs"));
    }

    public string RootDirectory { get; }

    public string TemplateOverlayDirectory { get; }

    public string AuditDirectory { get; }

    public string BatchQueueSnapshotPath { get; }

    public string ProofOutputDirectory { get; }

    public string PrintOutputDirectory { get; }

    public static LegacyRuntimePaths Detect()
    {
        return FromRootDirectory(Path.Combine(Path.GetTempPath(), "jan-label"));
    }

    public static LegacyRuntimePaths FromRootDirectory(string rootDirectory)
    {
        return new LegacyRuntimePaths(rootDirectory);
    }

    private static string ResolvePath(string envVar, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(envVar);
        return string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(fallback)
            : Path.GetFullPath(configured);
    }
}
