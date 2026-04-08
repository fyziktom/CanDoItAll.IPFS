using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class BrowserUploadStagingTests
{
    [TestMethod]
    public async Task CreateAsync_Stages_A_Single_File_Without_Promoting_It_To_A_Folder()
    {
        await using var staging = await global::BrowserUploadStaging.CreateAsync(
            CreateForm(files:
            [
                CreateFile("browser-note.txt", "browser upload proof")
            ]),
            CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(staging.IsSingleFile);
        Assert.IsNotNull(staging.SingleFilePath);
        Assert.AreEqual(staging.UploadRootPath, Path.GetDirectoryName(staging.SingleFilePath!)!);
        Assert.AreEqual("browser-note.txt", Path.GetFileName(staging.SingleFilePath));
        Assert.AreEqual("browser upload proof", await File.ReadAllTextAsync(staging.SingleFilePath!).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task CreateAsync_Uses_The_Sanitized_Root_Name_And_Preserves_Nested_Files()
    {
        await using var staging = await global::BrowserUploadStaging.CreateAsync(
            CreateForm(
                rootName: "C:/Users/dell/Documents/summer-trip",
                directoryHints: ["album"],
                files:
                [
                    CreateFile("album/cover.txt", "cover art"),
                    CreateFile("album/day-01/notes.txt", "day one")
                ]),
            CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(staging.IsSingleFile);
        Assert.AreEqual("summer-trip", Path.GetFileName(staging.UploadRootPath));
        Assert.IsNotNull(staging.SingleFilePath);
        StringAssert.EndsWith(staging.SingleFilePath, Path.Combine("summer-trip", "album", "cover.txt"));
        Assert.AreEqual("cover art", await File.ReadAllTextAsync(Path.Combine(staging.UploadRootPath, "album", "cover.txt")).ConfigureAwait(false));
        Assert.AreEqual("day one", await File.ReadAllTextAsync(Path.Combine(staging.UploadRootPath, "album", "day-01", "notes.txt")).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task CreateAsync_Rejects_Path_Traversal_In_File_And_Directory_Hints()
    {
        var fileException = await ThrowsAsync<InvalidOperationException>(() =>
            global::BrowserUploadStaging.CreateAsync(
                CreateForm(files:
                [
                    CreateFile("../escape.txt", "escape")
                ]),
                CancellationToken.None)).ConfigureAwait(false);
        Assert.AreEqual("Uploaded paths must stay within the selected folder.", fileException.Message);

        var directoryException = await ThrowsAsync<InvalidOperationException>(() =>
            global::BrowserUploadStaging.CreateAsync(
                CreateForm(directoryHints: ["../secret"]),
                CancellationToken.None)).ConfigureAwait(false);
        Assert.AreEqual("Uploaded paths must stay within the selected folder.", directoryException.Message);
    }

    private static IFormCollection CreateForm(
        IReadOnlyList<IFormFile>? files = null,
        string? rootName = null,
        IReadOnlyList<string>? directoryHints = null)
    {
        var values = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(rootName))
        {
            values["rootName"] = rootName;
        }

        if (directoryHints is not null && directoryHints.Count > 0)
        {
            values["dir"] = new StringValues([.. directoryHints]);
        }

        var formFiles = new FormFileCollection();
        if (files is not null)
        {
            foreach (var file in files)
            {
                formFiles.Add(file);
            }
        }

        return new FormCollection(values, formFiles);
    }

    private static IFormFile CreateFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
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
}
