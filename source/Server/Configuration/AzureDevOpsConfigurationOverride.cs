using Octopus.Data.Model;


namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationOverride
    {

        public bool IsOverriding { get; set; }

        public string? BaseUrl { get; set; }

        public SensitiveString? PersonalAccessToken { get; set; }

        public ReleaseNoteOptions ReleaseNoteOptions { get; set; } = new ReleaseNoteOptions();
    }
}
