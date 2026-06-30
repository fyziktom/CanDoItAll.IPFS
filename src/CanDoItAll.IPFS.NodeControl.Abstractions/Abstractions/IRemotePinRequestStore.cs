using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface IRemotePinRequestStore
{
    event Action<StoredRemotePinRequest>? RequestChanged;

    StoredRemotePinRequest Add(RemotePinRequestEnvelope request);

    StoredRemotePinRequest? Get(string requestId);

    IReadOnlyList<StoredRemotePinRequest> List();

    StoredRemotePinRequest Update(string requestId, Action<StoredRemotePinRequest> update);
}
