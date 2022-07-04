using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Octopus.Data;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Results;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    interface IAdoApiClient
    {
        IResultFromExtension<WorkItemLink[]> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls);
        IResultFromExtension<string[]> GetProjectList(AdoProjectUrls adoUrl, string? personalAccessToken = null);
        IResult CheckWeCanGetBuilds(AdoProjectUrls adoProjectUrls, string? personalAccessToken = null);
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

        string? GetPersonalAccessToken(AdoProjectUrls adoUrl)
        {
            try
            {
                var azureDevOpsConnection = store.GetMostQualifiedConnection(adoUrl);

                systemLog.Info(azureDevOpsConnection?.PersonalAccessToken?.Value != null
                    ? $"Found the PersonalAccessToken for {adoUrl.ProjectUrl ?? adoUrl.OrganizationUrl}"
                    : $"Could not find a PersonalAccessToken for {adoUrl.ProjectUrl ?? adoUrl.OrganizationUrl}");

                return azureDevOpsConnection?.PersonalAccessToken?.Value;
            }
            catch
            {
                return null;
            }
        }

        public IResult CheckWeCanGetBuilds(AdoProjectUrls adoProjectUrls, string? personalAccessToken = null)
        {
            var buildsUrl = $"{adoProjectUrls.ProjectUrl}/_apis/build/builds?$top=1&api-version=4.1";
            var (status, jObject) = client.Get(buildsUrl, personalAccessToken ?? GetPersonalAccessToken(adoProjectUrls));
            
            if (!status.IsSuccessStatusCode())
            {
                return Result.Failed($"Error while checking whether we can retrieve builds from Azure DevOps: {status.ToDescription(jObject, true)}");
            }

            return Result.Success();
        }

        IResultFromExtension<(int id, string url)[]> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls)
        {
            // ReSharper disable once StringLiteralTypo
            var workItemsUrl = $"{adoBuildUrls.ProjectUrl}/_apis/build/builds/{adoBuildUrls.BuildId}/workitems?api-version=4.1";

            var (status, jObject) = client.Get(workItemsUrl, GetPersonalAccessToken(adoBuildUrls));
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<(int id, string url)[]>.Success(Array.Empty<(int, string)>());
            }

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<(int id, string url)[]>.Failed($"Error while fetching work item references from Azure DevOps: {status.ToDescription(jObject)}");
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

        IResultFromExtension<(string title, int? commentCount)> GetWorkItem(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}?api-version=4.1", GetPersonalAccessToken(adoProjectUrls));
            
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<(string title, int? commentCount)>.Success((workItemId.ToString(), 0));
            }

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<(string title, int? commentCount)>.Failed($"Error while fetching work item details from Azure DevOps: {status.ToDescription(jObject)}");
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

        /// <returns>Up to 200 comments on the specified work item.</returns>
        IResultFromExtension<string[]> GetWorkItemComments(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}/comments?api-version=4.1-preview.2",
                GetPersonalAccessToken(adoProjectUrls));

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

        string BuildWorkItemBrowserUrl(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            // ReSharper disable once StringLiteralTypo
            return $"{adoProjectUrls.ProjectUrl}/_workitems?_a=edit&id={workItemId}";
        }

        string? GetReleaseNote(AdoProjectUrls adoProjectUrls, int workItemId, int? commentCount = null)
        {
            var releaseNotePrefix = store.GetMostQualifiedConnection(adoProjectUrls)?.ReleaseNoteOptions.ReleaseNotePrefix;
            if (string.IsNullOrWhiteSpace(releaseNotePrefix) || commentCount == 0)
            {
                return null;
            }

            var comments = GetWorkItemComments(adoProjectUrls, workItemId);
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

        IResultFromExtension<WorkItemLink> GetWorkItemLink(AdoProjectUrls adoProjectUrls, int workItemId)
        {
            var workItem = GetWorkItem(adoProjectUrls, workItemId) as ISuccessResult<(string title, int? commentCount)>;
            var releaseNote = workItem != null ? GetReleaseNote(adoProjectUrls, workItemId, workItem.Value.commentCount) : null;

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

        public IResultFromExtension<WorkItemLink[]> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls)
        {
            var workItemsRefs = GetBuildWorkItemsRefs(adoBuildUrls);
            if (workItemsRefs is FailureResult failure)
                return ResultFromExtension<WorkItemLink[]>.Failed(failure.Errors);

            var workItemLinks = ((ISuccessResult<(int id, string url)[]>)workItemsRefs).Value
                .Select(w => GetWorkItemLink(adoBuildUrls, w.id))
                .ToArray();
            var validWorkItemLinks = workItemLinks
                .OfType<ISuccessResult<WorkItemLink>>()
                .Select(r => r.Value)
                .ToArray();
            return ResultFromExtension<WorkItemLink[]>.Success(validWorkItemLinks);
        }

        public IResultFromExtension<string[]> GetProjectList(AdoProjectUrls adoUrl, string? personalAccessToken = null)
        {
            var (status, jObject) = client.Get($"{adoUrl.OrganizationUrl}/_apis/projects?api-version=4.1",
                personalAccessToken ?? GetPersonalAccessToken(adoUrl));

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<string[]>.Failed($"Error while fetching project list from Azure DevOps: {status.ToDescription(jObject, true)}");
            }

            return ResultFromExtension<string[]>.Success(jObject?["value"]?
                .Select(p => p["name"]?.ToString())
                .Cast<string>() // cast to keep the compiler happy with nullable checks
                .ToArray() ?? Array.Empty<string>());
        }
    }
}