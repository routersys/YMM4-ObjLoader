using ObjLoader.Rendering.Mathematics;
using System.Buffers;

namespace ObjLoader.Services.Rendering.Spatial;

internal class OctreeNode
{
    public CullingBox Bounds;
    public int[] ItemIndices = Array.Empty<int>();
    public int ItemCount;
    public OctreeNode[]? Children;

    public void Init(CullingBox bounds)
    {
        Bounds = bounds;
        ItemCount = 0;
        if (Children != null)
        {
            ArrayPool<OctreeNode>.Shared.Return(Children);
            Children = null;
        }
    }

    public void AddIndex(int index)
    {
        if (ItemCount >= ItemIndices.Length)
        {
            int newSize = ItemIndices.Length == 0 ? 4 : ItemIndices.Length * 2;
            int[] newArray = ArrayPool<int>.Shared.Rent(newSize);
            if (ItemCount > 0)
            {
                Array.Copy(ItemIndices, newArray, ItemCount);
            }
            if (ItemIndices.Length > 0)
            {
                ArrayPool<int>.Shared.Return(ItemIndices);
            }
            ItemIndices = newArray;
        }
        ItemIndices[ItemCount++] = index;
    }

    public void Clear()
    {
        if (ItemIndices.Length > 0)
        {
            ArrayPool<int>.Shared.Return(ItemIndices);
            ItemIndices = Array.Empty<int>();
        }
        ItemCount = 0;
        if (Children != null)
        {
            ArrayPool<OctreeNode>.Shared.Return(Children);
            Children = null;
        }
    }
}