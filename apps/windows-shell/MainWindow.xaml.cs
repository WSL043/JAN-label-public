using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace JanLabel.WindowsShell;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private ModuleModel? _selectedModule;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        WorkspaceFactory.SeedModules(Modules);
        SelectedModule = Modules[1];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModuleModel> Modules { get; } = new();

    public ObservableCollection<RibbonGroupModel> CurrentRibbonGroups { get; } = new();

    public ObservableCollection<string> CurrentHeaderActions { get; } = new();

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
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(WorkspaceTagline));
            OnPropertyChanged(nameof(CurrentWorkspace));
        }
    }

    public WorkspaceModel? CurrentWorkspace => SelectedModule?.Workspace;

    public string WindowTitle => $"JAN Label Workstation - {SelectedModule?.Label ?? "Designer"}";

    public string WorkspaceTagline => SelectedModule?.Tagline ?? "Windows-native operator workstation";

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
