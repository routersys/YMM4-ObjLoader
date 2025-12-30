using ObjLoader.Localization;
using System.ComponentModel.DataAnnotations;

namespace ObjLoader.Core
{
    public enum ProjectionType
    {
        [Display(Name = nameof(Texts.Projection_Parallel), ResourceType = typeof(Texts))]
        Parallel,
        [Display(Name = nameof(Texts.Projection_Perspective), ResourceType = typeof(Texts))]
        Perspective
    }
}