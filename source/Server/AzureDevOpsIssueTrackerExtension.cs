using Autofac;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.Extensions.Infrastructure;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Octopus.Server.Extensibility.Extensions.Model.Spaces;
using Octopus.Server.Extensibility.Extensions.WorkItems;
using Octopus.Server.Extensibility.HostServices.Web;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.AdoClients;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Configuration;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Spaces;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.Web;
using Octopus.Server.Extensibility.IssueTracker.AzureDevOps.WorkItems;

namespace Octopus.Server.Extensibility.IssueTracker.AzureDevOps
{
    [OctopusPlugin("Azure DevOps Issue Tracker", "Octopus Deploy")]
    public class AzureDevOpsIssueTrackerExtension : IOctopusExtension
    {
        public void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureDevOpsConfigurationMapping>()
                .As<IConfigurationDocumentMapper>()
                .InstancePerLifetimeScope();

            builder.RegisterType<DatabaseInitializer>()
                .As<IExecuteWhenDatabaseInitializes>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AzureDevOpsConfigurationStore>()
                .As<IAzureDevOpsConfigurationStore>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AzureDevOpsConfigurationSettings>()
                .As<IAzureDevOpsConfigurationSettings>()
                .As<IHasConfigurationSettings>()
                .As<IHasConfigurationSettingsResource>()
                .As<IContributeMappings>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AzureDevOpsIssueTracker>()
                .As<IIssueTracker>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SpaceSettingsMetadataProvider>()
                .As<IContributeSpaceSettingsMetadata>()
                .InstancePerDependency();

            builder.RegisterType<AzureDevOpsConfigureCommands>()
                .As<IContributeToConfigureCommand>()
                .InstancePerDependency();

            builder.RegisterType<HttpJsonClient>()
                .As<IHttpJsonClient>()
                .InstancePerDependency();
            HttpJsonClient.AuthMessageScope = "to read scopes 'Build' and 'Work items'";

            builder.RegisterType<AdoApiClient>()
                .As<IAdoApiClient>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AzureDevOpsConnectivityCheckAction>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<AzureDevOpsIssueTrackerHomeLinksContributor>()
                .As<IHomeLinksContributor>()
                .InstancePerDependency();

            builder.RegisterType<WorkItemLinkMapper>()
                .As<IWorkItemLinkMapper>()
                .InstancePerDependency();

            builder.RegisterType<HtmlConvert>()
                .AsSelf()
                .InstancePerLifetimeScope();
        }
    }
}