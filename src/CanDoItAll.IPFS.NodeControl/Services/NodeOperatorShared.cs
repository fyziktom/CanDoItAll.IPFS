using System.Text;

namespace CanDoItAll.IPFS.NodeControl.Services;

internal static class NodeOperatorDisplay
{
    public static string ResolveDisplayName(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimmed = value.Trim();
            var lastSlash = trimmed.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < trimmed.Length - 1
                ? trimmed[(lastSlash + 1)..]
                : trimmed;
        }

        return fallback;
    }

    public static string GetTypeLabel(bool isDirectory)
        => isDirectory ? "File folder" : "File";
}

internal static class NodePreviewTextReader
{
    public static async Task<string> ReadUtf8PreviewAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes];
        var read = await stream.ReadAsync(buffer.AsMemory(0, maxBytes), cancellationToken).ConfigureAwait(false);
        return GetUtf8Preview(buffer[..read], maxBytes);
    }

    public static string GetUtf8Preview(byte[] bytes, int maxBytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (LooksBinary(bytes))
        {
            return string.Empty;
        }

        var preview = Encoding.UTF8.GetString(bytes);
        if (bytes.Length < maxBytes)
        {
            return preview;
        }

        return $"{preview}\n\n... preview truncated ...";
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var controlCharacters = 0;
        var inspected = Math.Min(bytes.Length, 512);

        for (var index = 0; index < inspected; index++)
        {
            var current = bytes[index];
            if (current == 0)
            {
                return true;
            }

            if (current < 32 && current is not (byte)'\r' and not (byte)'\n' and not (byte)'\t')
            {
                controlCharacters++;
            }
        }

        return controlCharacters > inspected / 8;
    }
}
