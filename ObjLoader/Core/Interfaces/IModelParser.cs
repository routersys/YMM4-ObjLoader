using ObjLoader.Core.Models;

namespace ObjLoader.Core.Interfaces
{
    public interface IModelParser
    {
        bool CanParse(string extension);
        ObjModel Parse(string path);
    }
}