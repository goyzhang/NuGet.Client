// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.DependencyResolver.Tests;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Commands.Test
{
    public class RestoreSummaryTests
    {
        private static readonly NoOpRestoreResult NoOpRestoreResult = new NoOpRestoreResult(
            success: true,
            lockFile: null,
            previousLockFile: null,
            lockFilePath: null,
            cacheFile: null,
            cacheFilePath: null,
            projectStyle: ProjectStyle.PackageReference,
            elapsedTime: TimeSpan.Zero);

        private static readonly RestoreResult RestoreResult = new RestoreResult(
            success: true,
            restoreGraphs: new[]
            {
                RestoreTargetGraph.Create(
                    new[]
                    {
                        new GraphNode<RemoteResolveResult>(new LibraryRange("project", LibraryDependencyTarget.All))
                        {
                            Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity("project", new NuGetVersion("1.0.0"), LibraryType.Project))
                            {
                                Data = new RemoteResolveResult
                                {
                                    Match = new RemoteMatch
                                    {
                                        Provider = null
                                    }
                                }
                            }
                        }
                    },
                    new TestRemoteWalkContext(),
                    NullLogger.Instance,
                    FrameworkConstants.CommonFrameworks.NetStandard10)
            },
            compatibilityCheckResults: Enumerable.Empty<CompatibilityCheckResult>(),
            msbuildFiles: Enumerable.Empty<MSBuildOutputFile>(),
            lockFile: null,
            previousLockFile: null,
            lockFilePath: null,
            cacheFile: null,
            cacheFilePath: null,
            packagesLockFilePath: null,
            packagesLockFile: null,
            projectStyle: ProjectStyle.PackageReference,
            elapsedTime: TimeSpan.Zero);

        private readonly ITestOutputHelper _testOutputHelper;

        public RestoreSummaryTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Log_WhenAllProjectsAreUpToDate_LogsMessageIndicatingAllProjectsAreUpToDate()
        {
            var restoreSummaries = new List<RestoreSummary>
            {
                new RestoreSummary(
                    NoOpRestoreResult,
                    inputPath: "ProjectA",
                    configFiles: new List<string>
                    {
                        "ConfigFile1",
                        "ConfigFile2"
                    },
                    sourceRepositories: new List<SourceRepository>
                    {
                        new SourceRepository(new PackageSource("https://nuget.org"), Enumerable.Empty<INuGetResourceProvider>())
                    },
                    errors: Enumerable.Empty<RestoreLogMessage>())
            };

            var logger = new TestLogger(_testOutputHelper);

            RestoreSummary.Log(logger, restoreSummaries);

            logger.MinimalMessages.Should().Contain(i => i.Equals(Strings.Log_AllProjectsUpToDate));
        }

        [Fact]
        public void Log_WhenOnlyOneProject_LogsNothing()
        {
            var restoreSummaries = new List<RestoreSummary>
            {
                new RestoreSummary(
                    RestoreResult,
                    inputPath: "ProjectA",
                    configFiles: new List<string>
                    {
                        "ConfigFile1"
                    },
                    sourceRepositories: new List<SourceRepository>
                    {
                        new SourceRepository(new PackageSource("https://nuget.org"), Enumerable.Empty<INuGetResourceProvider>())
                    },
                    errors: Enumerable.Empty<RestoreLogMessage>())
            };

            var logger = new TestLogger(_testOutputHelper);

            RestoreSummary.Log(logger, restoreSummaries);

            logger.MinimalMessages.Should().BeEmpty();
        }

        [Fact]
        public void Log_WhenSomeProjectsOutOfDate_LogsMessageIndicatingHowManyProjectsAreUpToDate()
        {
            var restoreSummaries = new List<RestoreSummary>
            {
                new RestoreSummary(
                    NoOpRestoreResult,
                    inputPath: "ProjectA",
                    configFiles: new List<string>
                    {
                        "ConfigFile1"
                    },
                    sourceRepositories: new List<SourceRepository>
                    {
                        new SourceRepository(new PackageSource("https://nuget.org"), Enumerable.Empty<INuGetResourceProvider>()),
                        new SourceRepository(new PackageSource("https://myget.org"), Enumerable.Empty<INuGetResourceProvider>())
                    },
                    errors: Enumerable.Empty<RestoreLogMessage>()),
                new RestoreSummary(
                    RestoreResult,
                    inputPath: "ProjectB",
                    configFiles: new List<string>
                    {
                        "ConfigFile1"
                    },
                    sourceRepositories: new List<SourceRepository>
                    {
                        new SourceRepository(new PackageSource("https://nuget.org"), Enumerable.Empty<INuGetResourceProvider>())
                    },
                    errors: Enumerable.Empty<RestoreLogMessage>())
            };

            var logger = new TestLogger(_testOutputHelper);

            RestoreSummary.Log(logger, restoreSummaries);

            logger.MinimalMessages.Should().Contain(i => i.Equals(string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectUpToDateSummary, 1, restoreSummaries.Count)));
        }
    }
}
