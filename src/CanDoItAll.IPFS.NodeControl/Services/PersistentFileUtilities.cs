using System.Globalization;
using System.Text;

namespace CanDoItAll.IPFS.NodeControl.Services;

internal static class PersistentFileUtilities
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private const string QuarantineDirectoryName = "quarantine";

    public static string GetBackupPath(string filePath)
        => $"{filePath}.bak";

    public static string GetQuarantineDirectory(string filePath)
        => Path.Combine(Path.GetDirectoryName(filePath) ?? Path.GetTempPath(), QuarantineDirectoryName);

    public static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static void WriteAllTextAtomically(string filePath, string content)
    {
        EnsureParentDirectory(filePath);

        var directory = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
        var tempFilePath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempFilePath, filePath, GetBackupPath(filePath), ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public static string? QuarantineFile(string filePath, Exception exception)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var quarantineDirectory = GetQuarantineDirectory(filePath);
        Directory.CreateDirectory(quarantineDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var fileName = $"{Path.GetFileNameWithoutExtension(filePath)}.corrupt-{timestamp}{Path.GetExtension(filePath)}";
        var quarantinedPath = Path.Combine(quarantineDirectory, fileName);
        File.Move(filePath, quarantinedPath);
        File.WriteAllText($"{quarantinedPath}.error.txt", exception.ToString(), Utf8WithoutBom);
        return quarantinedPath;
    }

    public static void QuarantineRelatedFiles(string filePath, Exception exception, params string[] siblingSuffixes)
    {
        QuarantineFile(filePath, exception);
        foreach (var suffix in siblingSuffixes)
        {
            var siblingPath = $"{filePath}{suffix}";
            if (File.Exists(siblingPath))
            {
                QuarantineFile(siblingPath, exception);
            }
        }
    }
}
