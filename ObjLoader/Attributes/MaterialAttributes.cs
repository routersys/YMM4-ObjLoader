namespace ObjLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MaterialGroupAttribute : Attribute
    {
        public string Id { get; }
        public string TitleKey { get; }
        public int Order { get; }

        public MaterialGroupAttribute(string id, string titleKey, int order = 0)
        {
            Id = id;
            TitleKey = titleKey;
            Order = order;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public abstract class MaterialPropertyAttribute : Attribute
    {
        public string GroupId { get; }
        public string LabelKey { get; }
        public int Order { get; }

        protected MaterialPropertyAttribute(string groupId, string labelKey, int order)
        {
            GroupId = groupId;
            LabelKey = labelKey;
            Order = order;
        }
    }

    public class MaterialRangeAttribute : MaterialPropertyAttribute
    {
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }

        public MaterialRangeAttribute(string groupId, string labelKey, double min, double max, double step = 0.01, int order = 0)
            : base(groupId, labelKey, order)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }

    public class MaterialColorAttribute : MaterialPropertyAttribute
    {
        public MaterialColorAttribute(string groupId, string labelKey, int order = 0)
            : base(groupId, labelKey, order)
        {
        }
    }
}