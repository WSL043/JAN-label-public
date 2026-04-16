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
        var moduleLabel = SelectedModule?.Label ?? "Shell";
        LastActionStatus = $"{moduleLabel}: {actionLabel} requested. Prototype shell acknowledged the action and keeps proof/print authority in desktop-shell.";
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
