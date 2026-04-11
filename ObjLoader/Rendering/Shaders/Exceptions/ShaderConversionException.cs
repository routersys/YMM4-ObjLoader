using ObjLoader.Localization;

namespace ObjLoader.Rendering.Shaders.Exceptions;

public class ShaderConversionException : Exception
{
    public ShaderConversionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(Texts.ShaderConversion_ArgumentNull, nameof(message));
        }
    }
}