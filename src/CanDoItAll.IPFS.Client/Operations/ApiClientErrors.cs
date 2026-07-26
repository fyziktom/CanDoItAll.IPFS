using System;

namespace Ipfs.Engine.Client.Operations
{
    internal static class ApiClientErrors
    {
        public static NotSupportedException MissingServerCapability(string operation, string reason)
        {
            return new NotSupportedException(
                $"{operation} is not available through the current HTTP API. {reason}");
        }
    }
}
