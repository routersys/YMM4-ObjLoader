using ObjLoader.Localization;
using System.ComponentModel.DataAnnotations;

namespace ObjLoader.Plugin
{
    public enum LightType
    {
        [Display(Name = nameof(Texts.LightType_Point), ResourceType = typeof(Texts))]
        Point,
        [Display(Name = nameof(Texts.LightType_Spot), ResourceType = typeof(Texts))]
        Spot,
        [Display(Name = nameof(Texts.LightType_Sun), ResourceType = typeof(Texts))]
        Sun,
        [Display(Name = nameof(Texts.LightType_Area), ResourceType = typeof(Texts))]
        Area
    }
}