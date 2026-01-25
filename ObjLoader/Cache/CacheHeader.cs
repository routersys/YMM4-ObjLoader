namespace ObjLoader.Cache
{
    public readonly struct CacheHeader
    {
        public const int CurrentSignature = 0x4A424F04;
        public const int CurrentVersion = 5;

        public int Signature { get; }
        public int Version { get; }
        public long Timestamp { get; }
        public string OriginalPath { get; }
        public string ParserId { get; }
        public int ParserVersion { get; }
        public string PluginVersion { get; }

        public CacheHeader(long timestamp, string originalPath, string parserId, int parserVersion, string pluginVersion)
        {
            Signature = CurrentSignature;
            Version = CurrentVersion;
            Timestamp = timestamp;
            OriginalPath = originalPath;
            ParserId = parserId;
            ParserVersion = parserVersion;
            PluginVersion = pluginVersion;
        }

        public CacheHeader(int signature, int version, long timestamp, string originalPath, string parserId, int parserVersion, string pluginVersion)
        {
            Signature = signature;
            Version = version;
            Timestamp = timestamp;
            OriginalPath = originalPath;
            ParserId = parserId;
            ParserVersion = parserVersion;
            PluginVersion = pluginVersion;
        }

        public bool IsValid(long expectedTimestamp, string expectedPath, string expectedParserId, int expectedParserVersion, string expectedPluginVersion)
        {
            return Signature == CurrentSignature &&
                   Version == CurrentVersion &&
                   Timestamp >= expectedTimestamp &&
                   string.Equals(OriginalPath, expectedPath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(ParserId, expectedParserId, StringComparison.Ordinal) &&
                   ParserVersion == expectedParserVersion &&
                   string.Equals(PluginVersion, expectedPluginVersion, StringComparison.Ordinal);
        }
    }
}