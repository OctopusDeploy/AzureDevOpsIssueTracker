using System.Collections.Generic;
using System.Linq;
using Octopus.Server.Extensibility.Extensions.Model.Spaces;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.Metadata;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Spaces
{
    class SpaceSettingsMetadataProvider: IContributeSpaceSettingsMetadata
    {

        private readonly IAzureDevOpsConfigurationStore store;

        public SpaceSettingsMetadataProvider(IAzureDevOpsConfigurationStore store)
        {
            this.store = store;
        }

        public string ExtensionId => AzureDevOpsConfigurationStore.SingletonId;
        public string ExtensionName => AzureDevOpsIssueTracker.Name;

        public List<PropertyMetadata> Properties => store.GetIsEnabled()
            ? new MetadataGenerator().GetMetadata<AzureDevOpsConfigurationOverrideResource>().Types.First().Properties
            : new List<PropertyMetadata>();
    }
}