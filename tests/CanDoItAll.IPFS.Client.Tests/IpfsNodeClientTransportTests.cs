using System.Net;
using System.Net.Http;
using System.Text;
using Ipfs;
using Ipfs.Engine.Client;
using Ipfs.Engine.Client.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanDoItAll.IPFS.Client.Tests;

[TestClass]
public sealed class IpfsNodeClientTransportTests
{
    private const string KnownCid =
        "QmYwAPJzv5CZsnAzt8auV2J9m9wM4BvP64QekJy5oQ1gnS";

    private readonly TestContext testContext;

    public IpfsNodeClientTransportTests(TestContext testContext)
    {
        this.testContext = testContext;
    }

    [TestMethod]
    public async Task VersionAsync_NormalizedBaseAddressAndApiPath_PostsExpectedRoute()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, """{"Version":"1.2.3"}"""));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/root")
        };
        var client = new IpfsNodeClient(
            httpClient,
            new IpfsNodeClientOptions { ApiPath = "/api/v0/" });

        // Act
        var version = await client.Generic.VersionAsync(testContext.CancellationToken);

        // Assert
        Assert.AreEqual(HttpMethod.Post, handler.LastMethod);
        Assert.AreEqual(
            "https://node.example/root/api/v0/version",
            handler.LastRequestUri);
        Assert.AreEqual("1.2.3", version["Version"]);
        Assert.AreEqual(new Uri("https://node.example/root/"), client.Options.BaseAddress);
        Assert.AreEqual("api/v0", client.Options.ApiPath);
    }

    [TestMethod]
    public async Task SetAsync_ValuesRequireEscaping_AppendsRepeatedArgumentsInOrder()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, """{"Key":"ignored","Value":"ignored"}"""));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        await client.Config.SetAsync(
            "Client Test/Value",
            "alpha+beta & gamma",
            testContext.CancellationToken);

        // Assert
        Assert.AreEqual(
            "https://node.example/api/v0/config"
                + "?arg=Client%20Test%2FValue"
                + "&arg=alpha%2Bbeta%20%26%20gamma",
            handler.LastRequestUri);
    }

    [TestMethod]
    [DataRow("ftp://node.example/", "api/v0", "BaseAddress")]
    [DataRow("https://node.example/", "api/v0?tenant=one", "ApiPath")]
    public void Constructor_InvalidOptions_ThrowsBeforeDispatch(
        string baseAddress,
        string apiPath,
        string expectedParameterName)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, "{}"));
        using var httpClient = new HttpClient(handler);
        var options = new IpfsNodeClientOptions
        {
            BaseAddress = new Uri(baseAddress),
            ApiPath = apiPath
        };

        // Act
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new IpfsNodeClient(httpClient, options));

        // Assert
        Assert.AreEqual(expectedParameterName, exception.ParamName);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task VersionAsync_JsonApiError_MapsDetailsAndDisposesResponseContent()
    {
        // Arrange
        const string payload =
            """{"Message":"invalid request","Code":"E_BAD","Details":["first","second"]}""";
        var content = new TrackingJsonContent(payload);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = content
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsApiException>(
            async () => await client.Generic.VersionAsync(testContext.CancellationToken));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.AreEqual("version", exception.Route);
        Assert.AreEqual("E_BAD", exception.ErrorCode);
        Assert.AreEqual(payload, exception.RawBody);
        Assert.HasCount(2, exception.Details);
        Assert.Contains("first", exception.Details);
        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task VersionAsync_HandlerCancellationWithoutCallerCancellation_ThrowsTransportException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => throw new OperationCanceledException("simulated timeout"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsTransportException>(
            async () => await client.Generic.VersionAsync(testContext.CancellationToken));

        // Assert
        Assert.AreEqual("version", exception.Route);
        Assert.Contains("timed out", exception.Message);
        Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
    }

    [TestMethod]
    public async Task SetAsync_TransportFailure_DoesNotDiscloseQueryValues()
    {
        // Arrange
        const string secretKey = "Credentials.ApiToken";
        const string secretValue = "do-not-log-this-token";
        var handler = new StubHttpMessageHandler(
            static (_, _) => throw new HttpRequestException("simulated connection failure"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsTransportException>(
            async () => await client.Config.SetAsync(
                secretKey,
                secretValue,
                testContext.CancellationToken));

        // Assert
        Assert.AreEqual("config", exception.Route);
        Assert.Contains("route 'config'", exception.Message);
        Assert.DoesNotContain(secretKey, exception.Message);
        Assert.DoesNotContain(secretValue, exception.Message);
        Assert.DoesNotContain("?", exception.Message);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public async Task VersionAsync_CallerCancels_PropagatesOperationCanceledException()
    {
        // Arrange
        var dispatchStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            dispatchStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return await JsonResponse(HttpStatusCode.OK, "{}");
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        using var callerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);

        // Act
        var request = client.Generic.VersionAsync(callerCancellation.Token);
        await dispatchStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            testContext.CancellationToken);
        callerCancellation.Cancel();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await request);
    }

    [TestMethod]
    public async Task ReadFileAsync_ReturnedStreamDisposed_DisposesResponseStream()
    {
        // Arrange
        var responseStream = new TrackingMemoryStream(Encoding.UTF8.GetBytes("content"));
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseStream)
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var stream = await client.FileSystem.ReadFileAsync(
            "/ipfs/example",
            testContext.CancellationToken);
        stream.Dispose();

        // Assert
        Assert.IsTrue(responseStream.IsDisposed);
        Assert.AreEqual(
            "https://node.example/api/v0/cat?arg=%2Fipfs%2Fexample",
            handler.LastRequestUri);
    }

    [TestMethod]
    public async Task ReadFileAsync_StreamAcquisitionFails_DisposesResponseContent()
    {
        // Arrange
        var content = new ThrowingReadStreamContent(
            new IOException("simulated stream acquisition failure"));
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await client.FileSystem.ReadFileAsync(
                "/ipfs/example",
                testContext.CancellationToken));

        // Assert
        Assert.AreEqual("simulated stream acquisition failure", exception.Message);
        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public async Task SubscribeAsync_CallerCancels_PropagatesCancellationAndDisposesResponse()
    {
        // Arrange
        var responseStream = new BlockingReadStream();
        var content = new TrackingStreamContent(responseStream);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        using var callerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);

        // Act
        var subscription = client.PubSub.SubscribeAsync(
            "updates",
            static _ => { },
            callerCancellation.Token);
        await responseStream.ReadStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            testContext.CancellationToken);
        callerCancellation.Cancel();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await subscription);
        Assert.IsTrue(responseStream.IsDisposed);
        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task PutAsync_CallerOwnedStreamOnSuccess_RemainsOpen()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            await request.Content!.CopyToAsync(Stream.Null, cancellationToken);
            return await JsonResponse(
                HttpStatusCode.OK,
                $$"""{"Key":"{{KnownCid}}"}""");
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        using var input = new TrackingMemoryStream(Encoding.UTF8.GetBytes("block"));

        // Act
        var cid = await client.Block.PutAsync(
            input,
            cancel: testContext.CancellationToken);

        // Assert
        Assert.AreEqual(KnownCid, cid.ToString());
        Assert.IsFalse(input.IsDisposed);
        input.Position = 0;
        Assert.AreEqual((int)'b', input.ReadByte());
    }

    [TestMethod]
    public async Task PutAsync_ApiError_RemainsOwnedByCaller()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(
                HttpStatusCode.BadRequest,
                """{"Message":"invalid block"}"""));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        using var input = new TrackingMemoryStream(Encoding.UTF8.GetBytes("block"));

        // Act
        await Assert.ThrowsExactlyAsync<IpfsApiException>(
            async () => await client.Block.PutAsync(
                input,
                cancel: testContext.CancellationToken));

        // Assert
        Assert.IsFalse(input.IsDisposed);
        Assert.AreEqual((int)'b', input.ReadByte());
    }

    [TestMethod]
    public async Task VersionAsync_NullJson_ThrowsSerializationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, "null"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsSerializationException>(
            async () => await client.Generic.VersionAsync(
                testContext.CancellationToken));

        // Assert
        Assert.AreEqual("version", exception.Route);
        Assert.AreEqual("null", exception.RawPayload);
    }

    [TestMethod]
    public async Task RemoveAsync_NullJsonOptionalResponse_ThrowsSerializationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, "null"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsSerializationException>(
            async () => await client.Block.RemoveAsync(
                Cid.Decode(KnownCid),
                cancel: testContext.CancellationToken));

        // Assert
        Assert.AreEqual("block/rm", exception.Route);
        Assert.AreEqual("null", exception.RawPayload);
    }

    [TestMethod]
    public async Task SubscribeAsync_NullNdjsonItem_ThrowsSerializationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            static (_, _) => JsonResponse(HttpStatusCode.OK, "null\n"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        var callbackInvoked = false;

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsSerializationException>(
            async () => await client.PubSub.SubscribeAsync(
                "updates",
                _ => callbackInvoked = true,
                testContext.CancellationToken));

        // Assert
        Assert.AreEqual("pubsub/sub", exception.Route);
        Assert.AreEqual("null", exception.RawPayload);
        Assert.IsFalse(callbackInvoked);
    }

    [TestMethod]
    public async Task SubscribeAsync_MidStreamIoFailure_ThrowsTransportException()
    {
        // Arrange
        var sender = Convert.ToBase64String(new MultiHash(KnownCid).ToArray());
        var responseStream = new MidStreamFailureStream(
            Encoding.UTF8.GetBytes(
                $$"""{"from":"{{sender}}","topicIDs":["updates"]}""" + "\n"),
            new IOException("simulated connection reset"));
        var content = new TrackingStreamContent(responseStream);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        var callbackCount = 0;

        // Act
        var exception = await Assert.ThrowsExactlyAsync<IpfsTransportException>(
            async () => await client.PubSub.SubscribeAsync(
                "updates",
                _ => callbackCount++,
                testContext.CancellationToken));

        // Assert
        Assert.AreEqual("pubsub/sub", exception.Route);
        Assert.Contains("line-delimited", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        Assert.AreEqual(1, callbackCount);
        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task ReadFileAsync_InnerDisposeThrows_StillDisposesResponseContent()
    {
        // Arrange
        var responseStream = new ThrowingDisposeStream();
        var content = new TrackingStreamContent(
            responseStream,
            disposeContentStream: false);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://node.example/")
        };
        var client = new IpfsNodeClient(httpClient);
        var stream = await client.FileSystem.ReadFileAsync(
            "/ipfs/example",
            testContext.CancellationToken);

        // Act
        var exception = Assert.ThrowsExactly<IOException>(() => stream.Dispose());

        // Assert
        Assert.AreEqual("simulated dispose failure", exception.Message);
        Assert.IsTrue(responseStream.DisposeAttempted);
        Assert.IsTrue(content.IsDisposed);
    }

    private static Task<HttpResponseMessage> JsonResponse(
        HttpStatusCode statusCode,
        string payload)
    {
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> sendAsync;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            this.sendAsync = sendAsync;
        }

        public int CallCount { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri?.OriginalString;
            return sendAsync(request, cancellationToken);
        }
    }

    private sealed class TrackingJsonContent : HttpContent
    {
        private readonly byte[] payload;

        public TrackingJsonContent(string payload)
        {
            this.payload = Encoding.UTF8.GetBytes(payload);
            Headers.ContentType = new("application/json");
        }

        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            stream.Write(payload, 0, payload.Length);
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = payload.LongLength;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public TrackingMemoryStream(byte[] buffer)
            : base(buffer, writable: false)
        {
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingReadStreamContent : HttpContent
    {
        private readonly Exception exception;

        public ThrowingReadStreamContent(Exception exception)
        {
            this.exception = exception;
        }

        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            return Task.FromException(exception);
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromException<Stream>(exception);
        }

        protected override Task<Stream> CreateContentReadStreamAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromException<Stream>(exception);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TrackingStreamContent : HttpContent
    {
        private readonly Stream stream;
        private readonly bool disposeContentStream;

        public TrackingStreamContent(
            Stream stream,
            bool disposeContentStream = true)
        {
            this.stream = stream;
            this.disposeContentStream = disposeContentStream;
        }

        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(
            Stream target,
            TransportContext? context)
        {
            return stream.CopyToAsync(target);
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult(stream);
        }

        protected override Task<Stream> CreateContentReadStreamAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            if (disposeContentStream)
            {
                base.Dispose(disposing);
            }
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        public TaskCompletionSource<bool> ReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingDisposeStream : MemoryStream
    {
        public bool DisposeAttempted { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeAttempted = true;
                throw new IOException("simulated dispose failure");
            }

            base.Dispose(disposing);
        }
    }

    private sealed class MidStreamFailureStream : MemoryStream
    {
        private readonly Exception failure;
        private bool returnedPayload;

        public MidStreamFailureStream(byte[] payload, Exception failure)
            : base(payload, writable: false)
        {
            this.failure = failure;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!returnedPayload)
            {
                returnedPayload = true;
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            return Task.FromException<int>(failure);
        }
    }
}
