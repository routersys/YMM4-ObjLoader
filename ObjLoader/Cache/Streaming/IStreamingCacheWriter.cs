using ObjLoader.Cache.Core;
using ObjLoader.Core.Models;
using System.Numerics;

namespace ObjLoader.Cache.Streaming
{
    public interface IStreamingCacheWriter : IDisposable
    {
        void WriteHeader(CacheHeader header);
        void WriteThumbnail(byte[] thumbnail);
        void WriteMetadata(int vertexCount, int indexCount, List<ModelPart> parts, Vector3 center, float scale);
        void WriteVertexChunk(ReadOnlySpan<byte> vertexData);
        void WriteIndexChunk(ReadOnlySpan<byte> indexData);
        void Commit();
        void Rollback();
    }
}