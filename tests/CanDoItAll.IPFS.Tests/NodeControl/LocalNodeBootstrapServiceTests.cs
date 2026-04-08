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
                try
                {
                    await service.StopLocalNodeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort cleanup. Any remaining issue should surface in the assertion below.
                }

                await AssertEndpointListeningAsync(settings.BuildBaseAddress(), shouldBeListening: false).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task EnsureNodeForSettingsAsync_Rejects_An_Unhealthy_Local_Listener()
    {
        var port = GetUnusedPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        using var cts = new CancellationTokenSource();
        var serveTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var bytes = Encoding.UTF8.GetBytes("broken");
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
        });

        var settings = new NodeConnectionSettings
        {
            BaseUrl = $"http://127.0.0.1:{port}/",
            ApiPath = "api/v0",
            TimeoutSeconds = 15
        };

        var targetRegistry = new CurrentNodeTargetRegistry();
        targetRegistry.Update(settings, isHydrated: true);
        var service = new LocalNodeBootstrapService(targetRegistry, NullLogger<LocalNodeBootstrapService>.Instance);

        try
        {
            var exception = await ThrowsAsync<InvalidOperationException>(
                () => service.EnsureNodeForSettingsAsync(settings)).ConfigureAwait(false);

            StringAssert.Contains(exception.Message, "health probe failed");
            StringAssert.Contains(exception.Message, settings.BuildBaseAddress().ToString());
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            await serveTask.ConfigureAwait(false);
        }
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
                try
                {
                    await service.StopLocalNodeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort cleanup only.
                }
            }
        }).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ResolveStartupSettingsAsync_Keeps_A_Healthy_Remote_Target()
    {
        var remotePort = GetUnusedPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{remotePort}/");
        listener.Start();

        using var cts = new CancellationTokenSource();
        var serveTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                var bytes = Encoding.UTF8.GetBytes("{\"Version\":\"test\"}");
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
        });

        var preferredSettings = new NodeConnectionSettings
        {
            BaseUrl = $"http://127.0.0.1:{remotePort}/",
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
            Assert.AreEqual(preferredSettings.BuildBaseAddress(), resolved.BuildBaseAddress());
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            await serveTask.ConfigureAwait(false);
        }
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
}
