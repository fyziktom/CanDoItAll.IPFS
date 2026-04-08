using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ObservabilityTelemetryTests
{
    [TestMethod]
    public async Task CurrentNodeLeaseFactory_Emits_Custom_Activity_And_Metrics()
    {
        var targetRegistry = CreateRegistry("http://203.0.113.10:5001/", timeoutSeconds: 15);
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new CurrentNodeLeaseFactory(
            targetRegistry,
            new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance),
            httpClientFactory);
        var activities = new List<string>();
        var measurements = new List<string>();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == NodeControlTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity.OperationName)
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == NodeControlTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => measurements.Add(instrument.Name));
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => measurements.Add(instrument.Name));
        meterListener.Start();

        using var lease = await factory.CreateLeaseAsync(NodeConnectionRequestCategory.Admin, CancellationToken.None).ConfigureAwait(false);

        CollectionAssert.Contains(httpClientFactory.RequestedNames, NodeControlHttpClientNames.NodeAdmin);
        CollectionAssert.Contains(activities, "node.create-lease");
        CollectionAssert.Contains(measurements, "ipfs.nodecontrol.operation.count");
        CollectionAssert.Contains(measurements, "ipfs.nodecontrol.operation.duration");
    }

    [TestMethod]
    public async Task Named_Node_HttpClient_Propagates_Correlation_Header_From_Request_Context()
    {
        var handler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNodeControlHttpClients();
        services.Configure<HttpClientFactoryOptions>(NodeControlHttpClientNames.NodeRead, options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler);
        });

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            TraceIdentifier = "corr-test-123"
        };

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(NodeControlHttpClientNames.NodeRead);
        client.BaseAddress = new Uri("http://127.0.0.1/");

        using var response = await client.PostAsync("api/v0/version", content: null, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(handler.LastRequest);
        Assert.IsTrue(handler.LastRequest!.Headers.TryGetValues(NodeControlTelemetry.CorrelationHeaderName, out var values));
        Assert.AreEqual("corr-test-123", values.Single());
    }

    [TestMethod]
    public void RemotePinWorkflow_Enqueue_Emits_Custom_Activity()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ipfs-node-control-observability-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var activities = new List<string>();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == NodeControlTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity.OperationName)
        };
        ActivitySource.AddActivityListener(activityListener);

        try
        {
            var requestStore = new RemotePinRequestStore(Options.Create(new RemotePinRequestStoreOptions
            {
                FilePath = Path.Combine(tempRoot, "remote-pin-requests.json")
            }));
            var settingsStore = new ServerNodeSettingsStore(Options.Create(new ServerNodeSettingsStoreOptions
            {
                FilePath = Path.Combine(tempRoot, "current-node-settings.json")
            }));
            settingsStore.Save(new NodeConnectionSettings
            {
                Label = "Receiver node",
                BaseUrl = "http://203.0.113.10:5001/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            });
            var targetRegistry = new CurrentNodeTargetRegistry(Options.Create(new NodeConnectionSettings()), settingsStore);
            var bootstrapService = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);
            var leaseFactory = new CurrentNodeLeaseFactory(targetRegistry, bootstrapService);
            var explorerIndexStore = new ExplorerIndexStore(Options.Create(new ExplorerIndexStoreOptions
            {
                FilePath = Path.Combine(tempRoot, "explorer-index.db")
            }));
            var workflow = new RemotePinRequestWorkflowService(
                requestStore,
                leaseFactory,
                explorerIndexStore,
                new RemotePinRequestSecurityService(Options.Create(new RemotePinSecurityOptions
                {
                    CompatibilityModeEnabled = true
                })),
                NullLogger<RemotePinRequestWorkflowService>.Instance);

            workflow.Enqueue(new RemotePinRequestEnvelope
            {
                RequestId = Guid.NewGuid().ToString("N"),
                RequestedAtUtc = DateTimeOffset.UtcNow,
                Note = "Telemetry proof",
                Sender = new RemotePinSenderSnapshot(
                    "Sender node",
                    "http://127.0.0.1:5092/",
                    "http://127.0.0.1:5001/",
                    "12D3KooWSender",
                    ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
                Content = new RemotePinContentSnapshot(
                    "/ipfs/bafy-telemetry-proof",
                    "bafy-telemetry-proof",
                    "telemetry-proof.txt",
                    IsDirectory: false,
                    Size: 64,
                    ChildCount: 0)
            });

            CollectionAssert.Contains(activities, "remote-pin.enqueue");
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static CurrentNodeTargetRegistry CreateRegistry(string baseUrl, int timeoutSeconds)
    {
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(new NodeConnectionSettings
        {
            Label = "Observability test node",
            BaseUrl = baseUrl,
            ApiPath = "api/v0",
            TimeoutSeconds = timeoutSeconds
        }, isHydrated: true);
        return targetRegistry;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        public List<string> RequestedNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedNames.Add(name);
            return new HttpClient(new StaticResponseHandler());
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            });
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            });
        }
    }
}
