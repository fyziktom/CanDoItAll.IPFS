#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class LocalNodeBootstrapServiceTests
{
    [TestMethod]
    public async Task Start_Stop_And_Restart_Local_Node_On_A_Custom_Port()
    {
        await WithIsolatedLocalHostEnvironmentAsync(async () =>
        {
            var settings = new NodeConnectionSettings
            {
                BaseUrl = $"http://127.0.0.1:{GetUnusedPort()}/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };

            var targetRegistry = new CurrentNodeTargetRegistry();
            targetRegistry.Update(settings, isHydrated: true);
            var service = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);

            try
            {
                await service.StartLocalNodeAsync().ConfigureAwait(false);
                var firstVersion = await PostAsync(settings, "api/v0/version").ConfigureAwait(false);
                StringAssert.Contains(firstVersion, "\"Version\"");

                await service.StopLocalNodeAsync().ConfigureAwait(false);
                await AssertEndpointListeningAsync(settings.BuildBaseAddress(), shouldBeListening: false).ConfigureAwait(false);

                await service.StartLocalNodeAsync().ConfigureAwait(false);
                var secondVersion = await PostAsync(settings, "api/v0/version").ConfigureAwait(false);
                StringAssert.Contains(secondVersion, "\"Version\"");

                await service.RestartLocalNodeAsync().ConfigureAwait(false);
                var peer = await PostAsync(settings, "api/v0/id").ConfigureAwait(false);
                StringAssert.Contains(peer, "\"id\"");
            }
            finally
            {
                await service.StopLocalNodeAsync().ConfigureAwait(false);
                await AssertEndpointListeningAsync(settings.BuildBaseAddress(), shouldBeListening: false).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task StopLocalNodeAsync_TargetPortChanged_StopsTheOwnedHostProcess()
    {
        await WithIsolatedLocalHostEnvironmentAsync(async () =>
        {
            var originalSettings = new NodeConnectionSettings
            {
                BaseUrl = $"http://127.0.0.1:{GetUnusedPort()}/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };
            var retargetedSettings = new NodeConnectionSettings
            {
                BaseUrl = $"http://localhost:{GetUnusedPort()}/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };

            var targetRegistry = new CurrentNodeTargetRegistry();
            targetRegistry.Update(originalSettings, isHydrated: true);
            var service = new LocalNodeBootstrapService(
                targetRegistry,
                NullLogger<LocalNodeBootstrapService>.Instance);

            try
            {
                await service.StartLocalNodeAsync().ConfigureAwait(false);
                await AssertEndpointListeningAsync(
                    originalSettings.BuildBaseAddress(),
                    shouldBeListening: true).ConfigureAwait(false);

                targetRegistry.Update(retargetedSettings, isHydrated: true);
                await service.StopLocalNodeAsync().ConfigureAwait(false);

                await AssertEndpointListeningAsync(
                    originalSettings.BuildBaseAddress(),
                    shouldBeListening: false).ConfigureAwait(false);
            }
            finally
            {
                await service.StopLocalNodeAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public async Task StartLocalNodeAsync_ListenerAppearsInsideStartGate_RecoversWithoutDeadlock()
    {
        var settings = new NodeConnectionSettings
        {
            BaseUrl = "http://127.0.0.1:51234/",
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };
        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(settings, isHydrated: true);
        var controller = new ListenerRaceNodeHostController();
        var service = new LocalNodeBootstrapService(
            targetRegistry,
            controller,
            NullLogger<LocalNodeBootstrapService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await service.StartLocalNodeAsync(timeout.Token).ConfigureAwait(false);

        Assert.AreEqual(1, controller.StopAttempts);
        Assert.AreEqual(1, controller.StartAttempts);
    }

    [TestMethod]
    public async Task EnsureNodeForSettingsAsync_Rejects_An_Unhealthy_Local_Listener()
    {
        await using var listener = TcpStatusServer.Start(
            HttpStatusCode.InternalServerError,
            "broken");

        var settings = new NodeConnectionSettings
        {
            BaseUrl = listener.BaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };

        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(settings, isHydrated: true);
        var service = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);

        var exception = await ThrowsAsync<InvalidOperationException>(
            () => service.EnsureNodeForSettingsAsync(settings)).ConfigureAwait(false);

        StringAssert.Contains(exception.Message, "health probe failed");
        StringAssert.Contains(exception.Message, settings.BuildBaseAddress().ToString());
    }

    [TestMethod]
    public async Task ResolveStartupSettingsAsync_Falls_Back_To_Local_Default_When_Remote_Target_Is_Unreachable()
    {
        await WithIsolatedLocalHostEnvironmentAsync(async () =>
        {
            var preferredSettings = new NodeConnectionSettings
            {
                BaseUrl = "http://192.0.2.10:5001/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };
            var fallbackSettings = new NodeConnectionSettings
            {
                BaseUrl = $"http://127.0.0.1:{GetUnusedPort()}/",
                ApiPath = "api/v0",
                TimeoutSeconds = 15
            };

            var targetRegistry = new CurrentNodeTargetRegistry();
            targetRegistry.Update(preferredSettings, isHydrated: true);
            var service = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);

            try
            {
                var resolved = await service.ResolveStartupSettingsAsync(preferredSettings, fallbackSettings).ConfigureAwait(false);

                Assert.AreEqual(fallbackSettings.BuildBaseAddress(), resolved.BuildBaseAddress());
                var version = await PostAsync(resolved, "api/v0/version").ConfigureAwait(false);
                StringAssert.Contains(version, "\"Version\"");
            }
            finally
            {
                targetRegistry.Update(fallbackSettings, isHydrated: true);
                await service.StopLocalNodeAsync().ConfigureAwait(false);
                await AssertEndpointListeningAsync(fallbackSettings.BuildBaseAddress(), shouldBeListening: false).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ResolveStartupSettingsAsync_Keeps_A_Healthy_Remote_Target()
    {
        await using var listener = TcpStatusServer.Start(
            HttpStatusCode.OK,
            "{\"Version\":\"test\"}");

        var preferredSettings = new NodeConnectionSettings
        {
            BaseUrl = listener.BaseAddress.ToString(),
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };
        var fallbackSettings = new NodeConnectionSettings
        {
            BaseUrl = $"http://127.0.0.1:{GetUnusedPort()}/",
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };

        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(preferredSettings, isHydrated: true);
        var service = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);

        var resolved = await service.ResolveStartupSettingsAsync(preferredSettings, fallbackSettings).ConfigureAwait(false);
        Assert.AreEqual(preferredSettings.BuildBaseAddress(), resolved.BuildBaseAddress());
    }

    private static async Task<string> PostAsync(NodeConnectionSettings settings, string route)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = settings.BuildBaseAddress(),
            Timeout = TimeSpan.FromSeconds(15)
        };

        using var response = await httpClient.PostAsync(route, content: null).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static async Task AssertEndpointListeningAsync(Uri endpoint, bool shouldBeListening)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        while (!timeoutCts.IsCancellationRequested)
        {
            using var probe = new TcpClient();
            try
            {
                await probe.ConnectAsync(endpoint.Host, endpoint.Port, timeoutCts.Token).ConfigureAwait(false);
                if (shouldBeListening)
                {
                    return;
                }
            }
            catch
            {
                if (!shouldBeListening)
                {
                    return;
                }
            }

            await Task.Delay(250, timeoutCts.Token).ConfigureAwait(false);
        }

        Assert.Fail(shouldBeListening
            ? $"Timed out waiting for {endpoint} to start listening."
            : $"Timed out waiting for {endpoint} to stop listening.");
    }

    private static int GetUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (T ex)
        {
            return ex;
        }

        Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
        return null!;
    }

    private static async Task WithIsolatedLocalHostEnvironmentAsync(Func<Task> action)
    {
        var originalPassphrase = Environment.GetEnvironmentVariable("IPFS_PASS");
        var originalRepositoryPath = Environment.GetEnvironmentVariable("IPFS_PATH");
        var repositoryPath = Path.Combine(Path.GetTempPath(), "ipfs-local-node-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);

        Environment.SetEnvironmentVariable("IPFS_PASS", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("IPFS_PATH", repositoryPath);

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IPFS_PASS", originalPassphrase);
            Environment.SetEnvironmentVariable("IPFS_PATH", originalRepositoryPath);

            try
            {
                if (Directory.Exists(repositoryPath))
                {
                    Directory.Delete(repositoryPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private sealed class TcpStatusServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource shutdown = new();
        private readonly byte[] response;
        private readonly Task serveTask;

        private TcpStatusServer(HttpStatusCode statusCode, string body)
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            BaseAddress = new Uri($"http://127.0.0.1:{endpoint.Port}/");

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var reason = statusCode == HttpStatusCode.OK
                ? "OK"
                : "Internal Server Error";
            response = Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {(int)statusCode} {reason}\r\n"
                + "Content-Type: application/json\r\n"
                + $"Content-Length: {bodyBytes.Length}\r\n"
                + "Connection: close\r\n"
                + "\r\n"
                + body);
            serveTask = ServeAsync();
        }

        public Uri BaseAddress { get; }

        public static TcpStatusServer Start(HttpStatusCode statusCode, string body)
            => new(statusCode, body);

        public async ValueTask DisposeAsync()
        {
            shutdown.Cancel();
            listener.Stop();
            try
            {
                await serveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                shutdown.Dispose();
            }
        }

        private async Task ServeAsync()
        {
            while (!shutdown.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener
                        .AcceptTcpClientAsync(shutdown.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException) when (shutdown.IsCancellationRequested)
                {
                    return;
                }

                using (client)
                {
                    try
                    {
                        var stream = client.GetStream();
                        var buffer = new byte[2048];
                        using var request = new MemoryStream();
                        while (request.Length < 16 * 1024)
                        {
                            var count = await stream
                                .ReadAsync(buffer, shutdown.Token)
                                .ConfigureAwait(false);
                            if (count == 0)
                            {
                                break;
                            }

                            request.Write(buffer, 0, count);
                            var requestText = Encoding.ASCII.GetString(
                                request.GetBuffer(),
                                0,
                                (int)request.Length);
                            if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
                            {
                                await stream
                                    .WriteAsync(response, shutdown.Token)
                                    .ConfigureAwait(false);
                                break;
                            }
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (SocketException)
                    {
                    }
                }
            }
        }
    }

    private sealed class ListenerRaceNodeHostController : CanDoItAll.IPFS.NodeControl.Abstractions.INodeHostController
    {
        private int listeningProbeCount;
        private bool started;

        public int StartAttempts { get; private set; }

        public int StopAttempts { get; private set; }

        public string? FindRepoRoot(string? startPath = null)
            => "test-repo";

        public bool IsLocalEndpoint(Uri endpoint)
            => true;

        public Task<bool> IsEndpointListeningAsync(
            Uri endpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            listeningProbeCount++;
            return Task.FromResult(listeningProbeCount >= 2);
        }

        public Task<bool> IsEndpointHealthyAsync(
            Uri endpoint,
            string relativePath,
            HttpMethod? method = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(started);
        }

        public Task WaitForEndpointStateAsync(
            Uri endpoint,
            bool shouldBeListening,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public bool TryStartLocalNodeHost(
            string repoRoot,
            Uri endpoint,
            out int? processId)
        {
            StartAttempts++;
            started = true;
            processId = 42;
            return true;
        }

        public bool TryStopLocalNodeHost(
            string repoRoot,
            Uri endpoint,
            TimeSpan waitTimeout,
            out int? processId)
        {
            StopAttempts++;
            processId = 41;
            return true;
        }

        public Task<bool> EnsureOwnedLocalNodeHostExitedAsync(
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
}
