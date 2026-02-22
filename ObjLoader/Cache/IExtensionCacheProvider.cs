using System.IO;
using ObjLoader.Core.Models;

namespace ObjLoader.Cache
{
    public interface IExtensionCacheProvider
    {
        string ProviderId { get; }
        bool HasExtensionData(ObjModel model);
        void WriteExtensionData(BinaryWriter bw, ObjModel model);
        void ReadExtensionData(BinaryReader br, ObjModel model);
    }
}