using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace SlnxMerger.Slnx;

public class SlnxDocument
{
    private readonly Dictionary<string, SlnxNode> _folders =
        new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; private set; } = "";
    public string Directory { get; private set; } = "";
    public XDocument Xml { get; private set; } = new();
    public SlnxNode Root { get; private set; } = new() { Kind = NodeKind.Root };
    public bool UsesNestedFolders { get; private set; }

    public static SlnxDocument Load(string path)
    {
        var doc = new SlnxDocument
        {
            FilePath = Path.GetFullPath(path),
        };
        doc.Directory = Path.GetDirectoryName(doc.FilePath)
            ?? throw new InvalidDataException("Chemin de fichier invalide.");

        doc.Xml = XDocument.Load(doc.FilePath);

        if (doc.Xml.Root is null || doc.Xml.Root.Name.LocalName != "Solution")
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' n'est pas un .slnx valide : élément racine <Solution> introuvable.");

        doc.Root = new SlnxNode { Kind = NodeKind.Root, Name = "(racine)", FolderPath = "/" };
        doc._folders["/"] = doc.Root;

        doc.ParseContainer(doc.Xml.Root, doc.Root, isRootContainer: true);
        return doc;
    }


    private void ParseContainer(XElement container, SlnxNode parentFolder, bool isRootContainer)
    {
        foreach (var el in container.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "Folder":
                {
                    var raw = (string?)el.Attribute("Name") ?? "";
                    string fullPath;

                    if (raw.StartsWith('/'))
                        fullPath = NormalizeFolderPath(raw);
                    else
                        fullPath = parentFolder.FolderPath + raw.Trim('/') + "/";

                    if (!isRootContainer)
                        UsesNestedFolders = true;

                    var folderNode = EnsureFolderNode(fullPath);
                    folderNode.Element ??= el;

                    ParseContainer(el, folderNode, isRootContainer: false);
                    break;
                }

                case "Project":
                case "File":
                {
                    var rel = (string?)el.Attribute("Path") ?? "";
                    var abs = ResolveAbsolute(rel);
                    var node = new SlnxNode
                    {
                        Kind = el.Name.LocalName == "Project" ? NodeKind.Project : NodeKind.File,
                        Name = Path.GetFileName(rel.Replace('\\', '/').TrimEnd('/')),
                        Key = abs.Replace('\\', '/').ToLowerInvariant(),
                        RelativePath = rel,
                        AbsolutePath = abs,
                        Element = el,
                        Parent = parentFolder,
                    };
                    parentFolder.Children.Add(node);
                    break;
                }
            }
        }
    }

    private string ResolveAbsolute(string relative)
    {
        var cleaned = relative.Replace('\\', Path.DirectorySeparatorChar)
                              .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory, cleaned));
    }

    public static string NormalizeFolderPath(string path)
    {
        var trimmed = path.Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "/" : "/" + trimmed + "/";
    }

    public SlnxNode EnsureFolderNode(string fullPath)
    {
        fullPath = NormalizeFolderPath(fullPath);
        if (_folders.TryGetValue(fullPath, out var existing))
            return existing;

        var current = Root;
        var pathSoFar = "/";
        foreach (var segment in fullPath.Trim('/').Split('/'))
        {
            pathSoFar += segment + "/";
            if (!_folders.TryGetValue(pathSoFar, out var node))
            {
                node = new SlnxNode
                {
                    Kind = NodeKind.Folder,
                    Name = segment,
                    Key = segment.ToLowerInvariant(),
                    FolderPath = pathSoFar,
                    Parent = current,
                };
                current.Children.Add(node);
                _folders[pathSoFar] = node;
            }
            current = node;
        }
        return current;
    }

    public XElement EnsureFolderElement(SlnxNode folder)
    {
        if (folder.Kind == NodeKind.Root)
            return Xml.Root!;
        if (folder.Element != null)
            return folder.Element;

        XElement el;
        if (UsesNestedFolders)
        {
            var parentEl = folder.Parent is null || folder.Parent.Kind == NodeKind.Root
                ? Xml.Root!
                : EnsureFolderElement(folder.Parent);
            el = new XElement("Folder", new XAttribute("Name", folder.Name));
            InsertSorted(parentEl, el);
        }
        else
        {
            el = new XElement("Folder", new XAttribute("Name", folder.FolderPath));
            InsertSorted(Xml.Root!, el);
        }

        folder.Element = el;
        return el;
    }

    public int AddSubtree(SlnxNode targetParentFolder, SlnxNode source)
    {
        if (source.Kind == NodeKind.Folder)
        {
            var folder = EnsureFolderNode(targetParentFolder.FolderPath + source.Name + "/");
            if (UsesNestedFolders || source.Children.Count == 0 ||
                source.Children.Any(c => c.Kind is NodeKind.Project or NodeKind.File))
            {
                EnsureFolderElement(folder);
            }

            var count = 0;
            foreach (var child in source.Children)
                count += AddSubtree(folder, child);
            return count;
        }

        if (targetParentFolder.Children.Any(c => c.Kind == source.Kind && c.Key == source.Key))
            return 0;

        var containerEl = EnsureFolderElement(targetParentFolder);

        var el = new XElement(source.Element ?? new XElement(source.Kind.ToString()));
        var rel = Path.GetRelativePath(Directory, source.AbsolutePath!)
                      .Replace('\\', '/');
        el.SetAttributeValue("Path", rel);
        InsertSorted(containerEl, el);

        var node = new SlnxNode
        {
            Kind = source.Kind,
            Name = source.Name,
            Key = source.Key,
            RelativePath = rel,
            AbsolutePath = source.AbsolutePath,
            Element = el,
            Parent = targetParentFolder,
        };
        targetParentFolder.Children.Add(node);
        return 1;
    }

    public int RemoveSubtree(SlnxNode node)
    {
        var count = 0;

        foreach (var child in node.Children.ToList())
            count += RemoveSubtree(child);

        node.Element?.Remove();
        node.Parent?.Children.Remove(node);

        if (node.Kind == NodeKind.Folder)
            _folders.Remove(node.FolderPath);
        else
            count += 1;

        return count;
    }

    private static void InsertSorted(XElement container, XElement el)
    {
        var key = SortKey(el);
        var group = container.Elements(el.Name).ToList();

        var insertAfter = group.LastOrDefault(e =>
            string.Compare(SortKey(e), key, StringComparison.OrdinalIgnoreCase) <= 0);

        if (insertAfter != null)
            insertAfter.AddAfterSelf(el);
        else if (group.Count > 0)
            group[0].AddBeforeSelf(el);
        else
            container.Add(el);
    }

    private static string SortKey(XElement e) =>
        (string?)e.Attribute("Name") ?? (string?)e.Attribute("Path") ?? "";


    public void Save(bool backup = true)
    {
        if (backup && File.Exists(FilePath))
            File.Copy(FilePath, FilePath + ".bak", overwrite: true);

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
        };
        using var writer = XmlWriter.Create(FilePath, settings);
        Xml.Save(writer);
    }

    public static int CountItems(SlnxNode node)
    {
        if (node.Kind is NodeKind.Project or NodeKind.File)
            return 1;
        return node.Children.Sum(CountItems);
    }
}
