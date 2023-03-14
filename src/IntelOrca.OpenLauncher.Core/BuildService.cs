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
                builds.Add(GetBuild(release, isRelease));
            }
            var latestBuild = await GetLatestBuildAsync(repo, isRelease);
            if (latestBuild != null && !builds.Any(x => x.Version == latestBuild.Version))
            {
                builds.Capacity++;
                builds.Add(latestBuild);
            }
            return builds.MoveToImmutable();
        }

        public async Task<Build> GetLatestBuildAsync(RepositoryName repo, bool isRelease)
        {
            var release = await _gitHubClient.Repository.Release.GetLatest(repo.Owner, repo.Name)
                .ConfigureAwait(false);
            return GetBuild(release, isRelease);
        }

        private static Build GetBuild(Release release, bool isRelease) =>
            new Build(isRelease, release.PublishedAt?.DateTime ?? DateTime.MinValue, release.TagName, GetAssets(release));

        private static ImmutableArray<BuildAsset> GetAssets(Release release) =>
            release.Assets
                .Select(x => new BuildAsset(x.Name, new Uri(x.BrowserDownloadUrl), x.ContentType, x.Size))
                .ToImmutableArray();
    }
}
