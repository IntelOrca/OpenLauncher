using System;
using System.Linq;
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
            var builds = await buildService.GetBuildsAsync(Game.OpenLoco);
            var build = builds.First(x => x.Version == "v22.05.1");
            Assert.Equal("v22.05.1", build.Version);
            Assert.Equal(new DateTime(2022, 5, 17, 20, 6, 15), build.PublishedAt);
            Assert.Equal("OpenLoco-v22.05.1-macos.zip", build.Assets[0].Name);
            Assert.Equal(new Uri("https://github.com/OpenLoco/OpenLoco/releases/download/v22.05.1/OpenLoco-v22.05.1-macos.zip"), build.Assets[0].Uri);
            Assert.Equal("application/x-zip-compressed", build.Assets[0].ContentType);
            Assert.Equal(4157592, build.Assets[0].Size);
        }
    }
}
