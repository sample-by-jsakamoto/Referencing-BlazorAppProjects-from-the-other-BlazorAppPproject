using System.Net;
using System.Net.Sockets;

namespace BlazorMixApps.Test.Fixtures;

internal static class TcpNetwork
{
    public static int GetFreeTcpPortNumber()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
