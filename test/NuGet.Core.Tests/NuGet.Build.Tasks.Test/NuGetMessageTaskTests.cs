// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.Common;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class NuGetMessageTaskTests
    {
        public static IEnumerable<object[]> GetMessageImportanceValues()
        {
            foreach (var value in Enum.GetValues(typeof(MessageImportance)))
            {
                switch (value)
                {
                    case MessageImportance.High:
                        yield return new object[] { value.ToString(), LogLevel.Minimal };
                        break;

                    case MessageImportance.Normal:
                        yield return new object[] { value.ToString(), LogLevel.Information };
                        break;

                    case MessageImportance.Low:
                        yield return new object[] { value.ToString(), LogLevel.Debug };
                        break;
                }
            }
        }

        [Fact]
        public void Execute_WhenInvalidImportanceSpecified_MessageIsLogged()
        {
            var buildEngine = new TestBuildEngine();

            var task = new NuGetMessageTask
            {
                BuildEngine = buildEngine,
                Name = nameof(Strings.MessageWithNoArgs),
                Importance = "Invalid"
            };

            task.Log.TaskResources = Strings.ResourceManager;

            task.Execute().Should().BeTrue();

            buildEngine.TestLogger.LogMessages.Should().Contain(i =>
                i.Level == LogLevel.Information &&
                i.Message.Equals(Strings.MessageWithNoArgs));
        }

        [Theory]
        [MemberData(nameof(GetMessageImportanceValues))]
        public void Execute_WhenNameAndArgsSupplied_MessageIsLogged(string messageImportance, LogLevel expectedLogLevel)
        {
            var buildEngine = new TestBuildEngine();

            var task = new NuGetMessageTask
            {
                BuildEngine = buildEngine,
                Name = nameof(Strings.MessageWithTwoArgs),
                Args = new[] { "Foo", "Bar" },
                Importance = messageImportance
            };

            task.Log.TaskResources = Strings.ResourceManager;

            task.Execute().Should().BeTrue();

            buildEngine.TestLogger.LogMessages.Should().Contain(i =>
                i.Level == expectedLogLevel &&
                i.Message.Equals(string.Format(Strings.MessageWithTwoArgs, "Foo", "Bar")));
        }

        [Theory]
        [MemberData(nameof(GetMessageImportanceValues))]
        public void Execute_WhenNameSupplied_MessageIsLogged(string messageImportance, LogLevel expectedLogLevel)
        {
            var buildEngine = new TestBuildEngine();

            var task = new NuGetMessageTask
            {
                BuildEngine = buildEngine,
                Name = nameof(Strings.MessageWithNoArgs),
                Importance = messageImportance
            };

            task.Log.TaskResources = Strings.ResourceManager;

            task.Execute().Should().BeTrue();

            buildEngine.TestLogger.LogMessages.Should().Contain(i =>
                i.Level == expectedLogLevel &&
                i.Message.Equals(Strings.MessageWithNoArgs));
        }
    }
}
