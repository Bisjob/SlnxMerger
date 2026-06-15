using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SlnxMerger.Diff;
using SlnxMerger.Localization;
using SlnxMerger.Slnx;

namespace SlnxMerger;

public class DiffNodeViewModel(DiffNode node) : INotifyPropertyChanged
{
    private bool isChecked = node.Status != DiffStatus.Same;
    private bool isExpanded = true;

    public DiffNode Node { get; } = node;
    public ObservableCollection<DiffNodeViewModel> Children { get; } = [];

    public string Icon => Node.Kind switch
    {
        NodeKind.Folder => "\uE8B7", 
        NodeKind.Project => "\uE7B8",
        _ => "\uE8A5",
    };

    public string Name => Node.Name;

    public string StatusLabel => Node.Status switch
    {
        DiffStatus.OnlyLeft => Localizer.Get("StatusLabelOnlyLeft"),
        DiffStatus.OnlyRight => Localizer.Get("StatusLabelOnlyRight"),
        _ => "",
    };

    public bool IsActionable => Node.Status != DiffStatus.Same;

    public string StatusColor => Node.Status switch
    {
        DiffStatus.OnlyLeft => "#00D68F",  
        DiffStatus.OnlyRight => "#FF4D6D", 
        _ => "#FFFFFF",                    
    };

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value) return;
            isChecked = value;
            OnPropertyChanged();
            foreach (var child in Children)
                child.IsChecked = value;
        }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set { isExpanded = value; OnPropertyChanged(); }
    }

    public void SetCheckedRecursive(bool value)
    {
        IsChecked = value;
        foreach (var child in Children)
            child.SetCheckedRecursive(value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
