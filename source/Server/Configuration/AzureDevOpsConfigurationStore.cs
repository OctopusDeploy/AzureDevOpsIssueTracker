using System.Threading;
using System.Threading.Tasks;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationStore : ExtensionConfigurationStoreAsync<AzureDevOpsConfiguration>,
        IAzureDevOpsConfigurationStore
    {
        public static string CommentParser = "Azure DevOps";
        public static string SingletonId = "issuetracker-azuredevops";

        public AzureDevOpsConfigurationStore(IConfigurationStoreAsync configurationStore) : base(configurationStore)
        {
        }

        public override string Id => SingletonId;

        public async Task<string?> GetBaseUrl(CancellationToken cancellationToken)
        {
            return await GetProperty(doc => doc.BaseUrl?.Trim('/'), cancellationToken);
        }

        public async Task SetBaseUrl(string? baseUrl, CancellationToken cancellationToken)
        {
            await SetProperty(doc => doc.BaseUrl = baseUrl?.Trim('/'), cancellationToken);
        }

        public async Task<SensitiveString?> GetPersonalAccessToken(CancellationToken cancellationToken)
        {
            return await GetProperty(doc => doc.PersonalAccessToken, cancellationToken);
        }

        public async Task SetPersonalAccessToken(SensitiveString? value, CancellationToken cancellationToken)
        {
            await SetProperty(doc => doc.PersonalAccessToken = value, cancellationToken);
        }

        public async Task<string?> GetReleaseNotePrefix(CancellationToken cancellationToken)
        {
            return await GetProperty(doc => doc.ReleaseNoteOptions.ReleaseNotePrefix, cancellationToken);
        }

        public async Task SetReleaseNotePrefix(string? releaseNotePrefix, CancellationToken cancellationToken)
        {
            await SetProperty(doc => doc.ReleaseNoteOptions.ReleaseNotePrefix = releaseNotePrefix, cancellationToken);
        }
    }
}