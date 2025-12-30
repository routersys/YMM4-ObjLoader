using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class PlyParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".ply";

        public ObjModel Parse(string path)
        {
            return new ObjModel();
        }
    }
}