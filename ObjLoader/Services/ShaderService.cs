using System.IO;
using ObjLoader.Utilities;

namespace ObjLoader.Services
{
    public class ShaderService
    {
        public string LoadAndAdaptShader(string shaderFilePath)
        {
            if (string.IsNullOrEmpty(shaderFilePath) || !File.Exists(shaderFilePath)) return string.Empty;

            var converter = new HlslShaderConverter();
            var source = EncodingUtil.ReadAllText(shaderFilePath);
            return converter.Convert(source);
        }
    }
}