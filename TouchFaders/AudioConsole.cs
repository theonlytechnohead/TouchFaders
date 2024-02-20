using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TouchFaders {
    class AudioConsole : IConsole {

        public static State state;
        public enum State {
            DISCONNECTED, STARTING, SYNCING, RUNNING, STOPPING
        }


        static TcpClient client;
        static NetworkStream outputStream;
        static TcpListener listener;
        static NetworkStream inputStream;
        static TcpClient consoleClient;

        /// <summary>
        /// Uses native SCP commands over a TCP connection for modern consoles
        /// </summary>
        /// <param name="host">String containing any valid interpretation of an IP address to parse</param>
        public void Connect (string host) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;

            IPAddress address;
            if (!IPAddress.TryParse(host, out address)) {
                return;
            }
            IPEndPoint console = new IPEndPoint(address, 49280);
            client = new TcpClient();
            client.Connect(console);
            outputStream = client.GetStream();

            byte[] buffer = Encoding.UTF8.GetBytes("devstatus runmode\n");
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("devinfo productname\n");
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("devinfo deviceid\n"); // equivalent to UNIT ID
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("devinfo devicename\n");
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("scpmode encoding utf8\n");
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("scpmode keepalive 2000\n");
            outputStream.Write(buffer, 0, buffer.Length);

            Task.Run(() => {
                while (state != State.DISCONNECTED || state != State.STOPPING) {
                    byte[] buffer = new byte[client.ReceiveBufferSize];
                    int bytes = outputStream.Read(buffer, 0, buffer.Length);

                    string message = Encoding.UTF8.GetString(buffer, 0, bytes);
                    process(message);
                }
            });
        }

        public void Sync () {
            if (state != State.RUNNING) return;
            state = State.SYNCING;
            // TODO: perform sync
            state = State.RUNNING;
        }

        public void Send (string message) {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            outputStream.Write(buffer, 0, buffer.Length);
        }

        public void Disconnect () {
            if (state != State.RUNNING) return;
            state = State.STOPPING;
            outputStream.Close();
            client.Close();
            state = State.DISCONNECTED;
        }

        void process (string message) {
            string[] messages = message.Split('\n');
            foreach (var m in messages) {
                if (m.Length == 0) continue;
                System.Console.WriteLine(m);
            }
        }

    }
}