﻿using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Resources.IssueTrackers;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    public interface IAdoApiClient
    {
        SuccessOrErrorResult<WorkItemLink[]> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls);
        SuccessOrErrorResult<string[]> GetProjectList(AdoUrl adoUrl, string personalAccessToken = null, bool testing = false);
        SuccessOrErrorResult<int[]> GetBuildList(AdoProjectUrls adoProjectUrls, string personalAccessToken = null, bool testing = false);
        SuccessOrErrorResult<(int id, string url)[]> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, string personalAccessToken = null, bool testing = false);
    }

    public class AdoApiClient : IAdoApiClient
    {
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly IHttpJsonClient client;
        private readonly HtmlConvert htmlConvert;

        public AdoApiClient(IAzureDevOpsConfigurationStore store, IHttpJsonClient client, HtmlConvert htmlConvert)
        {
            this.store = store;
            this.client = client;
            this.htmlConvert = htmlConvert;
        }

        internal string GetPersonalAccessToken(AdoUrl adoUrl)
        {
            try
            {
                return new Uri(store.GetBaseUrl().TrimEnd('/'), UriKind.Absolute).IsBaseOf(new Uri(adoUrl.OrganizationUrl, UriKind.Absolute))
                    ? store.GetPersonalAccessToken()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public SuccessOrErrorResult<(int id, string url)[]> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, string personalAccessToken = null,
            bool testing = false)
        {
            // ReSharper disable once StringLiteralTypo
            var workItemsUrl = $"{adoBuildUrls.ProjectUrl}/_apis/build/builds/{adoBuildUrls.BuildId}/workitems?api-version=4.1";

            var (status, jObject) = client.Get(workItemsUrl, personalAccessToken ?? GetPersonalAccessToken(adoBuildUrls));
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return new (int, string)[0];
            }

            if (!status.IsSuccessStatusCode())
            {
                return SuccessOrErrorResult.Failure($"Error while fetching work item references from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            try
            {
                return jObject["value"]
                    .Select(el => (el["id"].Value<int>(), el["url"].ToString()))
                    .ToArray();
            }
            catch
            {
                return SuccessOrErrorResult.Failure("Unable to interpret work item references from Azure DevOps.");
            }
        }

        public SuccessOrErrorResult<(string title, int? commentCount)> GetWorkItem(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}?api-version=4.1",
                GetPersonalAccessToken(adoProjectUrls));
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return (workItemId.ToString(), 0);
            }

            if (!status.IsSuccessStatusCode())
            {
                return SuccessOrErrorResult.Failure($"Error while fetching work item details from Azure DevOps: {status.ToDescription(jObject)}");
            }

            try
            {
                var fields = jObject["fields"];
                return (fields["System.Title"].ToString(), fields["System.CommentCount"]?.Value<int>());
            }
            catch
            {
                return SuccessOrErrorResult.Failure("Unable to interpret work item details from Azure DevOps.");
            }
        }

        /// <returns>Up to 200 comments on the specified work item.</returns>
        public SuccessOrErrorResult<string[]> GetWorkItemComments(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}/comments?api-version=4.1-preview.2",
                GetPersonalAccessToken(adoProjectUrls));

            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return new string[0];
            }

            if (!status.IsSuccessStatusCode())
            {
                return SuccessOrErrorResult.Failure($"Error while fetching work item comments from Azure DevOps: {status.ToDescription(jObject)}");
            }

            string[] commentsHtml;
            try
            {
                commentsHtml = jObject["comments"]
                    .Select(c => c["text"].ToString())
                    .ToArray();
            }
            catch
            {
                return SuccessOrErrorResult.Failure("Unable to interpret work item comments from Azure DevOps.");
            }

            return commentsHtml
                .Select(h => htmlConvert.ToPlainText(h))
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
        }

        public string BuildWorkItemBrowserUrl(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            return $"{adoProjectUrls.ProjectUrl}/_workitems?_a=edit&id={workItemId}";
        }

        public SuccessOrErrorResult<string> GetReleaseNote(AdoProjectUrls adoProjectUrls, int workItemId, int? commentCount = null)
        {
            var releaseNotePrefix = store.GetReleaseNotePrefix();
            if (string.IsNullOrWhiteSpace(releaseNotePrefix) || commentCount == 0)
            {
                return null;
            }

            var comments = GetWorkItemComments(adoProjectUrls, workItemId);
            if (!comments.Succeeded)
            {
                return SuccessOrErrorResult.Failure(comments);
            }

            var releaseNoteRegex = new Regex("^" + Regex.Escape(releaseNotePrefix), RegexOptions.IgnoreCase);
            // Return (last, if multiple found) comment that matched release note prefix
            var releaseNoteComment = comments.Value
                ?.LastOrDefault(ct => releaseNoteRegex.IsMatch(ct ?? ""));
            var releaseNote = releaseNoteComment != null
                ? releaseNoteRegex.Replace(releaseNoteComment, "").Trim()
                : null;
            return releaseNote;
        }

        public SuccessOrErrorResult<WorkItemLink> GetWorkItemLink(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            var workItem = GetWorkItem(adoProjectUrls, workItemId);
            var releaseNote = GetReleaseNote(adoProjectUrls, workItemId, workItem.Value.commentCount);

            var workItemLink = new WorkItemLink
            {
                Id = workItemId.ToString(),
                LinkUrl = BuildWorkItemBrowserUrl(adoProjectUrls, workItemId),
                Description = !string.IsNullOrWhiteSpace(releaseNote.Value)
                    ? releaseNote.Value
                    : !string.IsNullOrWhiteSpace(workItem.Value.title)
                        ? workItem.Value.title
                        : workItemId.ToString(),
                Source = AzureDevOpsConfigurationStore.CommentParser
            };

            return SuccessOrErrorResult.Conditional(workItemLink, workItem, releaseNote);
        }

        public SuccessOrErrorResult<WorkItemLink[]> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls)
        {
            var workItemsRefs = GetBuildWorkItemsRefs(adoBuildUrls);
            if (!workItemsRefs.Succeeded)
            {
                return SuccessOrErrorResult.Failure(workItemsRefs);
            }

            var workItemLinks = workItemsRefs.Value
                .Select(w => GetWorkItemLink(adoBuildUrls, w.id))
                .ToArray();
            var validWorkItemLinks = workItemLinks
                .Select(r => r.Value)
                .Where(v => v != null)
                .ToArray();
            return SuccessOrErrorResult.Conditional(validWorkItemLinks, workItemLinks);
        }

        public SuccessOrErrorResult<string[]> GetProjectList(AdoUrl adoUrl, string personalAccessToken = null, bool testing = false)
        {
            var (status, jObject) = client.Get($"{adoUrl.OrganizationUrl}/_apis/projects?api-version=4.1",
                personalAccessToken ?? GetPersonalAccessToken(adoUrl));

            if (!status.IsSuccessStatusCode())
            {
                return SuccessOrErrorResult.Failure($"Error while fetching project list from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            return jObject["value"]
                .Select(p => p["name"].ToString())
                .ToArray();
        }

        public SuccessOrErrorResult<int[]> GetBuildList(AdoProjectUrls adoProjectUrls, string personalAccessToken = null, bool testing = false)
        {
            var (status, jObject) = client.Get($"{adoProjectUrls.ProjectUrl}/_apis/build/builds?api-version=4.1",
                personalAccessToken ?? GetPersonalAccessToken(adoProjectUrls));

            if (!status.IsSuccessStatusCode())
            {
                return SuccessOrErrorResult.Failure($"Error while fetching build list from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            return jObject["value"]
                .Select(b => (int) b["id"])
                .ToArray();
        }
    }
}