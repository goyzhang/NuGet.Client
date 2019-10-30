// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Utility;

namespace NuGet.VisualStudio.Telemetry
{
    public sealed class PackageSourceTelemetry : IDisposable
    {
        private ConcurrentDictionary<string, Data> _data;
        private List<SourceRepository> _sources;
        private Guid _parentId;

        public PackageSourceTelemetry(List<SourceRepository> sources, Guid parentId)
        {
            _data = new ConcurrentDictionary<string, Data>();
            ProtocolDiagnostics.Event += ProtocolDiagnostics_Event;
            _sources = sources;
            _parentId = parentId;
        }

        private void ProtocolDiagnostics_Event(ProtocolDiagnosticEvent pdEvent)
        {
            var data = _data.GetOrAdd(pdEvent.Source, _ => new Data());

            var resourceData = pdEvent.Url.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                ? data.Nupkg
                : data.Metadata;

            lock (resourceData.Lock)
            {
                ApplyTiming(resourceData.EventTiming, pdEvent.EventDuration);

                if (pdEvent.HeaderDuration.HasValue)
                {
                    if (resourceData.HeaderTiming == null)
                    {
                        resourceData.HeaderTiming = new ResourceTimingData();
                    }

                    ApplyTiming(resourceData.HeaderTiming, pdEvent.HeaderDuration.Value);
                }

                if (pdEvent.IsSuccess)
                {
                    resourceData.Successful++;
                }

                if (pdEvent.IsRetry)
                {
                    resourceData.Retries++;
                }

                if (pdEvent.IsCancelled)
                {
                    resourceData.Cancelled++;
                }
            }
        }

        private void ApplyTiming(ResourceTimingData timingData, TimeSpan duration)
        {
            timingData.Requests++;
            timingData.TotalDuration += duration;

            if (timingData.MinDuration > duration)
            {
                timingData.MinDuration = duration;
            }

            if (timingData.MaxDuration < duration)
            {
                timingData.MaxDuration = duration;
            }
        }

        public void Dispose()
        {
            ProtocolDiagnostics.Event -= ProtocolDiagnostics_Event;

            var parentId= _parentId.ToString();
            foreach (var source in _sources)
            {
                Data data;
                if (_data.TryGetValue(source.PackageSource.Name, out data) && (data.Metadata.EventTiming.Requests + data.Nupkg.EventTiming.Requests) > 0)
                {
                    var @event = new TelemetryEvent("PackageSourceDiagnostics",
                        new Dictionary<string, object>()
                        {
                            { "ParentId", parentId},
                        });

                    // source info
                    @event.AddPiiData("source.url", source.PackageSource.Source);
                    @event["source.type"] = source.PackageSource.IsHttp ? "http" : source.PackageSource.IsLocal ? "local" : "unknown";
                    @event["source.protocol"] = source.PackageSource.ProtocolVersion;
                    var msFeed = GetMsFeed(source.PackageSource.SourceUri);
                    if (msFeed != null)
                    {
                        @event["source.msfeed"] = msFeed;
                    }

                    // metadata
                    lock (data.Metadata.Lock)
                    {
                        @event["metadata.requests"] = data.Metadata.EventTiming.Requests;
                        @event["metadata.success"] = data.Metadata.Successful;
                        @event["metadata.retries"] = data.Metadata.Retries;
                        @event["metadata.cancelled"] = data.Metadata.Cancelled;

                        if (data.Metadata.EventTiming.Requests > 0)
                        {
                            @event["metadata.timing.min"] = data.Metadata.EventTiming.MinDuration.TotalMilliseconds;
                            @event["metadata.timing.mean"] = data.Metadata.EventTiming.TotalDuration.TotalMilliseconds / data.Metadata.EventTiming.Requests;
                            @event["metadata.timing.max"] = data.Metadata.EventTiming.MaxDuration.TotalMilliseconds;
                        }

                        if (data.Metadata.HeaderTiming != null && data.Metadata.HeaderTiming.Requests > 0)
                        {
                            @event["metadata.header.timing.min"] = data.Metadata.HeaderTiming.MinDuration.TotalMilliseconds;
                            @event["metadata.header.timing.mean"] = data.Metadata.HeaderTiming.TotalDuration.TotalMilliseconds / data.Metadata.HeaderTiming.Requests;
                            @event["metadata.header.timing.max"] = data.Metadata.HeaderTiming.MaxDuration.TotalMilliseconds;
                        }
                    }

                    // nupkgs
                    lock (data.Nupkg.Lock)
                    {
                        @event["nupkg.requests"] = data.Nupkg.EventTiming.Requests;
                        @event["nupkg.success"] = data.Nupkg.Successful;
                        @event["nupkg.retries"] = data.Nupkg.Retries;
                        @event["nupkg.cancelled"] = data.Nupkg.Cancelled;

                        if (data.Nupkg.EventTiming.Requests > 0)
                        {
                            @event["nupkg.timing.min"] = data.Nupkg.EventTiming.MinDuration.TotalMilliseconds;
                            @event["nupkg.timing.mean"] = data.Nupkg.EventTiming.TotalDuration.TotalMilliseconds / data.Nupkg.EventTiming.Requests;
                            @event["nupkg.timing.max"] = data.Nupkg.EventTiming.MaxDuration.TotalMilliseconds;
                        }

                        if (data.Nupkg.HeaderTiming != null && data.Nupkg.HeaderTiming.Requests > 0)
                        {
                            @event["nupkg.header.timing.min"] = data.Nupkg.HeaderTiming.MinDuration.TotalMilliseconds;
                            @event["nupkg.header.timing.mean"] = data.Nupkg.HeaderTiming.TotalDuration.TotalMilliseconds / data.Nupkg.HeaderTiming.Requests;
                            @event["nupkg.header.timing.max"] = data.Nupkg.HeaderTiming.MaxDuration.TotalMilliseconds;
                        }
                    }

                    TelemetryActivity.EmitTelemetryEvent(@event);
                }
            }
       }

        private string GetMsFeed(Uri sourceUri)
        {
            var scheme = sourceUri.Scheme;
            if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                var host = sourceUri.Host;
                if (host.Equals("nuget.org", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".nuget.org", StringComparison.OrdinalIgnoreCase))
                {
                    return "nuget.org";
                }
                else if (host.Equals("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase))
                {
                    return "Azure Artifacts";
                }
                else if (host.Equals("nuget.pkg.github.com", StringComparison.OrdinalIgnoreCase))
                {
                    return "GitHub Package Registry";
                }
            }
            else
            {
                if (sourceUri.LocalPath.EndsWith(@"\Microsoft SDKs\NuGetPackages\", StringComparison.OrdinalIgnoreCase))
                {
                    return "VS Offline";
                }
            }

            return null;
        }

        private class Data
        {
            public ResourceData Metadata { get; }
            public ResourceData Nupkg { get; }

            public Data()
            {
                Metadata = new ResourceData();
                Nupkg = new ResourceData();
            }
        }

        private class ResourceData
        {
            public object Lock = new object();
            public ResourceTimingData EventTiming = new ResourceTimingData();
            public ResourceTimingData HeaderTiming;
            public int Successful;
            public int Retries;
            public int Cancelled;
        }

        private class ResourceTimingData
        {
            public int Requests;
            public TimeSpan TotalDuration;
            public TimeSpan MinDuration = TimeSpan.MaxValue;
            public TimeSpan MaxDuration;
        }
    }
}
