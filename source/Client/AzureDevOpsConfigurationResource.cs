using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Octopus.Client.Extensibility.Attributes;
using Octopus.Client.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Client.Model;

namespace Octopus.Client.Extensibility.IssueTracker.AzureDevOps
{
    public class AzureDevOpsConfigurationResource : ExtensionConfigurationResource
    {
        public const string BaseUrlDisplayName = "Azure DevOps Base Url";
        public const string BaseUrlDescription = "Set the base url for the Azure DevOps organization or collection or project.";

        public const string PersonalAccessTokenDescription =
            "A Personal Access Token (PAT) authorized to read scopes 'Build' and 'Work items', added under User Settings.";

        public AzureDevOpsConfigurationResource()
        {
            Id = "issuetracker-azuredevops";
        }

        [DisplayName(BaseUrlDisplayName)]
        [Description(BaseUrlDescription)]
        [Required]
        [Writeable]
        public string BaseUrl { get; set; }

        [DisplayName("Personal Access Token")]
        [Description(PersonalAccessTokenDescription)]
        [Writeable]
        public SensitiveValue PersonalAccessToken { get; set; }

        [DisplayName("Release Note Options")]
        public ReleaseNoteOptionsResource ReleaseNoteOptions { get; set; } = new ReleaseNoteOptionsResource();
    }

    public class ReleaseNoteOptionsResource
    {
        public const string ReleaseNotePrefixDescription =
            "Set the prefix to look for when finding release notes for Azure DevOps issues. For example `Release note:`.";

        [DisplayName("Release Note Prefix")]
        [Description(ReleaseNotePrefixDescription)]
        [Writeable]
        public string ReleaseNotePrefix { get; set; }
    }
}