using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;
using ObjLoader.Localization;

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
            string name = type.ToString();
            try
            {
                var locName = Texts.ResourceManager.GetString($"Easing_{type}");
                if (!string.IsNullOrEmpty(locName)) name = locName;
            }
            catch { }

            var data = new EasingData { Name = name, IsCustom = false, PresetType = type };
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
                case EasingType.QuartIn:
                    p0.HandleOutX = 0.895; p0.HandleOutY = 0.03;
                    p1.HandleInX = -0.315; p1.HandleInY = -0.78;
                    break;
                case EasingType.QuartOut:
                    p0.HandleOutX = 0.165; p0.HandleOutY = 0.84;
                    p1.HandleInX = -0.56; p1.HandleInY = 0;
                    break;
                case EasingType.QuartInOut:
                    p0.HandleOutX = 0.77; p0.HandleOutY = 0;
                    p1.HandleInX = -0.825; p1.HandleInY = 0;
                    break;
                case EasingType.QuintIn:
                    p0.HandleOutX = 0.755; p0.HandleOutY = 0.05;
                    p1.HandleInX = -0.145; p1.HandleInY = -0.94;
                    break;
                case EasingType.QuintOut:
                    p0.HandleOutX = 0.23; p0.HandleOutY = 1;
                    p1.HandleInX = -0.68; p1.HandleInY = 0;
                    break;
                case EasingType.QuintInOut:
                    p0.HandleOutX = 0.86; p0.HandleOutY = 0;
                    p1.HandleInX = -0.93; p1.HandleInY = 0;
                    break;
                case EasingType.ExpoIn:
                    p0.HandleOutX = 0.95; p0.HandleOutY = 0.05;
                    p1.HandleInX = -0.205; p1.HandleInY = -0.965;
                    break;
                case EasingType.ExpoOut:
                    p0.HandleOutX = 0.19; p0.HandleOutY = 1;
                    p1.HandleInX = -0.78; p1.HandleInY = 0;
                    break;
                case EasingType.ExpoInOut:
                    p0.HandleOutX = 1; p0.HandleOutY = 0;
                    p1.HandleInX = 0; p1.HandleInY = 0;
                    break;
                case EasingType.CircIn:
                    p0.HandleOutX = 0.6; p0.HandleOutY = 0.04;
                    p1.HandleInX = -0.02; p1.HandleInY = -0.665;
                    break;
                case EasingType.CircOut:
                    p0.HandleOutX = 0.075; p0.HandleOutY = 0.82;
                    p1.HandleInX = -0.835; p1.HandleInY = 0;
                    break;
                case EasingType.CircInOut:
                    p0.HandleOutX = 0.785; p0.HandleOutY = 0.135;
                    p1.HandleInX = -0.85; p1.HandleInY = -0.14;
                    break;
                case EasingType.BackIn:
                    p0.HandleOutX = 0.6; p0.HandleOutY = -0.28;
                    p1.HandleInX = -0.265; p1.HandleInY = -0.955;
                    break;
                case EasingType.BackOut:
                    p0.HandleOutX = 0.175; p0.HandleOutY = 0.885;
                    p1.HandleInX = -0.68; p1.HandleInY = 0.275;
                    break;
                case EasingType.BackInOut:
                    p0.HandleOutX = 0.68; p0.HandleOutY = -0.55;
                    p1.HandleInX = -0.735; p1.HandleInY = 0.55;
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
            if (data.IsCustom)
            {
                string path = Path.Combine(UserEasingDir, $"{SanitizeFileName(data.Name)}.xml");
                if (File.Exists(path)) File.Delete(path);
            }
            Presets.Remove(data);
        }

        private static string SanitizeFileName(string name)
        {
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }
    }
}