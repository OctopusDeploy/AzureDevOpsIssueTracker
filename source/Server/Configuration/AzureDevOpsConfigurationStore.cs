using Octopus.Data.Model;
using Octopus.Data.Storage.Configuration;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationStore : ExtensionConfigurationStore<AzureDevOpsConfiguration>,
        IAzureDevOpsConfigurationStore
    {
        public static string CommentParser = "Azure DevOps";
        public static string SingletonId = "issuetracker-azuredevops";

        public AzureDevOpsConfigurationStore(IConfigurationStore configurationStore) : base(configurationStore)
        {
        }

        public override string Id => SingletonId;

        public string? GetBaseUrl()
        {
            return GetProperty(doc => doc.BaseUrl?.Trim('/'));
        }

        public void SetBaseUrl(string? baseUrl)
        {
            SetProperty(doc => doc.BaseUrl = baseUrl?.Trim('/'));
        }

        public SensitiveString? GetPersonalAccessToken() => GetProperty(doc => doc.PersonalAccessToken);
        public void SetPersonalAccessToken(SensitiveString? value) => SetProperty(doc => doc.PersonalAccessToken = value);
        public string? GetReleaseNotePrefix() => GetProperty(doc => doc.ReleaseNoteOptions.ReleaseNotePrefix);
        public void SetReleaseNotePrefix(string? releaseNotePrefix) => SetProperty(doc => doc.ReleaseNoteOptions.ReleaseNotePrefix = releaseNotePrefix);
    }
}