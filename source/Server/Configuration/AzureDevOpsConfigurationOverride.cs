using Octopus.Data.Model;


namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationOverride
    {
        public bool IsOverriding { get; set; }

        public Setting[] Settings { get; set; } = new[] { new Setting() };
    }
    class Setting
    {
        public string? BaseUrl { get; set; }

        public SensitiveString? PersonalAccessToken { get; set; }

        public ReleaseNoteOptions ReleaseNoteOptions { get; set; } = new ReleaseNoteOptions();
    }
}
