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

        public async Task<ImmutableArray<Build>> GetBuildsAsync(Game game, bool includeDevelop)
        {
            var releaseBuilds = await GetBuildsAsync(game.ReleaseRepository, true);
            if (includeDevelop && game.DevelopRepository is RepositoryName repo)
            {
                var developBuilds = await GetBuildsAsync(repo, false);
                return releaseBuilds.AddRange(developBuilds).Sort();
            }
            else
            {
                return releaseBuilds.Sort();
            }
        }

        public async Task<ImmutableArray<Build>> GetBuildsAsync(RepositoryName repo, bool isRelease)
        {
            var apiOptions = new ApiOptions()
            {
                StartPage = 1,
                PageCount = 1,
                PageSize = 50
            };
            var releases = await _gitHubClient.Repository.Release.GetAll(repo.Owner, repo.Name, apiOptions)
                .ConfigureAwait(false);
            var builds = ImmutableArray.CreateBuilder<Build>(initialCapacity: releases.Count);
            foreach (var release in releases)
            {
                var assets = release.Assets
                    .Select(x => new BuildAsset(x.Name, new Uri(x.BrowserDownloadUrl), x.ContentType, x.Size))
                    .ToImmutableArray();
                var build = new Build(isRelease, release.PublishedAt?.DateTime ?? DateTime.MinValue, release.TagName, assets);
                builds.Add(build);
            }
            return builds.MoveToImmutable();
        }
    }
}
