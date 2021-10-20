using System.Threading;
using System.Threading.Tasks;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    interface IAzureDevOpsConfigurationStore : IExtensionConfigurationStoreAsync<AzureDevOpsConfiguration>
    {
        Task<string?> GetBaseUrl(CancellationToken cancellationToken);
        Task SetBaseUrl(string? baseUrl, CancellationToken cancellationToken);
        Task<SensitiveString?> GetPersonalAccessToken(CancellationToken cancellationToken);
        Task SetPersonalAccessToken(SensitiveString? value, CancellationToken cancellationToken);
        Task<string?> GetReleaseNotePrefix(CancellationToken cancellationToken);
        Task SetReleaseNotePrefix(string? releaseNotePrefix, CancellationToken cancellationToken);
    }
}