using System;
using System.IO;
using System.Text;
using System.Linq;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.NodeControl.Components.Pages;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class ApplicationLogStoreTests
{
    [TestMethod]
    public void ApplicationLogStore_ReadRecent_Filters_And_Formats_A_Selected_Window()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var store = CreateStore(tempRoot);
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow.AddHours(-2),
                "Warning",
                "Old.Category",
                "Too old",
                0,
                null));
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "Warning",
                "Current.Category",
                "Visible warning",
                0,
                null));
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                "Error",
                "Current.Category",
                "Visible error",
                0,
                "System.InvalidOperationException: boom"));

            var slice = store.ReadRecent("10m");
            var text = Encoding.UTF8.GetString(store.BuildPlainTextSlice(slice));

            Assert.AreEqual(2, slice.Entries.Count);
            Assert.AreEqual("Visible error", slice.Entries[0].Message);
            StringAssert.Contains(text, "Visible warning");
            StringAssert.Contains(text, "System.InvalidOperationException: boom");
            Assert.IsFalse(text.Contains("Too old", StringComparison.Ordinal));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void Logs_Page_Renders_Current_Window_Entries()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var store = CreateStore(tempRoot);
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow.AddHours(-2),
                "Warning",
                "Old.Category",
                "Outside the default window",
                0,
                null));
            store.Write(new ApplicationLogEntry(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "Error",
                "Current.Category",
                "Inside the default window",
                0,
                "System.Exception: failure"));

            using var context = new Bunit.TestContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
            context.Services.AddCanDoItAllBaseLib();
            context.Services.AddSingleton(store);

            var cut = context.RenderComponent<Logs>();

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "Inside the default window");
                Assert.IsFalse(cut.Markup.Contains("Outside the default window", StringComparison.Ordinal));
                StringAssert.Contains(cut.Markup, "Last hour");
                StringAssert.Contains(cut.Markup, "Download window");
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    [TestMethod]
    public void ApplicationLogStore_Rotates_And_Retains_Archived_Files()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var filePath = Path.Combine(tempRoot, "application.log");
            var store = new ApplicationLogStore(Options.Create(new ApplicationLogStoreOptions
            {
                FilePath = filePath,
                MaxEntriesPerWindow = 100,
                MaxEntriesPerFile = 2,
                RetainedArchiveFileCount = 2
            }));

            for (var index = 1; index <= 7; index++)
            {
                store.Write(new ApplicationLogEntry(
                    DateTimeOffset.UtcNow.AddSeconds(index),
                    "Info",
                    "Rotation.Category",
                    $"entry-{index}",
                    index,
                    null));
            }

            var archiveFiles = Directory.GetFiles(tempRoot, "application-*.log");
            var slice = store.ReadRecent("1h", 10);

            Assert.AreEqual(2, archiveFiles.Length);
            Assert.AreEqual(5, slice.Entries.Count);
            Assert.AreEqual("entry-7", slice.Entries[0].Message);
            Assert.IsFalse(slice.Entries.Any(entry => entry.Message == "entry-1"));
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static ApplicationLogStore CreateStore(string tempRoot)
        => new(Options.Create(new ApplicationLogStoreOptions
        {
            FilePath = Path.Combine(tempRoot, "application.log"),
            MaxEntriesPerWindow = 100
        }));

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-log-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
}
