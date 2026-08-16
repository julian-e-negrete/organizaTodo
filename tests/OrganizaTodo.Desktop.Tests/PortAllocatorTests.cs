using System.Net;
using System.Net.Sockets;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PortAllocatorTests
{
    [Fact]
    public void FindAvailablePort_WithPreferredPortFree_ReturnsIt()
    {
        var preferred = PortAllocator.FindAvailablePort();

        var result = PortAllocator.FindAvailablePort(preferred);

        Assert.Equal(preferred, result);
    }

    [Fact]
    public void FindAvailablePort_WithPreferredPortTaken_ReturnsDifferentAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var takenPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = PortAllocator.FindAvailablePort(takenPort);

        Assert.NotEqual(takenPort, result);
        Assert.True(PortAllocator.IsPortAvailable(result));
    }
}
