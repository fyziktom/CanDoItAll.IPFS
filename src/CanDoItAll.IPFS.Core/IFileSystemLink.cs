using System;
using System.Collections.Generic;
using System.Text;

namespace Ipfs
{
    /// <summary>
    ///    A link to another file system node in IPFS.
    /// </summary>
    public interface IFileSystemLink : IMerkleLink
    {
        /// <summary>
        ///   Determines if the linked node is a directory.
        /// </summary>
        bool IsDirectory { get; }

        /// <summary>
        ///   The cumulative file-content size for the linked node.
        /// </summary>
        long ContentSize { get; }

        /// <summary>
        ///   The number of direct children for the linked node when it is a directory.
        /// </summary>
        int ChildCount { get; }
    }
}
