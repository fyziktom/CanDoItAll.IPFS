using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.NodeControl.Components.Pages.PinRequestsComponents;
using CanDoItAll.IPFS.NodeControl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Tests.NodeControl;

[TestClass]
public sealed class PinRequestComponentsTests
{
    [TestMethod]
    public void PinRequestsFilterToolbar_Renders_Counts_And_Raises_Filter_Selection()
    {
        using var context = CreateContext();
        PinRequestFilter? selectedFilter = null;

        var cut = context.RenderComponent<PinRequestsFilterToolbar>(parameters => parameters
            .Add(component => component.Filters,
            [
                new PinRequestFilterSummary(PinRequestFilter.All, "All", 3, false),
                new PinRequestFilterSummary(PinRequestFilter.Pending, "Pending", 1, true),
                new PinRequestFilterSummary(PinRequestFilter.Accepted, "Accepted", 2, false)
            ])
            .Add(component => component.VisibleCount, 1)
            .Add(component => component.OnFilterSelected, filter => selectedFilter = filter));

        StringAssert.Contains(cut.Markup, "Pending");
        StringAssert.Contains(cut.Markup, "Accepted");
        StringAssert.Contains(cut.Markup, "1 item shown");

        cut.FindAll("button")
            .Single(button =>
                button.TextContent.Contains("Accepted", StringComparison.Ordinal)
                && button.TextContent.Contains("2", StringComparison.Ordinal))
            .Click();

        Assert.AreEqual(PinRequestFilter.Accepted, selectedFilter);
    }

    [TestMethod]
    public void PinRequestCard_Renders_Request_Metadata_And_Pending_Actions()
    {
        using var context = CreateContext();
        var request = CreateStoredRequest();
        string? openedRequestId = null;
        PinRequestCopyRequest? copied = null;

        var cut = context.RenderComponent<PinRequestCard>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.IsBusy, false)
            .Add(component => component.OnOpenDetails, requestId => openedRequestId = requestId)
            .Add(component => component.OnCopy, copyRequest => copied = copyRequest));

        StringAssert.Contains(cut.Markup, "Backup copy");
        StringAssert.Contains(cut.Markup, "Compatibility");
        StringAssert.Contains(cut.Markup, "Folder");
        StringAssert.Contains(cut.Markup, "4 KB");
        StringAssert.Contains(cut.Markup, "Copy address");
        StringAssert.Contains(cut.Markup, "Accept");
        StringAssert.Contains(cut.Markup, "Reject");

        cut.FindAll("button")
            .Single(button => string.Equals(button.TextContent.Trim(), "Details", StringComparison.Ordinal))
            .Click();

        cut.Find("code[title='12D3KooWSender']")
            .ParentElement!
            .QuerySelector("button.rp-copy-button")!
            .Click();

        Assert.AreEqual(request.Request.RequestId, openedRequestId);
        Assert.IsNotNull(copied);
        Assert.AreEqual("Sender ID", copied.Label);
    }

    [TestMethod]
    public void PinRequestDetailsDialog_Renders_Callouts_And_Copy_Actions()
    {
        using var context = CreateContext();
        var request = CreateStoredRequest();
        PinRequestCopyRequest? copied = null;

        var cut = context.RenderComponent<PinRequestDetailsDialog>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.IsBusy, false)
            .Add(component => component.OnCopy, copyRequest => copied = copyRequest));

        StringAssert.Contains(cut.Markup, "Compatibility mode");
        StringAssert.Contains(cut.Markup, "Direct items");
        StringAssert.Contains(cut.Markup, "Please pin this folder for the remote team.");
        StringAssert.Contains(cut.Markup, "/ipfs/bafy-request");

        cut.Find("code[title='bafy-request']")
            .ParentElement!
            .QuerySelector("button.rp-copy-button")!
            .Click();

        Assert.IsNotNull(copied);
    }

    private static Bunit.TestContext CreateContext()
    {
        var context = new Bunit.TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static StoredRemotePinRequest CreateStoredRequest()
        => new()
        {
            ReceivedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Request = new RemotePinRequestEnvelope
            {
                RequestId = Guid.NewGuid().ToString("N"),
                RequestedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-8),
                Note = "Please pin this folder for the remote team.",
                Sender = new RemotePinSenderSnapshot(
                    "Sender node",
                    "http://sender-app/",
                    "http://sender-node/",
                    "12D3KooWSender",
                    ["/ip4/127.0.0.1/tcp/4001/p2p/12D3KooWSender"]),
                Content = new RemotePinContentSnapshot(
                    "/ipfs/bafy-request",
                    "bafy-request",
                    "Backup copy",
                    IsDirectory: true,
                    Size: 4096,
                    ChildCount: 4)
            },
            ResponseMessage = "Waiting for receiver action.",
            SecurityDisposition = RemotePinSecurityDisposition.Compatibility,
            SecurityMessage = "Legacy compatibility requests should only be accepted on trusted transitional networks.",
            State = RemotePinRequestState.Pending
        };
}
