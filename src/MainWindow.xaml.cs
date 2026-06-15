using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using SlnxMerger.Diff;
using SlnxMerger.Localization;
using SlnxMerger.Slnx;

namespace SlnxMerger;

public partial class MainWindow : Window
{
    private SlnxDocument? leftDocument;
    private SlnxDocument? rightDocument;
    private DiffNode? diffRoot;
    private ObservableCollection<DiffNodeViewModel> vmRoots = [];

    public MainWindow()
    {
        InitializeComponent();
        InitializeLanguageSelector();
        Loaded += (_, _) =>
        {
            ApplyWindowsTheme();
        };
    }

    private void ApplyWindowsTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        bool isDark = IsWindowsDarkMode();

        DwmSetWindowAttribute(
            hwnd,
            DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref isDark,
            Marshal.SizeOf<bool>());
    }

    private static bool IsWindowsDarkMode()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

        return (int?)key?.GetValue("AppsUseLightTheme") == 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref bool pvAttribute,
        int cbAttribute);


    private void BrowseLeft_Click(object sender, RoutedEventArgs e) => Browse(LeftPathBox);
    private void BrowseRight_Click(object sender, RoutedEventArgs e) => Browse(RightPathBox);

    private static void Browse(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFileDialog
        {
            Filter = Localizer.Get("OpenDialogFilter"),
            Title = Localizer.Get("OpenDialogTitle"),
        };
        if (dialog.ShowDialog() == true)
            target.Text = dialog.FileName;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        var slnx = files.Where(f => f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).ToList();
        if (slnx.Count == 0) return;

        if (string.IsNullOrWhiteSpace(LeftPathBox.Text))
            LeftPathBox.Text = slnx[0];
        else if (string.IsNullOrWhiteSpace(RightPathBox.Text))
            RightPathBox.Text = slnx[0];
        else
            LeftPathBox.Text = slnx[0];

        if (slnx.Count > 1)
            RightPathBox.Text = slnx[1];
    }

    private void Compare_Click(object sender, RoutedEventArgs e) => RunComparison();

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (leftDocument != null && rightDocument != null)
            RunComparison();
    }

    private void InitializeLanguageSelector()
    {
        if (Application.Current is not App app)
            return;

        foreach (var item in LanguageSelector.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string languageCode &&
                string.Equals(languageCode, app.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                LanguageSelector.SelectedItem = item;
                break;
            }
        }
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (LanguageSelector.SelectedItem is not ComboBoxItem item || item.Tag is not string languageCode)
            return;

        if (Application.Current is not App app)
            return;

        if (string.Equals(app.CurrentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            return;

        app.SetLanguage(languageCode);
        RefreshLocalizedState();
    }

    private void RefreshLocalizedState()
    {
        if (leftDocument != null && rightDocument != null)
        {
            diffRoot = DiffEngine.Compare(
                leftDocument, rightDocument,
                onlyCommonRoots: OnlyCommonRootsBox.IsChecked == true,
                out var ignoredRoots);

            RebuildTree();

            var (added, removed) = CountDifferences(diffRoot);
            var summary = added + removed == 0
                ? Localizer.Get("NoDiffSummary")
                : Localizer.Format("DiffSummary", added, removed);

            if (ignoredRoots.Count > 0)
                summary += Localizer.Format("IgnoredRootsSummary", string.Join(", ", ignoredRoots));

            StatusText.Text = summary;
            UpdateSummary();
            return;
        }

        StatusText.Text = Localizer.Get("InitialStatus");
        SummaryText.Text = "";
    }

    private void RunComparison()
    {
        var leftPath = LeftPathBox.Text.Trim().Trim('"');
        var rightPath = RightPathBox.Text.Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
        {
            StatusText.Text = Localizer.Get("StatusSelectBoth");
            return;
        }
        if (!File.Exists(leftPath)) { StatusText.Text = Localizer.Format("StatusFileNotFound", leftPath); return; }
        if (!File.Exists(rightPath)) { StatusText.Text = Localizer.Format("StatusFileNotFound", rightPath); return; }
        if (string.Equals(Path.GetFullPath(leftPath), Path.GetFullPath(rightPath), StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = Localizer.Get("StatusSameFile");
            return;
        }

        try
        {
            leftDocument = SlnxDocument.Load(leftPath);
            rightDocument = SlnxDocument.Load(rightPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localizer.Get("LoadErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        diffRoot = DiffEngine.Compare(
            leftDocument, rightDocument,
            onlyCommonRoots: OnlyCommonRootsBox.IsChecked == true,
            out var ignoredRoots);

        RebuildTree();

        var (added, removed) = CountDifferences(diffRoot);
        var summary = added + removed == 0
            ? Localizer.Get("NoDiffSummary")
            : Localizer.Format("DiffSummary", added, removed);

        if (ignoredRoots.Count > 0)
            summary += Localizer.Format("IgnoredRootsSummary", string.Join(", ", ignoredRoots));

        StatusText.Text = summary;
        UpdateSummary();
    }

    private static (int onlyLeft, int onlyRight) CountDifferences(DiffNode node)
    {
        int left = 0, right = 0;
        void Walk(DiffNode n)
        {
            if (n.Kind is NodeKind.Project or NodeKind.File)
            {
                if (n.Status == DiffStatus.OnlyLeft) left++;
                else if (n.Status == DiffStatus.OnlyRight) right++;
            }
            foreach (var c in n.Children) Walk(c);
        }
        Walk(node);
        return (left, right);
    }

    private void RebuildTree()
    {
        vmRoots = new ObservableCollection<DiffNodeViewModel>();
        if (diffRoot != null)
        {
            var onlyDiff = OnlyDiffBox.IsChecked == true;
            foreach (var child in diffRoot.Children)
            {
                var vm = BuildViewModel(child, onlyDiff);
                if (vm != null)
                    vmRoots.Add(vm);
            }
        }
        DiffTree.ItemsSource = vmRoots;
    }

    private static DiffNodeViewModel? BuildViewModel(DiffNode node, bool onlyDiff)
    {
        if (onlyDiff && !node.HasDifference)
            return null;

        var vm = new DiffNodeViewModel(node);
        foreach (var child in node.Children)
        {
            var childVm = BuildViewModel(child, onlyDiff);
            if (childVm != null)
                vm.Children.Add(childVm);
        }
        return vm;
    }

    private void CheckAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in vmRoots) vm.SetCheckedRecursive(true);
    }

    private void UncheckAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in vmRoots) vm.SetCheckedRecursive(false);
    }

    private void UpdateSummary()
    {
        if (diffRoot == null) { SummaryText.Text = ""; return; }
        var (l, r) = CountDifferences(diffRoot);
        SummaryText.Text = Localizer.Format("DeltaSummary", l, r);
    }


    private void ApplyToRight_Click(object sender, RoutedEventArgs e) => Apply(toRight: true);
    private void ApplyToLeft_Click(object sender, RoutedEventArgs e) => Apply(toRight: false);


    private void Apply(bool toRight)
    {
        if (leftDocument == null || rightDocument == null || diffRoot == null)
        {
            StatusText.Text = Localizer.Get("StatusRunComparisonFirst");
            return;
        }

        var target = toRight ? rightDocument : leftDocument;
        var sourceName = Path.GetFileName((toRight ? leftDocument : rightDocument)!.FilePath);
        var targetName = Path.GetFileName(target.FilePath);

        int added = 0, removed = 0;

        void Walk(DiffNodeViewModel vm)
        {
            var node = vm.Node;

            if (node.Status != DiffStatus.Same && vm.IsChecked)
            {
                var existsOnSourceSide = toRight
                    ? node.Status == DiffStatus.OnlyLeft
                    : node.Status == DiffStatus.OnlyRight;

                if (existsOnSourceSide)
                {
                    var sourceNode = (toRight ? node.Left : node.Right)!;
                    var targetParent = target.EnsureFolderNode(node.ParentFolderPath);
                    added += target.AddSubtree(targetParent, sourceNode);
                }
                else
                {
                    var targetNode = (toRight ? node.Right : node.Left)!;
                    removed += target.RemoveSubtree(targetNode);
                }
                return;
            }

            foreach (var child in vm.Children)
                Walk(child);
        }

        foreach (var vm in vmRoots)
            Walk(vm);

        if (added == 0 && removed == 0)
        {
            StatusText.Text = Localizer.Get("StatusNoCheckedItems");
            return;
        }

        var confirm = MessageBox.Show(this,
            Localizer.Format("MergeConfirmMessage", targetName, added, sourceName, removed),
            Localizer.Get("MergeConfirmTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            RunComparison();
            return;
        }

        try
        {
            target.Save(backup: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localizer.Get("WriteErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RunComparison();
        StatusText.Text = Localizer.Format("UpdateSuccess", targetName, added, removed, StatusText.Text);
    }
}
