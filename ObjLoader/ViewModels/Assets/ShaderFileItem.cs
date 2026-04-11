using ObjLoader.Localization;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Rendering.Shaders.Exceptions;
using ObjLoader.Services.Rendering;
using ObjLoader.Utilities;
using System.IO;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Assets
{
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

        private static readonly System.Text.RegularExpressions.Regex _errorRegex = new(@"\((\d+),(\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly ShaderService _shaderService = new();

        private void DispatchUI(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public async Task ValidateAsync()
        {
            if (IsNone)
            {
                DispatchUI(() =>
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
                });
                return;
            }

            if (string.IsNullOrEmpty(FullPath))
            {
                DispatchUI(() =>
                {
                    StatusColor = Brushes.Yellow;
                    StatusMessage = Texts.Shader_Status_Unknown;
                    ShortStatus = "⚠ " + Texts.Shader_Status_Unknown;
                    DetailedMessage = Texts.Shader_Status_Unknown;
                    ErrorCategory = string.Empty;
                    HasError = false;
                    LastValidationTime = null;
                    CodeSnippet = string.Empty;
                    ErrorLine = null;
                    ErrorColumn = null;
                });
                return;
            }

            if (!File.Exists(FullPath))
            {
                DispatchUI(() =>
                {
                    StatusColor = Brushes.Gray;
                    StatusMessage = Texts.Shader_Status_Unknown;
                    ShortStatus = "- " + Texts.Shader_Status_Unknown;
                    DetailedMessage = Texts.Shader_Status_Unknown;
                    ErrorCategory = string.Empty;
                    HasError = false;
                    LastValidationTime = DateTime.Now;
                    CodeSnippet = string.Empty;
                    ErrorLine = null;
                    ErrorColumn = null;
                });
                return;
            }

            if (!ShaderConverterFactory.IsSupported(FullPath))
            {
                HandleUnsupportedFormatError();
                return;
            }

            try
            {
                string convertedSource;

                if (ShaderConverterFactory.IsFxFormat(FullPath))
                {
                    convertedSource = await Task.Run(() => _shaderService.LoadAndAdaptShader(FullPath)).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(convertedSource))
                    {
                        HandleUnsupportedFormatError();
                        return;
                    }
                }
                else
                {
                    var source = await Task.Run(() => EncodingUtil.ReadAllText(FullPath)).ConfigureAwait(false);
                    var converter = new HlslShaderConverter();
                    try
                    {
                        convertedSource = await Task.Run(() => converter.Convert(source)).ConfigureAwait(false);
                    }
                    catch (ShaderNotRecognizedException)
                    {
                        HandleUnsupportedFormatError();
                        return;
                    }
                    catch (ShaderConversionException ex)
                    {
                        HandleConversionError(ex, source);
                        return;
                    }
                }

                var vsResult = await Task.Run(() => ShaderStore.Compile(convertedSource, "VS", "vs_5_0")).ConfigureAwait(false);
                if (vsResult.ByteCode == null)
                {
                    HandleCompilationError("VS", vsResult.Error!, convertedSource);
                    return;
                }

                var psResult = await Task.Run(() => ShaderStore.Compile(convertedSource, "PS", "ps_5_0")).ConfigureAwait(false);
                if (psResult.ByteCode == null)
                {
                    HandleCompilationError("PS", psResult.Error!, convertedSource);
                    return;
                }

                DispatchUI(() =>
                {
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
                });
            }
            catch (Exception ex)
            {
                HandleGeneralError(ex);
            }
        }

        private void HandleUnsupportedFormatError()
        {
            DispatchUI(() =>
            {
                StatusColor = Brushes.Orange;
                StatusMessage = Texts.Shader_Status_UnsupportedFormat;
                ShortStatus = "⚠ " + Texts.Shader_Status_UnsupportedFormat;
                DetailedMessage = Texts.Shader_Status_UnsupportedFormat;
                ErrorCategory = string.Empty;
                HasError = false;
                LastValidationTime = DateTime.Now;
                CodeSnippet = string.Empty;
                ErrorLine = null;
                ErrorColumn = null;
            });
        }

        private void HandleConversionError(ShaderConversionException ex, string source)
        {
            DispatchUI(() =>
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
            });
        }

        private void HandleCompilationError(string shaderType, string error, string source)
        {
            DispatchUI(() =>
            {
                StatusColor = Brushes.Red;
                HasError = true;
                LastValidationTime = DateTime.Now;
                ErrorCategory = string.Format(Texts.ShaderError_Category_Compilation, shaderType);

                var lineMatch = _errorRegex.Match(error);
                if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int line))
                {
                    ErrorLine = line;
                    ErrorColumn = int.TryParse(lineMatch.Groups[2].Value, out int col) ? col : null;
                    CodeSnippet = ExtractCodeSnippet(source, line);
                }
                else
                {
                    ErrorLine = null;
                    ErrorColumn = null;
                    CodeSnippet = string.Empty;
                }

                StatusMessage = string.Format(Texts.Shader_Status_Error, error);
                ShortStatus = "× " + ErrorCategory;

                var detailBuilder = new System.Text.StringBuilder();
                detailBuilder.AppendLine($"{ErrorCategory}");
                detailBuilder.AppendLine();

                if (ErrorLine.HasValue)
                {
                    detailBuilder.AppendLine($"{Texts.ShaderError_Location}: {Texts.ShaderError_Line} {ErrorLine}, {Texts.ShaderError_Column} {ErrorColumn?.ToString() ?? "?"}");
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
            });
        }

        private void HandleGeneralError(Exception ex)
        {
            DispatchUI(() =>
            {
                StatusColor = Brushes.Red;
                StatusMessage = string.Format(Texts.Shader_Status_Error, ex.Message);
                ShortStatus = "× " + Texts.ShaderError_Category_General;
                ErrorCategory = Texts.ShaderError_Category_General;
                HasError = true;
                LastValidationTime = DateTime.Now;
                CodeSnippet = string.Empty;
                ErrorLine = null;
                ErrorColumn = null;

                var detailBuilder = new System.Text.StringBuilder();
                detailBuilder.AppendLine($"{Texts.ShaderError_Category_General}");
                detailBuilder.AppendLine();
                detailBuilder.AppendLine($"{Texts.ShaderError_Message}:");
                detailBuilder.AppendLine(ex.ToString());

                DetailedMessage = detailBuilder.ToString();
            });
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
}