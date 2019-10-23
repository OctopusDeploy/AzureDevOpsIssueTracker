using Octopus.Diagnostics;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Resources.IssueTrackers;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems
{
    public class WorkItemLinkMapper : IWorkItemLinkMapper
    {
        private readonly IAzureDevOpsConfigurationStore store;
        private readonly IAdoApiClient client;

        public WorkItemLinkMapper(IAzureDevOpsConfigurationStore store, IAdoApiClient client)
        {
            this.store = store;
            this.client = client;
        }

        public string CommentParser => AzureDevOpsConfigurationStore.CommentParser;
        public bool IsEnabled => store.GetIsEnabled();

        public SuccessOrErrorResult<WorkItemLink[]> Map(OctopusBuildInformation buildInformation, ILogWithContext log)
        {
            // For ADO, we should ignore anything that wasn't built by ADO because we get work items from the build
            if (!IsEnabled
                || buildInformation?.BuildEnvironment != "Azure DevOps"
                || string.IsNullOrWhiteSpace(buildInformation?.BuildUrl))
                return null;

            return client.GetBuildWorkItemLinks(AdoBuildUrls.ParseBrowserUrl(buildInformation.BuildUrl));
        }
    }
}