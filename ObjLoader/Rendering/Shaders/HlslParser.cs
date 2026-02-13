using ObjLoader.Localization;
using System.Text;

namespace ObjLoader.Rendering.Shaders;

public sealed class HlslParser
{
    private static readonly HashSet<string> TextureTypes = new(StringComparer.Ordinal)
    {
        "Texture1D", "Texture2D", "Texture3D", "TextureCube",
        "Texture1DArray", "Texture2DArray", "TextureCubeArray",
        "Texture2DMS", "Texture2DMSArray",
        "RWTexture1D", "RWTexture2D", "RWTexture3D",
        "RWTexture1DArray", "RWTexture2DArray"
    };

    private static readonly HashSet<string> ResourceTypes = new(StringComparer.Ordinal)
    {
        "SamplerState", "SamplerComparisonState",
        "Buffer", "StructuredBuffer", "RWBuffer", "RWStructuredBuffer",
        "ByteAddressBuffer", "RWByteAddressBuffer",
        "AppendStructuredBuffer", "ConsumeStructuredBuffer",
        "RaytracingAccelerationStructure"
    };

    private static readonly HashSet<string> TypeModifiers = new(StringComparer.Ordinal)
    {
        "const", "static", "uniform", "extern", "inline",
        "volatile", "precise", "nointerpolation", "noperspective",
        "centroid", "sample", "linear", "globallycoherent",
        "snorm", "unorm", "row_major", "column_major"
    };

    private static readonly HashSet<string> ParameterModifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "in", "out", "inout",
        "point", "line", "triangle", "lineadj", "triangleadj"
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
        var pendingAttributes = new List<AttributeDefinition>();

        while (_position < _tokens.Count)
        {
            var token = Current();

            if (token.Type == TokenType.Preprocessor)
            {
                ast.AddPreprocessorDirective(token.Text);
                Advance();
                continue;
            }

            if (token.Type == TokenType.Attribute)
            {
                pendingAttributes.Add(ParseAttributeFromToken(token));
                Advance();
                continue;
            }

            if (token.Type == TokenType.Keyword)
            {
                if (string.Equals(token.Text, "struct", StringComparison.Ordinal))
                {
                    pendingAttributes.Clear();
                    ast.AddStructure(ParseStruct());
                    continue;
                }

                if (string.Equals(token.Text, "typedef", StringComparison.Ordinal))
                {
                    pendingAttributes.Clear();
                    ast.AddTypedef(ParseTypedef());
                    continue;
                }

                if (string.Equals(token.Text, "cbuffer", StringComparison.Ordinal) ||
                    string.Equals(token.Text, "tbuffer", StringComparison.Ordinal))
                {
                    pendingAttributes.Clear();
                    ast.AddConstantBuffer(ParseConstantBuffer());
                    continue;
                }

                if (IsResourceType(token.Text))
                {
                    pendingAttributes.Clear();
                    ast.AddGlobalVariable(ParseGlobalVariable());
                    continue;
                }

                if (!TryParseDeclaration(pendingAttributes, out var declaration))
                {
                    pendingAttributes.Clear();
                    Advance();
                    continue;
                }

                pendingAttributes.Clear();
                AddDeclarationToAst(ast, declaration!);
                continue;
            }

            if (token.Type == TokenType.Identifier)
            {
                if (!TryParseDeclaration(pendingAttributes, out var declaration))
                {
                    pendingAttributes.Clear();
                    Advance();
                    continue;
                }

                pendingAttributes.Clear();
                AddDeclarationToAst(ast, declaration!);
                continue;
            }

            pendingAttributes.Clear();
            Advance();
        }

        return ast;
    }

    private static AttributeDefinition ParseAttributeFromToken(Token token)
    {
        var text = token.Text;
        if (text.StartsWith('[')) text = text[1..];
        if (text.EndsWith(']')) text = text[..^1];

        var parenIndex = text.IndexOf('(');
        if (parenIndex >= 0)
        {
            var name = text[..parenIndex].Trim();
            var args = text[(parenIndex + 1)..];
            if (args.EndsWith(')')) args = args[..^1];
            return new AttributeDefinition { Name = name, Arguments = args.Trim() };
        }

        return new AttributeDefinition { Name = text.Trim(), Arguments = string.Empty };
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

    private bool TryParseDeclaration(List<AttributeDefinition> attributes, out object? declaration)
    {
        var savedPosition = _position;
        try
        {
            declaration = ParseDeclaration(attributes);
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

    private TypedefDeclaration ParseTypedef()
    {
        Expect("typedef");
        var originalType = ParseType();
        var aliasName = Expect(TokenType.Identifier).Text;
        Expect(";");

        return new TypedefDeclaration
        {
            OriginalType = originalType,
            AliasName = aliasName
        };
    }

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

    private string? ParseRegisterSpecification()
    {
        Advance();
        Expect("register");
        Expect("(");
        var reg = Expect(TokenType.Identifier).Text;
        if (Check(","))
        {
            Advance();
            Expect(TokenType.Identifier);
        }
        Expect(")");
        return reg;
    }

    private VariableDeclaration ParseVariableDeclaration()
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        name += ParseArraySpecification();

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

    private string ParseArraySpecification()
    {
        var sb = new StringBuilder();

        while (Check("["))
        {
            sb.Append('[');
            Advance();

            while (!Check("]") && _position < _tokens.Count)
            {
                sb.Append(Current().Text);
                Advance();
            }

            sb.Append(']');
            Expect("]");
        }

        return sb.ToString();
    }

    private VariableDeclaration ParseGlobalVariable()
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        name += ParseArraySpecification();

        string? registerSlot = null;
        if (Check(":"))
        {
            registerSlot = ParseRegisterSpecification();
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
            RegisterSlot = registerSlot
        };
    }

    private object ParseDeclaration(List<AttributeDefinition>? attributes = null)
    {
        var type = ParseType();
        var name = Expect(TokenType.Identifier).Text;

        if (Check("("))
        {
            return ParseFunction(type, name, attributes);
        }

        name += ParseArraySpecification();

        string? registerSlot = null;
        if (Check(":"))
        {
            var savedPos = _position;
            Advance();
            if (Check("register"))
            {
                _position = savedPos;
                registerSlot = ParseRegisterSpecification();
            }
            else
            {
                _position = savedPos;
            }
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
            RegisterSlot = registerSlot
        };
    }

    private FunctionDefinition ParseFunction(string returnType, string name, List<AttributeDefinition>? attributes)
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
                Body = string.Empty,
                Attributes = attributes?.ToArray() ?? Array.Empty<AttributeDefinition>()
            };
        }

        var body = ParseFunctionBody();

        return new FunctionDefinition
        {
            ReturnType = returnType,
            Name = name,
            Parameters = parameters,
            ReturnSemantic = returnSemantic,
            Body = body,
            Attributes = attributes?.ToArray() ?? Array.Empty<AttributeDefinition>()
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

            var modifier = new StringBuilder();
            while (ParameterModifiers.Contains(Current().Text))
            {
                if (modifier.Length > 0) modifier.Append(' ');
                modifier.Append(Current().Text);
                Advance();
            }

            var paramType = ParseType();
            var paramName = Expect(TokenType.Identifier).Text;

            paramName += ParseArraySpecification();

            string? semantic = null;
            if (Check(":"))
            {
                Advance();
                semantic = Expect(TokenType.Identifier).Text;
            }

            var fullType = modifier.Length > 0 ? modifier + " " + paramType : paramType;

            parameters.Add(new ParameterDeclaration
            {
                Type = fullType,
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
            var depth = 1;
            while (depth > 0 && _position < _tokens.Count)
            {
                if (Check("<"))
                {
                    depth++;
                    type.Append('<');
                    Advance();
                }
                else if (Check(">"))
                {
                    depth--;
                    type.Append('>');
                    Advance();
                }
                else
                {
                    if (Check(","))
                    {
                        type.Append(", ");
                    }
                    else
                    {
                        type.Append(Current().Text);
                    }
                    Advance();
                }
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