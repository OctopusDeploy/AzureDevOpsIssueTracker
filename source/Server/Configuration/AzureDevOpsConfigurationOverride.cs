using System.Security;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationOverride
    {

        public bool IsOverriding { get; set; }
        
        public string? BaseUrl { get; set; }
        
        public SecureString? PersonalAccessToken { get; set; }
        
        public ReleaseNoteOptionsResource ReleaseNoteOptions { get; set; } = new ReleaseNoteOptionsResource();
    }
}
