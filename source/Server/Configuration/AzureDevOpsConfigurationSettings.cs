using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Server.Extensibility.HostServices.Mapping;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationSettings :
        ExtensionConfigurationSettingsAsync<AzureDevOpsConfiguration, AzureDevOpsConfigurationResource, IAzureDevOpsConfigurationStore>,
        IAzureDevOpsConfigurationSettings
    {
        public AzureDevOpsConfigurationSettings(IAzureDevOpsConfigurationStore configurationDocumentStore) : base(configurationDocumentStore)
        {
        }

        public override string Id => AzureDevOpsConfigurationStore.SingletonId;

        public override string ConfigurationSetName => "Azure DevOps Issue Tracker";

        public override string Description => "Azure DevOps Issue Tracker settings";

        public override async IAsyncEnumerable<IConfigurationValue> GetConfigurationValues([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var isEnabled = await ConfigurationDocumentStore.GetIsEnabled(cancellationToken);
            yield return new ConfigurationValue<bool>("Octopus.IssueTracker.AzureDevOpsIssueTracker", isEnabled,
                isEnabled, "Is Enabled");
            var baseUrl = await ConfigurationDocumentStore.GetBaseUrl(cancellationToken);
            yield return new ConfigurationValue<string?>("Octopus.IssueTracker.AzureDevOpsBaseUrl", baseUrl,
                isEnabled && !string.IsNullOrWhiteSpace(baseUrl),
                AzureDevOpsConfigurationResource.BaseUrlDisplayName);
            var personalAccessToken = await ConfigurationDocumentStore.GetPersonalAccessToken(cancellationToken);
            yield return new ConfigurationValue<SensitiveString?>("Octopus.IssueTracker.AzureDevOpsPersonalAccessToken",
                personalAccessToken,
                false, "Azure DevOps Personal Access Token");
            var releaseNotePrefix = await ConfigurationDocumentStore.GetReleaseNotePrefix(cancellationToken);
            yield return new ConfigurationValue<string?>("Octopus.IssueTracker.AzureDevOpsReleaseNotePrefix",
                releaseNotePrefix,
                isEnabled && !string.IsNullOrWhiteSpace(releaseNotePrefix), "AzureDevOps Release Note Prefix");
        }

        public override void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureDevOpsConfigurationResource, AzureDevOpsConfiguration>();
            builder.Map<ReleaseNoteOptionsResource, ReleaseNoteOptions>();
        }
    }
}