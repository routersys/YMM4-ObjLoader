using ObjLoader.Localization;

namespace ObjLoader.Rendering.Shaders;

public enum TokenType
{
    Keyword,
    Identifier,
    NumericLiteral,
    StringLiteral,
    Operator,
    Preprocessor,
    Attribute,
    EndOfFile
}

public sealed record Token(TokenType Type, string Text, int Line, int Column);

public sealed class HlslTokenizer
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "float", "float2", "float3", "float4",
        "int", "int2", "int3", "int4",
        "uint", "uint2", "uint3", "uint4",
        "bool", "bool2", "bool3", "bool4",
        "half", "half2", "half3", "half4",
        "double", "double2", "double3", "double4",
        "min16float", "min16float2", "min16float3", "min16float4",
        "min16int", "min16int2", "min16int3", "min16int4",
        "min16uint", "min16uint2", "min16uint3", "min16uint4",
        "min10float", "min10float2", "min10float3", "min10float4",
        "float16_t", "float16_t2", "float16_t3", "float16_t4",
        "int16_t", "int16_t2", "int16_t3", "int16_t4",
        "uint16_t", "uint16_t2", "uint16_t3", "uint16_t4",
        "int64_t", "uint64_t",
        "matrix", "float4x4", "float3x3", "float2x2",
        "float4x3", "float3x4", "float4x2", "float2x4", "float3x2", "float2x3",
        "struct", "class", "interface",
        "cbuffer", "tbuffer",
        "Texture1D", "Texture2D", "Texture3D", "TextureCube",
        "Texture1DArray", "Texture2DArray", "TextureCubeArray",
        "Texture2DMS", "Texture2DMSArray",
        "RWTexture1D", "RWTexture2D", "RWTexture3D",
        "RWTexture1DArray", "RWTexture2DArray",
        "SamplerState", "SamplerComparisonState",
        "Buffer", "StructuredBuffer", "RWBuffer", "RWStructuredBuffer",
        "ByteAddressBuffer", "RWByteAddressBuffer",
        "AppendStructuredBuffer", "ConsumeStructuredBuffer",
        "InputPatch", "OutputPatch",
        "PointStream", "LineStream", "TriangleStream",
        "RaytracingAccelerationStructure",
        "RayDesc", "RayQuery",
        "globallycoherent",
        "void", "return", "if", "else", "for", "while", "do",
        "switch", "case", "default", "break", "continue", "discard",
        "const", "static", "uniform", "extern", "inline",
        "volatile", "precise", "nointerpolation", "noperspective",
        "centroid", "sample", "linear",
        "groupshared",
        "register", "packoffset",
        "in", "out", "inout",
        "true", "false",
        "typedef",
        "namespace",
        "point", "line", "triangle", "lineadj", "triangleadj",
        "snorm", "unorm",
        "row_major", "column_major"
    };

    private static readonly HashSet<string> DoubleCharOperators = new(StringComparer.Ordinal)
    {
        "==", "!=", "<=", ">=", "&&", "||", "+=", "-=", "*=", "/=", "%=",
        "++", "--", "<<", ">>", "->", "::", "&=", "|=", "^="
    };

    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    public HlslTokenizer(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < _source.Length)
        {
            SkipWhitespace();
            if (_position >= _source.Length)
            {
                break;
            }

            var token = ReadNextToken();
            if (token is not null)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private Token? ReadNextToken()
    {
        if (_position >= _source.Length)
        {
            return null;
        }

        var startLine = _line;
        var startColumn = _column;
        char current = _source[_position];

        if (current == '/' && PeekChar() == '/')
        {
            SkipLineComment();
            return null;
        }

        if (current == '/' && PeekChar() == '*')
        {
            SkipBlockComment();
            return null;
        }

        if (current == '#')
        {
            return ReadPreprocessor(startLine, startColumn);
        }

        if (current == '[')
        {
            var attr = TryReadAttribute(startLine, startColumn);
            if (attr is not null)
            {
                return attr;
            }
        }

        if (current == '"')
        {
            return ReadStringLiteral(startLine, startColumn);
        }

        if (char.IsAsciiDigit(current) || (current == '.' && char.IsAsciiDigit(PeekChar())))
        {
            return ReadNumericLiteral(startLine, startColumn);
        }

        if (char.IsAsciiLetter(current) || current == '_')
        {
            return ReadIdentifierOrKeyword(startLine, startColumn);
        }

        return ReadOperatorOrPunctuation(startLine, startColumn);
    }

    private Token? TryReadAttribute(int line, int column)
    {
        var savedPosition = _position;
        var savedLine = _line;
        var savedColumn = _column;

        Advance();
        SkipWhitespace();

        if (_position >= _source.Length || (!char.IsAsciiLetter(_source[_position]) && _source[_position] != '_'))
        {
            _position = savedPosition;
            _line = savedLine;
            _column = savedColumn;
            return null;
        }

        var nameStart = _position;
        while (_position < _source.Length && IsIdentifierChar(_source[_position]))
        {
            Advance();
        }
        var name = _source[nameStart.._position];

        if (!IsKnownAttribute(name))
        {
            _position = savedPosition;
            _line = savedLine;
            _column = savedColumn;
            return null;
        }

        SkipWhitespace();

        var fullText = "[" + name;

        if (_position < _source.Length && _source[_position] == '(')
        {
            Advance();
            var depth = 1;
            var argsStart = _position;
            while (_position < _source.Length && depth > 0)
            {
                if (_source[_position] == '(') depth++;
                else if (_source[_position] == ')') depth--;
                if (depth > 0) Advance();
            }
            var args = _source[argsStart.._position];
            if (_position < _source.Length) Advance();
            fullText += "(" + args + ")";
        }

        SkipWhitespace();
        if (_position < _source.Length && _source[_position] == ']')
        {
            Advance();
        }

        fullText += "]";

        return new Token(TokenType.Attribute, fullText, line, column);
    }

    private static readonly HashSet<string> KnownAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "numthreads", "maxvertexcount", "domain", "partitioning",
        "outputtopology", "outputcontrolpoints", "patchconstantfunc",
        "maxtessfactor", "instance",
        "earlydepthstencil", "flatten", "branch", "unroll", "loop",
        "forcecase", "call",
        "fastopt", "allow_uav_condition",
        "WaveSize", "NodeDispatch", "NodeLaunch",
        "shader", "RootSignature"
    };

    private static bool IsKnownAttribute(string name)
    {
        return KnownAttributes.Contains(name);
    }

    private Token ReadIdentifierOrKeyword(int line, int column)
    {
        var start = _position;

        while (_position < _source.Length && IsIdentifierChar(_source[_position]))
        {
            Advance();
        }

        var text = _source[start.._position];
        var type = ReservedKeywords.Contains(text) ? TokenType.Keyword : TokenType.Identifier;
        return new Token(type, text, line, column);
    }

    private static bool IsIdentifierChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private Token ReadNumericLiteral(int line, int column)
    {
        var start = _position;

        if (_position + 1 < _source.Length &&
            _source[_position] == '0' &&
            char.ToLowerInvariant(_source[_position + 1]) == 'x')
        {
            Advance();
            Advance();
            while (_position < _source.Length && IsHexDigit(_source[_position]))
            {
                Advance();
            }
        }
        else if (_position + 1 < _source.Length &&
                 _source[_position] == '0' &&
                 char.ToLowerInvariant(_source[_position + 1]) == 'b')
        {
            Advance();
            Advance();
            while (_position < _source.Length && (_source[_position] == '0' || _source[_position] == '1'))
            {
                Advance();
            }
        }
        else
        {
            while (_position < _source.Length && char.IsAsciiDigit(_source[_position]))
            {
                Advance();
            }

            if (_position < _source.Length && _source[_position] == '.')
            {
                Advance();
                while (_position < _source.Length && char.IsAsciiDigit(_source[_position]))
                {
                    Advance();
                }
            }

            if (_position < _source.Length && char.ToLowerInvariant(_source[_position]) == 'e')
            {
                Advance();
                if (_position < _source.Length && (_source[_position] is '+' or '-'))
                {
                    Advance();
                }
                while (_position < _source.Length && char.IsAsciiDigit(_source[_position]))
                {
                    Advance();
                }
            }
        }

        while (_position < _source.Length && char.IsAsciiLetter(_source[_position]))
        {
            Advance();
        }

        var text = _source[start.._position];
        return new Token(TokenType.NumericLiteral, text, line, column);
    }

    private static bool IsHexDigit(char c) =>
        char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private Token ReadStringLiteral(int line, int column)
    {
        var start = _position;
        Advance();

        while (_position < _source.Length && _source[_position] != '"')
        {
            if (_source[_position] == '\\' && _position + 1 < _source.Length)
            {
                Advance();
            }
            Advance();
        }

        if (_position >= _source.Length)
        {
            throw new HlslParseException(
                Texts.ShaderParser_UnterminatedString,
                line,
                column);
        }

        Advance();
        var text = _source[start.._position];
        return new Token(TokenType.StringLiteral, text, line, column);
    }

    private Token ReadPreprocessor(int line, int column)
    {
        var start = _position;

        while (_position < _source.Length && _source[_position] != '\n')
        {
            if (_position + 1 < _source.Length &&
                _source[_position] == '\\' &&
                _source[_position + 1] == '\n')
            {
                Advance();
                Advance();
                continue;
            }
            Advance();
        }

        var text = _source[start.._position].Trim();
        return new Token(TokenType.Preprocessor, text, line, column);
    }

    private Token ReadOperatorOrPunctuation(int line, int column)
    {
        if (_position + 1 < _source.Length)
        {
            var twoChar = _source.Substring(_position, 2);
            if (DoubleCharOperators.Contains(twoChar))
            {
                Advance();
                Advance();
                return new Token(TokenType.Operator, twoChar, line, column);
            }
        }

        char current = _source[_position];
        Advance();
        return new Token(TokenType.Operator, current.ToString(), line, column);
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            Advance();
        }
    }

    private void SkipLineComment()
    {
        while (_position < _source.Length && _source[_position] != '\n')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        Advance();
        Advance();

        while (_position < _source.Length)
        {
            if (_position + 1 < _source.Length &&
                _source[_position] == '*' &&
                _source[_position + 1] == '/')
            {
                Advance();
                Advance();
                break;
            }
            Advance();
        }
    }

    private void Advance()
    {
        if (_position < _source.Length)
        {
            if (_source[_position] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private char PeekChar(int offset = 1)
    {
        var pos = _position + offset;
        return pos < _source.Length ? _source[pos] : '\0';
    }
}

public sealed class HlslParseException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public HlslParseException(string message, int line, int column)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    public HlslParseException(string message, int line, int column, Exception? innerException)
        : base(message, innerException)
    {
        Line = line;
        Column = column;
    }
}