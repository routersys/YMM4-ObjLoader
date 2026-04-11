using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Views.Dialogs;
using System.Collections.ObjectModel;

namespace ObjLoader.ViewModels.Camera;

internal class CameraKeyframeManager(
    ObjLoaderParameter parameter,
    ObservableCollection<CameraKeyframe> keyframes,
    Func<double> getCurrentTime,
    Func<(double cx, double cy, double cz, double tx, double ty, double tz)> getCameraState,
    Action<CameraKeyframe?> setSelectedKeyframe,
    Action updateAnimation,
    Action syncToParameter,
    Action resetCameraToDefault)
{
    public void AddKeyframe(CameraKeyframe? currentSelectedKeyframe)
    {
        var state = getCameraState();
        var currentTime = getCurrentTime();
        var keyframe = new CameraKeyframe
        {
            Time = currentTime,
            CamX = state.cx,
            CamY = state.cy,
            CamZ = state.cz,
            TargetX = state.tx,
            TargetY = state.ty,
            TargetZ = state.tz,
            Easing = EasingManager.Presets.FirstOrDefault()?.Clone() ?? new EasingData()
        };

        int index = -1;
        int lo = 0;
        int hi = keyframes.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            double t = keyframes[mid].Time;
            if (Math.Abs(t - currentTime) < 0.001)
            {
                index = mid;
                break;
            }
            if (t < currentTime) lo = mid + 1;
            else hi = mid - 1;
        }

        if (index >= 0)
        {
            keyframes.RemoveAt(index);
            keyframes.Insert(index, keyframe);
        }
        else
        {
            keyframes.Insert(lo, keyframe);
        }

        setSelectedKeyframe(keyframe);
        parameter.Keyframes = [.. keyframes];
    }

    public void RemoveKeyframe(CameraKeyframe? selectedKeyframe)
    {
        if (selectedKeyframe != null)
        {
            keyframes.Remove(selectedKeyframe);
            setSelectedKeyframe(null);

            if (keyframes.Count == 0)
            {
                resetCameraToDefault();
            }

            parameter.Keyframes = [.. keyframes];
            updateAnimation();
            syncToParameter();
        }
    }

    public void SavePreset(EasingData? selectedKeyframeEasing)
    {
        if (selectedKeyframeEasing == null) return;
        var dialog = new NameDialog();
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultName))
        {
            selectedKeyframeEasing.Name = dialog.ResultName;
            EasingManager.SavePreset(selectedKeyframeEasing);
        }
    }

    public void DeletePreset(EasingData? selectedKeyframeEasing, Action<EasingData?> setSelectedKeyframeEasing)
    {
        if (selectedKeyframeEasing != null && selectedKeyframeEasing.IsCustom)
        {
            EasingManager.DeletePreset(selectedKeyframeEasing);
            setSelectedKeyframeEasing(EasingManager.Presets.FirstOrDefault());
        }
    }
}