using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

namespace IntelOrca.OpenLauncher.Core
{
    public class BuildService
    {
        private readonly GitHubClient _gitHubClient = new GitHubClient(new ProductHeaderValue("OpenLauncher"));

        public async Task<ImmutableArray<Build>> GetBuildsAsync(Game game)
        {
            var releases = await _gitHubClient.Repository.Release.GetAll(game.Repository.Owner, game.Repository.Name).ConfigureAwait(false);
            var builds = ImmutableArray.CreateBuilder<Build>(initialCapacity: releases.Count);
            foreach (var release in releases)
            {
                var assets = release.Assets
                    .Select(x => new BuildAsset(x.Name, new Uri(x.BrowserDownloadUrl), x.ContentType, x.Size))
                    .ToImmutableArray();
                var build = new Build(release.PublishedAt?.DateTime ?? DateTime.MinValue, release.TagName, assets);
                builds.Add(build);
            }
            return builds.MoveToImmutable();
        }
    }
}
