namespace ObjLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class ModelParserAttribute : Attribute
    {
        public string[] Extensions { get; }
        public int Version { get; }

        public ModelParserAttribute(int version, params string[] extensions)
        {
            Version = version;
            Extensions = extensions ?? Array.Empty<string>();
        }
    }
}