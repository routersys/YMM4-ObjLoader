using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

namespace ObjLoader.Plugin
{
    public static class EasingManager
    {
        private static string UserEasingDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "User", "Easings");
        public static ObservableCollection<EasingData> Presets { get; } = new ObservableCollection<EasingData>();

        static EasingManager()
        {
            LoadPresets();
        }

        public static void LoadPresets()
        {
            Presets.Clear();
            foreach (EasingType type in Enum.GetValues(typeof(EasingType)))
            {
                Presets.Add(CreatePreset(type));
            }

            if (!Directory.Exists(UserEasingDir)) return;

            foreach (var file in Directory.GetFiles(UserEasingDir, "*.xml"))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(EasingData));
                    using var stream = new FileStream(file, FileMode.Open);
                    if (serializer.Deserialize(stream) is EasingData data)
                    {
                        data.IsCustom = true;
                        Presets.Add(data);
                    }
                }
                catch { }
            }
        }

        private static EasingData CreatePreset(EasingType type)
        {
            var data = new EasingData { Name = type.ToString(), IsCustom = false, PresetType = type };
            data.Points.Clear();
            var p0 = new EasingPoint(0, 0);
            var p1 = new EasingPoint(1, 1);

            switch (type)
            {
                case EasingType.Linear:
                    p0.HandleOutX = 0.33; p0.HandleOutY = 0.33;
                    p1.HandleInX = -0.33; p1.HandleInY = -0.33;
                    break;
                case EasingType.SineIn:
                    p0.HandleOutX = 0.47; p0.HandleOutY = 0;
                    p1.HandleInX = -0.255; p1.HandleInY = -0.285;
                    break;
                case EasingType.SineOut:
                    p0.HandleOutX = 0.39; p0.HandleOutY = 0.575;
                    p1.HandleInX = -0.435; p1.HandleInY = 0;
                    break;
                case EasingType.SineInOut:
                    p0.HandleOutX = 0.445; p0.HandleOutY = 0.05;
                    p1.HandleInX = -0.45; p1.HandleInY = -0.05;
                    break;
                case EasingType.QuadIn:
                    p0.HandleOutX = 0.55; p0.HandleOutY = 0.085;
                    p1.HandleInX = -0.32; p1.HandleInY = -0.32;
                    break;
                case EasingType.QuadOut:
                    p0.HandleOutX = 0.25; p0.HandleOutY = 0.46;
                    p1.HandleInX = -0.45; p1.HandleInY = -0.085;
                    break;
                case EasingType.QuadInOut:
                    p0.HandleOutX = 0.455; p0.HandleOutY = 0.03;
                    p1.HandleInX = -0.485; p1.HandleInY = -0.03;
                    break;
                case EasingType.CubicIn:
                    p0.HandleOutX = 0.55; p0.HandleOutY = 0.055;
                    p1.HandleInX = -0.33; p1.HandleInY = -0.33;
                    break;
                case EasingType.CubicOut:
                    p0.HandleOutX = 0.215; p0.HandleOutY = 0.61;
                    p1.HandleInX = -0.445; p1.HandleInY = -0.055;
                    break;
                case EasingType.CubicInOut:
                    p0.HandleOutX = 0.645; p0.HandleOutY = 0.045;
                    p1.HandleInX = -0.645; p1.HandleInY = -0.045;
                    break;
                default:
                    p0.HandleOutX = 0.33; p0.HandleOutY = 0.33;
                    p1.HandleInX = -0.33; p1.HandleInY = -0.33;
                    break;
            }
            data.Points.Add(p0);
            data.Points.Add(p1);
            return data;
        }

        public static void SavePreset(EasingData data)
        {
            if (!Directory.Exists(UserEasingDir)) Directory.CreateDirectory(UserEasingDir);

            var newData = data.Clone();
            newData.IsCustom = true;

            string path = Path.Combine(UserEasingDir, $"{SanitizeFileName(newData.Name)}.xml");
            var serializer = new XmlSerializer(typeof(EasingData));
            using var stream = new FileStream(path, FileMode.Create);
            serializer.Serialize(stream, newData);

            Presets.Add(newData);
        }

        public static void DeletePreset(EasingData data)
        {
            if (!data.IsCustom) return;
            string path = Path.Combine(UserEasingDir, $"{SanitizeFileName(data.Name)}.xml");
            if (File.Exists(path)) File.Delete(path);
            Presets.Remove(data);
        }

        private static string SanitizeFileName(string name)
        {
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }
    }
}