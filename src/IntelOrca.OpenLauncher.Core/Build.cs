using System;
using System.Collections.Immutable;

namespace IntelOrca.OpenLauncher.Core
{
    public class Build : IComparable<Build>
    {
        public DateTime? PublishedAt { get; }
        public string Version { get; }
        public ImmutableArray<BuildAsset> Assets { get; }
        public bool IsRelease { get; }

        public Build(bool isRelease, DateTime publishedAt, string version, ImmutableArray<BuildAsset> assets)
        {
            IsRelease = isRelease;
            PublishedAt = publishedAt;
            Version = version;
            Assets = assets;
        }

        public Version? ParsedVersion
        {
            get
            {
                var text = Version;
                if (text.StartsWith("v"))
                {
                    text = text[1..];
                }
                return System.Version.TryParse(text, out var parsedVersion) ? parsedVersion : null;
            }
        }

        public override string ToString() => Version;

        public int CompareTo(Build? other)
        {
            ArgumentNullException.ThrowIfNull(other);

            var a = PublishedAt;
            var b = other.PublishedAt;

            if (a is null && b is null)
                return 0;
            if (a is null)
                return 1;
            if (b is null)
                return -1;

            return b.Value.CompareTo(a.Value);
        }
    }
}
