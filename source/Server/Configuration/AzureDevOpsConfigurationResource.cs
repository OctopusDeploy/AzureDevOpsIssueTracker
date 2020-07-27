using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Octopus.Data.Resources;
using Octopus.Data.Resources.Attributes;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration
{
    [Description("Configure the Azure DevOps Issue Tracker. [Learn more](https://g.octopushq.com/AzureDevOpsIssueTracker).")]
    class AzureDevOpsConfigurationResource : ExtensionConfigurationResource
    {
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
        public SensitiveValue? PersonalAccessToken { get; set; }

        [DisplayName("Release Note Options")]
        public ReleaseNoteOptionsResource ReleaseNoteOptions { get; set; } = new ReleaseNoteOptionsResource();
    }

    class ReleaseNoteOptionsResource
    {
        public const string ReleaseNotePrefixDescription =
            "Set the prefix to look for when finding release notes for Azure DevOps issues. For example `Release note:`.";

        [DisplayName("Release Note Prefix")]
        [Description(ReleaseNotePrefixDescription)]
        [Writeable]
        public string? ReleaseNotePrefix { get; set; }
    }
}