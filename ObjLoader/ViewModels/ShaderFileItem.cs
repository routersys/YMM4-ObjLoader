using System.Windows.Media;
using Vortice.D3DCompiler;
using ObjLoader.Localization;
using ObjLoader.Rendering;
using ObjLoader.Services;
using ObjLoader.Utilities;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class ShaderFileItem : Bindable
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

        public ShaderFileItem(string fileName, string fullPath, bool isNone = false)
        {
            FileName = fileName;
            FullPath = fullPath;
            IsNone = isNone;

            if (IsNone)
            {
                _statusColor = Brushes.Gray;
                _statusMessage = Texts.Shader_None;
            }
            else
            {
                _statusColor = Brushes.Yellow;
                _statusMessage = Texts.Shader_Status_Unknown;
            }
        }

        public void Validate()
        {
            if (IsNone)
            {
                StatusColor = Brushes.Gray;
                StatusMessage = Texts.Shader_None;
                return;
            }

            if (string.IsNullOrEmpty(FullPath))
            {
                StatusColor = Brushes.Yellow;
                StatusMessage = Texts.Shader_Status_Unknown;
                return;
            }

            try
            {
                var source = EncodingUtil.ReadAllText(FullPath);
                var converter = new HlslShaderConverter();
                var convertedSource = converter.Convert(source);

                var vsResult = ShaderStore.Compile(convertedSource, "VS", "vs_5_0");
                if (vsResult.Blob == null)
                {
                    StatusColor = Brushes.Red;
                    StatusMessage = string.Format(Texts.Shader_Status_Error, "VS: " + vsResult.Error);
                    return;
                }
                using var vsBlob = vsResult.Blob;

                var psResult = ShaderStore.Compile(convertedSource, "PS", "ps_5_0");
                if (psResult.Blob == null)
                {
                    StatusColor = Brushes.Red;
                    StatusMessage = string.Format(Texts.Shader_Status_Error, "PS: " + psResult.Error);
                    return;
                }
                using var psBlob = psResult.Blob;

                StatusColor = Brushes.LightGreen;
                StatusMessage = Texts.Shader_Status_Success;
            }
            catch (System.Exception ex)
            {
                StatusColor = Brushes.Red;
                StatusMessage = string.Format(Texts.Shader_Status_Error, ex.Message);
            }
        }
    }
}