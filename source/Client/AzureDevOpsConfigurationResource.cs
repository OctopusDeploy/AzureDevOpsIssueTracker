﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Octopus.Client.Extensibility.Attributes;
using Octopus.Client.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Client.Model;

namespace Octopus.Client.Extensibility.IssueTracker.AzureDevOps
{
    public class AzureDevOpsConfigurationResource : ExtensionConfigurationResource
    {
        public const string BaseUrlDisplayName = "Azure DevOps Base Url";
        public const string BaseUrlDescription = "Set the base url for the Azure DevOps organization or collection.";

        public AzureDevOpsConfigurationResource()
        {
            Id = "issuetracker-azuredevops";
        }

        [DisplayName(BaseUrlDisplayName)]
        [Description(BaseUrlDescription)]
        [Required]
        [Writeable]
        public string BaseUrl { get; set; }

        public const string PersonalAccessTokenDescription =
            "A Personal Access Token authorized to read scopes 'Build' and 'Work items', added under User Settings.";

        [DisplayName("Personal Access Token")]
        [Description(PersonalAccessTokenDescription)]
        [Writeable]
        public SensitiveValue PersonalAccessToken { get; set; }
    }
}