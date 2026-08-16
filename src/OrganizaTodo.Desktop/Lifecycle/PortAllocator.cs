using System.Net;
using System.Net.Sockets;

namespace OrganizaTodo.Desktop.Lifecycle;

public static class PortAllocator
{
    public static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static int FindAvailablePort(int preferredPort) =>
        IsPortAvailable(preferredPort) ? preferredPort : FindAvailablePort();

    public static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
