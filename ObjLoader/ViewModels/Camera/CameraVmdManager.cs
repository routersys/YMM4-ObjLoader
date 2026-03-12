using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;
using Microsoft.Win32;
using ObjLoader.Localization;
using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;

namespace ObjLoader.ViewModels.Camera;

internal class CameraVmdManager(
    ObjLoaderParameter parameter,
    ObservableCollection<CameraKeyframe> keyframes,
    Action<double> setMaxDuration,
    Action<double> setCurrentTime,
    Action updateAnimation)
{
    public bool IsSelectedLayerPmx()
    {
        if (parameter.SelectedLayerIndex < 0 || parameter.SelectedLayerIndex >= parameter.Layers.Count) return false;
        var layer = parameter.Layers[parameter.SelectedLayerIndex];
        if (string.IsNullOrEmpty(layer.FilePath)) return false;
        return Path.GetExtension(layer.FilePath).Equals(".pmx", StringComparison.OrdinalIgnoreCase);
    }

    public void LoadVmdMotion(EventHandler<string>? onNotification)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Texts.Msg_VmdFileFilter}|*.vmd",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var vmdData = VmdParser.Parse(dialog.FileName);
            var layer = parameter.Layers[parameter.SelectedLayerIndex];

            layer.VmdMotionData = vmdData;
            layer.VmdFilePath = dialog.FileName;
            layer.VmdTimeOffset = 0;

            if (vmdData.BoneFrames.Count > 0)
            {
                var model = new Parsers.PmxParser().Parse(layer.FilePath);
                if (model.Bones.Count > 0)
                {
                    layer.BoneAnimatorInstance = new BoneAnimator(
                        model.Bones, vmdData.BoneFrames,
                        model.RigidBodies, model.Joints);
                }
            }

            if (vmdData.CameraFrames.Count > 0)
            {
                var (modelCenter, modelScale) = LoadModelNormalization(layer.FilePath);
                ApplyCameraFrames(vmdData, modelCenter, modelScale);
            }

            int totalFrames = vmdData.CameraFrames.Count + vmdData.BoneFrames.Count;
            onNotification?.Invoke(this, string.Format(Texts.Msg_VmdLoadSuccess, totalFrames));
        }
        catch (Exception ex)
        {
            onNotification?.Invoke(this, string.Format(Texts.Msg_VmdLoadFailed, ex.Message));
        }
    }

    public void LoadCameraVmd(EventHandler<string>? onNotification)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Texts.Msg_VmdFileFilter}|*.vmd",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var vmdData = VmdParser.Parse(dialog.FileName);

            if (vmdData.CameraFrames.Count == 0)
            {
                onNotification?.Invoke(this, Texts.Msg_CameraVmdNoCameraFrames);
                return;
            }

            var layer = parameter.Layers[parameter.SelectedLayerIndex];
            var (modelCenter, modelScale) = LoadModelNormalization(layer.FilePath);
            ApplyCameraFrames(vmdData, modelCenter, modelScale);
            onNotification?.Invoke(this, string.Format(Texts.Msg_CameraVmdLoadSuccess, vmdData.CameraFrames.Count));
        }
        catch (Exception ex)
        {
            onNotification?.Invoke(this, string.Format(Texts.Msg_CameraVmdLoadFailed, ex.Message));
        }
    }

    public void ResetModelVmd(EventHandler<string>? onNotification)
    {
        if (parameter.SelectedLayerIndex < 0 || parameter.SelectedLayerIndex >= parameter.Layers.Count) return;
        var layer = parameter.Layers[parameter.SelectedLayerIndex];
        layer.VmdMotionData = null;
        layer.BoneAnimatorInstance = null;
        layer.VmdFilePath = string.Empty;
        layer.VmdTimeOffset = 0;
        onNotification?.Invoke(this, Texts.Msg_ModelVmdReset);
    }

    public void ResetCameraVmd(EventHandler<string>? onNotification)
    {
        keyframes.Clear();
        parameter.Keyframes = [];
        setCurrentTime(0);
        updateAnimation();
        onNotification?.Invoke(this, Texts.Msg_CameraVmdReset);
    }

    private static (Vector3 modelCenter, float modelScale) LoadModelNormalization(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return (Vector3.Zero, 1.0f);

        try
        {
            var loader = new Parsers.ObjModelLoader();
            var model = loader.Load(filePath);
            return (model.ModelCenter, model.ModelScale);
        }
        catch
        {
            return (Vector3.Zero, 1.0f);
        }
    }

    private void ApplyCameraFrames(VmdData vmdData, Vector3 modelCenter, float modelScale)
    {
        var newKeyframes = VmdMotionApplier.ConvertCameraFrames(vmdData, modelCenter, modelScale)
            .OrderBy(k => k.Time)
            .ToList();

        keyframes.Clear();
        foreach (var kf in newKeyframes) keyframes.Add(kf);
        parameter.Keyframes = [.. keyframes];

        double duration = VmdMotionApplier.GetDuration(vmdData);
        if (duration > 0) setMaxDuration(duration);

        setCurrentTime(0);
        updateAnimation();
    }
}