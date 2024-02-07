using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TouchFaders {
	class BroadcastUDPClient : UdpClient {

		public BroadcastUDPClient () : base() {
			//Calls the protected Client property belonging to the UdpClient base class.
			Socket s = this.Client;
			//Uses the Socket returned by Client to set an option that is not available using UdpClient.
			s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
		}

		public BroadcastUDPClient (IPEndPoint ipLocalEndpoint) : base(ipLocalEndpoint) {
			//Calls the protected Client property belonging to the UdpClient base class.
			Socket s = this.Client;
			//Uses the Socket returned by Client to set an option that is not available using UdpClient.
			s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
		}
	}
}
