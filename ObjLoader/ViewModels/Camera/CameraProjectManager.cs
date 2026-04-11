using Microsoft.Win32;
using ObjLoader.Localization;
using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace ObjLoader.ViewModels.Camera;

internal class CameraProjectManager(
    ObjLoaderParameter parameter,
    ObservableCollection<CameraKeyframe> keyframes,
    Func<double> getMaxDuration,
    Action<double> setMaxDuration,
    Func<bool> getIsTargetFixed,
    Action<bool> setIsTargetFixed,
    Action updateAnimation,
    Action<string> setCurrentFilePath)
{
    public void OpenProject()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Texts.Msg_ProjectFileFilter}|*.olcp",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            setCurrentFilePath(dialog.FileName);
            LoadProjectFile(dialog.FileName);
        }
    }

    public void SaveProject(string currentFilePath)
    {
        if (string.IsNullOrEmpty(currentFilePath)) SaveProjectAs(currentFilePath);
        else SaveProjectFile(currentFilePath);
    }

    public void SaveProjectAs(string currentFilePath)
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"{Texts.Msg_ProjectFileFilter}|*.olcp",
            FileName = Path.GetFileName(currentFilePath)
        };
        if (dialog.ShowDialog() == true)
        {
            setCurrentFilePath(dialog.FileName);
            SaveProjectFile(dialog.FileName);
        }
    }

    private void LoadProjectFile(string path)
    {
        try
        {
            var serializer = new XmlSerializer(typeof(CameraProjectData));
            using var stream = new FileStream(path, FileMode.Open);
            if (serializer.Deserialize(stream) is CameraProjectData data)
            {
                var sorted = (data.Keyframes ?? []).OrderBy(k => k.Time).ToList();
                keyframes.Clear();
                foreach (var k in sorted) keyframes.Add(k);

                parameter.Keyframes = [.. keyframes];
                setMaxDuration(data.Duration);
                setIsTargetFixed(data.IsTargetFixed);
                updateAnimation();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Texts.Msg_FailedToLoad, ex.Message));
        }
    }

    private void SaveProjectFile(string path)
    {
        try
        {
            var data = new CameraProjectData
            {
                Keyframes = [.. keyframes],
                Duration = getMaxDuration(),
                IsTargetFixed = getIsTargetFixed()
            };
            var serializer = new XmlSerializer(typeof(CameraProjectData));
            using var stream = new FileStream(path, FileMode.Create);
            serializer.Serialize(stream, data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Texts.Msg_FailedToSave, ex.Message));
        }
    }
}