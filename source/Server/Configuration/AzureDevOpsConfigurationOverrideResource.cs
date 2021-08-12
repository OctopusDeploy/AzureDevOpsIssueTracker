using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    class AzureDevOpsConfigurationOverrideResource: IExtensionConfigurationOverrideResource
    {
        [DisplayName("Override Settings")]
        [Description("Whether or not this extension overrides settings for this space")]
        [Required]
        [Writeable]
        public bool IsOverriding { get; set; }
        
        public const string BaseUrlDisplayName = "Azure DevOps Base Url";
        public const string BaseUrlDescription = "Set the base url for the Azure DevOps organization or collection or project.";

        [DisplayName(BaseUrlDisplayName)]
        [Description(BaseUrlDescription)]
        [Required]
        [Writeable]
        public string? BaseUrl { get; set; }

        public const string PersonalAccessTokenDescription =
            "A Personal Access Token (PAT) authorized to read scopes 'Build' and 'Work items', added under User Settings.";

        [DisplayName("Personal Access Token")]
        [Description(PersonalAccessTokenDescription)]
        [Writeable]
        [AllowConnectivityCheck("Azure DevOps configuration", AzureDevOpsIssueTrackerApi.ApiConnectivityCheck, nameof(BaseUrl), nameof(PersonalAccessToken))]
        public SensitiveValue PersonalAccessToken { get; set; }

        [DisplayName("Release Note Options")]
        public ReleaseNoteOptionsResource ReleaseNoteOptions { get; set; } = new ReleaseNoteOptionsResource();
    }
}