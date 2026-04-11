using ObjLoader.Localization;

namespace ObjLoader.Rendering.Shaders.Exceptions;

public sealed class ShaderNotRecognizedException : ShaderConversionException
{
    public ShaderNotRecognizedException()
        : base(Texts.Shader_Status_UnsupportedFormat)
    {
    }
}