using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Fluent;

namespace JanLabel.WindowsShell;

public partial class MainWindow : RibbonWindow, INotifyPropertyChanged
{
    private const string DefaultActionStatus = "Live companion state is loading; proof, print, audit, and catalog authority remain in desktop-shell.";
    private const string SeededRuntimeLabel = "seeded shell";
    private const string LiveRuntimeLabel = "live companion";
    private const string SeededFallbackRuntimeLabel = "seeded fallback";
    private const string StaleLiveRuntimeLabel = "stale live snapshot";

    private readonly DesktopShellCompanionClient _companionClient = new();
    private readonly List<INotifyPropertyChanged> _observedNestedContexts = new();
    private INotifyPropertyChanged? _observedWorkspace;
    private ModuleModel? _selectedModule;
    private string _lastActionStatus = DefaultActionStatus;
    private string _shellRuntimeLabel = SeededRuntimeLabel;
    private string _shellRuntimeSummary = "Seeded workstation shell is visible while the desktop-shell companion loads.";
    private bool _hasLiveSnapshotLoaded;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        WorkspaceFactory.SeedModules(Modules);
        SelectedModule = Modules.FirstOrDefault((module) => module.Label == "Designer") ?? Modules.FirstOrDefault();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModuleModel> Modules { get; } = new();

    public ObservableCollection<RibbonGroupModel> CurrentRibbonGroups { get; } = new();

    public ObservableCollection<ShellActionModel> CurrentHeaderActions { get; } = new();

    public ObservableCollection<ContextBadgeModel> CurrentContextBadges { get; } = new();

    public ObservableCollection<StatusStripItemModel> CurrentStatusStripItems { get; } = new();

    public ShellActionModel QuickAccessRefreshAction
        => ResolveQuickAccessAction(
            ShellActions.RefreshState,
            "Refresh is not exposed for the current lane.",
            ShellActions.RefreshState,
            ShellActions.RefreshSubjects,
            ShellActions.RefreshAudit,
            ShellActions.QueueSnapshot);

    public ShellActionModel QuickAccessPreviewAction
        => ResolveQuickAccessAction(
            ShellActions.PrintPreview,
            "Preview refresh is not exposed for the current lane.",
            ShellActions.ViewPreview,
            ShellActions.PrintPreview,
            ShellActions.RustPreview);

    public ShellActionModel QuickAccessApproveAction
        => ResolveQuickAccessAction(
            ShellActions.ApproveProof,
            "Proof review is not exposed for the current lane.",
            ShellActions.ApproveProof,
            ShellActions.Approve);

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

            DetachWorkspaceObservers();
            AttachWorkspaceObservers(value?.Workspace);
            ReplaceCollection(CurrentRibbonGroups, value?.RibbonGroups);
            ReplaceCollection(CurrentHeaderActions, value?.HeaderActions);
            SyncShellChromeFromSelection();
            OnPropertyChanged(nameof(WorkspaceTagline));
            OnPropertyChanged(nameof(CurrentWorkspace));
            OnPropertyChanged(nameof(QuickAccessRefreshAction));
            OnPropertyChanged(nameof(QuickAccessPreviewAction));
            OnPropertyChanged(nameof(QuickAccessApproveAction));
        }
    }

    public WorkspaceModel? CurrentWorkspace => SelectedModule?.Workspace;

    public string WindowTitle
    {
        get
        {
            var moduleLabel = SelectedModule?.Label ?? "Designer";
            var focusLabel = DescribeWorkspaceFocus(SelectedModule?.Workspace);
            return string.IsNullOrWhiteSpace(focusLabel)
                ? $"JAN Label Workstation - {moduleLabel}"
                : $"JAN Label Workstation - {moduleLabel} - {focusLabel}";
        }
    }

    public string WorkspaceTagline => SelectedModule?.Tagline ?? "Windows-native operator workstation";

    public string CurrentWorkspaceLead
    {
        get
        {
            var workspaceLead = BuildDynamicWorkspaceLead(SelectedModule?.Workspace) ?? SelectedModule?.Lead ?? "Operator-facing shell context is not available.";
            return string.Equals(_shellRuntimeLabel, LiveRuntimeLabel, StringComparison.Ordinal)
                ? workspaceLead
                : $"{_shellRuntimeSummary} {workspaceLead}";
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshLiveStateAsync("Loaded live desktop-shell companion state.");
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        await _companionClient.DisposeAsync();
    }

    private void SyncShellChromeFromSelection()
    {
        RefreshObservedNestedContexts(SelectedModule?.Workspace);
        RefreshContextBadges(SelectedModule);
        RefreshStatusStrip(SelectedModule);
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentWorkspaceLead));
    }

    private void RefreshContextBadges(ModuleModel? module)
    {
        CurrentContextBadges.Clear();
        CurrentContextBadges.Add(new ContextBadgeModel("Runtime", _shellRuntimeLabel, ResolveShellRuntimeAccent(_shellRuntimeLabel)));

        if (module?.ContextBadges is not null)
        {
            foreach (var badge in module.ContextBadges)
            {
                CurrentContextBadges.Add(badge);
            }
        }

        foreach (var badge in BuildDynamicContextBadges(module?.Workspace))
        {
            CurrentContextBadges.Add(badge);
        }
    }

    private void RefreshStatusStrip(ModuleModel? module)
    {
        CurrentStatusStripItems.Clear();
        CurrentStatusStripItems.Add(new StatusStripItemModel("Action", LastActionStatus));
        CurrentStatusStripItems.Add(new StatusStripItemModel("Runtime", _shellRuntimeLabel));
        CurrentStatusStripItems.Add(new StatusStripItemModel("Companion", BuildShellRuntimeStatusValue()));

        foreach (var item in BuildDynamicStatusStripItems(module?.Workspace))
        {
            CurrentStatusStripItems.Add(item);
        }

        if (module?.StatusStripItems is null)
        {
            return;
        }

        foreach (var item in module.StatusStripItems)
        {
            CurrentStatusStripItems.Add(item);
        }
    }

    private void AttachWorkspaceObservers(WorkspaceModel? workspace)
    {
        if (workspace is null)
        {
            return;
        }

        _observedWorkspace = workspace;
        _observedWorkspace.PropertyChanged += ObservedContextChanged;
        RefreshObservedNestedContexts(workspace);
    }

    private void DetachWorkspaceObservers()
    {
        if (_observedWorkspace is not null)
        {
            _observedWorkspace.PropertyChanged -= ObservedContextChanged;
            _observedWorkspace = null;
        }

        foreach (var context in _observedNestedContexts)
        {
            context.PropertyChanged -= ObservedContextChanged;
        }

        _observedNestedContexts.Clear();
    }

    private void RefreshObservedNestedContexts(WorkspaceModel? workspace)
    {
        foreach (var context in _observedNestedContexts)
        {
            context.PropertyChanged -= ObservedContextChanged;
        }

        _observedNestedContexts.Clear();

        switch (workspace)
        {
            case HomeWorkspaceModel home:
                AddObservedNestedContext(home.TemplateLibrary);
                break;
            case DesignerWorkspaceModel designer:
                AddObservedNestedContext(designer.TemplateLibrary);
                AddObservedNestedContext(designer.SelectedElementProperties);
                break;
        }
    }

    private void AddObservedNestedContext(INotifyPropertyChanged context)
    {
        _observedNestedContexts.Add(context);
        context.PropertyChanged += ObservedContextChanged;
    }

    private void ObservedContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncShellChromeFromSelection();
    }

    private async Task RecordShellActionAsync(string actionLabel)
    {
        try
        {
            var originLabel = SelectedModule?.Label ?? "Shell";
            var routedModule = ResolveTargetModule(actionLabel);
            if (routedModule is not null && !ReferenceEquals(routedModule, SelectedModule))
            {
                SelectedModule = routedModule;
            }

            var targetLabel = SelectedModule?.Label ?? originLabel;
            var routeVerb = string.Equals(originLabel, targetLabel, StringComparison.Ordinal) ? "stayed in" : "routed to";
            var focusStatus = ApplyActionFocus(actionLabel, SelectedModule?.Workspace);
            var focusPrefix = string.IsNullOrWhiteSpace(focusStatus) ? string.Empty : $"{focusStatus} ";
            var actionResult = await ExecuteShellActionAsync(actionLabel, targetLabel);
            var suffix = string.IsNullOrWhiteSpace(actionResult) ? BuildActionOutcome(actionLabel, targetLabel) : actionResult;
            LastActionStatus = $"{actionLabel}: {routeVerb} {targetLabel}. {focusPrefix}{suffix}";
        }
        catch (Exception ex)
        {
            LastActionStatus = $"{actionLabel}: desktop-shell companion action failed. {ex.Message}";
        }

        RefreshStatusStrip(SelectedModule);
    }

    private async void ShellActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is ShellActionModel action && !string.IsNullOrWhiteSpace(action.Label))
        {
            await RecordShellActionAsync(action.Label);
        }
    }

    private async void RibbonActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.Tag is ShellActionModel action &&
            !string.IsNullOrWhiteSpace(action.Label))
        {
            await RecordShellActionAsync(action.Label);
        }
    }

    private async void QuickAccessButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Fluent.Button button)
        {
            if (button.Tag is ShellActionModel action && !string.IsNullOrWhiteSpace(action.Label))
            {
                await RecordShellActionAsync(action.Label);
                return;
            }

            await RecordShellActionAsync(button.Header?.ToString() ?? "Quick access action");
        }
    }

    private async Task<string?> ExecuteShellActionAsync(string actionLabel, string targetLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.RefreshSnapshotActions))
        {
            var refreshed = await RefreshLiveStateAsync($"Refreshed live desktop-shell state for {targetLabel}.");
            return refreshed
                ? BuildRefreshActionResult(actionLabel)
                : BuildRefreshFailureResult(actionLabel);
        }

        if (MatchesAction(actionLabel, ShellActions.ApproveActions))
        {
            return await ReviewFocusedProofAsync(approve: true);
        }

        if (MatchesAction(actionLabel, ShellActions.RejectActions))
        {
            return await ReviewFocusedProofAsync(approve: false);
        }

        if (MatchesAction(actionLabel, ShellActions.ExportAudit))
        {
            var export = await _companionClient.ExportAuditLedgerAsync(new AuditExportRequestDto());
            var refreshed = await RefreshLiveStateAsync("Refreshed live audit state after export.");
            var refreshClause = refreshed
                ? "The refreshed audit lane now reflects the export."
                : "The export succeeded, but the follow-up companion refresh failed; audit views may still be stale.";
            return $"Export returned {export.DispatchCount} dispatch entr{(export.DispatchCount == 1 ? "y" : "ies")} and {export.ProofCount} proof entr{(export.ProofCount == 1 ? "y" : "ies")} from desktop-shell. {refreshClause}";
        }

        return null;
    }

    private static string BuildRefreshActionResult(string actionLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.TemplateCatalogRefreshActions))
        {
            return "Template library, overlay winner, and catalog governance state were refreshed from desktop-shell.";
        }

        if (MatchesAction(actionLabel, ShellActions.PreviewRefreshActions))
        {
            return "Preview output and authority state were refreshed from desktop-shell.";
        }

        if (MatchesAction(actionLabel, ShellActions.PrintSubjectRefreshActions))
        {
            return "Proof subjects, dispatch route, and bridge state were refreshed from desktop-shell.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditRefreshActions))
        {
            return "Audit search state and proof-review visibility were refreshed from desktop-shell.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditBundleRefreshActions))
        {
            return "Audit backup bundle inventory and restore guardrails were refreshed from desktop-shell.";
        }

        if (MatchesAction(actionLabel, ShellActions.BatchSnapshotRefreshActions))
        {
            return "Shared batch snapshot rows and submit state were refreshed from desktop-shell; import, retry, and submit remain disabled in WPF.";
        }

        return "Companion-backed bridge, catalog, preview, and audit state were refreshed.";
    }

    private static string BuildRefreshFailureResult(string actionLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.TemplateCatalogRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; template library and catalog state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.PreviewRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; preview state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.PrintSubjectRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; proof subject and route state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; audit search state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditBundleRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; bundle inventory may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.BatchSnapshotRefreshActions))
        {
            return "desktop-shell companion refresh did not complete; shared batch snapshot rows may be stale.";
        }

        return "desktop-shell companion refresh did not complete; the shell stayed in its current state.";
    }

    private async Task<string> ReviewFocusedProofAsync(bool approve)
    {
        var proofJobId = GetFocusedProofJobId();
        if (string.IsNullOrWhiteSpace(proofJobId))
        {
            var refreshed = await RefreshLiveStateAsync("Refreshed live state while looking for a pending proof.");
            proofJobId = GetFocusedProofJobId();
            if (!refreshed && string.IsNullOrWhiteSpace(proofJobId))
            {
                return "desktop-shell companion refresh failed while looking for a pending proof; the shell stayed in its current state.";
            }
        }

        if (string.IsNullOrWhiteSpace(proofJobId))
        {
            return "No pending proof is currently selected in the companion-backed lanes.";
        }

        var actorDisplayName = Environment.UserName;
        var request = new ProofReviewRequestDto
        {
            ProofJobId = proofJobId,
            ActorUserId = string.IsNullOrWhiteSpace(actorDisplayName) ? "windows-shell" : actorDisplayName,
            ActorDisplayName = string.IsNullOrWhiteSpace(actorDisplayName) ? "windows-shell" : actorDisplayName,
            DecidedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Notes = approve
                ? "Approved from the Windows-native shell companion path."
                : "Rejected from the Windows-native shell companion path.",
        };

        if (approve)
        {
            await _companionClient.ApproveProofAsync(request);
        }
        else
        {
            await _companionClient.RejectProofAsync(request);
        }

        var reviewSummary = $"{(approve ? "Approved" : "Rejected")} {proofJobId} through the desktop-shell companion.";
        var refreshedAfterReview = await RefreshLiveStateAsync(reviewSummary);
        return refreshedAfterReview
            ? reviewSummary
            : $"{reviewSummary} The follow-up companion refresh failed, so the visible proof state may still be stale.";
    }

    private string? GetFocusedProofJobId()
    {
        return SelectedModule?.Workspace switch
        {
            PrintConsoleWorkspaceModel printConsole when printConsole.SelectedProof is not null => printConsole.SelectedProof.Label,
            HistoryWorkspaceModel history when history.SelectedPendingProof is not null => history.SelectedPendingProof.Label,
            _ => null,
        };
    }

    private async Task<bool> RefreshLiveStateAsync(string successMessage)
    {
        var preferredModuleLabel = SelectedModule?.Label;
        var preferredFocus = DescribeWorkspaceFocus(CurrentWorkspace);

        try
        {
            var snapshot = await _companionClient.LoadSnapshotAsync();
            ApplyLiveSnapshot(snapshot, preferredModuleLabel, preferredFocus);
            _hasLiveSnapshotLoaded = true;
            SetShellRuntimeState(
                LiveRuntimeLabel,
                "desktop-shell companion is supplying live bridge, catalog, governance, preview, proof, audit, and shared batch state.");
            LastActionStatus = successMessage;
            RefreshStatusStrip(SelectedModule);
            return true;
        }
        catch (Exception ex)
        {
            SetShellRuntimeState(
                _hasLiveSnapshotLoaded ? StaleLiveRuntimeLabel : SeededFallbackRuntimeLabel,
                _hasLiveSnapshotLoaded
                    ? "desktop-shell companion refresh failed; the last live snapshot remains visible."
                    : "desktop-shell companion refresh failed; the seeded explanatory shell remains visible.");
            LastActionStatus = $"desktop-shell companion refresh failed: {ex.Message}";
            RefreshStatusStrip(SelectedModule);
            return false;
        }
    }

    private void ApplyLiveSnapshot(
        ShellWorkspaceSnapshot snapshot,
        string? preferredModuleLabel,
        string? preferredFocus)
    {
        DetachWorkspaceObservers();
        Modules.Clear();
        WorkspaceLiveFactory.LoadModules(Modules, snapshot);
        SelectedModule = Modules.FirstOrDefault((module) => string.Equals(module.Label, preferredModuleLabel, StringComparison.Ordinal))
            ?? Modules.FirstOrDefault((module) => string.Equals(module.Label, "Home", StringComparison.Ordinal))
            ?? Modules.FirstOrDefault();
        RestoreWorkspaceFocus(SelectedModule?.Workspace, preferredFocus);
    }

    private static void RestoreWorkspaceFocus(WorkspaceModel? workspace, string? preferredFocus)
    {
        if (workspace is null || string.IsNullOrWhiteSpace(preferredFocus))
        {
            return;
        }

        switch (workspace)
        {
            case HomeWorkspaceModel home:
                home.FocusTemplateEntry(preferredFocus);
                break;
            case DesignerWorkspaceModel designer:
                if (!designer.FocusTemplateEntry(preferredFocus))
                {
                    designer.FocusCanvasElement(preferredFocus);
                }

                break;
            case PrintConsoleWorkspaceModel printConsole:
                if (!printConsole.ProofQueue.Any((item) => item.Label == preferredFocus))
                {
                    var job = printConsole.JobRows.FirstOrDefault((item) => item.Subject == preferredFocus);
                    if (job is not null)
                    {
                        printConsole.SelectedJob = job;
                    }
                }
                else
                {
                    printConsole.SelectedProof = printConsole.ProofQueue.First((item) => item.Label == preferredFocus);
                }

                break;
            case BatchJobsWorkspaceModel batchJobs:
                var session = batchJobs.ImportSessions.FirstOrDefault((item) => item.Label == preferredFocus);
                if (session is not null)
                {
                    batchJobs.SelectedImportSession = session;
                    break;
                }

                var batch = batchJobs.BatchRows.FirstOrDefault((item) => item.BatchId == preferredFocus);
                if (batch is not null)
                {
                    batchJobs.SelectedBatch = batch;
                }

                break;
            case HistoryWorkspaceModel history:
                var proof = history.PendingProofs.FirstOrDefault((item) => item.Label == preferredFocus);
                if (proof is not null)
                {
                    history.SelectedPendingProof = proof;
                    break;
                }

                var bundle = history.BundleRows.FirstOrDefault((item) => item.Label == preferredFocus);
                if (bundle is not null)
                {
                    history.SelectedBundle = bundle;
                    break;
                }

                var auditRow = history.AuditRows.FirstOrDefault((item) => item.Subject == preferredFocus);
                if (auditRow is not null)
                {
                    history.SelectedAuditRow = auditRow;
                }

                break;
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

    private ShellActionModel ResolveQuickAccessAction(string fallbackLabel, string unavailableSummary, params string[] candidates)
    {
        var match = FindActionInSelectedModule(candidates);
        return match ?? ShellActionModel.Disabled(fallbackLabel, unavailableSummary);
    }

    private ModuleModel? ResolveTargetModule(string actionLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.ReviewActions))
        {
            return SelectedModule?.Label switch
            {
                "Print Console" => SelectedModule,
                "History" => SelectedModule,
                _ => FindModule("History"),
            };
        }

        if (MatchesAction(actionLabel, ShellActions.HomeRouteActions))
        {
            return FindModule("Home");
        }

        if (MatchesAction(actionLabel, ShellActions.DesignerRouteActions))
        {
            return FindModule("Designer");
        }

        if (MatchesAction(actionLabel, ShellActions.PrintConsoleRouteActions))
        {
            return FindModule("Print Console");
        }

        if (MatchesAction(actionLabel, ShellActions.BatchRouteActions))
        {
            return FindModule("Batch Jobs");
        }

        if (MatchesAction(actionLabel, ShellActions.HistoryRouteActions))
        {
            return FindModule("History");
        }

        return null;
    }

    private ModuleModel? FindModule(string label)
    {
        return Modules.FirstOrDefault((module) => string.Equals(module.Label, label, StringComparison.Ordinal));
    }

    private ShellActionModel? FindActionInSelectedModule(params string[] candidates)
    {
        var module = SelectedModule;
        if (module is null)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            var headerAction = module.HeaderActions.FirstOrDefault((action) => string.Equals(action.Label, candidate, StringComparison.OrdinalIgnoreCase));
            if (headerAction is not null)
            {
                return headerAction;
            }

            foreach (var group in module.RibbonGroups)
            {
                var ribbonAction = group.Commands.FirstOrDefault((action) => string.Equals(action.Label, candidate, StringComparison.OrdinalIgnoreCase));
                if (ribbonAction is not null)
                {
                    return ribbonAction;
                }
            }
        }

        return null;
    }

    private static IEnumerable<ContextBadgeModel> BuildDynamicContextBadges(WorkspaceModel? workspace)
    {
        if (workspace is HomeWorkspaceModel home && home.TemplateLibrary.SelectedTemplate is { } homeTemplate)
        {
            yield return new ContextBadgeModel("Selected", homeTemplate.Name, Brushes.SteelBlue);
            yield return new ContextBadgeModel("State", homeTemplate.State, ResolveStateAccent(homeTemplate.State));
            yield return new ContextBadgeModel("Source", homeTemplate.Source, Brushes.DarkGoldenrod);
            yield break;
        }

        if (workspace is DesignerWorkspaceModel designer)
        {
            var objectName = DescribeDesignerSelection(designer);
            if (!string.IsNullOrWhiteSpace(objectName))
            {
                yield return new ContextBadgeModel("Object", objectName, Brushes.SteelBlue);
            }

            if (!string.IsNullOrWhiteSpace(designer.SelectedElementProperties.Binding))
            {
                yield return new ContextBadgeModel("Binding", designer.SelectedElementProperties.Binding, Brushes.ForestGreen);
            }

            yield break;
        }

        if (workspace is PrintConsoleWorkspaceModel printConsole)
        {
            if (printConsole.SelectedProof is { } proof)
            {
                yield return new ContextBadgeModel("Focus", proof.Label, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", proof.Badge, ResolveStateAccent(proof.Badge));
                yield return new ContextBadgeModel("Route", proof.Route, ResolveRouteAccent(proof.Route));
                yield break;
            }

            if (printConsole.SelectedJob is { } job)
            {
                yield return new ContextBadgeModel("Focus", job.Subject, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", job.Status, ResolveStateAccent(job.Status));
                yield return new ContextBadgeModel("Route", job.Route, ResolveRouteAccent(job.Route));
                yield break;
            }

            yield break;
        }

        if (workspace is BatchJobsWorkspaceModel batchJobs)
        {
            if (batchJobs.SelectedImportSession is { } session)
            {
                yield return new ContextBadgeModel("Focus", session.Label, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", session.Badge, ResolveStateAccent(session.Badge));
                yield return new ContextBadgeModel("Route", session.Route, ResolveRouteAccent(session.Route));
                yield break;
            }

            if (batchJobs.SelectedBatch is { } batch)
            {
                yield return new ContextBadgeModel("Focus", batch.BatchId, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", batch.Status, ResolveStateAccent(batch.Status));
                yield return new ContextBadgeModel("Route", batch.SubmitRoute, ResolveRouteAccent(batch.SubmitRoute));
                yield break;
            }

            yield break;
        }

        if (workspace is HistoryWorkspaceModel history)
        {
            if (history.SelectedPendingProof is { } pendingProof)
            {
                yield return new ContextBadgeModel("Focus", pendingProof.Label, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", pendingProof.Badge, ResolveStateAccent(pendingProof.Badge));
                yield return new ContextBadgeModel("Artifact", pendingProof.Route, ResolveRouteAccent(pendingProof.Route));
                yield break;
            }

            if (history.SelectedBundle is { } bundle)
            {
                yield return new ContextBadgeModel("Focus", bundle.Label, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", bundle.Badge, ResolveStateAccent(bundle.Badge));
                yield return new ContextBadgeModel("Validation", bundle.Route, ResolveRouteAccent(bundle.Route));
                yield break;
            }

            if (history.SelectedAuditRow is { } auditRow)
            {
                yield return new ContextBadgeModel("Focus", auditRow.Subject, Brushes.SteelBlue);
                yield return new ContextBadgeModel("State", auditRow.Status, ResolveStateAccent(auditRow.Status));
                yield return new ContextBadgeModel("Lane", auditRow.Lane, Brushes.DarkGoldenrod);
            }
        }
    }

    private static IEnumerable<StatusStripItemModel> BuildDynamicStatusStripItems(WorkspaceModel? workspace)
    {
        if (workspace is HomeWorkspaceModel home && home.TemplateLibrary.SelectedTemplate is { } homeTemplate)
        {
            yield return new StatusStripItemModel("Template", homeTemplate.Name);
            yield return new StatusStripItemModel("State", homeTemplate.State);
            yield break;
        }

        if (workspace is DesignerWorkspaceModel designer)
        {
            yield return new StatusStripItemModel("Object", DescribeDesignerSelection(designer));
            if (!string.IsNullOrWhiteSpace(designer.SelectedElementProperties.DispatchAuthority))
            {
                yield return new StatusStripItemModel("Authority", designer.SelectedElementProperties.DispatchAuthority);
            }

            yield break;
        }

        if (workspace is PrintConsoleWorkspaceModel printConsole)
        {
            if (printConsole.SelectedProof is { } proof)
            {
                yield return new StatusStripItemModel("Focus", proof.Label);
                yield return new StatusStripItemModel("State", proof.Badge);
                yield return new StatusStripItemModel("Route", proof.Route);
                yield break;
            }

            if (printConsole.SelectedJob is { } job)
            {
                yield return new StatusStripItemModel("Focus", job.Subject);
                yield return new StatusStripItemModel("State", job.Status);
                yield return new StatusStripItemModel("Route", job.Route);
                yield break;
            }

            yield break;
        }

        if (workspace is BatchJobsWorkspaceModel batchJobs)
        {
            if (batchJobs.SelectedImportSession is { } session)
            {
                yield return new StatusStripItemModel("Focus", session.Label);
                yield return new StatusStripItemModel("State", session.Badge);
                yield return new StatusStripItemModel("Route", session.Route);
                yield break;
            }

            if (batchJobs.SelectedBatch is { } batch)
            {
                yield return new StatusStripItemModel("Focus", batch.BatchId);
                yield return new StatusStripItemModel("State", batch.Status);
                yield return new StatusStripItemModel("Route", batch.SubmitRoute);
                yield break;
            }

            yield break;
        }

        if (workspace is HistoryWorkspaceModel history)
        {
            if (history.SelectedPendingProof is { } pendingProof)
            {
                yield return new StatusStripItemModel("Focus", pendingProof.Label);
                yield return new StatusStripItemModel("State", pendingProof.Badge);
                yield return new StatusStripItemModel("Artifact", pendingProof.Route);
                yield break;
            }

            if (history.SelectedBundle is { } bundle)
            {
                yield return new StatusStripItemModel("Focus", bundle.Label);
                yield return new StatusStripItemModel("State", bundle.Badge);
                yield return new StatusStripItemModel("Validation", bundle.Route);
                yield break;
            }

            if (history.SelectedAuditRow is { } auditRow)
            {
                yield return new StatusStripItemModel("Focus", auditRow.Subject);
                yield return new StatusStripItemModel("State", auditRow.Status);
                yield return new StatusStripItemModel("Lane", auditRow.Lane);
            }
        }
    }

    private void SetShellRuntimeState(string label, string summary)
    {
        _shellRuntimeLabel = label;
        _shellRuntimeSummary = summary;
        RefreshContextBadges(SelectedModule);
        RefreshStatusStrip(SelectedModule);
        OnPropertyChanged(nameof(CurrentWorkspaceLead));
    }

    private string BuildShellRuntimeStatusValue()
    {
        return _shellRuntimeLabel switch
        {
            LiveRuntimeLabel => "desktop-shell live",
            SeededFallbackRuntimeLabel => "seeded after refresh failure",
            StaleLiveRuntimeLabel => "showing last live snapshot",
            _ => "seeded until first refresh",
        };
    }

    private static string? DescribeWorkspaceFocus(WorkspaceModel? workspace)
    {
        return workspace switch
        {
            HomeWorkspaceModel home => home.TemplateLibrary.SelectedTemplate?.Name,
            DesignerWorkspaceModel designer => DescribeDesignerSelection(designer),
            PrintConsoleWorkspaceModel printConsole when printConsole.SelectedProof is not null => printConsole.SelectedProof.Label,
            PrintConsoleWorkspaceModel printConsole when printConsole.SelectedJob is not null => printConsole.SelectedJob.Subject,
            BatchJobsWorkspaceModel batchJobs when batchJobs.SelectedImportSession is not null => batchJobs.SelectedImportSession.Label,
            BatchJobsWorkspaceModel batchJobs when batchJobs.SelectedBatch is not null => batchJobs.SelectedBatch.BatchId,
            HistoryWorkspaceModel history when history.SelectedPendingProof is not null => history.SelectedPendingProof.Label,
            HistoryWorkspaceModel history when history.SelectedBundle is not null => history.SelectedBundle.Label,
            HistoryWorkspaceModel history when history.SelectedAuditRow is not null => history.SelectedAuditRow.Subject,
            _ => null,
        };
    }

    private static string? BuildDynamicWorkspaceLead(WorkspaceModel? workspace)
    {
        return workspace switch
        {
            HomeWorkspaceModel home when home.TemplateLibrary.SelectedTemplate is { } template
                => $"{template.Name} is currently {template.State} from the {template.Source} catalog path. Dispatch route: {template.Dispatch}.",
            DesignerWorkspaceModel designer => BuildDesignerLead(designer),
            PrintConsoleWorkspaceModel printConsole when printConsole.SelectedProof is { } proof
                => $"{proof.Label} is {proof.Badge}. {proof.Blocker} Next action: {proof.NextAction}",
            PrintConsoleWorkspaceModel printConsole when printConsole.SelectedJob is { } job
                => $"{job.Subject} is {job.Status} on {job.Route}. {job.Blocker} Next action: {job.NextAction}",
            BatchJobsWorkspaceModel batchJobs when batchJobs.SelectedImportSession is { } session
                => $"{session.Label} is focused with state {session.Badge}. {session.Blocker} Next action: {session.NextAction}",
            BatchJobsWorkspaceModel batchJobs when batchJobs.SelectedBatch is { } batch
                => $"{batch.BatchId} is {batch.Status} for {batch.Template}. {batch.Blocker} Retry rule: {batch.RetryRule}",
            HistoryWorkspaceModel history when history.SelectedPendingProof is { } pendingProof
                => $"{pendingProof.Label} is {pendingProof.Badge}. {pendingProof.Blocker} Next action: {pendingProof.NextAction}",
            HistoryWorkspaceModel history when history.SelectedBundle is { } bundle
                => $"{bundle.Label} is {bundle.Badge}. {bundle.Blocker} Next action: {bundle.NextAction}",
            HistoryWorkspaceModel history when history.SelectedAuditRow is { } auditRow
                => $"{auditRow.Subject} is {auditRow.Status} in the {auditRow.Lane} lane. Action: {auditRow.ActionRequired}",
            _ => null,
        };
    }

    private static string? ApplyActionFocus(string actionLabel, WorkspaceModel? workspace)
    {
        return workspace switch
        {
            HomeWorkspaceModel home => ApplyHomeFocus(actionLabel, home),
            DesignerWorkspaceModel designer => ApplyDesignerFocus(actionLabel, designer),
            PrintConsoleWorkspaceModel printConsole => ApplyPrintConsoleFocus(actionLabel, printConsole),
            BatchJobsWorkspaceModel batchJobs => ApplyBatchJobsFocus(actionLabel, batchJobs),
            HistoryWorkspaceModel history => ApplyHistoryFocus(actionLabel, history),
            _ => null,
        };
    }

    private static string? ApplyHomeFocus(string actionLabel, HomeWorkspaceModel workspace)
    {
        if (MatchesAction(actionLabel, ShellActions.HomeDefaultFocusActions))
        {
            if (workspace.FocusTemplateByState("default"))
            {
                return $"Focused {workspace.TemplateLibrary.SelectionHeading} so the winning default stays visible from Home.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.HomeFallbackFocusActions))
        {
            if (workspace.FocusTemplateByState("fallback"))
            {
                return $"Focused {workspace.TemplateLibrary.SelectionHeading} so rollback posture remains visible during release review.";
            }
        }

        return $"This action is not yet implemented in the native shell: {actionLabel}.";
    }

    private static string? ApplyDesignerFocus(string actionLabel, DesignerWorkspaceModel workspace)
    {
        if (MatchesAction(actionLabel, ShellActions.DesignerTemplateFocusActions))
        {
            if (workspace.FocusTemplateByState("default"))
            {
                return $"Focused {workspace.TemplateLibrary.SelectionHeading} so overlay authority and dispatch-safe state stay in view.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.DesignerDraftFocusActions))
        {
            if (workspace.FocusTemplateByState("draft"))
            {
                return $"Focused {workspace.TemplateLibrary.SelectionHeading} to surface the draft-only save boundary.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.DesignerPreviewFocusActions))
        {
            if (workspace.FocusCanvasElement("BARCODE"))
            {
                return $"Focused {DescribeDesignerSelection(workspace)} so proof-safe barcode output remains visible.";
            }
        }

        return null;
    }

    private static string? ApplyPrintConsoleFocus(string actionLabel, PrintConsoleWorkspaceModel workspace)
    {
        if (MatchesAction(actionLabel, ShellActions.PrintProofFocusActions))
        {
            if (workspace.FocusPendingProof())
            {
                return $"Focused {workspace.SelectionHeading} so approval state and blocker context are explicit.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.PrintHoldFocusActions))
        {
            if (workspace.FocusHeldJob())
            {
                return $"Focused {workspace.SelectionHeading} so the current subject blocker stays visible before any desktop-shell handoff.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.PrintSubjectFocusActions))
        {
            if (workspace.FocusReadyJob())
            {
                return $"Focused {workspace.SelectionHeading} so the current proof / dispatch subject stays visible.";
            }
        }

        return null;
    }

    private static string? ApplyBatchJobsFocus(string actionLabel, BatchJobsWorkspaceModel workspace)
    {
        if (MatchesAction(actionLabel, ShellActions.BatchImportFocusActions))
        {
            if (workspace.FocusImportSessionWithWarnings() || workspace.FocusFirstImportSession())
            {
                return $"Focused {workspace.SelectionHeading} so workbook assumptions and warnings stay visible.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.BatchRetryFocusActions))
        {
            if (workspace.FocusFailedBatch())
            {
                return $"Focused {workspace.SelectionHeading} so retry eligibility stays visible before action.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.BatchReadyFocusActions))
        {
            if (workspace.FocusReadyBatch())
            {
                return $"Focused {workspace.SelectionHeading} so ready-row counts and blockers are visible together.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.BatchFreezeFocusActions))
        {
            if (workspace.FocusSubmittedBatch())
            {
                return $"Focused {workspace.SelectionHeading} so submit-time mutation locks are explicit.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.BatchDraftFocusActions))
        {
            if (workspace.FocusDraftBatch())
            {
                return $"Focused {workspace.SelectionHeading} so the unsaved-template blocker is visible.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.BatchWarningFocusActions))
        {
            if (workspace.FocusImportSessionWithWarnings())
            {
                return $"Focused {workspace.SelectionHeading} so risky JAN rows are visible before submit.";
            }
        }

        return null;
    }

    private static string? ApplyHistoryFocus(string actionLabel, HistoryWorkspaceModel workspace)
    {
        if (MatchesAction(actionLabel, ShellActions.ReviewActions))
        {
            if (workspace.FocusPendingProof())
            {
                return $"Focused {workspace.SelectionHeading} so review state and recovery impact remain visible.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.HistoryAuditFocusActions))
        {
            if (workspace.FocusApprovedAudit())
            {
                return $"Focused {workspace.SelectionHeading} so the current audit reference stays in view.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.HistoryRetentionFocusActions))
        {
            if (workspace.FocusRetentionAudit())
            {
                return $"Focused {workspace.SelectionHeading} so retention impact is visible before apply.";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.HistoryRestoreFocusActions))
        {
            if (workspace.FocusLatestBundle())
            {
                return $"Focused {workspace.SelectionHeading} so restore safety and validation stay visible.";
            }
        }

        return null;
    }

    private static Brush ResolveStateAccent(string state)
    {
        if (state.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("stable", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("default", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.ForestGreen;
        }

        if (state.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("held", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("latest", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("fallback", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.DarkGoldenrod;
        }

        if (state.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Firebrick;
        }

        return Brushes.SteelBlue;
    }

    private static Brush ResolveRouteAccent(string route)
    {
        if (route.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("pending", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Firebrick;
        }

        if (route.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("proof", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("validated", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.SteelBlue;
        }

        return Brushes.DarkGoldenrod;
    }

    private static Brush ResolveShellRuntimeAccent(string runtimeLabel)
    {
        return runtimeLabel switch
        {
            LiveRuntimeLabel => Brushes.ForestGreen,
            SeededFallbackRuntimeLabel => Brushes.Firebrick,
            StaleLiveRuntimeLabel => Brushes.DarkGoldenrod,
            _ => Brushes.SteelBlue,
        };
    }

    private static string BuildDesignerLead(DesignerWorkspaceModel designer)
    {
        var objectName = DescribeDesignerSelection(designer);
        var binding = string.IsNullOrWhiteSpace(designer.SelectedElementProperties.Binding) ? "n/a" : designer.SelectedElementProperties.Binding;
        var authority = string.IsNullOrWhiteSpace(designer.SelectedElementProperties.DispatchAuthority)
            ? "desktop-shell"
            : designer.SelectedElementProperties.DispatchAuthority;

        return $"{objectName} is focused with binding {binding}. Proof and print authority remain {authority}.";
    }

    private static string DescribeDesignerSelection(DesignerWorkspaceModel workspace)
    {
        if (!string.IsNullOrWhiteSpace(workspace.SelectedElementProperties.Name))
        {
            return workspace.SelectedElementProperties.Name;
        }

        return workspace.SelectedCanvasElement?.Caption ?? "the current canvas object";
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
            "Designer" when string.Equals(actionLabel, ShellActions.SaveToCatalog, StringComparison.OrdinalIgnoreCase)
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
