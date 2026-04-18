namespace JanLabel.WindowsShell.Core;

public sealed record WindowsShellAppPaths
{
    public const string AppRootEnvVar = "JAN_LABEL_APP_ROOT";

    private WindowsShellAppPaths(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        StateDirectory = Path.Combine(RootDirectory, "state");
        DatabasePath = Path.Combine(StateDirectory, "janlabel.db");
        BatchSnapshotPath = Path.Combine(StateDirectory, "batch-queue-snapshot.json");
        CatalogDirectory = Path.Combine(RootDirectory, "catalog");
        CatalogManifestPath = Path.Combine(CatalogDirectory, "template-manifest.json");
        ProofArtifactsDirectory = Path.Combine(RootDirectory, "artifacts", "proofs");
        PrintArtifactsDirectory = Path.Combine(RootDirectory, "artifacts", "prints");
        ExportsDirectory = Path.Combine(RootDirectory, "exports");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
        LegacyRuntimeImportDirectory = Path.Combine(BackupsDirectory, "legacy-runtime");
    }

    public string RootDirectory { get; }

    public string StateDirectory { get; }

    public string DatabasePath { get; }

    public string BatchSnapshotPath { get; }

    public string CatalogDirectory { get; }

    public string CatalogManifestPath { get; }

    public string ProofArtifactsDirectory { get; }

    public string PrintArtifactsDirectory { get; }

    public string ExportsDirectory { get; }

    public string BackupsDirectory { get; }

    public string LegacyRuntimeImportDirectory { get; }

    public static WindowsShellAppPaths Resolve()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(AppRootEnvVar);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return FromRootDirectory(configuredRoot);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return FromRootDirectory(Path.Combine(localAppData, "JANLabel"));
    }

    public static WindowsShellAppPaths FromRootDirectory(string rootDirectory)
    {
        return new WindowsShellAppPaths(rootDirectory);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(CatalogDirectory);
        Directory.CreateDirectory(ProofArtifactsDirectory);
        Directory.CreateDirectory(PrintArtifactsDirectory);
        Directory.CreateDirectory(ExportsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LegacyRuntimeImportDirectory);
    }
}
