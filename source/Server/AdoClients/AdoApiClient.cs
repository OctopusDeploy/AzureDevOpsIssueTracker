using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octopus.Data;
using Octopus.Data.Model;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.HostServices.Model.Spaces;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;
using Octopus.Server.Extensibility.Mediator;
using Octopus.Server.Extensibility.Results;
using Octopus.Server.MessageContracts.Features.IssueTrackers;
using Octopus.Server.MessageContracts.Features.Spaces;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients
{
    interface IAdoApiClient
    {
        Task<IResultFromExtension<(int id, string url)[]>> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, SensitiveString? personalAccessToken,
            CancellationToken cancellationToken, bool testing = false);

        Task<IResultFromExtension<(string title, int? commentCount)>> GetWorkItem(AdoProjectUrls adoProjectUrls, int workItemId, SensitiveString? personalAccessToken,
            CancellationToken cancellationToken,
            bool testing = false);

        Task<IResultFromExtension<WorkItemLink[]>> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls, SpaceId spaceId, CancellationToken cancellationToken);
        Task<IResultFromExtension<string[]>> GetProjectList(AdoUrl adoUrl, SensitiveString? personalAccessToken, CancellationToken cancellationToken,
            bool testing = false);
    }

    class AdoApiClient : IAdoApiClient
    {
        private readonly ISystemLog systemLog;
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly IHttpJsonClient client;
        private readonly HtmlConvert htmlConvert;
        private readonly IMediator mediator;

        public AdoApiClient(ISystemLog systemLog, IAzureDevOpsConfigurationStore store, IHttpJsonClient client, HtmlConvert htmlConvert, IMediator mediator)
        {
            this.systemLog = systemLog;
            this.store = store;
            this.client = client;
            this.htmlConvert = htmlConvert;
            this.mediator = mediator;
        }

        private async Task<SensitiveString?> GetPersonalAccessToken(AdoUrl adoUrl, SpaceId spaceId, CancellationToken cancellationToken)
        {
            try
            {
                var (baseUrl, accessToken) = await GetBaseUrlWithOverrideCheck(adoUrl, spaceId, cancellationToken);
                if (baseUrl is null || accessToken is null )
                    return null;
                var uri = new Uri(baseUrl.TrimEnd('/'), UriKind.Absolute);
                return uri.IsBaseOf(new Uri(adoUrl.OrganizationUrl, UriKind.Absolute)) ? accessToken : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<(string? baseUrl, SensitiveString? accessToken)> GetBaseUrlWithOverrideCheck(AdoUrl adoUrl, SpaceId spaceId, CancellationToken cancellationToken)
        {
            var spaceSettings = await mediator.Request(
                new GetSpaceExtensionSettingsRequest<AzureDevOpsConfigurationOverride>(
                    AzureDevOpsConfigurationStore.SingletonId,
                    spaceId.Value.ToSpaceIdOrName()),
                cancellationToken);
            
            if (spaceSettings.Values?.IsOverriding == true)
            {
                var values = spaceSettings.Values?.Settings.FirstOrDefault(x => new Uri(x.BaseUrl?.TrimEnd('/'), UriKind.Absolute).IsBaseOf(new Uri(adoUrl.OrganizationUrl, UriKind.Absolute)));
                return values is null ? (null, null) : (values.BaseUrl, values.PersonalAccessToken);
                // return (spaceSettings.Values?.BaseUrl, spaceSettings.Values?.PersonalAccessToken);
            }

            return (store.GetBaseUrl(), store.GetPersonalAccessToken());
        }

        public async Task<IResultFromExtension<(int id, string url)[]>> GetBuildWorkItemsRefs(AdoBuildUrls adoBuildUrls, SensitiveString? personalAccessToken,
            CancellationToken cancellationToken,
            bool testing = false)
        {
            // ReSharper disable once StringLiteralTypo
            var workItemsUrl = $"{adoBuildUrls.ProjectUrl}/_apis/build/builds/{adoBuildUrls.BuildId}/workitems?api-version=4.1";

            var (status, jObject) = await client.Get(workItemsUrl, personalAccessToken?.Value);
            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<(int id, string url)[]>.Success(new (int, string)[0]);
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
            SensitiveString? personalAccessToken, CancellationToken cancellationToken, bool testing = false)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = await client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}?api-version=4.1",
                personalAccessToken?.Value);
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

                return ResultFromExtension<(string title, int? commentCount)>.Success((fields["System.Title"]?.ToString() ?? string.Empty, fields["System.CommentCount"]?.Value<int>() ?? default(int)));
            }
            catch
            {
                return ResultFromExtension<(string title, int? commentCount)>.Failed("Unable to interpret work item details from Azure DevOps.");
            }
        }

        /// <returns>Up to 200 comments on the specified work item.</returns>
        public async Task<IResultFromExtension<string[]>> GetWorkItemComments(AdoProjectUrls adoProjectUrls, int workItemId, SpaceId spaceId, CancellationToken cancellationToken)
        {
            // ReSharper disable once StringLiteralTypo
            var (status, jObject) = await client.Get($"{adoProjectUrls.ProjectUrl}/_apis/wit/workitems/{workItemId}/comments?api-version=4.1-preview.2",
                (await GetPersonalAccessToken(adoProjectUrls, spaceId, cancellationToken))?.Value);

            if (status.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return ResultFromExtension<string[]>.Success(new string[0]);
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

        async Task<string?> GetReleaseNote(AdoProjectUrls adoProjectUrls, int workItemId, SpaceId spaceId, CancellationToken cancellationToken, int? commentCount = null)
        {
            var releaseNotePrefix = store.GetReleaseNotePrefix();
            if (string.IsNullOrWhiteSpace(releaseNotePrefix) || commentCount == 0)
            {
                return null;
            }

            var comments = await GetWorkItemComments(adoProjectUrls, workItemId, spaceId, cancellationToken);
            if (comments is FailureResult failure)
            {
                // if we can't retrieve the comments then move on without
                systemLog.WarnFormat("Error retrieving Azure DevOps comments for work item {0}. Error: {1}", workItemId, failure.ErrorString);
                return null;
            }

            var releaseNoteRegex = new Regex("^" + Regex.Escape(releaseNotePrefix), RegexOptions.IgnoreCase);
            // Return (last, if multiple found) comment that matched release note prefix
            var releaseNoteComment = ((ISuccessResult<string[]>)comments).Value
                .LastOrDefault(ct => releaseNoteRegex.IsMatch(ct ?? ""));
            var releaseNote = releaseNoteComment != null
                ? releaseNoteRegex.Replace(releaseNoteComment, "").Trim()
                : null;
            return releaseNote;
        }

        async Task<IResultFromExtension<WorkItemLink>> GetWorkItemLink(AdoProjectUrls adoProjectUrls, int workItemId, SpaceId spaceId, CancellationToken cancellationToken)
        {
            var accessToken = await GetPersonalAccessToken(adoProjectUrls, spaceId, cancellationToken);
            if (accessToken is null)
            {
                throw new ApplicationException("Access token could not be determined");
            }
            var workItem = await GetWorkItem(adoProjectUrls, workItemId, accessToken, cancellationToken) as ISuccessResult<(string title, int? commentCount)>;
            var releaseNote = workItem != null ? await GetReleaseNote(adoProjectUrls, workItemId, spaceId, cancellationToken, workItem.Value.commentCount) : null;

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

        public async Task<IResultFromExtension<WorkItemLink[]>> GetBuildWorkItemLinks(AdoBuildUrls adoBuildUrls, SpaceId spaceId, CancellationToken cancellationToken)
        {
            var accessToken = await GetPersonalAccessToken(adoBuildUrls, spaceId, cancellationToken);
            if (accessToken is null)
            {
                throw new ApplicationException("Access token could not be determined");
            }

            var workItemsRefs = await GetBuildWorkItemsRefs(adoBuildUrls, accessToken, cancellationToken);
            if (workItemsRefs is FailureResult failure)
                return ResultFromExtension<WorkItemLink[]>.Failed(failure.Errors);

            var workItemLinks = await Task.WhenAll(((ISuccessResult<(int id, string url)[]>)workItemsRefs).Value
                .Select(w => GetWorkItemLink(adoBuildUrls, w.id, spaceId, cancellationToken))
                .ToArray());
            var validWorkItemLinks = workItemLinks
                .OfType<ISuccessResult<WorkItemLink>>()
                .Select(r => r.Value)
                .ToArray();
            return ResultFromExtension<WorkItemLink[]>.Success(validWorkItemLinks);
        }

        public async Task<IResultFromExtension<string[]>> GetProjectList(AdoUrl adoUrl, SensitiveString? personalAccessToken, CancellationToken cancellationToken,
            bool testing = false)
        {
            var (status, jObject) = await client.Get($"{adoUrl.OrganizationUrl}/_apis/projects?api-version=4.1",
                personalAccessToken?.Value);

            if (!status.IsSuccessStatusCode())
            {
                return ResultFromExtension<string[]>.Failed($"Error while fetching project list from Azure DevOps: {status.ToDescription(jObject, testing)}");
            }

            return ResultFromExtension<string[]>.Success(jObject?["value"]?
                .Select(p => p["name"]?.ToString())
                .Cast<string>() // cast to keep the compiler happy with nullable checks
                .ToArray() ?? Array.Empty<string>());
        }
    }
}