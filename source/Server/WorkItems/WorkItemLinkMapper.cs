using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Results;
using Octopus.Server.MessageContracts.Features.BuildInformation;
using Octopus.Server.MessageContracts.Features.IssueTrackers;
using Octopus.Server.MessageContracts.Features.Spaces;

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

        public async Task<IResultFromExtension<WorkItemLink[]>> Map(SpaceId spaceId, OctopusBuildInformation buildInformation, CancellationToken cancellationToken)
        {
            // For ADO, we should ignore anything that wasn't built by ADO because we get work items from the build
            if (!IsEnabled)
                return ResultFromExtension<WorkItemLink[]>.ExtensionDisabled();
            if (buildInformation?.BuildEnvironment != "Azure DevOps"
                || string.IsNullOrWhiteSpace(buildInformation?.BuildUrl))
                return ResultFromExtension<WorkItemLink[]>.Success(Array.Empty<WorkItemLink>());
            
            return await client.GetBuildWorkItemLinks(AdoBuildUrls.ParseBrowserUrl(buildInformation.BuildUrl), spaceId, cancellationToken);
        }
    }
}