using Microsoft.Win32;

namespace CanDoItAll.IPFS.DesktopHost;

internal sealed class WindowsStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "CanDoItAll.IPFS.NodeControl";

    public bool IsRegistered()
    {
        if (!TryBuildStartupCommand(out var expectedCommand))
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var registeredCommand = key?.GetValue(StartupValueName) as string;
        return string.Equals(registeredCommand, expectedCommand, StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        if (!TryBuildStartupCommand(out var startupCommand))
        {
            throw new InvalidOperationException("Could not determine the current control app command line for Windows startup registration.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the current-user Windows startup registry key.");
        key.SetValue(StartupValueName, startupCommand, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
    }

    private static bool TryBuildStartupCommand(out string command)
    {
        command = string.Empty;

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var commandLineArguments = Environment.GetCommandLineArgs();
        if (commandLineArguments.Length == 0)
        {
            return false;
        }

        var arguments = new List<string>();
        if (IsDotNetHost(processPath))
        {
            if (commandLineArguments.Length < 2)
            {
                return false;
            }

            processPath = Path.GetFullPath(commandLineArguments[0]);
            arguments.Add(Path.GetFullPath(commandLineArguments[1]));
            arguments.AddRange(commandLineArguments.Skip(2));
        }
        else
        {
            arguments.AddRange(commandLineArguments.Skip(1));
        }

        command = Quote(processPath);
        if (arguments.Count > 0)
        {
            command = $"{command} {string.Join(' ', arguments.Select(Quote))}";
        }

        return true;
    }

    private static bool IsDotNetHost(string processPath)
    {
        var fileName = Path.GetFileName(processPath);
        return string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value)
        => $"\"{value}\"";
}
