using ObjLoader.Rendering.Shaders;
using ObjLoader.Rendering.Shaders.Fx;
using ObjLoader.Rendering.Shaders.Interfaces;
using ObjLoader.Utilities;
using System.IO;

namespace ObjLoader.Services.Rendering
{
    public class ShaderService : IShaderLoader
    {
        string? IShaderLoader.Load(string path)
        {
            var source = LoadAndAdaptShader(path);
            return string.IsNullOrEmpty(source) ? null : source;
        }

        public string LoadAndAdaptShader(string shaderFilePath)
        {
            if (string.IsNullOrEmpty(shaderFilePath)) return string.Empty;
            if (!File.Exists(shaderFilePath)) return string.Empty;
            if (!ShaderConverterFactory.IsSupported(shaderFilePath)) return string.Empty;

            try
            {
                var source = EncodingUtil.ReadAllText(shaderFilePath);

                if (ShaderConverterFactory.IsFxFormat(shaderFilePath))
                    return ConvertFxShader(source, shaderFilePath);

                var converter = ShaderConverterFactory.CreateForFile(shaderFilePath);
                return converter is null ? string.Empty : converter.Convert(source);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ConvertFxShader(string source, string filePath)
        {
            var preprocessor = new FxPreprocessor();
            var baseDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var expandedSource = preprocessor.Process(source, baseDirectory);
            return new FxShaderConverter().Convert(expandedSource);
        }
    }
}