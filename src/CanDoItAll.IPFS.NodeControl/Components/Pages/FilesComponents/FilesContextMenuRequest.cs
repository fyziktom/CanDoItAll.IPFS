using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Components.Pages.FilesComponents;

public sealed record FilesContextMenuRequest(
    NodeExplorerItemSnapshot Item,
    Microsoft.AspNetCore.Components.Web.MouseEventArgs Args);
