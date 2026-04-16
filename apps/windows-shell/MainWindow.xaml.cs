using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Fluent;

namespace JanLabel.WindowsShell;

public partial class MainWindow : RibbonWindow, INotifyPropertyChanged
{
    private const string DefaultActionStatus = "Shell actions are wired for prototype feedback; proof and print authority still route through desktop-shell.";

    private ModuleModel? _selectedModule;
    private string _lastActionStatus = DefaultActionStatus;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        WorkspaceFactory.SeedModules(Modules);
        SelectedModule = Modules.FirstOrDefault((module) => module.Label == "Designer") ?? Modules.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModuleModel> Modules { get; } = new();

    public ObservableCollection<RibbonGroupModel> CurrentRibbonGroups { get; } = new();

    public ObservableCollection<string> CurrentHeaderActions { get; } = new();

    public ObservableCollection<ContextBadgeModel> CurrentContextBadges { get; } = new();

    public ObservableCollection<StatusStripItemModel> CurrentStatusStripItems { get; } = new();

    public string LastActionStatus
    {
        get => _lastActionStatus;
        private set => SetProperty(ref _lastActionStatus, value);
    }

    public ModuleModel? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (!SetProperty(ref _selectedModule, value))
            {
                return;
            }

            ReplaceCollection(CurrentRibbonGroups, value?.RibbonGroups);
            ReplaceCollection(CurrentHeaderActions, value?.HeaderActions);
            ReplaceCollection(CurrentContextBadges, value?.ContextBadges);
            RefreshStatusStrip(value);
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(WorkspaceTagline));
            OnPropertyChanged(nameof(CurrentWorkspace));
            OnPropertyChanged(nameof(CurrentWorkspaceLead));
        }
    }

    public WorkspaceModel? CurrentWorkspace => SelectedModule?.Workspace;

    public string WindowTitle => $"JAN Label Workstation - {SelectedModule?.Label ?? "Designer"}";

    public string WorkspaceTagline => SelectedModule?.Tagline ?? "Windows-native operator workstation";

    public string CurrentWorkspaceLead => SelectedModule?.Lead ?? "Operator-facing shell context is not available.";

    private void RefreshStatusStrip(ModuleModel? module)
    {
        CurrentStatusStripItems.Clear();
        CurrentStatusStripItems.Add(new StatusStripItemModel("Action", LastActionStatus));

        if (module?.StatusStripItems is null)
        {
            return;
        }

        foreach (var item in module.StatusStripItems)
        {
            CurrentStatusStripItems.Add(item);
        }
    }

    private void RecordShellAction(string actionLabel)
    {
        var originLabel = SelectedModule?.Label ?? "Shell";
        var routedModule = ResolveTargetModule(actionLabel);
        if (routedModule is not null && !ReferenceEquals(routedModule, SelectedModule))
        {
            SelectedModule = routedModule;
        }

        var targetLabel = SelectedModule?.Label ?? originLabel;
        var routeVerb = string.Equals(originLabel, targetLabel, StringComparison.Ordinal) ? "stayed in" : "routed to";
        LastActionStatus = $"{actionLabel}: {routeVerb} {targetLabel}. {BuildActionOutcome(actionLabel, targetLabel)}";
        RefreshStatusStrip(SelectedModule);
    }

    private void ShellActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is string actionLabel && !string.IsNullOrWhiteSpace(actionLabel))
        {
            RecordShellAction(actionLabel);
        }
    }

    private void RibbonActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string actionLabel && !string.IsNullOrWhiteSpace(actionLabel))
        {
            RecordShellAction(actionLabel);
        }
    }

    private void QuickAccessButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Fluent.Button button)
        {
            RecordShellAction(button.Header?.ToString() ?? "Quick access action");
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T>? items)
    {
        target.Clear();
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private ModuleModel? ResolveTargetModule(string actionLabel)
    {
        if (MatchesAction(
                actionLabel,
                "Refresh State",
                "Open Handoff",
                "View Preview",
                "Current State",
                "Release Notes",
                "Preview Package",
                "Pin Workspace",
                "Export State"))
        {
            return FindModule("Home");
        }

        if (MatchesAction(
                actionLabel,
                "Open Library",
                "Overlay Status",
                "Catalog Rules",
                "New Format",
                "Print Preview",
                "Paste",
                "Duplicate",
                "Delete",
                "Text",
                "Barcode",
                "Line",
                "Box",
                "Align Left",
                "Make Same Size",
                "Snap",
                "Record Browser",
                "Query Prompt",
                "Named Data Sources",
                "Rust Preview",
                "Save to Catalog"))
        {
            return FindModule("Designer");
        }

        if (MatchesAction(
                actionLabel,
                "Refresh Queue",
                "Hold",
                "Release",
                "Open PDF",
                "Approve",
                "Dispatch Batch",
                "Route Check",
                "Run Proof",
                "Print",
                "Proof"))
        {
            return FindModule("Print Console");
        }

        if (MatchesAction(
                actionLabel,
                "Import Workbook",
                "Retry Failed",
                "Queue Snapshot",
                "CSV",
                "XLSX",
                "Alias Map",
                "Submit Ready",
                "Freeze Row",
                "Fixture Check",
                "Unknown Template",
                "JAN Warnings"))
        {
            return FindModule("Batch Jobs");
        }

        if (MatchesAction(
                actionLabel,
                "Approve Proof",
                "Reject Proof",
                "Search Ledger",
                "Export Audit",
                "Trim Retention",
                "Pin Artifact",
                "Retention Dry Run",
                "List Bundles",
                "Validate Bundle",
                "Restore"))
        {
            return FindModule("History");
        }

        if (actionLabel.Contains("Preview", StringComparison.OrdinalIgnoreCase))
        {
            return FindModule("Designer");
        }

        if (actionLabel.Contains("Proof", StringComparison.OrdinalIgnoreCase))
        {
            return FindModule("Print Console");
        }

        return null;
    }

    private ModuleModel? FindModule(string label)
    {
        return Modules.FirstOrDefault((module) => string.Equals(module.Label, label, StringComparison.Ordinal));
    }

    private static bool MatchesAction(string actionLabel, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.Equals(actionLabel, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildActionOutcome(string actionLabel, string targetLabel)
    {
        return targetLabel switch
        {
            "Home" => "Migration and release context stays visible before touching another operational lane.",
            "Designer" when string.Equals(actionLabel, "Save to Catalog", StringComparison.OrdinalIgnoreCase)
                => "Saved catalog state is the only template state this shell treats as proof-safe.",
            "Designer" => "Authoring state, overlay impact, and rollback remain visible on one screen.",
            "Print Console" => "Proof lineage and dispatch route should be reviewed before print unlock.",
            "Batch Jobs" => "Import assumptions, retry eligibility, and queue blockers are now in view.",
            "History" => "Audit, proof review, and restore safety remain explicit before operator approval.",
            _ => "Proof and print authority still route through desktop-shell.",
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
