using System;
using System.Collections.Immutable;

namespace IntelOrca.OpenLauncher.Core
{
    public class Build
    {
        public DateTime? PublishedAt { get; }
        public string Version { get; }
        public ImmutableArray<BuildAsset> Assets { get; }

        public Build(DateTime publishedAt, string version, ImmutableArray<BuildAsset> assets)
        {
            PublishedAt = publishedAt;
            Version = version;
            Assets = assets;
        }
    }
}
