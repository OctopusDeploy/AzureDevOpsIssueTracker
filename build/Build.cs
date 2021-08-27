using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Tooling;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild, IExtensionBuild
{
    public Enumeration Configuration => global::Configuration.Release;
    public string NuspecFilePath => "../../build/Octopus.Server.Extensibility.AzureDevOpsIssueTracker.nuspec";
    
    public static int Main() => Execute<Build>(x => ((IExtensionBuild)x).Default);
    
}