using System.Xml.Linq;

namespace SlnxMerger.Slnx;

public enum NodeKind
{
    Root,
    Folder,
    Project,
    File
}

public class SlnxNode
{
    public NodeKind Kind { get; init; }
    public string Name { get; init; } = "";
    public string Key { get; init; } = "";
    public string? RelativePath { get; set; }
    public string? AbsolutePath { get; set; }
    public string FolderPath { get; set; } = "";
    public XElement? Element { get; set; }
    public SlnxNode? Parent { get; set; }
    public List<SlnxNode> Children { get; } =[];
    public override string ToString() => $"{Kind} {Name}";
}
