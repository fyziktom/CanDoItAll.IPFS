using System;
using System.Net;

namespace Ipfs.Engine.Client.Transport
{
    /// <summary>
    ///   Represents an error returned by the IPFS HTTP API.
    /// </summary>
    public class IpfsApiException : Exception
    {
        public IpfsApiException(HttpStatusCode statusCode, string route, string message, string? errorCode = null, string? rawBody = null, string[]? details = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Route = route ?? throw new ArgumentNullException(nameof(route));
            ErrorCode = errorCode;
            RawBody = rawBody;
            Details = details ?? Array.Empty<string>();
        }

        public HttpStatusCode StatusCode { get; }

        public string Route { get; }

        public string? ErrorCode { get; }

        public string? RawBody { get; }

        public string[] Details { get; }
    }

    /// <summary>
    ///   Represents a transport failure before the API returned a valid result.
    /// </summary>
    public class IpfsTransportException : Exception
    {
        public IpfsTransportException(string route, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
        }

        public string Route { get; }
    }

    /// <summary>
    ///   Represents invalid JSON or NDJSON returned by the API.
    /// </summary>
    public class IpfsSerializationException : Exception
    {
        public IpfsSerializationException(string route, string message, string? rawPayload = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            RawPayload = rawPayload;
        }

        public string Route { get; }

        public string? RawPayload { get; }
    }
}
