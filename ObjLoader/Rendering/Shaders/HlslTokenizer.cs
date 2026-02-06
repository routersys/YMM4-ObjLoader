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
        "matrix", "float4x4", "float3x3", "float2x2",
        "struct", "cbuffer", "tbuffer",
        "Texture1D", "Texture2D", "Texture3D", "TextureCube",
        "Texture1DArray", "Texture2DArray", "TextureCubeArray",
        "RWTexture1D", "RWTexture2D", "RWTexture3D",
        "SamplerState", "SamplerComparisonState",
        "Buffer", "StructuredBuffer", "RWBuffer", "RWStructuredBuffer",
        "void", "return", "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue",
        "const", "static", "uniform", "extern", "inline",
        "register", "packoffset",
        "in", "out", "inout",
        "true", "false"
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

        if (_position < _source.Length && char.IsAsciiLetter(_source[_position]))
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