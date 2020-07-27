using System;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.HostServices.Model.BuildInformation;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Resources.IssueTrackers;
using Octopus.Server.Extensibility.Results;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems
{
    class WorkItemLinkMapper : IWorkItemLinkMapper
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

        public IResultFromExtension<WorkItemLink[]> Map(OctopusBuildInformation buildInformation)
        {
            // For ADO, we should ignore anything that wasn't built by ADO because we get work items from the build
            if (!IsEnabled)
                return ResultFromExtension<WorkItemLink[]>.ExtensionDisabled();
            if (buildInformation?.BuildEnvironment != "Azure DevOps"
                || string.IsNullOrWhiteSpace(buildInformation?.BuildUrl))
                return ResultFromExtension<WorkItemLink[]>.Success(Array.Empty<WorkItemLink>());

            return client.GetBuildWorkItemLinks(AdoBuildUrls.ParseBrowserUrl(buildInformation.BuildUrl));
        }
    }
}