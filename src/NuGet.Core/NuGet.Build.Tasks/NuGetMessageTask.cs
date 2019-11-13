// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// A task that logs a message from the localized <see cref="Strings"/> resource.
    /// </summary>
    public sealed class NuGetMessageTask : Task
    {
        public NuGetMessageTask()
            : base(Strings.ResourceManager)
        {
        }

        public string[] Args { get; set; }

        public string Importance { get; set; }

        [Required]
        public string Name { get; set; }

        public override bool Execute()
        {
            MessageImportance messageImportance = MessageImportance.Normal;

            if (!string.IsNullOrWhiteSpace(Importance) && !Enum.TryParse(Importance, ignoreCase: true, out messageImportance))
            {
                messageImportance = MessageImportance.Normal;
            }

            Log.LogMessageFromResources(messageImportance, Name, Args);

            return true;
        }
    }
}
