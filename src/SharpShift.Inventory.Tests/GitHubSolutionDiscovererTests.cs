using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.Discovery;
using SharpShift.Inventory.Core.Models;
using System.Net;

namespace SharpShift.Inventory.Tests
{
    // Minimal fake handler for HttpClient to return canned GitHub API responses.
    public class FakeHttpMessageHandler(string response) : HttpMessageHandler
    {
        private readonly string _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response)
            };
            return Task.FromResult(res);
        }
    }

    public class GitHubSolutionDiscovererTests
    {
        [Test]
        public async Task GitHubSolutionDiscoverer_ReturnsArchivedRepoFlag()
        {
            // sample GitHub repo payload with archived repo
            var json = "[ { \"html_url\": \"https://github.com/testowner/testrepo\", \"name\": \"testrepo\", \"archived\": false, \"owner\": { \"login\": \"testowner\" } }, { \"html_url\": \"https://github.com/testowner/oldrepo\", \"name\": \"oldrepo\", \"archived\": true, \"owner\": { \"login\": \"testowner\" } } ]";

            var handler = new FakeHttpMessageHandler(json);
            var client = new HttpClient(handler);

            try
            {
                Environment.SetEnvironmentVariable("GITHUB_ACCOUNTS", "testowner");

                var disc = new GitHubSolutionDiscoverer(client);
                var results = await disc.DiscoverSolutionsAsync(null);
                Assert.IsNotNull(results);
                var list = new List<SolutionDiscoveryEntry>(results);
                Assert.IsTrue(list.Exists(r => r.RepoName == "testrepo" && r.IsArchived == false));
                Assert.IsTrue(list.Exists(r => r.RepoName == "oldrepo" && r.IsArchived == true));
            }
            finally
            {
                Environment.SetEnvironmentVariable("GITHUB_ACCOUNTS", null);
            }
        }
    }
}
