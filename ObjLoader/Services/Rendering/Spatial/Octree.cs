using ObjLoader.Rendering.Mathematics;
using System.Buffers;
using System.Numerics;

namespace ObjLoader.Services.Rendering.Spatial;

internal class Octree
{
    private OctreeNode _root;
    private CullingBox[] _itemBounds = Array.Empty<CullingBox>();
    private bool[] _disableCulling = Array.Empty<bool>();
    private const int MaxItems = 16;
    private const int MaxDepth = 6;

    private readonly Stack<OctreeNode> _nodePool = new Stack<OctreeNode>(256);

    public Octree()
    {
        _root = GetNode(new CullingBox());
    }

    public void Build(CullingBox rootBounds, CullingBox[] itemBounds, bool[] disableCulling, int itemCount)
    {
        Clear();
        _root = GetNode(rootBounds);
        _itemBounds = itemBounds;
        _disableCulling = disableCulling;

        for (int i = 0; i < itemCount; i++)
        {
            Insert(i, _itemBounds[i], _root, 0);
        }
    }

    private OctreeNode GetNode(CullingBox bounds)
    {
        if (_nodePool.Count > 0)
        {
            var node = _nodePool.Pop();
            node.Init(bounds);
            return node;
        }
        var newNode = new OctreeNode();
        newNode.Init(bounds);
        return newNode;
    }

    private void ReturnNode(OctreeNode node)
    {
        if (node.Children != null)
        {
            for (int i = 0; i < 8; i++)
            {
                if (node.Children[i] != null)
                {
                    ReturnNode(node.Children[i]);
                }
            }
        }
        node.Clear();
        _nodePool.Push(node);
    }

    private void Insert(int index, CullingBox bounds, OctreeNode node, int depth)
    {
        if (depth >= MaxDepth || (node.Children == null && node.ItemCount < MaxItems))
        {
            node.AddIndex(index);
            if (node.ItemCount >= MaxItems && depth < MaxDepth)
            {
                Split(node);
            }
            return;
        }

        if (node.Children == null)
        {
            Split(node);
        }

        bool insertedToChild = false;
        for (int i = 0; i < 8; i++)
        {
            if (Contains(node.Children![i].Bounds, bounds))
            {
                Insert(index, bounds, node.Children[i], depth + 1);
                insertedToChild = true;
                break;
            }
        }

        if (!insertedToChild)
        {
            node.AddIndex(index);
        }
    }

    private void Split(OctreeNode node)
    {
        node.Children = ArrayPool<OctreeNode>.Shared.Rent(8);
        if (node.Children.Length > 8)
            Array.Clear(node.Children, 8, node.Children.Length - 8);
        Vector3 min = node.Bounds.Min;
        Vector3 max = node.Bounds.Max;
        Vector3 center = (min + max) * 0.5f;

        node.Children[0] = GetNode(new CullingBox(new Vector3(min.X, min.Y, min.Z), new Vector3(center.X, center.Y, center.Z)));
        node.Children[1] = GetNode(new CullingBox(new Vector3(center.X, min.Y, min.Z), new Vector3(max.X, center.Y, center.Z)));
        node.Children[2] = GetNode(new CullingBox(new Vector3(min.X, center.Y, min.Z), new Vector3(center.X, max.Y, center.Z)));
        node.Children[3] = GetNode(new CullingBox(new Vector3(center.X, center.Y, min.Z), new Vector3(max.X, max.Y, center.Z)));
        node.Children[4] = GetNode(new CullingBox(new Vector3(min.X, min.Y, center.Z), new Vector3(center.X, center.Y, max.Z)));
        node.Children[5] = GetNode(new CullingBox(new Vector3(center.X, min.Y, center.Z), new Vector3(max.X, center.Y, max.Z)));
        node.Children[6] = GetNode(new CullingBox(new Vector3(min.X, center.Y, center.Z), new Vector3(center.X, max.Y, max.Z)));
        node.Children[7] = GetNode(new CullingBox(new Vector3(center.X, center.Y, center.Z), new Vector3(max.X, max.Y, max.Z)));

        int[] oldIndices = node.ItemIndices;
        int oldCount = node.ItemCount;

        node.ItemIndices = Array.Empty<int>();
        node.ItemCount = 0;

        for (int j = 0; j < oldCount; j++)
        {
            int index = oldIndices[j];
            bool inserted = false;
            for (int i = 0; i < 8; i++)
            {
                if (Contains(node.Children[i].Bounds, _itemBounds[index]))
                {
                    node.Children[i].AddIndex(index);
                    inserted = true;
                    break;
                }
            }
            if (!inserted)
            {
                node.AddIndex(index);
            }
        }

        if (oldIndices.Length > 0)
        {
            ArrayPool<int>.Shared.Return(oldIndices);
        }
    }

    private bool Contains(CullingBox container, CullingBox content)
    {
        return container.Min.X <= content.Min.X && container.Min.Y <= content.Min.Y && container.Min.Z <= content.Min.Z &&
               container.Max.X >= content.Max.X && container.Max.Y >= content.Max.Y && container.Max.Z >= content.Max.Z;
    }

    public void GetVisibleItems(Frustum frustum, List<int> result, int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            if (_disableCulling[i])
            {
                result.Add(i);
            }
        }
        Query(_root, frustum, result);
    }

    private void Query(OctreeNode node, Frustum frustum, List<int> result)
    {
        if (!frustum.Intersects(node.Bounds)) return;

        for (int i = 0; i < node.ItemCount; i++)
        {
            int index = node.ItemIndices[i];
            if (!_disableCulling[index] && frustum.Intersects(_itemBounds[index]))
            {
                result.Add(index);
            }
        }

        if (node.Children != null)
        {
            for (int i = 0; i < 8; i++)
            {
                if (node.Children[i] != null)
                {
                    Query(node.Children[i], frustum, result);
                }
            }
        }
    }

    public void Clear()
    {
        ReturnNode(_root);
        _itemBounds = Array.Empty<CullingBox>();
        _disableCulling = Array.Empty<bool>();
    }
}