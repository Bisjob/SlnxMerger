using SlnxMerger.Localization;
using SlnxMerger.Slnx;

namespace SlnxMerger.Diff;

public enum DiffStatus
{
    Same,
    OnlyLeft,
    OnlyRight,
}

public class DiffNode
{
    public NodeKind Kind { get; init; }
    public string Name { get; init; } = "";
    public DiffStatus Status { get; init; }
    public SlnxNode? Left { get; init; }
    public SlnxNode? Right { get; init; }

    public string ParentFolderPath { get; init; } = "/";

    public List<DiffNode> Children { get; } = new();

    public bool HasDifference { get; private set; }

    internal void ComputeHasDifference()
    {
        var has = Status != DiffStatus.Same;
        foreach (var child in Children)
        {
            child.ComputeHasDifference();
            has |= child.HasDifference;
        }
        HasDifference = has;
    }
}

public static class DiffEngine
{
    public static DiffNode Compare(
        SlnxDocument left,
        SlnxDocument right,
        bool onlyCommonRoots,
        out List<string> ignoredRoots)
    {
        var root = new DiffNode
        {
            Kind = NodeKind.Root,
            Name = Localizer.Get("RootSolutionName"),
            Status = DiffStatus.Same,
            Left = left.Root,
            Right = right.Root,
        };

        var ignored = new List<string>();
        CompareChildren(root, left.Root, right.Root, depth: 0, onlyCommonRoots, ignored);
        root.ComputeHasDifference();

        ignoredRoots = ignored;
        return root;
    }

    private static void CompareChildren(
        DiffNode parent,
        SlnxNode? left,
        SlnxNode? right,
        int depth,
        bool onlyCommonRoots,
        List<string> ignoredRoots)
    {
        var leftChildren = left?.Children ?? new List<SlnxNode>();
        var rightChildren = right?.Children ?? new List<SlnxNode>();

        var leftMap = leftChildren.ToDictionary(c => (c.Kind, c.Key));
        var rightMap = rightChildren.ToDictionary(c => (c.Kind, c.Key));

        var allKeys = leftChildren.Select(c => (c.Kind, c.Key, c.Name))
            .Concat(rightChildren.Select(c => (c.Kind, c.Key, c.Name)))
            .DistinctBy(t => (t.Kind, t.Key))
            .OrderBy(t => KindOrder(t.Kind))
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var parentPath = (left ?? right)?.FolderPath ?? "/";

        foreach (var (kind, key, name) in allKeys)
        {
            leftMap.TryGetValue((kind, key), out var l);
            rightMap.TryGetValue((kind, key), out var r);

            var status = (l, r) switch
            {
                (not null, not null) => DiffStatus.Same,
                (not null, null) => DiffStatus.OnlyLeft,
                _ => DiffStatus.OnlyRight,
            };

            if (depth == 0 && kind == NodeKind.Folder && onlyCommonRoots && status != DiffStatus.Same)
            {
                var side = status == DiffStatus.OnlyLeft
                    ? Localizer.Get("SideLeft")
                    : Localizer.Get("SideRight");

                ignoredRoots.Add(Localizer.Format("IgnoredRootItem", name, side));
                continue;
            }

            var node = new DiffNode
            {
                Kind = kind,
                Name = name,
                Status = status,
                Left = l,
                Right = r,
                ParentFolderPath = parentPath,
            };
            parent.Children.Add(node);

            if (kind == NodeKind.Folder)
                CompareChildren(node, l, r, depth + 1, onlyCommonRoots, ignoredRoots);
        }
    }

    private static int KindOrder(NodeKind kind) => kind switch
    {
        NodeKind.Folder => 0,
        NodeKind.Project => 1,
        _ => 2,
    };
}
