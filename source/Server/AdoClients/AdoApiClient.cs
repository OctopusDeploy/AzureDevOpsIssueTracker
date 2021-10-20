using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octopus.Data;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;
using Octopus.Server.MessageContracts.Features.IssueTrackers;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    interface IAdoApiClient
    {
        Task<IResultFromExtension<(int id, string url)[]>> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, string? personalAccessToken = null, bool testing = false, CancellationToken cancellationToken = default);
        Task<IResultFromExtension<(string title, int? commentCount)>> GetWorkItem(AdoProjectUrls adoProjectUrls, int workItemId, string? personalAccessToken = null,
            bool testing = false, CancellationToken cancellationToken = default);
        Task<IResultFromExtension<WorkItemLink[]>> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls, CancellationToken cancellationToken);
        Task<IResultFromExtension<string[]>> GetProjectList(AdoUrl adoUrl, string? personalAccessToken = null, bool testing = false, CancellationToken cancellationToken = default);
    }

    class AdoApiClient : IAdoApiClient
    {
        private readonly ISystemLog systemLog;
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly IHttpJsonClient client;
        private readonly HtmlConvert htmlConvert;

        public AdoApiClient(ISystemLog systemLog, IAzureDevOpsConfigurationStore store, IHttpJsonClient client, HtmlConvert htmlConvert)
        {
            this.systemLog = systemLog;
            this.store = store;
            this.client = client;
            this.htmlConvert = htmlConvert;
        }

        public async Task<IResultFromExtension<(int id, string url)[]>> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, string? personalAccessToken = null,
            bool testing = false, CancellationToken cancellationToken = default)
        {
            // ReSharper disable once StringLiteralTypo
            var workItemsUrl = $"{adoBuildUrls.ProjectUrl}/_apis/build/builds/{adoBuildUrls.BuildId}/workitems?api-version=4.1";

            var (status, jObject) = await client.Get(workItemsUrl, personalAccessToken ?? await GetPersonalAccessToken(adoBuildUrls, cancellationToken), cancellationToken);
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<(int id, string url)[]>.Success(Array.Empty<(int, string)>());
            }

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<(int id, string url)[]>.Failed($"Error while fetching work item references from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            try
            {
                return ResultFromExtension<(int id, string url)[]>.Success(jObject?["value"]?
                    .Select(el => (el["id"]?.Value<int>() ?? default(int), el["url"]?.ToString() ?? string.Empty))
                    .ToArray() ?? Array.Empty<(int id, string url)>());
            }
            catch
            {
                return ResultFromExtension<(int id, string url)[]>.Failed("Unable to interpret work item references from Azure DevOps.");
            }
        }

        public async Task<IResultFromExtension<(string title, int? commentCount)>> GetWorkItem(AdoProjectUrls adoProjectUrls, int workItemId,
            string? personalAccessToken = null, bool testing = false, CancellationToken cancellationToken = default)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = await client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}?api-version=4.1",
                personalAccessToken ?? await GetPersonalAccessToken(adoProjectUrls, cancellationToken), cancellationToken);
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<(string title, int? commentCount)>.Success((workItemId.ToString(), 0));
            }

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<(string title, int? commentCount)>.Failed($"Error while fetching work item details from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            try
            {
                var fields = jObject?["fields"];
                if (fields == null)
                    return ResultFromExtension<(string title, int? commentCount)>.Failed("Unable to interpret work item details from Azure DevOps. `fields` element is missing.");

                return ResultFromExtension<(string title, int? commentCount)>.Success((fields?["System.Title"]?.ToString() ?? string.Empty, fields?["System.CommentCount"]?.Value<int>() ?? default(int)));
            }
            catch
            {
                return ResultFromExtension<(string title, int? commentCount)>.Failed("Unable to interpret work item details from Azure DevOps.");
            }
        }

        public async Task<IResultFromExtension<WorkItemLink[]>> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls, CancellationToken cancellationToken)
        {
            var workItemsRefs = await GetBuildWorkItemsRefs(adoBuildUrls, cancellationToken: cancellationToken);
            if (workItemsRefs is FailureResult failure)
                return ResultFromExtension<WorkItemLink[]>.Failed(failure.Errors);

            List<IResultFromExtension<WorkItemLink>> workItemLinks = new List<IResultFromExtension<WorkItemLink>>();
            foreach (var w in ((ISuccessResult<(int id, string url)[]>)workItemsRefs).Value)
            {
                var resultFromExtension = await GetWorkItemLink(adoBuildUrls, w.id, cancellationToken);
                workItemLinks.Add( resultFromExtension);
            }

            var validWorkItemLinks = workItemLinks
                .OfType<ISuccessResult<WorkItemLink>>()
                .Select(r => r.Value)
                .ToArray();
            return ResultFromExtension<WorkItemLink[]>.Success(validWorkItemLinks);
        }

        public async Task<IResultFromExtension<string[]>> GetProjectList(AdoUrl adoUrl, string? personalAccessToken = null, bool testing = false, CancellationToken cancellationToken = default)
        {
            var (status, jObject) = await client.Get($"{adoUrl.OrganizationUrl}/_apis/projects?api-version=4.1",
                personalAccessToken ?? await GetPersonalAccessToken(adoUrl, cancellationToken), cancellationToken);

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<string[]>.Failed($"Error while fetching project list from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            return ResultFromExtension<string[]>.Success(jObject?["value"]?
                .Select(p => p["name"]?.ToString())
                .Cast<string>() // cast to keep the compiler happy with nullable checks
                .ToArray() ?? Array.Empty<string>());
        }

        /// <returns>Up to 200 comments on the specified work item.</returns>
        private async Task<IResultFromExtension<string[]>> GetWorkItemComments(AdoProjectUrls adoProjectUrls, int workItemId, CancellationToken cancellationToken)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = await client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}/comments?api-version=4.1-preview.2",
                await GetPersonalAccessToken(adoProjectUrls, cancellationToken), cancellationToken);

            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<string[]>.Success(Array.Empty<string>());
            }

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<string[]>.Failed($"Error while fetching work item comments from Azure DevOps: {status.ToDescription(jObject)}");
            }

            string[] commentsHtml;
            try
            {
                commentsHtml = jObject?["comments"]?
                    .Select(c => c["text"]?.ToString())
                    .Where(c => c != null)
                    .Cast<string>() // cast to keep the compiler happy with nullable checks
                    .ToArray() ?? Array.Empty<string>();
            }
            catch
            {
                return ResultFromExtension<string[]>.Failed("Unable to interpret work item comments from Azure DevOps.");
            }

            return ResultFromExtension<string[]>.Success(commentsHtml
                .Select(h => htmlConvert.ToPlainText(h))
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Cast<string>() // cast to keep the compiler happy with nullable checks
                .ToArray());
        }

        private async Task<string?> GetReleaseNote(AdoProjectUrls adoProjectUrls, int workItemId, int? commentCount = null, CancellationToken cancellationToken = default)
        {
            var releaseNotePrefix = await store.GetReleaseNotePrefix(cancellationToken);
            if (string.IsNullOrWhiteSpace(releaseNotePrefix) || commentCount == 0)
            {
                return null;
            }

            var comments = await GetWorkItemComments(adoProjectUrls, workItemId, cancellationToken);
            if (comments is FailureResult failure)
            {
                // if we can't retrieve the comments then move on without
                systemLog.WarnFormat("Error retrieving Azure DevOps comments for work item {0}. Error: {1}", workItemId, failure.ErrorString);
                return null;
            }

            var releaseNoteRegex = new Regex("^" + Regex.Escape(releaseNotePrefix), RegexOptions.IgnoreCase);
            // Return (last, if multiple found) comment that matched release note prefix
            var releaseNoteComment = ((ISuccessResult<string[]>)comments).Value
                ?.LastOrDefault(ct => releaseNoteRegex.IsMatch(ct ?? ""));
            var releaseNote = releaseNoteComment != null
                ? releaseNoteRegex.Replace(releaseNoteComment, "").Trim()
                : null;
            return releaseNote;
        }

        static string BuildWorkItemBrowserUrl(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            return $"{adoProjectUrls.ProjectUrl}/_workitems?_a=edit&id={workItemId}";
        }

        private async Task<string?> GetPersonalAccessToken(AdoUrl adoUrl, CancellationToken cancellationToken)
        {
            try
            {
                var baseUrl = await store.GetBaseUrl(cancellationToken);
                if (baseUrl == null)
                    return null;
                var uri = new Uri(baseUrl.TrimEnd('/'), UriKind.Absolute);
                return uri.IsBaseOf(new Uri(adoUrl.OrganizationUrl, UriKind.Absolute)) ? (await store.GetPersonalAccessToken(cancellationToken))?.Value : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<IResultFromExtension<WorkItemLink>> GetWorkItemLink(AdoProjectUrls adoProjectUrls, int workItemId, CancellationToken cancellationToken)
        {
            var workItem = await GetWorkItem(adoProjectUrls, workItemId, cancellationToken: cancellationToken) as ISuccessResult<(string title, int? commentCount)>;
            var releaseNote = workItem != null ? await GetReleaseNote(adoProjectUrls, workItemId, workItem.Value.commentCount, cancellationToken) : null;

            var workItemLink = new WorkItemLink
            {
                Id = workItemId.ToString(),
                LinkUrl = BuildWorkItemBrowserUrl(adoProjectUrls, workItemId),
                Description = !string.IsNullOrWhiteSpace(releaseNote)
                    ? releaseNote
                    : workItem != null && !string.IsNullOrWhiteSpace(workItem.Value.title)
                        ? workItem.Value.title
                        : workItemId.ToString(),
                Source = AzureDevOpsConfigurationStore.CommentParser
            };

            return ResultFromExtension<WorkItemLink>.Success(workItemLink);
        }
    }
}