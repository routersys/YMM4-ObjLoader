namespace ObjLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MenuAttribute : Attribute
    {
        public string Group { get; set; } = string.Empty;
        public string GroupNameKey { get; set; } = string.Empty;
        public string GroupAcceleratorKey { get; set; } = string.Empty;
        public string NameKey { get; set; } = string.Empty;
        public Type? ResourceType { get; set; }
        public int Order { get; set; } = 0;
        public string Icon { get; set; } = string.Empty;
        public bool IsCheckable { get; set; }
        public string CheckPropertyName { get; set; } = string.Empty;
        public bool IsSeparatorAfter { get; set; }
        public string InputGestureText { get; set; } = string.Empty;
        public string AcceleratorKey { get; set; } = string.Empty;

        public MenuAttribute()
        {

        }
    }
}