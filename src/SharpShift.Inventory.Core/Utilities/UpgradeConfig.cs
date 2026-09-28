namespace SharpShift.Inventory.Core.Utilities
{
    public static class UpgradeConfig
    {
        public const string GithubClonePathEnv = "GITHUB_CLONE_PATH";
        public const string GithubCloneDepthEnv = "GITHUB_CLONE_DEPTH";
        public const string GithubCloneTimeoutEnv = "GITHUB_CLONE_TIMEOUT_MS";
        public const string LegacyMetadataEnv = "SHARPSHIFT_LEGACY_METADATA";
        public const int DefaultCloneDepth = 1;
        public const int DefaultCloneTimeoutMs = 600_000; // 10 minutes
    }
}
