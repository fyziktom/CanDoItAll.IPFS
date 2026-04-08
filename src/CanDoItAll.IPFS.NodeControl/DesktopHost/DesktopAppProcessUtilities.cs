using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CanDoItAll.IPFS.DesktopHost;

internal sealed record RepoAppDescriptor(
    string DisplayName,
    string ProjectDirectoryName,
    string ProjectFileName,
    string AssemblyBaseName,
    string FallbackUrl);

internal static class DesktopAppProcessUtilities
{
    private static readonly string[] CandidateConfigurations = ["Debug", "Release"];
    private static readonly string[] CandidateTargetFrameworks = ["net10.0-windows", "net10.0"];
    private static readonly string[] CandidatePublishedSubdirectories = ["", "node", "engine"];

    public static string? FindRepoRoot(string? startPath = null)
    {
        foreach (var candidatePath in EnumerateCandidatePaths(startPath))
        {
            foreach (var current in EnumerateCandidateDirectories(candidatePath))
            {
                if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.IPFS.slnx")))
                {
                    return current.FullName;
                }
            }
        }

        return null;
    }

    public static string? FindAppRoot(RepoAppDescriptor descriptor, string? startPath = null)
    {
        foreach (var candidatePath in EnumerateCandidatePaths(startPath))
        {
            foreach (var current in EnumerateCandidateDirectories(candidatePath))
            {
                if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.IPFS.slnx"))
                    || ContainsPublishedApp(current.FullName, descriptor))
                {
                    return current.FullName;
                }
            }
        }

        return null;
    }

    public static string GetProjectPath(string repoRoot, RepoAppDescriptor descriptor)
        => Path.Combine(repoRoot, descriptor.ProjectDirectoryName, descriptor.ProjectFileName);

    public static string GetProjectDirectory(string repoRoot, RepoAppDescriptor descriptor)
        => Path.Combine(repoRoot, descriptor.ProjectDirectoryName);

    public static Process? StartRepoApp(
        string repoRoot,
        RepoAppDescriptor descriptor,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        foreach (var executablePath in EnumeratePublishedExecutableCandidates(repoRoot, descriptor))
        {
            if (!File.Exists(executablePath))
            {
                continue;
            }

            var workingDirectory = Path.GetDirectoryName(executablePath) ?? repoRoot;
            return StartProcess(executablePath, workingDirectory, environmentVariables, Array.Empty<string>());
        }

        foreach (var dllPath in EnumeratePublishedDllCandidates(repoRoot, descriptor))
        {
            if (!File.Exists(dllPath))
            {
                continue;
            }

            var workingDirectory = Path.GetDirectoryName(dllPath) ?? repoRoot;
            return StartProcess("dotnet", workingDirectory, environmentVariables, [dllPath]);
        }

        foreach (var executablePath in EnumerateExecutableCandidates(repoRoot, descriptor))
        {
            if (!File.Exists(executablePath))
            {
                continue;
            }

            var workingDirectory = Path.GetDirectoryName(executablePath) ?? GetProjectDirectory(repoRoot, descriptor);
            return StartProcess(executablePath, workingDirectory, environmentVariables, Array.Empty<string>());
        }

        foreach (var dllPath in EnumerateDllCandidates(repoRoot, descriptor))
        {
            if (!File.Exists(dllPath))
            {
                continue;
            }

            var workingDirectory = Path.GetDirectoryName(dllPath) ?? GetProjectDirectory(repoRoot, descriptor);
            return StartProcess("dotnet", workingDirectory, environmentVariables, [dllPath]);
        }

        var projectPath = GetProjectPath(repoRoot, descriptor);
        if (!File.Exists(projectPath))
        {
            return null;
        }

        return StartProcess(
            "dotnet",
            GetProjectDirectory(repoRoot, descriptor),
            environmentVariables,
            ["run", "--project", projectPath, "--no-launch-profile"]);
    }

    public static Process? StartCurrentProcessClone(IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var workingDirectory = Directory.Exists(AppContext.BaseDirectory)
            ? AppContext.BaseDirectory
            : Directory.GetCurrentDirectory();
        return StartProcess(
            processPath,
            workingDirectory,
            environmentVariables,
            Environment.GetCommandLineArgs().Skip(1));
    }

    public static void OpenBrowser(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public static bool IsLocalEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (endpoint.IsLoopback)
        {
            return true;
        }

        var host = endpoint.Host;
        if (IPAddress.TryParse(host, out var address))
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            return GetLocalAddresses().Contains(address);
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            if (string.Equals(host, Dns.GetHostName(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var localAddresses = GetLocalAddresses();
            return Dns.GetHostAddresses(host).Any(localAddresses.Contains);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static async Task<bool> IsTcpEndpointListeningAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(750));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeoutCts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public static async Task WaitForEndpointStateAsync(
        Uri endpoint,
        bool shouldBeListening,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            if (await IsTcpEndpointListeningAsync(endpoint, timeoutCts.Token).ConfigureAwait(false) == shouldBeListening)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token).ConfigureAwait(false);
        }

        throw new TimeoutException(
            shouldBeListening
                ? $"Timed out waiting for {endpoint} to start listening."
                : $"Timed out waiting for {endpoint} to stop listening.");
    }

    public static async Task<bool> IsHttpEndpointHealthyAsync(
        Uri endpoint,
        string relativePath,
        HttpMethod? method = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using var client = new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(2)
        };

        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, relativePath.TrimStart('/'));
        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public static bool TryStopRepoAppProcess(
        string repoRoot,
        RepoAppDescriptor descriptor,
        Uri endpoint,
        TimeSpan waitTimeout,
        out int? processId)
    {
        processId = null;

        var process = FindRepoAppProcess(repoRoot, descriptor);
        if (process is null)
        {
            return false;
        }

        processId = process.Id;
        try
        {
            if (!IsProcessListeningOnEndpoint(process.Id, endpoint))
            {
                return false;
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit((int)waitTimeout.TotalMilliseconds))
                {
                    return false;
                }
            }

            return process.HasExited;
        }
        catch
        {
            return false;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string repoRoot, RepoAppDescriptor descriptor)
    {
        var projectDirectory = GetProjectDirectory(repoRoot, descriptor);
        foreach (var configuration in CandidateConfigurations)
        {
            foreach (var targetFramework in CandidateTargetFrameworks)
            {
                yield return Path.Combine(projectDirectory, "bin", configuration, targetFramework, $"{descriptor.AssemblyBaseName}.exe");
            }
        }
    }

    private static IEnumerable<string> EnumeratePublishedExecutableCandidates(string root, RepoAppDescriptor descriptor)
    {
        foreach (var subdirectory in CandidatePublishedSubdirectories)
        {
            var candidateDirectory = string.IsNullOrWhiteSpace(subdirectory)
                ? root
                : Path.Combine(root, subdirectory);
            if (!Directory.Exists(candidateDirectory))
            {
                continue;
            }

            yield return Path.Combine(candidateDirectory, descriptor.AssemblyBaseName);
            yield return Path.Combine(candidateDirectory, $"{descriptor.AssemblyBaseName}.exe");
        }
    }

    private static IEnumerable<string> EnumerateDllCandidates(string repoRoot, RepoAppDescriptor descriptor)
    {
        var projectDirectory = GetProjectDirectory(repoRoot, descriptor);
        foreach (var configuration in CandidateConfigurations)
        {
            foreach (var targetFramework in CandidateTargetFrameworks)
            {
                yield return Path.Combine(projectDirectory, "bin", configuration, targetFramework, $"{descriptor.AssemblyBaseName}.dll");
            }
        }
    }

    private static IEnumerable<string> EnumeratePublishedDllCandidates(string root, RepoAppDescriptor descriptor)
    {
        foreach (var subdirectory in CandidatePublishedSubdirectories)
        {
            var candidateDirectory = string.IsNullOrWhiteSpace(subdirectory)
                ? root
                : Path.Combine(root, subdirectory);
            if (!Directory.Exists(candidateDirectory))
            {
                continue;
            }

            yield return Path.Combine(candidateDirectory, $"{descriptor.AssemblyBaseName}.dll");
        }
    }

    private static Process? StartProcess(
        string fileName,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        try
        {
            return Process.Start(startInfo);
        }
        catch
        {
            return null;
        }
    }

    private static Process? FindRepoAppProcess(string repoRoot, RepoAppDescriptor descriptor)
    {
        var executableCandidates = EnumeratePublishedExecutableCandidates(repoRoot, descriptor)
            .Concat(EnumerateExecutableCandidates(repoRoot, descriptor))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcessesByName(descriptor.AssemblyBaseName))
        {
            try
            {
                var fileName = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (executableCandidates.Contains(Path.GetFullPath(fileName)))
                {
                    return process;
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        return null;
    }

    private static bool ContainsPublishedApp(string root, RepoAppDescriptor descriptor)
        => EnumeratePublishedExecutableCandidates(root, descriptor).Any(File.Exists)
            || EnumeratePublishedDllCandidates(root, descriptor).Any(File.Exists);

    private static IEnumerable<string> EnumerateCandidatePaths(string? startPath)
    {
        yield return startPath ?? string.Empty;
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static IEnumerable<DirectoryInfo> EnumerateCandidateDirectories(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            yield break;
        }

        DirectoryInfo? current = new(Path.GetFullPath(candidatePath));
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    private static bool IsProcessListeningOnEndpoint(int processId, Uri endpoint)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var queryProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            queryProcess.StartInfo.ArgumentList.Add("-NoProfile");
            queryProcess.StartInfo.ArgumentList.Add("-Command");
            queryProcess.StartInfo.ArgumentList.Add(
                $"$pids = Get-NetTCPConnection -State Listen -LocalPort {endpoint.Port} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique; if ($pids) {{ $pids -join ',' }}");

            if (!queryProcess.Start())
            {
                return false;
            }

            var output = queryProcess.StandardOutput.ReadToEnd().Trim();
            queryProcess.WaitForExit(3000);
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            return output
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => int.TryParse(value, out var listeningPid) && listeningPid == processId);
        }
        catch
        {
            return false;
        }
    }

    private static HashSet<IPAddress> GetLocalAddresses()
    {
        var addresses = new HashSet<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                addresses.Add(unicastAddress.Address);
            }
        }

        addresses.Add(IPAddress.Loopback);
        addresses.Add(IPAddress.IPv6Loopback);
        return addresses;
    }
}
