using ObjLoader.Localization;
using System.ComponentModel.DataAnnotations;

namespace ObjLoader.Settings
{
    public enum CoordinateSystem
    {
        [Display(Name = nameof(Texts.CoordinateSystem_RightHandedYUp), ResourceType = typeof(Texts))]
        RightHandedYUp,
        [Display(Name = nameof(Texts.CoordinateSystem_RightHandedZUp), ResourceType = typeof(Texts))]
        RightHandedZUp,
        [Display(Name = nameof(Texts.CoordinateSystem_LeftHandedYUp), ResourceType = typeof(Texts))]
        LeftHandedYUp,
        [Display(Name = nameof(Texts.CoordinateSystem_LeftHandedZUp), ResourceType = typeof(Texts))]
        LeftHandedZUp
    }

    public enum RenderCullMode
    {
        [Display(Name = nameof(Texts.CullMode_None), ResourceType = typeof(Texts))]
        None,
        [Display(Name = nameof(Texts.CullMode_Front), ResourceType = typeof(Texts))]
        Front,
        [Display(Name = nameof(Texts.CullMode_Back), ResourceType = typeof(Texts))]
        Back
    }

    public enum RenderQuality
    {
        [Display(Name = nameof(Texts.RenderQuality_High), ResourceType = typeof(Texts))]
        High,
        [Display(Name = nameof(Texts.RenderQuality_Standard), ResourceType = typeof(Texts))]
        Standard,
        [Display(Name = nameof(Texts.RenderQuality_Low), ResourceType = typeof(Texts))]
        Low
    }

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