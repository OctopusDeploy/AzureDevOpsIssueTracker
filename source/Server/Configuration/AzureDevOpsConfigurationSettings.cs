using System.Collections.Generic;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Server.Extensibility.HostServices.Mapping;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationSettings :
        ExtensionConfigurationSettings<AzureDevOpsConfiguration, AzureDevOpsConfigurationResource, IAzureDevOpsConfigurationStore>,
        IAzureDevOpsConfigurationSettings
    {
        public AzureDevOpsConfigurationSettings(IAzureDevOpsConfigurationStore configurationDocumentStore) : base(configurationDocumentStore)
        {
        }

        public override string Id => AzureDevOpsConfigurationStore.SingletonId;

        public override string ConfigurationSetName => "Azure DevOps Issue Tracker";

        public override string Description => "Azure DevOps Issue Tracker settings";

        public override IEnumerable<IConfigurationValue> GetConfigurationValues()
        {
            var isEnabled = ConfigurationDocumentStore.GetIsEnabled();
            yield return new ConfigurationValue<bool>("Octopus.IssueTracker.AzureDevOpsIssueTracker", isEnabled,
                isEnabled, "Is Enabled");
            yield return new ConfigurationValue<string?>("Octopus.IssueTracker.AzureDevOpsBaseUrl", ConfigurationDocumentStore.GetBaseUrl(),
                isEnabled && !string.IsNullOrWhiteSpace(ConfigurationDocumentStore.GetBaseUrl()),
                AzureDevOpsConfigurationResource.BaseUrlDisplayName);
            yield return new ConfigurationValue<SensitiveString?>("Octopus.IssueTracker.AzureDevOpsPersonalAccessToken",
                ConfigurationDocumentStore.GetPersonalAccessToken(),
                false, "Azure DevOps Personal Access Token");
            yield return new ConfigurationValue<string?>("Octopus.IssueTracker.AzureDevOpsReleaseNotePrefix",
                ConfigurationDocumentStore.GetReleaseNotePrefix(),
                isEnabled && !string.IsNullOrWhiteSpace(ConfigurationDocumentStore.GetReleaseNotePrefix()), "AzureDevOps Release Note Prefix");
        }

        public override void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureDevOpsConfigurationResource, AzureDevOpsConfiguration>();
            builder.Map<ReleaseNoteOptionsResource, ReleaseNoteOptions>();
        }
    }
}