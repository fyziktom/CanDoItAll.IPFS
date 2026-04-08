using CanDoItAll.IPFS.NodeControl.Models;

namespace CanDoItAll.IPFS.NodeControl.Abstractions;

public interface IServerNodeSettingsStore
{
    void Clear();

    NodeConnectionSettings? Load();

    void Save(NodeConnectionSettings settings);
}
