using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfiguration : ExtensionConfigurationDocument
    {
        public AzureDevOpsConfiguration() : base(AzureDevOpsConfigurationStore.SingletonId, "AzureDevOps", "Octopus Deploy", "1.0")
        {
        }

        public string? BaseUrl { get; set; }

        public SensitiveString? PersonalAccessToken { get; set; }

        public ReleaseNoteOptions ReleaseNoteOptions { get; set; } = new ReleaseNoteOptions();
    }

    class ReleaseNoteOptions
    {
        public string? ReleaseNotePrefix { get; set; }
    }
}