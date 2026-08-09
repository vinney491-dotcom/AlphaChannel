using System.Collections.ObjectModel;

namespace AlphaChannel.TexTools.UI;

public sealed class ItemTreeNode
{
    public ItemTreeNode(string name, ItemRow? item = null)
    {
        Name = name;
        Item = item;
    }

    public string Name { get; }
    public ItemRow? Item { get; }
    public ObservableCollection<ItemTreeNode> Children { get; } = new();
    public bool IsItem => Item != null;

    public override string ToString() => Name;
}
