using Ipfs.Engine.Client.Transport;

namespace Ipfs.Engine.Client.Operations
{
    public abstract class ApiClientBase
    {
        internal ApiClientBase(IpfsHttpTransport transport)
        {
            Transport = transport;
        }

        internal IpfsHttpTransport Transport { get; }

        protected static System.NotSupportedException MissingServerCapability(string operation, string reason)
        {
            return new System.NotSupportedException($"{operation} is not available through the current HTTP API. {reason}");
        }
    }
}
