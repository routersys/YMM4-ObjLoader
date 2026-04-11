using ObjLoader.Core.Models;
using System.IO;

namespace ObjLoader.Cache.Extensions
{
    public interface IExtensionCacheProvider
    {
        string ProviderId { get; }
        bool HasExtensionData(ObjModel model);
        void WriteExtensionData(BinaryWriter bw, ObjModel model);
        void ReadExtensionData(BinaryReader br, ObjModel model);
    }
}