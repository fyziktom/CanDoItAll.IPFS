using System;
using System.Collections.Generic;
using System.IO;

namespace Ipfs.Engine.Client.Models
{
    internal sealed class ClientDataBlock : IDataBlock
    {
        private readonly byte[] dataBytes;

        public ClientDataBlock(Cid id, byte[]? dataBytes, long? size = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            this.dataBytes = dataBytes ?? Array.Empty<byte>();
            Size = size ?? this.dataBytes.LongLength;
        }

        public byte[] DataBytes => dataBytes;

        public Stream DataStream => new MemoryStream(dataBytes, writable: false);

        public Cid Id { get; }

        public long Size { get; }
    }

    internal sealed class ClientMerkleLink : IFileSystemLink
    {
        public ClientMerkleLink(string? name, Cid id, long size, bool isDirectory = false, long contentSize = 0, int childCount = 0)
        {
            Name = name ?? string.Empty;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Size = size;
            IsDirectory = isDirectory;
            ContentSize = contentSize;
            ChildCount = childCount;
        }

        public string Name { get; }

        public Cid Id { get; }

        public long Size { get; }

        public bool IsDirectory { get; }

        public long ContentSize { get; }

        public int ChildCount { get; }
    }

    internal sealed class ClientFileSystemNode : IFileSystemNode
    {
        private readonly IReadOnlyList<IFileSystemLink> links;
        private readonly byte[] dataBytes;

        public ClientFileSystemNode(Cid id, bool isDirectory, long size, IEnumerable<IFileSystemLink>? links = null, byte[]? dataBytes = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            IsDirectory = isDirectory;
            Size = size;
            this.links = new List<IFileSystemLink>(links ?? Array.Empty<IFileSystemLink>());
            this.dataBytes = dataBytes ?? Array.Empty<byte>();
        }

        public bool IsDirectory { get; }

        public IEnumerable<IFileSystemLink> Links => links;

        public byte[] DataBytes => dataBytes;

        public Stream DataStream => new MemoryStream(dataBytes, writable: false);

        public Cid Id { get; }

        public long Size { get; }

        public IFileSystemLink ToLink(string name = "")
        {
            return new ClientMerkleLink(name, Id, Size, IsDirectory, Size, links.Count);
        }
    }

    internal sealed class ClientPublishedMessage : IPublishedMessage
    {
        private readonly byte[] dataBytes;

        public ClientPublishedMessage(Peer sender, IEnumerable<string> topics, byte[] sequenceNumber, byte[] dataBytes)
        {
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));
            Topics = topics ?? Array.Empty<string>();
            SequenceNumber = sequenceNumber ?? Array.Empty<byte>();
            this.dataBytes = dataBytes ?? Array.Empty<byte>();
            Id = new Cid
            {
                ContentType = "raw",
                Hash = MultiHash.ComputeHash(this.dataBytes, MultiHash.DefaultAlgorithmName)
            };
        }

        public Peer Sender { get; }

        public IEnumerable<string> Topics { get; }

        public byte[] SequenceNumber { get; }

        public byte[] DataBytes => dataBytes;

        public Stream DataStream => new MemoryStream(dataBytes, writable: false);

        public Cid Id { get; }

        public long Size => dataBytes.LongLength;
    }

    internal sealed class ClientKey : IKey
    {
        public ClientKey(string name, MultiHash id)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public MultiHash Id { get; }

        public string Name { get; }
    }
}
