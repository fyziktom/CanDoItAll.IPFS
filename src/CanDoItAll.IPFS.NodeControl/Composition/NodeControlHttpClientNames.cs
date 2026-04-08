namespace CanDoItAll.IPFS.NodeControl.Composition;

public static class NodeControlHttpClientNames
{
    public const string NodeRead = "IpfsNodeControl.Node.Read";
    public const string NodeGateway = "IpfsNodeControl.Node.Gateway";
    public const string NodeMutation = "IpfsNodeControl.Node.Mutation";
    public const string NodeAdmin = "IpfsNodeControl.Node.Admin";
    public const string NodeRemotePin = "IpfsNodeControl.Node.RemotePin";

    public const string RemotePinProbe = "IpfsNodeControl.RemotePin.Probe";
    public const string RemotePinProbeInsecure = "IpfsNodeControl.RemotePin.Probe.Insecure";
    public const string RemotePinSend = "IpfsNodeControl.RemotePin.Send";
    public const string RemotePinSendInsecure = "IpfsNodeControl.RemotePin.Send.Insecure";
}
