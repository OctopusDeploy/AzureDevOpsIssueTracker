using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Tools.ReSharper;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    readonly Configuration Configuration = Configuration.Release;

    [OctoVersion] readonly OctoVersionInfo OctoVersionInfo;

    [Solution] readonly Solution Solution;

    static AbsolutePath SourceDirectory => RootDirectory / "source";
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath PublishDirectory => RootDirectory / "publish";
    static AbsolutePath LocalPackagesDir => RootDirectory / ".." / "LocalPackages";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(PublishDirectory);
        });

    Target CalculateVersion => _ => _
        .Executes(() =>
        {
            //all the magic happens inside `[NukeOctoVersion]` above. we just need a target for TeamCity to call
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .DependsOn(Restore)
        .Executes(() =>
        {
            Logger.Info("Building AzureDevOps issue tracker v{0}", OctoVersionInfo.FullSemVer);

            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(OctoVersionInfo.FullSemVer)
                .EnableNoRestore());
        });

    Target CleanCode => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            ReSharperTasks.ReSharperCleanupCode(new ReSharperCleanupCodeSettings()
                .SetTargetPath(Solution.Path));

            if (IsLocalBuild) return;

            var gitRepo = GitRepository.FromLocalDirectory("./");
            if (gitRepo.Branch == null || gitRepo.Branch.StartsWith("prettybot/")) return;
            var prettyBotBranch = $"prettybot/{gitRepo.Branch}";

            if (prettyBotBranch is "main" or "master")
            {
                Logger.Info("Doing anything automated to the default branch is not recommended. Exiting.");
                return;
            }

            GitTasks.Git("config user.email \"bob@octopus.com\"");
            GitTasks.Git("config user.name \"prettybot[bot]\"");

            try
            {
                GitTasks.Git($"show-ref --verify --quiet refs/heads/{prettyBotBranch}");
                GitTasks.Git($"checkout -D {prettyBotBranch}");
            }
            catch
            {
                // ignored
            }


            GitTasks.Git("status");
            var gitStatus = GitTasks.Git("status -s");
            if (gitStatus.Count == 0)
            {
                var remote = GitTasks.Git($"git ls-remote origin {prettyBotBranch}");
                if (remote.Count == 0) GitTasks.Git($"push origin :{prettyBotBranch}");

                return;
            }

            GitTasks.Git($"checkout -b {prettyBotBranch}");
            GitTasks.Git("add -A .");
            GitTasks.Git("commit -m \"Run ReSharper code cleanup\"");
            GitTasks.Git($"push -f --set-upstream origin {prettyBotBranch}");
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .DependsOn(CleanCode)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .EnableNoRestore()
                .SetFilter(@"FullyQualifiedName\!~Integration.Tests"));
        });

    Target Pack => _ => _
        .DependsOn(Test)
        .Produces(ArtifactsDirectory / "*.nupkg")
        .Executes(() =>
        {
            Logger.Info("Packing AzureDevOps issue tracker v{0}", OctoVersionInfo.FullSemVer);

            // This is done to pass the data to github actions
            Console.Out.WriteLine($"::set-output name=semver::{OctoVersionInfo.FullSemVer}");
            Console.Out.WriteLine($"::set-output name=prerelease_tag::{OctoVersionInfo.PreReleaseTagWithDash}");

            DotNetPack(_ => _
                .SetProject(Solution)
                .SetVersion(OctoVersionInfo.FullSemVer)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild()
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.Normal)
                .SetProperty("NuspecFile", "../../build/Octopus.Server.Extensibility.AzureDevOpsIssueTracker.nuspec")
                .SetProperty("NuspecProperties", $"Version={OctoVersionInfo.FullSemVer}"));

            DotNetPack(_ => _
                .SetProject(RootDirectory / "source/Client/Client.csproj")
                .SetVersion(OctoVersionInfo.FullSemVer)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild()
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.Normal)
                .SetProperty("NuspecProperties", $"Version={OctoVersionInfo.FullSemVer}"));
        });

    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .TriggeredBy(Pack)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDir);
            ArtifactsDirectory.GlobFiles("*.nupkg")
                .ForEach(package =>
                {
                    CopyFileToDirectory(package, LocalPackagesDir);
                });
        });

    Target OutputPackagesToPush => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            var artifactPaths = ArtifactsDirectory.GlobFiles("*.nupkg")
                .NotEmpty()
                .Select(p => p.ToString());

            Console.WriteLine($"::set-output name=packages_to_push::{string.Join(',', artifactPaths)}");
        });

    Target Default => _ => _
        .DependsOn(OutputPackagesToPush);

    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);
}