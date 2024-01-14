using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using IntelOrca.OpenLauncher.Core;
using Xunit;

namespace IntelOrca.OpenLauncher.Tests
{
    public class BuildServiceTests
    {
        [Fact]
        public async Task GetBuildsAsync_OpenLoco_v22_05_1()
        {
            var buildService = new BuildService();
            var builds = await buildService.GetBuildsAsync(Game.OpenLoco, includeDevelop: false);
            var build = builds.First(b => b.Version == "v22.05.1");
            var buildAsset  = build.Assets.First(ba => ba.Platform == OSPlatform.OSX);
            Assert.Equal("v22.05.1", build.Version);
            Assert.Equal(new DateTime(2022, 5, 17, 20, 6, 15), build.PublishedAt);
            Assert.Equal("OpenLoco-v22.05.1-macos.zip", buildAsset.Name);
            Assert.Equal(new Uri("https://github.com/OpenLoco/OpenLoco/releases/download/v22.05.1/OpenLoco-v22.05.1-macos.zip"), buildAsset.Uri);
            Assert.Equal("application/x-zip-compressed", buildAsset.ContentType);
            Assert.Equal(4157592, buildAsset.Size);
        }
    }
}
