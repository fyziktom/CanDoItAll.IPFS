namespace CanDoItAll.IPFS.NodeControl.Models;

public enum NodeConnectionRequestCategory
{
    ReadOnlyUi = 0,
    Gateway = 1,
    Mutation = 2,
    Admin = 3,
    RemotePin = 4
}
