using System.Globalization;
using System.IO;
using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages.FilesComponents;

internal static class FilesUiText
{
    private static readonly HashSet<string> InlinePreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp",
        ".svg",
        ".pdf"
    };

    public static string CountLabel(int childCount)
        => $"{childCount:n0} item{(childCount == 1 ? string.Empty : "s")}";

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes:n0} B";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var scaled = bytes / Math.Pow(1024, unitIndex);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", scaled, units[unitIndex]);
    }

    public static string PreviewSizeOrCount(NodePreviewSnapshot snapshot)
        => snapshot.IsDirectory
            ? CountLabel(snapshot.ChildCount)
            : FormatSize(snapshot.Size);

    public static string Shorten(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= limit)
        {
            return value ?? string.Empty;
        }

        return $"{value[..(limit - 3)]}...";
    }

    public static bool HasTextPreview(NodePreviewSnapshot snapshot)
        => !snapshot.IsDirectory && !string.IsNullOrWhiteSpace(snapshot.PreviewText);

    public static bool CanRenderInlineFrame(NodePreviewSnapshot snapshot)
    {
        if (snapshot.IsDirectory || HasTextPreview(snapshot))
        {
            return false;
        }

        var extension = Path.GetExtension(snapshot.DisplayName);
        return !string.IsNullOrWhiteSpace(extension) && InlinePreviewExtensions.Contains(extension);
    }
}
