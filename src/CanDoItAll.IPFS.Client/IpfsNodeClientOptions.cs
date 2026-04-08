using System;

namespace Ipfs.Engine.Client
{
    /// <summary>
    ///   Options for <see cref="IpfsEngineClient"/>.
    /// </summary>
    public sealed class IpfsNodeClientOptions
    {
        /// <summary>
        ///   The base address of the IPFS node.
        /// </summary>
        public Uri? BaseAddress { get; set; }

        /// <summary>
        ///   The relative HTTP API path under the node root.
        /// </summary>
        public string ApiPath { get; set; } = "api/v0";
    }
}
