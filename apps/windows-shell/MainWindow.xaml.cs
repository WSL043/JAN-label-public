using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AdonisUI.Controls;
using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

public partial class MainWindow : AdonisWindow, INotifyPropertyChanged
{
    private const string DefaultActionStatus = "Connecting to local label services and loading the current workstation state.";
    private const string SeededRuntimeLabel = "Starting";
    private const string LiveRuntimeLabel = "Connected";
    private const string SeededFallbackRuntimeLabel = "Limited mode";
    private const string StaleLiveRuntimeLabel = "Stale data";

    private readonly DesktopShellCompanionClient _companionClient = new();
    private readonly List<INotifyPropertyChanged> _observedNestedContexts = new();
    private INotifyPropertyChanged? _observedWorkspace;
    private ModuleModel? _selectedModule;
    private string _lastActionStatus = DefaultActionStatus;
    private string _shellRuntimeLabel = SeededRuntimeLabel;
    private string _shellRuntimeSummary = "The shell is starting and waiting for the local service to return live data.";
    private bool _hasLiveSnapshotLoaded;
    private string _lastLoadedDesignerTemplateVersion = string.Empty;
    private int _designerTemplateLoadGeneration;
    private int _designerPreviewRenderGeneration;
    private CancellationTokenSource? _designerPreviewRefreshCts;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        WorkspaceFactory.SeedModules(Modules);
        SelectedModule = Modules.FirstOrDefault((module) => module.Label == "Home") ?? Modules.FirstOrDefault();
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
            _ = EnsureSelectedWorkspaceNativeStateAsync(value?.Workspace);
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
                ? $"JAN Label | {moduleLabel}"
                : $"JAN Label | {moduleLabel} | {focusLabel}";
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
        await RefreshLiveStateAsync("Loaded the latest workstation state.");
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        CancelPendingDesignerPreviewRefresh();
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
        if (SelectedModule?.Workspace is DesignerWorkspaceModel designer &&
            ReferenceEquals(sender, designer.TemplateLibrary) &&
            string.Equals(e.PropertyName, nameof(TemplateLibraryPanelModel.SelectedTemplate), StringComparison.Ordinal))
        {
            _ = EnsureDesignerTemplateLoadedAsync(designer, force: true);
        }

        if (SelectedModule?.Workspace is DesignerWorkspaceModel currentDesigner &&
            ReferenceEquals(sender, currentDesigner.SelectedElementProperties) &&
            IsDesignerPreviewRelevantProperty(e.PropertyName))
        {
            ScheduleDesignerDraftPreviewRefresh(currentDesigner);
        }

        SyncShellChromeFromSelection();
    }

    private void ScheduleDesignerDraftPreviewRefresh(DesignerWorkspaceModel workspace)
    {
        CancelPendingDesignerPreviewRefresh();
        var cts = new CancellationTokenSource();
        _designerPreviewRefreshCts = cts;
        _ = RefreshDesignerDraftPreviewDebouncedAsync(workspace, cts.Token);
    }

    private void CancelPendingDesignerPreviewRefresh()
    {
        if (_designerPreviewRefreshCts is null)
        {
            return;
        }

        _designerPreviewRefreshCts.Cancel();
        _designerPreviewRefreshCts.Dispose();
        _designerPreviewRefreshCts = null;
    }

    private async Task RefreshDesignerDraftPreviewDebouncedAsync(DesignerWorkspaceModel workspace, CancellationToken cancellationToken)
    {
        var requestedTemplateVersion = ResolveDesignerTemplateVersion(workspace) ?? "designer-draft";
        try
        {
            await Task.Delay(150, cancellationToken);
            await RefreshDesignerDraftPreviewAsync(workspace, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var activeTemplateVersion = ResolveDesignerTemplateVersion(workspace) ?? "designer-draft";
            if (string.Equals(activeTemplateVersion, requestedTemplateVersion, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDesignerPreviewFailure(workspace, requestedTemplateVersion, ex.Message);
            }
        }
    }

    private static bool IsDesignerPreviewRelevantProperty(string? propertyName)
    {
        return propertyName switch
        {
            nameof(DesignerSelectionModel.Name) => true,
            nameof(DesignerSelectionModel.Binding) => true,
            nameof(DesignerSelectionModel.Xmm) => true,
            nameof(DesignerSelectionModel.Ymm) => true,
            nameof(DesignerSelectionModel.WidthMm) => true,
            nameof(DesignerSelectionModel.HeightMm) => true,
            _ => false,
        };
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
            LastActionStatus = $"{actionLabel}: live service action failed. {ex.Message}";
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

    private void NavigateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string moduleLabel)
        {
            return;
        }

        var module = FindModule(moduleLabel);
        if (module is not null)
        {
            SelectedModule = module;
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowAboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            this,
            "JAN Label v1.0.0 foundation\n\nThis build is the Windows-only WPF workstation foundation for the v1.0.0 release path. The local .NET runtime now initializes app storage, SQLite state, and legacy import scaffolding before the shell starts.",
            "About JAN Label",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private async Task<string?> ExecuteShellActionAsync(string actionLabel, string targetLabel)
    {
        if (string.Equals(actionLabel, ShellActions.SaveToCatalog, StringComparison.OrdinalIgnoreCase))
        {
            return await SaveFocusedDesignerTemplateAsync();
        }

        if (SelectedModule?.Workspace is DesignerWorkspaceModel designer &&
            MatchesAction(actionLabel, ShellActions.PreviewRefreshActions))
        {
            CancelPendingDesignerPreviewRefresh();
            try
            {
                return await RefreshDesignerDraftPreviewAsync(
                    designer,
                    updateActionMessage: true,
                    prependPreviewMessage: true);
            }
            catch (Exception ex)
            {
                ApplyDesignerPreviewFailure(
                    designer,
                    ResolveDesignerTemplateVersion(designer) ?? "designer-draft",
                    ex.Message);
                return $"Native draft preview refresh failed: {ex.Message}";
            }
        }

        if (MatchesAction(actionLabel, ShellActions.RefreshSnapshotActions))
        {
            var refreshed = await RefreshLiveStateAsync($"Refreshed live service state for {targetLabel}.");
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

        if (string.Equals(actionLabel, ShellActions.RunProof, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateProofForFocusedJobAsync();
        }

        if (MatchesAction(actionLabel, ShellActions.ExportAudit))
        {
            var export = await GetAuditService().ExportAsync(
                new AuditExportRequest(
                    "windows-shell-audit-export",
                    new AuditQuery()));
            return $"Export wrote {export.EventCount} local audit entr{(export.EventCount == 1 ? "y" : "ies")} to {Path.GetFileName(export.ExportPath)}.";
        }

        return null;
    }

    private async Task<string> SaveFocusedDesignerTemplateAsync()
    {
        if (SelectedModule?.Workspace is not DesignerWorkspaceModel designer)
        {
            return "Designer workspace is not active, so no template save target is available.";
        }

        var document = DesignerTemplateDocumentFactory.Create(designer);
        var result = await GetTemplateCatalogService().SaveTemplateAsync(document);
        var snapshot = GetTemplateCatalogService().LoadCatalogSnapshot();
        ApplyNativeTemplateCatalogFallback(snapshot, document.TemplateVersion);
        ApplyDesignerSaveState(designer, result);
        CancelPendingDesignerPreviewRefresh();
        await RefreshDesignerDraftPreviewAsync(designer);
        return $"Saved {result.TemplateVersion} to the native local catalog at {Path.GetFileName(result.TemplatePath)}. Fresh proof review is still required before dispatch.";
    }

    private static string BuildRefreshActionResult(string actionLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.TemplateCatalogRefreshActions))
        {
            return "Template library, overlay winner, and catalog governance state were refreshed from the native packaged/local catalog.";
        }

        if (MatchesAction(actionLabel, ShellActions.PreviewRefreshActions))
        {
            return "Preview output and save-state guidance were refreshed from the live service.";
        }

        if (MatchesAction(actionLabel, ShellActions.PrintSubjectRefreshActions))
        {
            return "Proof subjects, dispatch route, and bridge state were refreshed from the live service.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditRefreshActions))
        {
            return "Audit rows and bundle inventory were refreshed into the local audit mirror, and the visible History / Print Console lanes now read back from that SQLite state.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditBundleRefreshActions))
        {
            return "Audit backup bundle inventory was refreshed into the local mirror; restore guardrails remain service-owned.";
        }

        if (MatchesAction(actionLabel, ShellActions.BatchSnapshotRefreshActions))
        {
            return "Shared batch snapshot rows and submit state were refreshed from the live service; import, retry, and submit remain disabled in WPF.";
        }

        return "Live bridge, preview, and audit state were refreshed, the local audit mirror was updated, and native catalog state was re-read.";
    }

    private static string BuildRefreshFailureResult(string actionLabel)
    {
        if (MatchesAction(actionLabel, ShellActions.TemplateCatalogRefreshActions))
        {
            return "Live service refresh did not complete; preview or audit state may be stale, but native catalog state was kept locally readable.";
        }

        if (MatchesAction(actionLabel, ShellActions.PreviewRefreshActions))
        {
            return "Live service refresh did not complete; preview state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.PrintSubjectRefreshActions))
        {
            return "Live service refresh did not complete; proof subject and route state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditRefreshActions))
        {
            return "Live service refresh did not complete; audit search state may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.AuditBundleRefreshActions))
        {
            return "Live service refresh did not complete; bundle inventory may be stale.";
        }

        if (MatchesAction(actionLabel, ShellActions.BatchSnapshotRefreshActions))
        {
            return "Live service refresh did not complete; shared batch snapshot rows may be stale.";
        }

        return "Live service refresh did not complete; the shell stayed in its current state.";
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
                return "Live service refresh failed while looking for a pending proof; the shell stayed in its current state.";
            }
        }

        if (string.IsNullOrWhiteSpace(proofJobId))
        {
            return "No pending proof is currently selected in the live review lanes.";
        }

        var actorDisplayName = Environment.UserName;
        await GetProofService().ReviewAsync(
            new ProofDecision(
                proofJobId,
                approve ? "approved" : "rejected",
                string.IsNullOrWhiteSpace(actorDisplayName) ? "windows-shell" : actorDisplayName,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                approve
                    ? "Approved from the Windows-native shell local proof path."
                    : "Rejected from the Windows-native shell local proof path."));

        var reviewSummary = $"{(approve ? "Approved" : "Rejected")} {proofJobId}.";
        var refreshedAfterReview = await RefreshLiveStateAsync(reviewSummary);
        return refreshedAfterReview
            ? reviewSummary
            : $"{reviewSummary} The follow-up refresh failed, so the visible proof state may still be stale.";
    }

    private async Task<string> CreateProofForFocusedJobAsync()
    {
        var request = BuildFocusedProofRequest();
        if (request is null)
        {
            var refreshed = await RefreshLiveStateAsync("Refreshed live state while looking for a subject to proof.");
            request = BuildFocusedProofRequest();
            if (!refreshed && request is null)
            {
                return "Live service refresh failed while looking for a subject to proof; the shell stayed in its current state.";
            }
        }

        if (request is null)
        {
            return "No Print Console subject is currently selected for native proof creation.";
        }

        var created = await GetProofService().CreateProofAsync(request);
        var summary = $"Created pending proof {created.ProofJobId} for {created.SubjectSku} on {created.TemplateVersion}.";
        var refreshedAfterCreate = await RefreshLiveStateAsync(summary);
        return refreshedAfterCreate
            ? summary
            : $"{summary} The follow-up refresh failed, so the visible proof lane may still be stale.";
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

    private ProofRequest? BuildFocusedProofRequest()
    {
        if (SelectedModule?.Workspace is not PrintConsoleWorkspaceModel printConsole)
        {
            return null;
        }

        var selectedJob = printConsole.SelectedJob;
        if (selectedJob is null)
        {
            return null;
        }

        if (!int.TryParse(selectedJob.Qty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            throw new InvalidOperationException(
                $"Selected subject '{selectedJob.Subject}' has an invalid quantity '{selectedJob.Qty}'.");
        }

        var actorDisplayName = Environment.UserName;
        var sampleJson = JsonSerializer.Serialize(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template_version"] = selectedJob.Template,
                ["brand"] = selectedJob.Brand,
                ["sku"] = selectedJob.Subject,
                ["jan"] = selectedJob.Jan,
                ["qty"] = qty.ToString(CultureInfo.InvariantCulture),
                ["job_id"] = selectedJob.JobId,
                ["job"] = selectedJob.JobId,
            });

        return new ProofRequest(
            selectedJob.Template,
            selectedJob.Subject,
            selectedJob.Brand,
            selectedJob.Jan,
            qty,
            string.IsNullOrWhiteSpace(actorDisplayName) ? "windows-shell" : actorDisplayName,
            sampleJson,
            selectedJob.Lineage,
            selectedJob.ParentJobId,
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            "Created from the Windows-native shell local proof path.");
    }

    private async Task<bool> RefreshLiveStateAsync(string successMessage)
    {
        var preferredModuleLabel = SelectedModule?.Label;
        var preferredFocus = DescribeWorkspaceFocus(CurrentWorkspace);
        var preferredTemplateVersion = DescribePreferredTemplateVersion(CurrentWorkspace);
        var nativeTemplateCatalogState = TryLoadNativeTemplateCatalogSnapshot();

        try
        {
            var snapshot = await _companionClient.LoadSnapshotAsync();
            await SyncLocalAuditAuthorityAsync(snapshot);
            await SyncLocalProofAuthorityAsync(snapshot);
            snapshot = await ApplyLocalAuditMirrorAsync(snapshot);
            ApplyLiveSnapshot(snapshot, preferredModuleLabel, preferredFocus);
            if (nativeTemplateCatalogState is not null)
            {
                ApplyNativeTemplateCatalogFallback(nativeTemplateCatalogState, preferredTemplateVersion);
            }

            _hasLiveSnapshotLoaded = true;
            SetShellRuntimeState(
                LiveRuntimeLabel,
                "Live bridge, template, preview, proof, audit, and batch data are available.");
            LastActionStatus = successMessage;
            RefreshStatusStrip(SelectedModule);
            return true;
        }
        catch (Exception ex)
        {
            if (nativeTemplateCatalogState is not null)
            {
                ApplyNativeTemplateCatalogFallback(nativeTemplateCatalogState, preferredTemplateVersion);
            }

            SetShellRuntimeState(
                _hasLiveSnapshotLoaded ? StaleLiveRuntimeLabel : SeededFallbackRuntimeLabel,
                _hasLiveSnapshotLoaded
                    ? "The latest refresh failed, so the last live snapshot is still on screen."
                    : "The live service is unavailable, so the shell is showing limited local state.");
            LastActionStatus = $"Refresh failed: {ex.Message}";
            RefreshStatusStrip(SelectedModule);
            return false;
        }
    }

    private TemplateCatalogSnapshot? TryLoadNativeTemplateCatalogSnapshot()
    {
        try
        {
            return GetTemplateCatalogService().LoadCatalogSnapshot();
        }
        catch
        {
            return null;
        }
    }

    private void ApplyNativeTemplateCatalogFallback(
        TemplateCatalogSnapshot state,
        string? preferredTemplateVersion = null)
    {
        foreach (var module in Modules)
        {
            switch (module.Workspace)
            {
                case HomeWorkspaceModel home:
                    ApplyNativeTemplatePanel(home.TemplateLibrary, state, "native packaged manifest + local overlay", preferredTemplateVersion);
                    break;
                case DesignerWorkspaceModel designer:
                    ApplyNativeTemplatePanel(designer.TemplateLibrary, state, "native catalog snapshot", preferredTemplateVersion);
                    break;
            }
        }

        SyncShellChromeFromSelection();
    }

    private static void ApplyNativeTemplatePanel(
        TemplateLibraryPanelModel panel,
        TemplateCatalogSnapshot state,
        string headerDetail,
        string? preferredTemplateVersion = null)
    {
        var entries = TemplateCatalogPresentation.BuildEntries(state);
        var resolvedPreferredTemplateVersion = string.IsNullOrWhiteSpace(preferredTemplateVersion)
            ? (string.IsNullOrWhiteSpace(state.EffectiveDefaultTemplateVersion)
                ? state.DefaultTemplateVersion
                : state.EffectiveDefaultTemplateVersion)
            : preferredTemplateVersion;
        var defaultTemplateVersion = string.IsNullOrWhiteSpace(state.EffectiveDefaultTemplateVersion)
            ? state.DefaultTemplateVersion
            : state.EffectiveDefaultTemplateVersion;
        var dispatchSafeCount = entries.Count((entry) => !entry.Dispatch.Contains("blocked", StringComparison.OrdinalIgnoreCase));
        var draftCount = entries.Count((entry) => string.Equals(entry.State, "draft", StringComparison.OrdinalIgnoreCase));
        var fallbackCount = entries.Count((entry) => string.Equals(entry.State, "fallback", StringComparison.OrdinalIgnoreCase));

        TemplateCatalogPresentation.ConfigurePanel(
            panel,
            entries,
            resolvedPreferredTemplateVersion,
            headerDetail,
            $"{dispatchSafeCount} dispatch-safe / {draftCount} draft / {fallbackCount} rollback");
        if (!string.IsNullOrWhiteSpace(preferredTemplateVersion))
        {
            panel.TrySelectTemplate(preferredTemplateVersion);
            return;
        }

        panel.TrySelectTemplate(defaultTemplateVersion);
    }

    private void ApplyLiveSnapshot(
        ShellWorkspaceSnapshot snapshot,
        string? preferredModuleLabel,
        string? preferredFocus)
    {
        DetachWorkspaceObservers();
        _lastLoadedDesignerTemplateVersion = string.Empty;
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

    private ITemplateCatalogService GetTemplateCatalogService()
    {
        return ((App)Application.Current).PlatformContext.TemplateCatalogService;
    }

    private IAuditService GetAuditService()
    {
        return ((App)Application.Current).PlatformContext.AuditService;
    }

    private IProofService GetProofService()
    {
        return ((App)Application.Current).PlatformContext.ProofService;
    }

    private IRenderService GetRenderService()
    {
        return ((App)Application.Current).PlatformContext.RenderService;
    }

    private async Task SyncLocalProofAuthorityAsync(ShellWorkspaceSnapshot snapshot)
    {
        var authorityRecords = snapshot.AuditSearch.Entries
            .Where(static (entry) => entry.Proof is not null)
            .Select(MapAuthorityProofRecord)
            .ToArray();
        if (authorityRecords.Length == 0)
        {
            return;
        }

        var proofService = GetProofService();
        await proofService.SyncFromAuthorityAsync(authorityRecords);
    }

    private async Task SyncLocalAuditAuthorityAsync(ShellWorkspaceSnapshot snapshot)
    {
        var dispatches = snapshot.AuditSearch.Entries
            .Select(MapAuthorityDispatchRecord)
            .ToArray();
        var bundles = snapshot.AuditBackupBundles
            .Select(MapAuthorityBundleRecord)
            .ToArray();
        await GetAuditService().SyncFromAuthorityAsync(dispatches, bundles);
    }

    private async Task<ShellWorkspaceSnapshot> ApplyLocalAuditMirrorAsync(ShellWorkspaceSnapshot snapshot)
    {
        var auditService = GetAuditService();
        var entries = await auditService.LoadEntriesAsync(new AuditQuery(Limit: Math.Max(snapshot.AuditSearch.Entries.Count, 200)));
        var bundles = await auditService.LoadBundlesAsync();

        return new ShellWorkspaceSnapshot(
            snapshot.BridgeStatus,
            snapshot.TemplateCatalog,
            snapshot.TemplateGovernance,
            new AuditSearchResultDto
            {
                Entries = entries.Select(MapLocalAuditMirrorEntry).ToList(),
            },
            bundles.Select(MapLocalBundleRecord).ToList(),
            snapshot.BatchQueueSnapshot,
            snapshot.PreviewTemplateVersion,
            snapshot.Preview,
            snapshot.PreviewError,
            snapshot.PreviewStatus,
            snapshot.PreviewSource,
            snapshot.PreviewMessage);
    }

    private static ProofLedgerRecord MapAuthorityProofRecord(AuditSearchEntryDto entry)
    {
        var proof = entry.Proof!;
        return new ProofLedgerRecord(
            proof.ProofJobId,
            proof.JobLineageId,
            proof.Status,
            proof.ArtifactPath,
            entry.Dispatch.MatchSubject.Sku,
            entry.Dispatch.TemplateVersion,
            proof.RequestedBy.DisplayName,
            proof.RequestedAt,
            proof.Notes,
            proof.Decision is null
                ? null
                : new ProofReviewSnapshot(
                    proof.Decision.Status,
                    proof.Decision.Actor.DisplayName,
                    proof.Decision.OccurredAt,
                    proof.Decision.Notes));
    }

    private static AuditDispatchRecord MapAuthorityDispatchRecord(AuditSearchEntryDto entry)
    {
        return new AuditDispatchRecord(
            entry.Dispatch.Audit.JobId,
            entry.Dispatch.Audit.JobLineageId,
            entry.Dispatch.Mode,
            entry.Dispatch.Audit.Event,
            entry.Dispatch.Audit.OccurredAt,
            entry.Dispatch.Audit.Actor.DisplayName,
            entry.Dispatch.TemplateVersion,
            entry.Dispatch.MatchSubject.Sku,
            entry.Dispatch.MatchSubject.Brand,
            entry.Dispatch.MatchSubject.JanNormalized,
            entry.Dispatch.MatchSubject.Qty,
            entry.Proof?.ArtifactPath ?? $"dispatch://{entry.Dispatch.Audit.JobId}",
            entry.Dispatch.ArtifactMediaType,
            entry.Dispatch.ArtifactByteSize,
            entry.Dispatch.SubmissionAdapterKind,
            entry.Dispatch.SubmissionExternalJobId,
            entry.Dispatch.Audit.Reason,
            entry.Dispatch.Audit.ParentJobId);
    }

    private static AuditBackupBundleRecord MapAuthorityBundleRecord(AuditBackupBundleDto bundle)
    {
        return new AuditBackupBundleRecord(
            bundle.FileName,
            bundle.FileName,
            bundle.FilePath,
            bundle.CreatedAtUtc,
            bundle.SizeBytes,
            "desktop-shell-companion");
    }

    private static AuditSearchEntryDto MapLocalAuditMirrorEntry(AuditMirrorEntry entry)
    {
        return new AuditSearchEntryDto
        {
            Dispatch = MapLocalDispatchRecord(entry.Dispatch),
            Proof = entry.Proof is null ? null : MapLocalProofRecord(entry.Proof),
        };
    }

    private static PersistedDispatchRecordDto MapLocalDispatchRecord(AuditDispatchRecord record)
    {
        return new PersistedDispatchRecordDto
        {
            Audit = new AuditEventDto
            {
                JobId = record.DispatchJobId,
                JobLineageId = record.JobLineageId,
                ParentJobId = record.ParentJobId,
                Actor = BuildAuditActor(record.Actor),
                Event = record.EventKind,
                OccurredAt = record.OccurredAtUtc,
                Reason = record.Reason,
            },
            Mode = record.Lane,
            TemplateVersion = record.TemplateVersion,
            MatchSubject = new DispatchMatchSubjectDto
            {
                Sku = record.SubjectSku,
                Brand = record.Brand,
                JanNormalized = record.JanNormalized,
                Qty = record.Qty,
            },
            ArtifactMediaType = record.ArtifactMediaType,
            ArtifactByteSize = record.ArtifactByteSize,
            SubmissionAdapterKind = record.AdapterKind,
            SubmissionExternalJobId = record.ExternalJobId,
        };
    }

    private static ProofRecordDto MapLocalProofRecord(ProofLedgerRecord record)
    {
        return new ProofRecordDto
        {
            ProofJobId = record.ProofJobId,
            JobLineageId = record.JobLineageId,
            RequestedBy = BuildAuditActor(record.RequestedBy),
            RequestedAt = record.RequestedAtUtc,
            Status = record.Status,
            ArtifactPath = record.ArtifactPath,
            Notes = record.Notes,
            Decision = record.Review is null
                ? null
                : new ProofDecisionDto
                {
                    Status = record.Review.Status,
                    Actor = BuildAuditActor(record.Review.Actor),
                    OccurredAt = record.Review.OccurredAtUtc,
                    Notes = record.Review.Notes,
                },
        };
    }

    private static AuditBackupBundleDto MapLocalBundleRecord(AuditBackupBundleRecord record)
    {
        return new AuditBackupBundleDto
        {
            FileName = record.FileName,
            FilePath = record.FilePath,
            CreatedAtUtc = record.CreatedAtUtc,
            SizeBytes = record.SizeBytes,
        };
    }

    private static AuditActorDto BuildAuditActor(string actor)
    {
        return new AuditActorDto
        {
            UserId = actor,
            DisplayName = actor,
        };
    }

    private Task EnsureSelectedWorkspaceNativeStateAsync(WorkspaceModel? workspace)
    {
        return workspace is DesignerWorkspaceModel designer
            ? EnsureDesignerTemplateLoadedAsync(designer)
            : Task.CompletedTask;
    }

    private async Task EnsureDesignerTemplateLoadedAsync(DesignerWorkspaceModel workspace, bool force = false)
    {
        var templateVersion = ResolveDesignerTemplateVersion(workspace);
        if (string.IsNullOrWhiteSpace(templateVersion))
        {
            return;
        }

        var catalogSnapshot = GetTemplateCatalogService().LoadCatalogSnapshot();
        if (!catalogSnapshot.Entries.Any((entry) => string.Equals(entry.Version, templateVersion, StringComparison.OrdinalIgnoreCase)))
        {
            _lastLoadedDesignerTemplateVersion = string.Empty;
            return;
        }

        if (!force && string.Equals(_lastLoadedDesignerTemplateVersion, templateVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var loadGeneration = unchecked(++_designerTemplateLoadGeneration);

        try
        {
            var document = await GetTemplateCatalogService().LoadTemplateAsync(templateVersion);
            if (!ShouldApplyDesignerTemplateLoad(workspace, templateVersion, loadGeneration))
            {
                return;
            }

            CancelPendingDesignerPreviewRefresh();
            DesignerTemplateWorkspaceHydrator.Apply(workspace, document);
            try
            {
                await RefreshDesignerDraftPreviewAsync(workspace);
            }
            catch (Exception ex)
            {
                if (string.Equals(ResolveDesignerTemplateVersion(workspace), document.TemplateVersion, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDesignerPreviewFailure(workspace, document.TemplateVersion, ex.Message);
                }
            }

            _lastLoadedDesignerTemplateVersion = document.TemplateVersion;

            if (ReferenceEquals(SelectedModule?.Workspace, workspace))
            {
                LastActionStatus = $"Opened {document.TemplateVersion} from the native {document.Source} catalog.";
                RefreshStatusStrip(SelectedModule);
                SyncShellChromeFromSelection();
            }
        }
        catch (Exception ex)
        {
            if (!ShouldApplyDesignerTemplateLoad(workspace, templateVersion, loadGeneration))
            {
                return;
            }

            _lastLoadedDesignerTemplateVersion = string.Empty;
            ApplyDesignerLoadFailure(workspace, templateVersion, ex.Message);

            if (ReferenceEquals(SelectedModule?.Workspace, workspace))
            {
                LastActionStatus = $"Open template failed: {ex.Message}";
                RefreshStatusStrip(SelectedModule);
                SyncShellChromeFromSelection();
            }
        }
    }

    private static string? ResolveDesignerTemplateVersion(DesignerWorkspaceModel workspace)
    {
        return workspace.TemplateLibrary.SelectedTemplate?.Name
            ?? workspace.SetupRows.FirstOrDefault((row) => string.Equals(row.Name, "Document", StringComparison.OrdinalIgnoreCase))?.Value
            ?? workspace.PrimaryDocumentTitle;
    }

    private bool ShouldApplyDesignerTemplateLoad(DesignerWorkspaceModel workspace, string requestedTemplateVersion, int loadGeneration)
    {
        if (loadGeneration != _designerTemplateLoadGeneration)
        {
            return false;
        }

        var selectedTemplateVersion = ResolveDesignerTemplateVersion(workspace);
        return string.Equals(selectedTemplateVersion, requestedTemplateVersion, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldApplyDesignerPreviewRender(
        DesignerWorkspaceModel workspace,
        string requestedTemplateVersion,
        int renderGeneration,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (renderGeneration != _designerPreviewRenderGeneration)
        {
            return false;
        }

        var selectedTemplateVersion = ResolveDesignerTemplateVersion(workspace);
        return string.Equals(selectedTemplateVersion, requestedTemplateVersion, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> RefreshDesignerDraftPreviewAsync(
        DesignerWorkspaceModel workspace,
        bool updateActionMessage = false,
        bool prependPreviewMessage = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bindings = BuildDesignerPreviewBindings(workspace);
        var bindingJson = JsonSerializer.Serialize(bindings);
        var document = DesignerTemplateDocumentFactory.CreateDesignerDocument(workspace, bindingJson);
        var renderGeneration = unchecked(++_designerPreviewRenderGeneration);
        var render = await GetRenderService().RenderAsync(
            document,
            new RenderRequest("SVG,PDF", bindingJson, bindings.TryGetValue("jan", out var jan) ? jan : null),
            cancellationToken);
        if (!ShouldApplyDesignerPreviewRender(workspace, document.TemplateVersion, renderGeneration, cancellationToken))
        {
            return "Skipped stale native draft preview update because the active template changed.";
        }

        var previewSvg = render.Svg;
        var renderWarnings = render.Warnings ?? Array.Empty<string>();
        var pdfWarning = renderWarnings.FirstOrDefault((warning) => warning.Contains("Draft PDF", StringComparison.OrdinalIgnoreCase));
        var draftPdfSummary = render.PdfBytes.Length == 0
            ? (pdfWarning ?? "not generated")
            : $"{render.PdfBytes.Length.ToString(CultureInfo.InvariantCulture)} bytes";

        workspace.PreviewSvg = previewSvg;
        workspace.StatusSummary = "native design surface + local render service draft SVG/PDF artifacts loaded; proof and print authority still remain hybrid";
        workspace.MessageSummary = $"{workspace.CanvasElements.Count} design object(s) in the current native draft preview.";
        UpsertPropertyRow(workspace.SetupRows, "Preview route", "local render service draft");
        UpsertPropertyRow(workspace.PreviewRows, "Mode", "native draft render");
        UpsertPropertyRow(workspace.PreviewRows, "Authority", "local render service draft");
        UpsertPropertyRow(workspace.PreviewRows, "Draft PDF", draftPdfSummary);
        UpsertPropertyRow(workspace.PreviewRows, "JAN", render.NormalizedJan ?? "n/a");
        UpsertStatusItem(
            workspace.StatusItems,
            "Preview",
            renderWarnings.Count == 0 ? "native draft" : "native draft warning",
            renderWarnings.Count == 0
                ? $"Preview pane is regenerated through the local render service from the current Designer surface, and that local render request now returns draft SVG/PDF artifacts from one shared scene model ({draftPdfSummary}); proof/print authority remains outside this pane."
                : $"Preview pane regenerated through the local render service from one shared scene model, but draft PDF generation degraded with warning: {string.Join(" | ", renderWarnings)} Proof/print authority remains outside this pane.",
            renderWarnings.Count == 0 ? "DRAFT" : "WARN",
            renderWarnings.Count == 0 ? Brushes.SteelBlue : Brushes.DarkGoldenrod);
        foreach (var warning in renderWarnings)
        {
            PrependMessageRow(
                workspace.MessageRows,
                new MessageRowModel("Warn", "preview", warning),
                6);
        }

        if (prependPreviewMessage)
        {
            PrependMessageRow(
                workspace.MessageRows,
                new MessageRowModel(
                    "Info",
                    "preview",
                    $"Regenerated native draft SVG/PDF artifacts for {document.TemplateVersion} through the local render service request."),
                6);
        }

        if (updateActionMessage && ReferenceEquals(SelectedModule?.Workspace, workspace))
        {
            LastActionStatus = $"Regenerated native draft SVG/PDF artifacts for {document.TemplateVersion} through the local render service.";
            RefreshStatusStrip(SelectedModule);
            SyncShellChromeFromSelection();
        }

        return "Regenerated native draft SVG/PDF artifacts from the current Designer surface through the local render service and shared draft scene model. Proof review / print authority remain outside this pane.";
    }

    private static Dictionary<string, string> BuildDesignerPreviewBindings(DesignerWorkspaceModel workspace)
    {
        var templateVersion = ResolveDesignerTemplateVersion(workspace) ?? "designer-draft";
        var bindings = DesignerDraftBindings.CreateBaseBindings(templateVersion);

        foreach (var row in workspace.RecordRows)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Value))
            {
                continue;
            }

            var normalizedKey = DesignerDraftBindings.NormalizeBindingKey(row.Name);
            if (!string.IsNullOrWhiteSpace(normalizedKey))
            {
                bindings[normalizedKey] = row.Value;
            }
        }

        return bindings;
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
            LiveRuntimeLabel => "Live service connected",
            SeededFallbackRuntimeLabel => "Running with limited local state",
            StaleLiveRuntimeLabel => "Showing the last successful snapshot",
            _ => "Starting and waiting for the first refresh",
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

    private static string? DescribePreferredTemplateVersion(WorkspaceModel? workspace)
    {
        return workspace switch
        {
            HomeWorkspaceModel home => home.TemplateLibrary.SelectedTemplate?.Name,
            DesignerWorkspaceModel designer => ResolveDesignerTemplateVersion(designer),
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
        if (string.Equals(actionLabel, ShellActions.SaveToCatalog, StringComparison.OrdinalIgnoreCase))
        {
            return workspace.TemplateLibrary.SelectedTemplate is null
                ? "Saving the current designer surface into the native local catalog."
                : $"Keeping {workspace.TemplateLibrary.SelectionHeading} focused for native catalog save.";
        }

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
        if (string.Equals(actionLabel, ShellActions.RunProof, StringComparison.OrdinalIgnoreCase))
        {
            if (workspace.FocusBlockedJob() || workspace.FocusHeldJob() || workspace.FocusReadyJob())
            {
                return $"Focused {workspace.SelectionHeading} so the native proof-create subject payload stays explicit.";
            }
        }

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
                return $"Focused {workspace.SelectionHeading} so the current subject blocker stays visible before any handoff.";
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
            ? "the live service"
            : designer.SelectedElementProperties.DispatchAuthority;

        return $"{objectName} is focused with binding {binding}. Proof and print still run through {authority}.";
    }

    private static void ApplyDesignerSaveState(DesignerWorkspaceModel workspace, TemplateCatalogSaveResult result)
    {
        UpsertPropertyRow(workspace.SetupRows, "Catalog source", "native local catalog");
        UpsertPropertyRow(workspace.SetupRows, "Proof review", "required after native save");
        UpsertPropertyRow(workspace.PreviewRows, "Template", result.TemplateVersion);
        UpsertStatusItem(
            workspace.StatusItems,
            "Save path",
            "native local catalog",
            $"{Path.GetFileName(result.TemplatePath)} saved; proof review required.",
            "LIVE",
            Brushes.ForestGreen);
        PrependMessageRow(
            workspace.MessageRows,
            new MessageRowModel(
                "Info",
                "catalog",
                $"Saved {result.TemplateVersion} to the native local catalog. Fresh proof review is still required before dispatch."),
            6);
    }

    private static void ApplyDesignerLoadFailure(DesignerWorkspaceModel workspace, string templateVersion, string error)
    {
        UpsertStatusItem(
            workspace.StatusItems,
            "Catalog",
            "load failed",
            $"Native open for {templateVersion} failed: {error}",
            "ERROR",
            Brushes.Firebrick);
        PrependMessageRow(
            workspace.MessageRows,
            new MessageRowModel(
                "Error",
                "catalog",
                $"Could not open {templateVersion} from the native catalog. {error}"),
            6);
    }

    private static void ApplyDesignerPreviewFailure(DesignerWorkspaceModel workspace, string templateVersion, string error)
    {
        workspace.PreviewSvg = string.Empty;
        workspace.StatusSummary = "native design surface loaded; draft preview refresh failed";
        UpsertPropertyRow(workspace.PreviewRows, "Draft PDF", "render failed");
        UpsertStatusItem(
            workspace.StatusItems,
            "Preview",
            "render failed",
            $"Native draft preview for {templateVersion} failed: {error}",
            "ERROR",
            Brushes.Firebrick);
        PrependMessageRow(
            workspace.MessageRows,
            new MessageRowModel(
                "Error",
                "preview",
                $"Could not regenerate the native draft preview for {templateVersion}. {error}"),
            6);
    }

    private static void UpsertPropertyRow(ObservableCollection<PropertyRowModel> rows, string name, string value)
    {
        for (var index = 0; index < rows.Count; index += 1)
        {
            if (!string.Equals(rows[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows[index] = new PropertyRowModel(name, value);
            return;
        }

        rows.Add(new PropertyRowModel(name, value));
    }

    private static void UpsertStatusItem(
        ObservableCollection<StatusItemModel> items,
        string label,
        string value,
        string detail,
        string tone,
        Brush accent)
    {
        for (var index = 0; index < items.Count; index += 1)
        {
            if (!string.Equals(items[index].Label, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items[index] = new StatusItemModel(label, value, detail, tone, accent);
            return;
        }

        items.Add(new StatusItemModel(label, value, detail, tone, accent));
    }

    private static void PrependMessageRow(
        ObservableCollection<MessageRowModel> rows,
        MessageRowModel message,
        int maxItems)
    {
        rows.Insert(0, message);
        while (rows.Count > maxItems)
        {
            rows.RemoveAt(rows.Count - 1);
        }
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
            "Home" when string.Equals(actionLabel, ShellActions.RefreshState, StringComparison.OrdinalIgnoreCase)
                => "Overview, service health, and current blockers were refreshed.",
            "Home" => "Overview, service health, and the next safe action stay visible before touching another workspace.",
            "Designer" when string.Equals(actionLabel, ShellActions.SaveToCatalog, StringComparison.OrdinalIgnoreCase)
                => "Saved catalog state is the only template state this shell treats as proof-safe.",
            "Designer" => "Authoring state, overlay impact, and rollback remain visible on one screen.",
            "Print Console" => "Proof lineage and dispatch route should be reviewed before print unlock.",
            "Batch Jobs" => "Import assumptions, retry eligibility, and queue blockers are now in view.",
            "History" => "Audit, proof review, and restore safety remain explicit before operator approval.",
            _ => "The current workspace and its next action are now in view.",
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
