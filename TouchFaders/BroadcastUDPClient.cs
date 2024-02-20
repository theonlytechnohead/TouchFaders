using System.Net;
using System.Net.Sockets;

namespace TouchFaders {
    class BroadcastUDPClient : UdpClient {

        public BroadcastUDPClient () : base() {
            // Calls the protected Client property belonging to the UdpClient base class.
            Socket socket = this.Client;
            socket.EnableBroadcast = true;
            // Uses the Socket returned by Client to set an option that is not available using UdpClient.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
        }

        public BroadcastUDPClient (IPEndPoint ipLocalEndpoint) : base(ipLocalEndpoint) {
            // Calls the protected Client property belonging to the UdpClient base class.
            Socket socket = this.Client;
            socket.EnableBroadcast = true;
            // Uses the Socket returned by Client to set an option that is not available using UdpClient.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
        }
    }
}
