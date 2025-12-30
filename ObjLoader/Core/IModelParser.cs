namespace ObjLoader.Core
{
    public interface IModelParser
    {
        bool CanParse(string extension);
        ObjModel Parse(string path);
    }
}