using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Tools.ReSharper;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.ValueInjection;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public interface IOctopusNukeBuild : INukeBuild
{
    Enumeration Configuration { get; }

    [Solution]
    Solution Solution => ValueInjectionUtility.TryGetValue(() => Solution);

    [OctoVersion]
    OctoVersionInfo OctoVersionInfo => ValueInjectionUtility.TryGetValue(() => OctoVersionInfo);

    AbsolutePath SourceDirectory => RootDirectory / "source";
    public AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    public AbsolutePath PublishDirectory => RootDirectory / "publish";
    public AbsolutePath LocalPackagesDir => RootDirectory / ".." / "LocalPackages";
}

public interface IRestore : IOctopusNukeBuild
{
    Target Restore => _ => _
        .TryDependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });
}

public interface IClean : IOctopusNukeBuild
{
    Target Clean => _ => _
        .TryBefore<IRestore>(x => x.Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(PublishDirectory);
        });
}

public interface ICompile : IOctopusNukeBuild
{
    Target Compile => _ => _
        .TryDependsOn<IClean>(x => x.Clean)
        .TryDependsOn<IRestore>(x => x.Restore)
        .Executes(() =>
        {
            Logger.Info("Building AzureDevOps issue tracker v{0}", OctoVersionInfo.FullSemVer);
            Logger.Success("AzureDevOps issue tracker v{0}", OctoVersionInfo.FullSemVer);

            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(OctoVersionInfo.FullSemVer)
                .EnableNoRestore());
        });
}

public interface ICleanCode : IOctopusNukeBuild
{
    Target CleanCode => _ => _
        .OnlyWhenStatic(() => !IsLocalBuild)
        .TryTriggeredBy<IPackComponent>(x => x.Pack)
        .TryTriggeredBy<IPackExtension>(x => x.Pack)
        .Executes(() =>
        {
            ReSharperTasks.ReSharperCleanupCode(new ReSharperCleanupCodeSettings()
                .SetTargetPath(Solution.Path));

            var currentBranch = GitRepository.FromLocalDirectory("./").Branch;
            if (currentBranch == null || currentBranch.StartsWith("prettybot/")) return;
            var prettyBotBranch = $"prettybot/{currentBranch}";

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
}

public interface ITest : IOctopusNukeBuild
{
    Target Test => _ => _
        .TryDependsOn<ICompile>(x => x.Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .EnableNoRestore()
                .SetFilter(@"FullyQualifiedName\!~Integration.Tests"));
        });
}

public interface IPackExtension : IOctopusNukeBuild
{
    public string NuspecFilePath { get; }

    Target Pack => _ => _
        .TryDependsOn<ITest>(x => x.Test)
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
                .SetProperty("NuspecFile", NuspecFilePath)
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
}

public interface IPackComponent : IOctopusNukeBuild
{
    Target Pack => _ => _
        .TryDependsOn<ITest>(x => x.Test)
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
                .SetProperty("NuspecProperties", $"Version={OctoVersionInfo.FullSemVer}"));
        });
}

public interface ICopyToLocalPackages : IOctopusNukeBuild
{
    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .TryTriggeredBy<IPackExtension>(x => x.Pack)
        .TryTriggeredBy<IPackComponent>(x => x.Pack)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDir);
            ArtifactsDirectory.GlobFiles("*.nupkg")
                .ForEach(package =>
                {
                    CopyFileToDirectory(package, LocalPackagesDir, FileExistsPolicy.Overwrite);
                });
        });
}

public interface IOutputPackagesToPush : IOctopusNukeBuild
{
    Target OutputPackagesToPush => _ => _
        .TryDependsOn<IPackExtension>(x => x.Pack)
        .Executes(() =>
        {
            var artifactPaths = ArtifactsDirectory.GlobFiles("*.nupkg")
                .NotEmpty()
                .Select(p => p.ToString());

            Console.WriteLine($"::set-output name=packages_to_push::{string.Join(',', artifactPaths)}");
        });
}

public interface IExtensionBuild :
    IRestore,
    IClean,
    ICompile,
    ICleanCode,
    ITest,
    IPackExtension,
    IOutputPackagesToPush,
    ICopyToLocalPackages
{
    Target Default => _ => _
        .TryDependsOn<IOutputPackagesToPush>(x => x.OutputPackagesToPush);
}

public interface IComponentBuild :
    IRestore,
    IClean,
    ICompile,
    ICleanCode,
    ITest,
    IPackComponent,
    IOutputPackagesToPush,
    ICopyToLocalPackages
{
    Target Default => _ => _
        .TryDependsOn<IOutputPackagesToPush>(x => x.OutputPackagesToPush);
}