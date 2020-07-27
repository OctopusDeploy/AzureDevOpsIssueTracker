using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    interface IAzureDevOpsConfigurationStore : IExtensionConfigurationStore<AzureDevOpsConfiguration>
    {
        string? GetBaseUrl();
        void SetBaseUrl(string? baseUrl);

        SensitiveString? GetPersonalAccessToken();
        void SetPersonalAccessToken(SensitiveString? value);
        string? GetReleaseNotePrefix();
        void SetReleaseNotePrefix(string? releaseNotePrefix);
    }
}