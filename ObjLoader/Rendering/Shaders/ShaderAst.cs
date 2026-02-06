namespace ObjLoader.Rendering.Shaders;

public sealed class ShaderAst
{
    public IReadOnlyList<string> PreprocessorDirectives => _preprocessorDirectives;
    public IReadOnlyList<StructDefinition> Structures => _structures;
    public IReadOnlyList<ConstantBufferDefinition> ConstantBuffers => _constantBuffers;
    public IReadOnlyList<VariableDeclaration> GlobalVariables => _globalVariables;
    public IReadOnlyList<FunctionDefinition> Functions => _functions;

    private readonly List<string> _preprocessorDirectives = new();
    private readonly List<StructDefinition> _structures = new();
    private readonly List<ConstantBufferDefinition> _constantBuffers = new();
    private readonly List<VariableDeclaration> _globalVariables = new();
    private readonly List<FunctionDefinition> _functions = new();

    internal void AddPreprocessorDirective(string directive)
    {
        ArgumentNullException.ThrowIfNull(directive);
        _preprocessorDirectives.Add(directive);
    }

    internal void AddStructure(StructDefinition structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        _structures.Add(structure);
    }

    internal void AddConstantBuffer(ConstantBufferDefinition buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _constantBuffers.Add(buffer);
    }

    internal void AddGlobalVariable(VariableDeclaration variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        _globalVariables.Add(variable);
    }

    internal void AddFunction(FunctionDefinition function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _functions.Add(function);
    }

    public bool HasFunction(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _functions.Exists(f => string.Equals(f.Name, name, StringComparison.Ordinal));
    }

    public bool HasConstantBuffer(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _constantBuffers.Exists(cb => string.Equals(cb.Name, name, StringComparison.Ordinal));
    }

    public bool HasTexture(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _globalVariables.Exists(v =>
            string.Equals(v.Name, name, StringComparison.Ordinal) &&
            v.Type.Contains("Texture", StringComparison.Ordinal));
    }

    public bool HasStruct(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _structures.Exists(s => string.Equals(s.Name, name, StringComparison.Ordinal));
    }
}

public sealed record StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<VariableDeclaration> Members { get; init; }

    public StructDefinition()
    {
        Name = string.Empty;
        Members = Array.Empty<VariableDeclaration>();
    }
}

public sealed record ConstantBufferDefinition
{
    public required string Name { get; init; }
    public required string Body { get; init; }

    public ConstantBufferDefinition()
    {
        Name = string.Empty;
        Body = string.Empty;
    }
}

public sealed record VariableDeclaration
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? Semantic { get; init; }

    public VariableDeclaration()
    {
        Type = string.Empty;
        Name = string.Empty;
    }
}

public sealed record ParameterDeclaration
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? Semantic { get; init; }

    public ParameterDeclaration()
    {
        Type = string.Empty;
        Name = string.Empty;
    }
}

public sealed record FunctionDefinition
{
    public required string ReturnType { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<ParameterDeclaration> Parameters { get; init; }
    public string? ReturnSemantic { get; init; }
    public required string Body { get; init; }

    public FunctionDefinition()
    {
        ReturnType = string.Empty;
        Name = string.Empty;
        Parameters = Array.Empty<ParameterDeclaration>();
        Body = string.Empty;
    }
}