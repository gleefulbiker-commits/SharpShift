using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers repositories for GitHub accounts or organizations provided via GITHUB_ACCOUNTS environment variable.
    /// Uses GITHUB_TOKEN for authentication when available.
    /// </summary>
    public class GitHubSolutionDiscoverer : ISolutionDiscoverer
    {
        private readonly HttpClient _http;

        public GitHubSolutionDiscoverer(HttpClient httpClient)
        {
            _http = httpClient ?? new HttpClient();
            // Ensure a User-Agent header for GitHub API
            if (_http.DefaultRequestHeaders.UserAgent.Count <= 0)
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("SharpShift-Inventory/1.0");

            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token) && !string.Equals(_http.DefaultRequestHeaders.Authorization?.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                // Use token if present
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath = null)
        {
            var accountsEnv = Environment.GetEnvironmentVariable("GITHUB_ACCOUNTS");
            if (string.IsNullOrWhiteSpace(accountsEnv))
                return [];

            var accounts = accountsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a));
            var results = new List<SolutionDiscoveryEntry>();

            foreach (var account in accounts)
            {
                var list = await ListReposForAccount(account);
                if (list != null)
                    results.AddRange(list);
            }

            return results;
        }

        private async Task<IEnumerable<SolutionDiscoveryEntry>?> ListReposForAccount(string account)
        {
            int page = 1;
            const int perPage = 100;
            int arrayLength = -1;

            var results = new List<SolutionDiscoveryEntry>();
            while (true)
            {
                var uri = $"https://api.github.com/users/{account}/repos?per_page={perPage}&page={page}";
                HttpResponseMessage resp;
                try
                {
                    resp = await _http.GetAsync(uri);
                }
                catch
                {
                    break;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    // Try org endpoint as fallback
                    var orgUri = $"https://api.github.com/orgs/{account}/repos?per_page={perPage}&page={page}";
                    try
                    {
                        resp = await _http.GetAsync(orgUri);
                    }
                    catch
                    {
                        break;
                    }

                    if (!resp.IsSuccessStatusCode)
                        break;
                }

                var json = await resp.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Array)
                        break;

                    arrayLength = root.GetArrayLength();
                    if (arrayLength == 0)
                        break;

                    foreach (var repo in root.EnumerateArray())
                    {
                        var htmlUrl = repo.GetProperty("html_url").GetString();
                        var name = repo.GetProperty("name").GetString();
                        string? owner = null;
                        if (repo.TryGetProperty("owner", out var ownerEl) && ownerEl.TryGetProperty("login", out var loginEl))
                            owner = loginEl.GetString();
                        var archived = repo.TryGetProperty("archived", out var archEl) && archEl.GetBoolean();

                        var entry = new SolutionDiscoveryEntry
                        {
                            Source = "GitHub",
                            RepoUrl = htmlUrl,
                            RepoName = name,
                            Owner = owner,
                            IsArchived = archived
                        };

                        // If configured, attempt to clone the repository locally for analysis. Do not clone archived repositories.
                        try
                        {
                            var cloneBase = Environment.GetEnvironmentVariable("GITHUB_CLONE_PATH");
                            if (!archived && !string.IsNullOrWhiteSpace(cloneBase))
                            {
                                try
                                {
                                    var safeOwner = string.IsNullOrWhiteSpace(owner) ? "unknown" : owner;
                                    var targetDir = System.IO.Path.Combine(cloneBase, safeOwner, name!);
                                    if (!System.IO.Directory.Exists(targetDir))
                                    {
                                        // Use GitHelper to perform clone (centralized checks and timeout handling)
                                        if (SharpShift.Inventory.Core.Utilities.GitHelper.IsGitAvailable())
                                        {
                                            // TryClone performs the clone with configured depth and timeout; ignore its result here
                                            _ = SharpShift.Inventory.Core.Utilities.GitHelper.TryClone(
                                                htmlUrl ?? string.Empty,
                                                targetDir,
                                                SharpShift.Inventory.Core.Utilities.UpgradeConfig.DefaultCloneDepth,
                                                SharpShift.Inventory.Core.Utilities.UpgradeConfig.DefaultCloneTimeoutMs);
                                        }
                                    }

                                    if (System.IO.Directory.Exists(targetDir))
                                        entry.LocalPath = System.IO.Path.GetFullPath(targetDir);
                                }
                                catch
                                {
                                    // ignore clone failures and continue
                                }
                            }
                        }
                        catch
                        {
                            // swallow any errors related to cloning configuration
                        }

                        results.Add(entry);
                    }
                }
                catch
                {
                    break;
                }

                // If fewer results than requested perPage then we've reached the last page
                if (arrayLength >= 0 && arrayLength < perPage)
                    break;

                page++;
            }

            return results;
        }
    }
}
