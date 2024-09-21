using System;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.OpenLauncher.Core;
using Xunit;

namespace IntelOrca.OpenLauncher.Tests
{
    public class BuildServiceTests
    {
        [Theory]
        [InlineData("macos", "v22.05.1", 4157592, "2022-05-17T20:06:15Z")]
        public async Task GetBuildsAsync_OpenLoco_v22_05_1(string system, string version, int size, string publishtime)
        {
            var buildService = new BuildService();
            var builds = await buildService.GetBuildsAsync(Game.OpenLoco, includeDevelop: false);
            var build = builds.First(x => x.Version == version && x.Assets.Any(t => IsMatchingSystemAsset(system, t)));

            Assert.Equal(version, build.Version);
            Assert.Equal(DateTime.Parse(publishtime).ToUniversalTime(), build.PublishedAt);
            Assert.Equal($"OpenLoco-{version}-{system}.zip", build.Assets.Where(t => IsMatchingSystemAsset(system, t)).First().Name);
            Assert.Equal(new Uri($"https://github.com/OpenLoco/OpenLoco/releases/download/{version}/OpenLoco-{version}-{system}.zip"), build.Assets.Where(t => t.Uri.AbsoluteUri.Contains("macos.zip")).First().Uri);
            Assert.Equal("application/x-zip-compressed", build.Assets.Where(t => IsMatchingSystemAsset(system, t)).First().ContentType);
            Assert.Equal(size, build.Assets.Where(t => IsMatchingSystemAsset(system, t)).First().Size);
        }

        private static bool IsMatchingSystemAsset(string system, BuildAsset t) => t.Uri.AbsoluteUri.Contains($"{system}.zip");
    }
}
