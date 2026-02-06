using ObjLoader.Localization;
using System.Text;

namespace ObjLoader.Rendering.Shaders;

public sealed class HlslParser
{
    private static readonly HashSet<string> TextureTypes = new(StringComparer.Ordinal)
    {
        "Texture1D", "Texture2D", "Texture3D", "TextureCube",
        "Texture1DArray", "Texture2DArray", "TextureCubeArray",
        "RWTexture1D", "RWTexture2D", "RWTexture3D"
    };

    private static readonly HashSet<string> ResourceTypes = new(StringComparer.Ordinal)
    {
        "SamplerState", "SamplerComparisonState",
        "Buffer", "StructuredBuffer", "RWBuffer", "RWStructuredBuffer"
    };

    private static readonly HashSet<string> TypeModifiers = new(StringComparer.Ordinal)
    {
        "const", "static", "uniform", "extern", "inline"
    };

    private static readonly HashSet<string> ParameterModifiers = new(StringComparer.Ordinal)
    {
        "in", "out", "inout"
    };

    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    public HlslParser(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokenizer = new HlslTokenizer(source);
        _tokens = tokenizer.Tokenize();
    }

    public ShaderAst Parse()
    {
        var ast = new ShaderAst();

        while (_position < _tokens.Count)
        {
            var token = Current();

            if (token.Type == TokenType.Preprocessor)
            {
                ast.AddPreprocessorDirective(token.Text);
                Advance();
                continue;
            }

            if (token.Type == TokenType.Keyword)
            {
                if (string.Equals(token.Text, "struct", StringComparison.Ordinal))
                {
                    ast.AddStructure(ParseStruct());
                    continue;
                }

                if (string.Equals(token.Text, "cbuffer", StringComparison.Ordinal) ||
                    string.Equals(token.Text, "tbuffer", StringComparison.Ordinal))
                {
                    ast.AddConstantBuffer(ParseConstantBuffer());
                    continue;
                }

                if (IsResourceType(token.Text))
                {
                    ast.AddGlobalVariable(ParseGlobalVariable());
                    continue;
                }

                if (!TryParseDeclaration(out var declaration))
                {
                    Advance();
                    continue;
                }

                AddDeclarationToAst(ast, declaration!);
                continue;
            }

            if (token.Type == TokenType.Identifier)
            {
                if (!TryParseDeclaration(out var declaration))
                {
                    Advance();
                    continue;
                }

                AddDeclarationToAst(ast, declaration!);
                continue;
            }

            Advance();
        }

        return ast;
    }

    private static void AddDeclarationToAst(ShaderAst ast, object declaration)
    {
        switch (declaration)
        {
            case FunctionDefinition func:
                ast.AddFunction(func);
                break;
            case VariableDeclaration var:
                ast.AddGlobalVariable(var);
                break;
        }
    }

    private bool TryParseDeclaration(out object? declaration)
    {
        var savedPosition = _position;
        try
        {
            declaration = ParseDeclaration();
            return true;
        }
        catch
        {
            _position = savedPosition;
            declaration = null;
            return false;
        }
    }

    private static bool IsResourceType(string text) =>
        TextureTypes.Contains(text) || ResourceTypes.Contains(text);

    private StructDefinition ParseStruct()
    {
        Expect("struct");
        var name = Expect(TokenType.Identifier).Text;
        Expect("{");

        var members = new List<VariableDeclaration>();
        while (!Check("}") && _position < _tokens.Count)
        {
            if (TryParseVariableDeclaration(out var member) && member is not null)
            {
                members.Add(member);
            }
            else
            {
                SkipToSemicolon();
            }
        }

        Expect("}");
        Expect(";");

        return new StructDefinition
        {
            Name = name,
            Members = members
        };
    }

    private bool TryParseVariableDeclaration(out VariableDeclaration? declaration)
    {
        var savedPosition = _position;
        try
        {
            declaration = ParseVariableDeclaration();
            return true;
        }
        catch
        {
            _position = savedPosition;
            declaration = null;
            return false;
        }
    }

    private ConstantBufferDefinition ParseConstantBuffer()
    {
        Advance();
        var name = Expect(TokenType.Identifier).Text;

        if (Check(":"))
        {
            ParseRegisterSpecification();
        }

        Expect("{");

        var body = new StringBuilder(256);
        var braceCount = 1;
        var lineBuilder = new StringBuilder();

        while (braceCount > 0 && _position < _tokens.Count)
        {
            var token = Current();

            if (string.Equals(token.Text, "{", StringComparison.Ordinal))
            {
                braceCount++;
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else if (string.Equals(token.Text, "}", StringComparison.Ordinal))
            {
                braceCount--;
                if (braceCount == 0)
                {
                    if (lineBuilder.Length > 0)
                    {
                        body.AppendLine(lineBuilder.ToString().Trim());
                    }
                    break;
                }
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else if (string.Equals(token.Text, ";", StringComparison.Ordinal))
            {
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else
            {
                if (lineBuilder.Length > 0)
                {
                    lineBuilder.Append(' ');
                }
                lineBuilder.Append(token.Text);
            }

            Advance();
        }

        Expect("}");
        Expect(";");

        return new ConstantBufferDefinition
        {
            Name = name,
            Body = body.ToString().Trim()
        };
    }

    private void ParseRegisterSpecification()
    {
        Advance();
        Expect("register");
        Expect("(");
        Expect(TokenType.Identifier);
        Expect(")");
    }

    private VariableDeclaration ParseVariableDeclaration()
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        type = ParseArraySpecification(type);

        string? semantic = null;
        if (Check(":"))
        {
            Advance();
            semantic = Expect(TokenType.Identifier).Text;
        }

        if (Check("="))
        {
            Advance();
            SkipToSemicolon();
        }
        else
        {
            Expect(";");
        }

        return new VariableDeclaration
        {
            Type = type,
            Name = name,
            Semantic = semantic
        };
    }

    private string ParseArraySpecification(string baseType)
    {
        if (!Check("["))
        {
            return baseType;
        }

        var type = new StringBuilder(baseType);
        type.Append('[');
        Advance();

        if (!Check("]"))
        {
            type.Append(Current().Text);
            Advance();
        }

        type.Append(']');
        Expect("]");

        return type.ToString();
    }

    private VariableDeclaration ParseGlobalVariable()
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        type = ParseArraySpecification(type);

        if (Check(":"))
        {
            ParseRegisterSpecification();
        }

        if (Check("="))
        {
            Advance();
            SkipToSemicolon();
        }
        else
        {
            Expect(";");
        }

        return new VariableDeclaration
        {
            Type = type,
            Name = name
        };
    }

    private object ParseDeclaration()
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        if (Check("("))
        {
            return ParseFunction(type, name);
        }

        type = ParseArraySpecification(type);

        if (Check("="))
        {
            Advance();
            SkipToSemicolon();
        }
        else
        {
            Expect(";");
        }

        return new VariableDeclaration
        {
            Type = type,
            Name = name
        };
    }

    private FunctionDefinition ParseFunction(string returnType, string name)
    {
        Expect("(");

        var parameters = ParseParameterList();

        Expect(")");

        string? returnSemantic = null;
        if (Check(":"))
        {
            Advance();
            returnSemantic = Expect(TokenType.Identifier).Text;
        }

        if (Check(";"))
        {
            Advance();
            return new FunctionDefinition
            {
                ReturnType = returnType,
                Name = name,
                Parameters = parameters,
                ReturnSemantic = returnSemantic,
                Body = string.Empty
            };
        }

        var body = ParseFunctionBody();

        return new FunctionDefinition
        {
            ReturnType = returnType,
            Name = name,
            Parameters = parameters,
            ReturnSemantic = returnSemantic,
            Body = body
        };
    }

    private List<ParameterDeclaration> ParseParameterList()
    {
        var parameters = new List<ParameterDeclaration>();

        while (!Check(")") && _position < _tokens.Count)
        {
            if (Check("void") && Peek(1)?.Text == ")")
            {
                Advance();
                break;
            }

            var modifier = string.Empty;
            if (ParameterModifiers.Contains(Current().Text))
            {
                modifier = Current().Text + " ";
                Advance();
            }

            var paramType = ParseType();
            var paramName = Expect(TokenType.Identifier).Text;

            paramType = ParseArraySpecification(paramType);

            string? semantic = null;
            if (Check(":"))
            {
                Advance();
                semantic = Expect(TokenType.Identifier).Text;
            }

            parameters.Add(new ParameterDeclaration
            {
                Type = modifier + paramType,
                Name = paramName,
                Semantic = semantic
            });

            if (Check(","))
            {
                Advance();
            }
        }

        return parameters;
    }

    private string ParseFunctionBody()
    {
        Expect("{");

        var body = new StringBuilder(512);
        var braceCount = 1;
        var lineBuilder = new StringBuilder();

        while (braceCount > 0 && _position < _tokens.Count)
        {
            var token = Current();

            if (string.Equals(token.Text, "{", StringComparison.Ordinal))
            {
                braceCount++;
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else if (string.Equals(token.Text, "}", StringComparison.Ordinal))
            {
                braceCount--;
                if (braceCount == 0)
                {
                    if (lineBuilder.Length > 0)
                    {
                        body.AppendLine(lineBuilder.ToString().Trim());
                    }
                    break;
                }
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else if (string.Equals(token.Text, ";", StringComparison.Ordinal))
            {
                lineBuilder.Append(token.Text);
                body.AppendLine(lineBuilder.ToString().Trim());
                lineBuilder.Clear();
            }
            else
            {
                if (lineBuilder.Length > 0 && ShouldAddSpace(token))
                {
                    lineBuilder.Append(' ');
                }
                lineBuilder.Append(token.Text);
            }

            Advance();
        }

        Expect("}");

        return body.ToString().Trim();
    }

    private static bool ShouldAddSpace(Token token)
    {
        if (token.Type == TokenType.Operator)
        {
            return string.Equals(token.Text, ",", StringComparison.Ordinal) ||
                   string.Equals(token.Text, ";", StringComparison.Ordinal);
        }
        return token.Type != TokenType.Operator;
    }

    private string ParseType()
    {
        var type = new StringBuilder(64);

        while (TypeModifiers.Contains(Current().Text))
        {
            type.Append(Current().Text);
            type.Append(' ');
            Advance();
        }

        var baseType = Current();
        if (baseType.Type is not TokenType.Keyword and not TokenType.Identifier)
        {
            throw new HlslParseException(
                string.Format(Texts.ShaderParser_ExpectedTypeButFoundText, baseType.Text),
                baseType.Line,
                baseType.Column);
        }

        type.Append(baseType.Text);
        Advance();

        if (Check("<"))
        {
            type.Append('<');
            Advance();
            type.Append(Current().Text);
            Advance();
            if (Check(">"))
            {
                type.Append('>');
                Advance();
            }
        }

        return type.ToString().Trim();
    }

    private void SkipToSemicolon()
    {
        var depth = 0;
        while (_position < _tokens.Count)
        {
            var text = Current().Text;

            if (string.Equals(text, "{", StringComparison.Ordinal))
            {
                depth++;
            }
            else if (string.Equals(text, "}", StringComparison.Ordinal))
            {
                depth--;
            }
            else if (string.Equals(text, ";", StringComparison.Ordinal) && depth == 0)
            {
                Advance();
                return;
            }

            Advance();
        }
    }

    private Token Current()
    {
        return _position < _tokens.Count
            ? _tokens[_position]
            : new Token(TokenType.EndOfFile, string.Empty, 0, 0);
    }

    private Token? Peek(int offset)
    {
        var pos = _position + offset;
        return pos >= 0 && pos < _tokens.Count ? _tokens[pos] : null;
    }

    private void Advance()
    {
        if (_position < _tokens.Count)
        {
            _position++;
        }
    }

    private bool Check(string text)
    {
        return _position < _tokens.Count &&
               string.Equals(_tokens[_position].Text, text, StringComparison.Ordinal);
    }

    private bool Check(TokenType type)
    {
        return _position < _tokens.Count && _tokens[_position].Type == type;
    }

    private Token Expect(string text)
    {
        var token = Current();
        if (!string.Equals(token.Text, text, StringComparison.Ordinal))
        {
            throw new HlslParseException(
                string.Format(Texts.ShaderParser_ExpectedTextFound, text, token.Text),
                token.Line,
                token.Column);
        }
        Advance();
        return token;
    }

    private Token Expect(params TokenType[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var token = Current();
        if (!Array.Exists(types, t => t == token.Type))
        {
            throw new HlslParseException(
                string.Format(Texts.ShaderParser_ExpectedTypeFound, string.Join(" or ", types), token.Type),
                token.Line,
                token.Column);
        }
        Advance();
        return token;
    }
}