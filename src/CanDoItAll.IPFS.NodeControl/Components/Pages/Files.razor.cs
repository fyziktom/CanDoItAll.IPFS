using System.IO;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.IPFS.NodeControl.Components.Pages.FilesComponents;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.JSInterop;
using WebMouseEventArgs = Microsoft.AspNetCore.Components.Web.MouseEventArgs;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages;

public partial class Files : IDisposable
{
    private IReadOnlyList<NodeExplorerItemSnapshot> pinnedItems = [];
    private IReadOnlyList<NodeExplorerItemSnapshot> rootItems = [];
    private NodeExplorerSnapshot? currentFolderSnapshot;
    private NodePreviewSnapshot? previewSnapshot;
    private NodePreviewSnapshot? detailSnapshot;
    private NodeFileSnapshot? topologySnapshot;
    private NodeExplorerItemSnapshot? contextMenuItem;
    private NodeExplorerItemSnapshot? shareTargetItem;
    private NodeExplorerItemSnapshot? unpinTargetItem;
    private readonly Dictionary<string, string> knownDisplayNamesByTarget = new(StringComparer.Ordinal);
    private double contextMenuLeft;
    private double contextMenuTop;
    private string inspectPath = string.Empty;
    private string textFileName = "note.txt";
    private string textContent = string.Empty;
    private string fileCanvasState = string.Empty;
    private string? selectedBrowsePath;
    private string? errorMessage;
    private bool pinnedItemsLoaded;
    private bool hasStartedInitialLoad;
    private bool isBusy;
    private bool showContextMenu;
    private bool showCreateTextPanel;
    private bool showPreviewPane;
    private bool showUploadModal;
    private bool textPin = true;
    private bool textWrap;
    private bool unpinDeleteImmediately;
    private bool uploadPin = true;
    private bool uploadWrap;
    private const string BrowserUploadEndpoint = "/api/files/upload-browser";

    private IReadOnlyList<NodeExplorerItemSnapshot> VisibleItems
        => currentFolderSnapshot?.Entries ?? rootItems;

    private bool CanDownloadSelection
        => previewSnapshot is not null && !previewSnapshot.IsDirectory;

    private bool CanGoBackOrUp
        => currentFolderSnapshot is not null;

    private bool IsPreviewPinned
        => previewSnapshot is not null
           && pinnedItems.Any(item => string.Equals(item.Target, previewSnapshot.Target, StringComparison.Ordinal));

    private bool IsVirtualPreview
        => previewSnapshot is not null && IsVirtualExplorerPath(previewSnapshot.Path);

    private bool IsInitialExplorerLoad
        => isBusy && !pinnedItemsLoaded && currentFolderSnapshot is null && rootItems.Count == 0;

    private bool ShowExplorerGuidanceRail
        => !showPreviewPane && !IsInitialExplorerLoad && VisibleItems.Count <= 1;

    private string ExplorerGridColumnTemplateXl
        => showPreviewPane || ShowExplorerGuidanceRail
            ? "minmax(0,2fr) minmax(24rem,0.8fr)"
            : "minmax(0,1fr)";

    private CanvasWorkbenchSurface fileSurface
        => NodeCanvasSurfaceFactory.CreateFileSurface(topologySnapshot, fileCanvasState);

    protected override void OnInitialized()
    {
        NodeSessionState.Changed += HandleNodeSessionChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && NodeSessionState.IsHydrated && !hasStartedInitialLoad)
        {
            hasStartedInitialLoad = true;
            await InitializeExplorerAsync();
        }
    }

    private async Task InitializeExplorerAsync()
    {
        await RunBusyAsync(async () =>
        {
            await RefreshCurrentViewCoreAsync();
        });
    }

    private async Task OpenPinnedRootsAsync()
    {
        await RunBusyAsync(async () =>
        {
            await LoadPinnedItemsCoreAsync();
            await OpenPinnedRootsCoreAsync(currentFolderSnapshot?.NormalizedPath ?? selectedBrowsePath);
        });
    }

    private async Task HandleBackOrUpAsync()
    {
        if (currentFolderSnapshot is null)
        {
            return;
        }

        var currentPath = currentFolderSnapshot.NormalizedPath;
        var parentPath = currentFolderSnapshot.ParentPath;

        await RunBusyAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                await BrowseToPathCoreAsync(parentPath, null, currentPath);
                return;
            }

            await LoadPinnedItemsCoreAsync();
            await OpenPinnedRootsCoreAsync(currentPath);
        });
    }

    private async Task SelectItemAsync(NodeExplorerItemSnapshot item)
    {
        await RunBusyAsync(async () =>
        {
            selectedBrowsePath = item.Path;
            inspectPath = item.Path;
            await LoadPreviewCoreAsync(item.Path, item.DisplayName);
        });
    }

    private Task HandleItemDoubleClickAsync(NodeExplorerItemSnapshot item)
        => item.IsDirectory
            ? OpenFolderAsync(item.Path, item.DisplayName)
            : OpenFileDetailsAsync(item.Path, item.DisplayName);

    private Task HandleBreadcrumbOpenAsync(NodeExplorerBreadcrumb breadcrumb)
        => OpenFolderAsync(breadcrumb.Path, breadcrumb.Label);

    private Task HandleContextMenuRequestedAsync(FilesContextMenuRequest request)
    {
        OpenItemContextMenu(request.Args, request.Item);
        return Task.CompletedTask;
    }

    private Task HandleContextMenuShareAsync()
    {
        if (contextMenuItem is not null)
        {
            OpenShareModal(contextMenuItem);
        }

        return Task.CompletedTask;
    }

    private Task HandleContextMenuUnpinAsync()
    {
        if (contextMenuItem is not null)
        {
            OpenUnpinModal(contextMenuItem);
        }

        return Task.CompletedTask;
    }

    private async Task OpenFolderAsync(string path, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await BrowseToPathCoreAsync(path, displayName);
        });
    }

    private async Task OpenPreviewFolderAsync()
    {
        if (previewSnapshot is null || !previewSnapshot.IsDirectory)
        {
            return;
        }

        await OpenFolderAsync(previewSnapshot.Path, previewSnapshot.DisplayName);
    }

    private async Task OpenSelectedFileDetailsAsync()
    {
        if (previewSnapshot is null || previewSnapshot.IsDirectory)
        {
            return;
        }

        await OpenFileDetailsAsync(previewSnapshot.Path, previewSnapshot.DisplayName);
    }

    private async Task OpenFileDetailsAsync(string path, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            detailSnapshot = previewSnapshot is not null
                             && string.Equals(previewSnapshot.Path, path, StringComparison.Ordinal)
                ? previewSnapshot
                : await ExplorerWorkflow.GetPreviewSnapshotAsync(path, displayName, CancellationToken.None);
        });
    }

    private void OpenItemContextMenu(WebMouseEventArgs args, NodeExplorerItemSnapshot item)
    {
        if (!HasContextMenuActions(item))
        {
            return;
        }

        selectedBrowsePath = item.Path;
        contextMenuItem = item;
        contextMenuLeft = args.ClientX;
        contextMenuTop = args.ClientY;
        showContextMenu = true;
    }

    private void CloseItemContextMenu()
    {
        showContextMenu = false;
        contextMenuItem = null;
    }

    private void OpenShareFromPreview()
    {
        var shareItem = BuildShareItemFromPreview();
        if (shareItem is null)
        {
            return;
        }

        OpenShareModal(shareItem);
    }

    private void OpenShareModal(NodeExplorerItemSnapshot item)
    {
        shareTargetItem = item;
        CloseItemContextMenu();
    }

    private void OpenUnpinModal(NodeExplorerItemSnapshot item)
    {
        if (!CanUnpinItem(item))
        {
            return;
        }

        unpinTargetItem = item;
        unpinDeleteImmediately = false;
        CloseItemContextMenu();
    }

    private Task HandleShareModalClosed()
    {
        shareTargetItem = null;
        return Task.CompletedTask;
    }

    private void CloseUnpinModal()
    {
        unpinDeleteImmediately = false;
        unpinTargetItem = null;
    }

    private Task HandleUnpinDeleteImmediatelyChanged(bool value)
    {
        unpinDeleteImmediately = value;
        return Task.CompletedTask;
    }

    private Task HandleInspectPathChanged(string? value)
    {
        inspectPath = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleTextFileNameChanged(string? value)
    {
        textFileName = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleTextContentChanged(string? value)
    {
        textContent = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleTextPinChanged(bool value)
    {
        textPin = value;
        return Task.CompletedTask;
    }

    private Task HandleTextWrapChanged(bool value)
    {
        textWrap = value;
        return Task.CompletedTask;
    }

    private Task HandleUploadPinChanged(bool value)
    {
        uploadPin = value;
        return Task.CompletedTask;
    }

    private Task HandleUploadWrapChanged(bool value)
    {
        uploadWrap = value;
        return Task.CompletedTask;
    }

    private void ShowFileUploadPanel()
    {
        showCreateTextPanel = false;
    }

    private void ShowCreateTextUploadPanel()
    {
        showCreateTextPanel = true;
    }

    private async Task InspectAsync()
    {
        if (string.IsNullOrWhiteSpace(inspectPath))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await BrowseToPathCoreAsync(inspectPath.Trim(), ResolveDisplayName(inspectPath, inspectPath));
        });
    }

    private void OpenUploadModal()
    {
        showCreateTextPanel = false;
        showUploadModal = true;
    }

    private void OpenTextUploadModal()
    {
        showCreateTextPanel = true;
        showUploadModal = true;
    }

    private void CloseUploadModal()
    {
        showUploadModal = false;
        showCreateTextPanel = false;
    }

    private void CloseDetailModal()
    {
        detailSnapshot = null;
    }

    private void CloseTopologyModal()
    {
        topologySnapshot = null;
    }

    private void CollapsePreviewPane()
        => showPreviewPane = false;

    private void ExpandPreviewPane()
        => showPreviewPane = true;

    private void TogglePreviewPane()
        => showPreviewPane = !showPreviewPane;

    private async Task CopyTextAsync(string value)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("filesExplorer.copyText", value);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Copied",
                Detail = value
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Copy action failed in the file explorer.");
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Copy failed",
                Detail = ex.Message
            });
        }
    }

    private async Task TogglePreviewPinAsync()
    {
        if (previewSnapshot is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (IsPreviewPinned)
            {
                OpenUnpinModal(BuildShareItemFromPreview()!);
                return;
            }

            await FileWorkflow.PinAsync(previewSnapshot.Target, recursive: true, CancellationToken.None);
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Pinned",
                Detail = previewSnapshot.Target
            });

            await RefreshCurrentViewCoreAsync(previewSnapshot.Path, forcePinnedReload: true);
        });
    }

    private async Task ConfirmUnpinAsync()
    {
        if (unpinTargetItem is null)
        {
            return;
        }

        var target = unpinTargetItem;
        await RunBusyAsync(async () =>
        {
            await FileWorkflow.UnpinAsync(target.Target, recursive: true, CancellationToken.None);
            var garbageCollected = false;
            if (unpinDeleteImmediately)
            {
                try
                {
                    await MaintenanceWorkflow.RunRepositoryGcAsync(CancellationToken.None);
                    garbageCollected = true;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Repository GC failed after unpinning {Cid}.", target.Target);
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Warning,
                        Summary = "Unpinned, GC failed",
                        Detail = $"{target.Target} was unpinned, but repository GC did not complete: {ex.Message}"
                    });
                }
            }

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = garbageCollected ? "Unpinned and collected" : "Unpinned",
                Detail = unpinDeleteImmediately
                    ? garbageCollected
                        ? $"{target.Target} was unpinned and repository GC completed. Blocks can remain if other content still references them."
                        : $"{target.Target} was unpinned. Repository GC was requested but did not complete."
                    : target.Target
            });

            CloseUnpinModal();
            await RefreshCurrentViewCoreAsync(target.Path, forcePinnedReload: true);
        });
    }

    private async Task OpenTopologyAsync()
    {
        var targetPath = previewSnapshot?.Path ?? currentFolderSnapshot?.NormalizedPath;
        if (string.IsNullOrWhiteSpace(targetPath) || IsVirtualExplorerPath(targetPath))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            topologySnapshot = await FileWorkflow.InspectFileSystemAsync(targetPath, CancellationToken.None);
        });
    }

    private async Task LoadPinnedItemsCoreAsync(bool forceReload = false)
    {
        if (pinnedItemsLoaded && !forceReload)
        {
            return;
        }

        if (!forceReload)
        {
            var cachedItems = ExplorerWorkflow.GetCachedPinnedExplorerItems();
            if (cachedItems.Count > 0)
            {
                var useCache = false;
                try
                {
                    useCache = await ExplorerWorkflow.HasTrustedCachedPinnedExplorerItemsAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Pinned explorer cache validation failed. Falling back to cached roots.");
                    useCache = true;
                }

                if (useCache)
                {
                    pinnedItems = cachedItems.Select(ApplyKnownDisplayName).ToList();
                    rootItems = BuildRootItems(pinnedItems);
                    pinnedItemsLoaded = true;
                    _ = RefreshPinnedItemsInBackgroundAsync();
                    return;
                }
            }
        }

        var loadedItems = await ExplorerWorkflow.ListPinnedExplorerItemsAsync(CancellationToken.None);
        pinnedItems = loadedItems.Select(ApplyKnownDisplayName).ToList();
        rootItems = BuildRootItems(pinnedItems);
        pinnedItemsLoaded = true;
    }

    private async Task RefreshCurrentViewCoreAsync(string? preferredSelectionPath = null, bool forcePinnedReload = false)
    {
        await LoadPinnedItemsCoreAsync(forcePinnedReload);
        if (currentFolderSnapshot is not null)
        {
            await BrowseToPathCoreAsync(
                currentFolderSnapshot.NormalizedPath,
                GetCurrentDisplayName(currentFolderSnapshot),
                preferredSelectionPath);
            return;
        }

        await OpenPinnedRootsCoreAsync(preferredSelectionPath ?? selectedBrowsePath ?? previewSnapshot?.Path);
    }

    private async Task OpenPinnedRootsCoreAsync(string? preferredSelectionPath = null)
    {
        currentFolderSnapshot = null;

        if (rootItems.Count == 0)
        {
            selectedBrowsePath = null;
            previewSnapshot = null;
            inspectPath = string.Empty;
            return;
        }

        var preferredItem = rootItems.FirstOrDefault(item =>
                                !string.IsNullOrWhiteSpace(preferredSelectionPath)
                                && (string.Equals(item.Path, preferredSelectionPath, StringComparison.Ordinal)
                                    || string.Equals(item.Target, preferredSelectionPath, StringComparison.Ordinal)))
                            ?? rootItems[0];

        selectedBrowsePath = preferredItem.Path;
        inspectPath = preferredItem.Path;
        await LoadPreviewCoreAsync(preferredItem.Path, preferredItem.DisplayName);
    }

    private async Task BrowseToPathCoreAsync(string path, string? displayName, string? preferredSelectionPath = null)
    {
        var snapshot = await ExplorerWorkflow.GetExplorerSnapshotAsync(path, CancellationToken.None);
        if (snapshot.Current.IsDirectory)
        {
            currentFolderSnapshot = snapshot;
            inspectPath = snapshot.NormalizedPath;

            var preferredChild = snapshot.Entries.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(preferredSelectionPath)
                && (string.Equals(item.Path, preferredSelectionPath, StringComparison.Ordinal)
                    || string.Equals(item.Target, preferredSelectionPath, StringComparison.Ordinal)));

            if (preferredChild is not null)
            {
                selectedBrowsePath = preferredChild.Path;
                await LoadPreviewCoreAsync(preferredChild.Path, preferredChild.DisplayName);
                return;
            }

            selectedBrowsePath = null;
            await LoadPreviewCoreAsync(snapshot.NormalizedPath, displayName ?? GetCurrentDisplayName(snapshot));
            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ParentPath))
        {
            var parentSnapshot = await ExplorerWorkflow.GetExplorerSnapshotAsync(snapshot.ParentPath, CancellationToken.None);
            currentFolderSnapshot = parentSnapshot;
            inspectPath = parentSnapshot.NormalizedPath;

            var matchingChild = parentSnapshot.Entries.FirstOrDefault(item =>
                                    string.Equals(item.Path, snapshot.NormalizedPath, StringComparison.Ordinal))
                                ?? parentSnapshot.Entries.FirstOrDefault(item =>
                                    string.Equals(item.Target, snapshot.Current.ResolvedId, StringComparison.Ordinal));

            if (matchingChild is not null)
            {
                selectedBrowsePath = matchingChild.Path;
                await LoadPreviewCoreAsync(matchingChild.Path, matchingChild.DisplayName);
                return;
            }
        }

        currentFolderSnapshot = null;
        selectedBrowsePath = snapshot.NormalizedPath;
        inspectPath = snapshot.NormalizedPath;
        await LoadPreviewCoreAsync(snapshot.NormalizedPath, displayName ?? ResolveDisplayName(snapshot.NormalizedPath, snapshot.Current.ResolvedId));
    }

    private async Task LoadPreviewCoreAsync(string path, string? displayName)
    {
        var snapshot = await ExplorerWorkflow.GetPreviewSnapshotAsync(path, displayName, CancellationToken.None);
        previewSnapshot = ApplyKnownDisplayName(snapshot);
    }

    private async Task ChooseFilesAsync()
    {
        await StartBrowserUploadAsync(directory: false);
    }

    private async Task ChooseFolderAsync()
    {
        await StartBrowserUploadAsync(directory: true);
    }

    private async Task StartBrowserUploadAsync(bool directory)
    {
        await RunBusyAsync(async () =>
        {
            var identifier = directory
                ? "filesExplorer.pickFolderAndUpload"
                : "filesExplorer.pickFilesAndUpload";

            var uploaded = await JSRuntime.InvokeAsync<NodeFileSnapshot?>(
                identifier,
                new
                {
                    endpoint = BrowserUploadEndpoint,
                    pin = uploadPin,
                    wrap = uploadWrap
                });

            if (uploaded is null)
            {
                return;
            }

            await HandleUploadedSnapshotCoreAsync(uploaded);
        });
    }

    private Task HandleDropZoneUploadCompletedAsync(NodeFileSnapshot uploaded)
        => RunBusyAsync(async () =>
        {
            await HandleUploadedSnapshotCoreAsync(uploaded);
        });

    private Task HandleDropZoneUploadFailedAsync(string message)
    {
        errorMessage = message;
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = "Upload failed",
            Detail = message
        });

        return InvokeAsync(StateHasChanged);
    }

    private async Task CreateTextAsync()
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            errorMessage = "Text content is required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var created = await FileWorkflow.UploadTextAsync(textFileName, textContent, textPin, textWrap, CancellationToken.None);
            await RefreshCurrentViewCoreAsync(created.ResolvedId, forcePinnedReload: true);
            showUploadModal = false;
            showCreateTextPanel = false;
            textContent = string.Empty;

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Text CID created",
                Detail = created.ResolvedId
            });
        });
    }

    private Task HandleWorkbenchStateChanged(string stateJson)
    {
        fileCanvasState = stateJson;
        return Task.CompletedTask;
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        errorMessage = null;
        isBusy = true;

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "File explorer action failed.");
            errorMessage = ex.Message;
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Files action failed",
                Detail = ex.Message
            });
        }
        finally
        {
            isBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void HandleNodeSessionChanged()
    {
        if (!NodeSessionState.IsHydrated)
        {
            return;
        }

        hasStartedInitialLoad = true;
        currentFolderSnapshot = null;
        detailSnapshot = null;
        errorMessage = null;
        previewSnapshot = null;
        rootItems = [];
        selectedBrowsePath = null;
        CloseItemContextMenu();
        shareTargetItem = null;
        CloseTopologyModal();
        CloseUnpinModal();
        CloseUploadModal();
        inspectPath = string.Empty;
        pinnedItemsLoaded = false;
        _ = InvokeAsync(InitializeExplorerAsync);
    }

    public void Dispose()
    {
        NodeSessionState.Changed -= HandleNodeSessionChanged;
    }

    private bool IsItemSelected(NodeExplorerItemSnapshot item)
        => string.Equals(item.Path, selectedBrowsePath, StringComparison.Ordinal)
           || string.Equals(item.Path, previewSnapshot?.Path, StringComparison.Ordinal);

    private bool IsCurrentPath(string path)
        => string.Equals(path, currentFolderSnapshot?.NormalizedPath, StringComparison.Ordinal);

    private NodeExplorerItemSnapshot? BuildShareItemFromPreview()
    {
        if (previewSnapshot is null || IsVirtualExplorerPath(previewSnapshot.Path))
        {
            return null;
        }

        return new NodeExplorerItemSnapshot(
            previewSnapshot.DisplayName,
            previewSnapshot.Path,
            previewSnapshot.Target,
            previewSnapshot.IsDirectory,
            previewSnapshot.TypeLabel,
            previewSnapshot.Size,
            previewSnapshot.ChildCount);
    }

    private string GetLocationTitle()
        => currentFolderSnapshot is null
            ? "Pinned files and folders"
            : GetCurrentDisplayName(currentFolderSnapshot);

    private string GetLocationDescription()
    {
        if (currentFolderSnapshot is null)
        {
            return "Single-click a card to preview it in the right panel. Double-click a folder to step into it or a file to open the details dialog.";
        }

        return $"{currentFolderSnapshot.Entries.Count} direct item{(currentFolderSnapshot.Entries.Count == 1 ? string.Empty : "s")} in this folder. Single-click previews. Double-click opens.";
    }

    private string GetTopologyTitle()
        => topologySnapshot is null
            ? "File topology"
            : ResolveDisplayName(topologySnapshot.RequestedPath, topologySnapshot.ResolvedId);

    private static string GetCurrentDisplayName(NodeExplorerSnapshot snapshot)
    {
        var label = snapshot.Breadcrumbs.LastOrDefault()?.Label;
        return string.IsNullOrWhiteSpace(label)
            ? ResolveDisplayName(snapshot.NormalizedPath, snapshot.Current.ResolvedId)
            : label;
    }

    private static string Shorten(string? value, int limit)
        => FilesUiText.Shorten(value, limit);

    private string BuildFileContentUrl(string target, string displayName, bool download)
    {
        var nameHint = ResolveContentNameHint(target, displayName);
        return $"/api/files/content?path={Uri.EscapeDataString(target)}&name={Uri.EscapeDataString(nameHint)}&download={download.ToString().ToLowerInvariant()}";
    }

    private async Task HandleUploadedSnapshotCoreAsync(NodeFileSnapshot uploaded)
    {
        RememberDisplayName(uploaded);
        await RefreshCurrentViewCoreAsync(uploaded.ResolvedId, forcePinnedReload: true);
        showUploadModal = false;
        showCreateTextPanel = false;

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = uploaded.IsDirectory ? "Folder uploaded" : "File uploaded",
            Detail = uploaded.ResolvedId
        });
    }

    private static string ResolveDisplayName(string? value, string fallback)
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

    private async Task RefreshPinnedItemsInBackgroundAsync()
    {
        try
        {
            var loadedItems = await ExplorerWorkflow.ListPinnedExplorerItemsAsync(CancellationToken.None);
            pinnedItems = loadedItems.Select(ApplyKnownDisplayName).ToList();
            rootItems = BuildRootItems(pinnedItems);

            if (currentFolderSnapshot is null && previewSnapshot is not null)
            {
                previewSnapshot = await ExplorerWorkflow.GetPreviewSnapshotAsync(previewSnapshot.Path, previewSnapshot.DisplayName, CancellationToken.None);
                previewSnapshot = ApplyKnownDisplayName(previewSnapshot);
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Background explorer index refresh failed.");
        }
    }

    private static IReadOnlyList<NodeExplorerItemSnapshot> BuildRootItems(IReadOnlyList<NodeExplorerItemSnapshot> pinnedRootItems)
    {
        if (pinnedRootItems.Count == 0)
        {
            return [];
        }

        var rootDirectories = pinnedRootItems
            .Where(item => item.IsDirectory)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target, StringComparer.Ordinal)
            .ToList();

        var unsortedFiles = pinnedRootItems
            .Where(item => !item.IsDirectory)
            .ToList();

        if (unsortedFiles.Count > 0)
        {
            rootDirectories.Insert(0, new NodeExplorerItemSnapshot(
                "UNSORTED",
                "/virtual/unsorted",
                "/virtual/unsorted",
                true,
                "Virtual folder",
                unsortedFiles.Sum(item => item.Size),
                unsortedFiles.Count));
        }

        return rootDirectories;
    }

    private static bool IsVirtualExplorerPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && path.Trim().StartsWith("/virtual/", StringComparison.OrdinalIgnoreCase);

    private bool HasContextMenuActions(NodeExplorerItemSnapshot item)
        => CanShareItem(item) || CanUnpinItem(item);

    private static bool CanShareItem(NodeExplorerItemSnapshot item)
        => !IsVirtualExplorerPath(item.Path);

    private bool CanUnpinItem(NodeExplorerItemSnapshot item)
        => !IsVirtualExplorerPath(item.Path)
           && pinnedItems.Any(pinned => string.Equals(pinned.Target, item.Target, StringComparison.Ordinal));

    private void RememberDisplayName(NodeFileSnapshot snapshot)
    {
        var requestedName = string.IsNullOrWhiteSpace(snapshot.RequestedPath)
            ? string.Empty
            : Path.GetFileName(snapshot.RequestedPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return;
        }

        knownDisplayNamesByTarget[snapshot.ResolvedId] = requestedName;
    }

    private string ResolveContentNameHint(string target, string displayName)
    {
        if (knownDisplayNamesByTarget.TryGetValue(target, out var knownDisplayName))
        {
            return knownDisplayName;
        }

        return displayName;
    }

    private NodeExplorerItemSnapshot ApplyKnownDisplayName(NodeExplorerItemSnapshot item)
    {
        if (!knownDisplayNamesByTarget.TryGetValue(item.Target, out var knownDisplayName))
        {
            return item;
        }

        return new NodeExplorerItemSnapshot(
            knownDisplayName,
            item.Path,
            item.Target,
            item.IsDirectory,
            item.TypeLabel,
            item.Size,
            item.ChildCount);
    }

    private NodePreviewSnapshot ApplyKnownDisplayName(NodePreviewSnapshot snapshot)
    {
        if (!knownDisplayNamesByTarget.TryGetValue(snapshot.Target, out var knownDisplayName))
        {
            return snapshot;
        }

        return new NodePreviewSnapshot
        {
            DisplayName = knownDisplayName,
            Target = snapshot.Target,
            Path = snapshot.Path,
            IsDirectory = snapshot.IsDirectory,
            TypeLabel = snapshot.TypeLabel,
            Size = snapshot.Size,
            ChildCount = snapshot.ChildCount,
            PreviewText = snapshot.PreviewText
        };
    }
}

