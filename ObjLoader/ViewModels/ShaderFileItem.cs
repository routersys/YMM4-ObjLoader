using System.Windows.Media;
using ObjLoader.Localization;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Utilities;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels;

public sealed class ShaderFileItem : Bindable
{
    public string FileName { get; }
    public string FullPath { get; }
    public bool IsNone { get; }

    private Brush _statusColor;
    public Brush StatusColor
    {
        get => _statusColor;
        private set => Set(ref _statusColor, value);
    }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    private string _shortStatus;
    public string ShortStatus
    {
        get => _shortStatus;
        private set => Set(ref _shortStatus, value);
    }

    private string _detailedMessage;
    public string DetailedMessage
    {
        get => _detailedMessage;
        private set => Set(ref _detailedMessage, value);
    }

    private string _errorCategory;
    public string ErrorCategory
    {
        get => _errorCategory;
        private set => Set(ref _errorCategory, value);
    }

    private DateTime? _lastValidationTime;
    public DateTime? LastValidationTime
    {
        get => _lastValidationTime;
        private set => Set(ref _lastValidationTime, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        private set => Set(ref _hasError, value);
    }

    private string _codeSnippet;
    public string CodeSnippet
    {
        get => _codeSnippet;
        private set => Set(ref _codeSnippet, value);
    }

    private int? _errorLine;
    public int? ErrorLine
    {
        get => _errorLine;
        private set => Set(ref _errorLine, value);
    }

    private int? _errorColumn;
    public int? ErrorColumn
    {
        get => _errorColumn;
        private set => Set(ref _errorColumn, value);
    }

    public ShaderFileItem(string fileName, string fullPath, bool isNone = false)
    {
        FileName = fileName;
        FullPath = fullPath;
        IsNone = isNone;

        _statusColor = Brushes.Gray;
        _statusMessage = string.Empty;
        _shortStatus = string.Empty;
        _detailedMessage = string.Empty;
        _errorCategory = string.Empty;
        _codeSnippet = string.Empty;

        if (IsNone)
        {
            _statusColor = Brushes.Gray;
            _statusMessage = Texts.Shader_None;
            _shortStatus = Texts.Shader_None;
            _detailedMessage = Texts.Shader_None;
        }
        else
        {
            _statusColor = Brushes.Yellow;
            _statusMessage = Texts.Shader_Status_Unknown;
            _shortStatus = "⚠ " + Texts.Shader_Status_Unknown;
            _detailedMessage = Texts.Shader_Status_Unknown;
        }
    }

    public void Validate()
    {
        if (IsNone)
        {
            StatusColor = Brushes.Gray;
            StatusMessage = Texts.Shader_None;
            ShortStatus = Texts.Shader_None;
            DetailedMessage = Texts.Shader_None;
            ErrorCategory = string.Empty;
            HasError = false;
            LastValidationTime = null;
            CodeSnippet = string.Empty;
            ErrorLine = null;
            ErrorColumn = null;
            return;
        }

        if (string.IsNullOrEmpty(FullPath))
        {
            StatusColor = Brushes.Yellow;
            StatusMessage = Texts.Shader_Status_Unknown;
            ShortStatus = "⚠ " + Texts.Shader_Status_Unknown;
            DetailedMessage = Texts.Shader_Status_Unknown;
            ErrorCategory = string.Empty;
            HasError = false;
            LastValidationTime = DateTime.Now;
            CodeSnippet = string.Empty;
            ErrorLine = null;
            ErrorColumn = null;
            return;
        }

        try
        {
            var source = EncodingUtil.ReadAllText(FullPath);
            var converter = new HlslShaderConverter();
            string convertedSource;

            try
            {
                convertedSource = converter.Convert(source);
            }
            catch (ShaderConversionException ex)
            {
                HandleConversionError(ex, source);
                return;
            }

            var vsResult = ShaderStore.Compile(convertedSource, "VS", "vs_5_0");
            if (vsResult.Blob == null)
            {
                HandleCompilationError("VS", vsResult.Error!, convertedSource);
                return;
            }
            using var vsBlob = vsResult.Blob;

            var psResult = ShaderStore.Compile(convertedSource, "PS", "ps_5_0");
            if (psResult.Blob == null)
            {
                HandleCompilationError("PS", psResult.Error!, convertedSource);
                return;
            }
            using var psBlob = psResult.Blob;

            StatusColor = Brushes.LightGreen;
            StatusMessage = Texts.Shader_Status_Success;
            ShortStatus = "✓ " + Texts.Shader_Status_Success;
            DetailedMessage = Texts.Shader_Status_Success;
            ErrorCategory = string.Empty;
            HasError = false;
            LastValidationTime = DateTime.Now;
            CodeSnippet = string.Empty;
            ErrorLine = null;
            ErrorColumn = null;
        }
        catch (Exception ex)
        {
            HandleGeneralError(ex);
        }
    }

    private void HandleConversionError(ShaderConversionException ex, string source)
    {
        StatusColor = Brushes.Red;
        HasError = true;
        LastValidationTime = DateTime.Now;

        if (ex.InnerException is HlslParseException parseEx)
        {
            ErrorCategory = Texts.ShaderError_Category_Syntax;
            ErrorLine = parseEx.Line;
            ErrorColumn = parseEx.Column;

            var errorMsg = ex.Message;
            StatusMessage = string.Format(Texts.Shader_Status_Error, errorMsg);
            ShortStatus = "× " + Texts.ShaderError_Category_Syntax;

            var detailBuilder = new System.Text.StringBuilder();
            detailBuilder.AppendLine($"{Texts.ShaderError_Category_Syntax}");
            detailBuilder.AppendLine();
            detailBuilder.AppendLine($"{Texts.ShaderError_Location}: {Texts.ShaderError_Line} {parseEx.Line}, {Texts.ShaderError_Column} {parseEx.Column}");
            detailBuilder.AppendLine();
            detailBuilder.AppendLine($"{Texts.ShaderError_Message}:");
            detailBuilder.AppendLine(errorMsg);

            CodeSnippet = ExtractCodeSnippet(source, parseEx.Line);
            if (!string.IsNullOrEmpty(CodeSnippet))
            {
                detailBuilder.AppendLine();
                detailBuilder.AppendLine($"{Texts.ShaderError_CodeContext}:");
                detailBuilder.AppendLine(CodeSnippet);
            }

            DetailedMessage = detailBuilder.ToString();
        }
        else
        {
            ErrorCategory = Texts.ShaderError_Category_Conversion;
            ErrorLine = null;
            ErrorColumn = null;

            StatusMessage = string.Format(Texts.Shader_Status_Error, ex.Message);
            ShortStatus = "× " + Texts.ShaderError_Category_Conversion;

            var detailBuilder = new System.Text.StringBuilder();
            detailBuilder.AppendLine($"{Texts.ShaderError_Category_Conversion}");
            detailBuilder.AppendLine();
            detailBuilder.AppendLine($"{Texts.ShaderError_Message}:");
            detailBuilder.AppendLine(ex.Message);

            DetailedMessage = detailBuilder.ToString();
            CodeSnippet = string.Empty;
        }
    }

    private void HandleCompilationError(string shaderType, string error, string source)
    {
        StatusColor = Brushes.Red;
        HasError = true;
        LastValidationTime = DateTime.Now;
        ErrorCategory = Texts.ShaderError_Category_Compilation;

        var errorMsg = $"{shaderType}: {error}";
        StatusMessage = string.Format(Texts.Shader_Status_Error, errorMsg);
        ShortStatus = "× " + Texts.ShaderError_Category_Compilation;

        var lineMatch = System.Text.RegularExpressions.Regex.Match(error, @"\((\d+),(\d+)");
        if (lineMatch.Success)
        {
            ErrorLine = int.Parse(lineMatch.Groups[1].Value);
            ErrorColumn = int.Parse(lineMatch.Groups[2].Value);
            CodeSnippet = ExtractCodeSnippet(source, ErrorLine.Value);
        }
        else
        {
            ErrorLine = null;
            ErrorColumn = null;
            CodeSnippet = string.Empty;
        }

        var detailBuilder = new System.Text.StringBuilder();
        detailBuilder.AppendLine($"{Texts.ShaderError_Category_Compilation} ({shaderType})");
        detailBuilder.AppendLine();

        if (ErrorLine.HasValue && ErrorColumn.HasValue)
        {
            detailBuilder.AppendLine($"{Texts.ShaderError_Location}: {Texts.ShaderError_Line} {ErrorLine}, {Texts.ShaderError_Column} {ErrorColumn}");
            detailBuilder.AppendLine();
        }

        detailBuilder.AppendLine($"{Texts.ShaderError_Message}:");
        detailBuilder.AppendLine(error);

        if (!string.IsNullOrEmpty(CodeSnippet))
        {
            detailBuilder.AppendLine();
            detailBuilder.AppendLine($"{Texts.ShaderError_CodeContext}:");
            detailBuilder.AppendLine(CodeSnippet);
        }

        DetailedMessage = detailBuilder.ToString();
    }

    private void HandleGeneralError(Exception ex)
    {
        StatusColor = Brushes.Red;
        HasError = true;
        LastValidationTime = DateTime.Now;
        ErrorCategory = Texts.ShaderError_Category_General;
        ErrorLine = null;
        ErrorColumn = null;
        CodeSnippet = string.Empty;

        StatusMessage = string.Format(Texts.Shader_Status_Error, ex.Message);
        ShortStatus = "× " + Texts.ShaderError_Category_General;

        var detailBuilder = new System.Text.StringBuilder();
        detailBuilder.AppendLine($"{Texts.ShaderError_Category_General}");
        detailBuilder.AppendLine();
        detailBuilder.AppendLine($"{Texts.ShaderError_Message}:");
        detailBuilder.AppendLine(ex.Message);

        DetailedMessage = detailBuilder.ToString();
    }

    private static string ExtractCodeSnippet(string source, int lineNumber)
    {
        try
        {
            var normalizedSource = source.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalizedSource.Split('\n');

            if (lineNumber < 1 || lineNumber > lines.Length)
            {
                return string.Empty;
            }

            var startLine = Math.Max(1, lineNumber - 2);
            var endLine = Math.Min(lines.Length, lineNumber + 2);
            var snippet = new System.Text.StringBuilder();

            for (int i = startLine; i <= endLine; i++)
            {
                var marker = i == lineNumber ? "→ " : "  ";
                var line = i <= lines.Length ? lines[i - 1] : string.Empty;
                snippet.AppendLine($"{marker}{i,4}: {line.TrimEnd()}");
            }

            return snippet.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}