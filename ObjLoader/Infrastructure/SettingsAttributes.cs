using System;

namespace ObjLoader.Infrastructure
{
    public enum SettingButtonPlacement
    {
        Content,
        BottomLeft,
        BottomRight
    }

    public enum SettingButtonType
    {
        Normal,
        OK,
        Cancel,
        Close
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SettingGroupAttribute : Attribute
    {
        public string Id { get; }
        public string Title { get; }
        public int Order { get; set; }
        public string Icon { get; set; } = "Geometry";
        public Type? ResourceType { get; set; }
        public string ParentId { get; set; } = string.Empty;

        public SettingGroupAttribute(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public abstract class SettingItemAttribute : Attribute
    {
        public string GroupId { get; }
        public string Label { get; }
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public Type? ResourceType { get; set; }
        public string EnableBy { get; set; } = string.Empty;
        public bool IsGroupHeader { get; set; }

        protected SettingItemAttribute(string groupId, string label)
        {
            GroupId = groupId;
            Label = label;
        }
    }

    public class TextSettingAttribute : SettingItemAttribute
    {
        public TextSettingAttribute(string groupId, string label) : base(groupId, label) { }
    }

    public class BoolSettingAttribute : SettingItemAttribute
    {
        public string TrueLabel { get; set; } = "On";
        public string FalseLabel { get; set; } = "Off";
        public BoolSettingAttribute(string groupId, string label) : base(groupId, label) { }
    }

    public class RangeSettingAttribute : SettingItemAttribute
    {
        public double Min { get; }
        public double Max { get; }
        public double Tick { get; set; } = 1.0;
        public string Unit { get; set; } = "";

        public RangeSettingAttribute(string groupId, string label, double min, double max) : base(groupId, label)
        {
            Min = min;
            Max = max;
        }
    }

    public class IntSpinnerSettingAttribute : SettingItemAttribute
    {
        public int Min { get; }
        public int Max { get; }

        public IntSpinnerSettingAttribute(string groupId, string label, int min, int max) : base(groupId, label)
        {
            Min = min;
            Max = max;
        }
    }

    public class EnumSettingAttribute : SettingItemAttribute
    {
        public EnumSettingAttribute(string groupId, string label) : base(groupId, label) { }
    }

    public class ColorSettingAttribute : SettingItemAttribute
    {
        public ColorSettingAttribute(string groupId, string label) : base(groupId, label) { }
    }

    public class FilePathSettingAttribute : SettingItemAttribute
    {
        public string Filter { get; set; } = "All files|*.*";
        public FilePathSettingAttribute(string groupId, string label) : base(groupId, label) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SettingButtonAttribute : Attribute
    {
        public string Label { get; }
        public SettingButtonPlacement Placement { get; set; } = SettingButtonPlacement.Content;
        public SettingButtonType Type { get; set; } = SettingButtonType.Normal;
        public string GroupId { get; set; } = string.Empty;
        public int Order { get; set; }
        public Type? ResourceType { get; set; }
        public string EnableBy { get; set; } = string.Empty;

        public SettingButtonAttribute(string label)
        {
            Label = label;
        }
    }
}