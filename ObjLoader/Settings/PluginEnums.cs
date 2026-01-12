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
}