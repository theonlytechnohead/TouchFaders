using System;
using System.Linq;
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
        public void Connect (string host, Action started, Action<string> startFailed) {
            if (state != State.DISCONNECTED) {
                startFailed("Can't start right now, state is " + state);
                return;
            }

            Console.WriteLine("Connecting to " + host);

            IPAddress address;
            if (!IPAddress.TryParse(host, out address)) {
                startFailed("Address is invalid!");
                return;
            }
            IPEndPoint console = new IPEndPoint(address, 49280);
            client = new TcpClient {
                SendTimeout = 100
            };
            try {
                client.Connect(console);
                outputStream = client.GetStream();
            } catch (Exception) {
                startFailed("Couldn't connect to " + address);
                return;
            }

            state = State.STARTING;

            byte[] buffer = Encoding.UTF8.GetBytes("devstatus runmode\n");
            outputStream.Write(buffer, 0, buffer.Length);

            Send("devinfo productname");
            Send("devinfo deviceid"); // equivalent to UNIT ID
            Send("devinfo devicename");
            Send("scpmode encoding utf8");
            Send("scpmode keepalive 2000");

            // test
            Send("get MIXER:Current/InCh/Fader/Level 0 0");

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
            byte[] buffer = Encoding.UTF8.GetBytes(message + "\n");
            outputStream.Write(buffer, 0, buffer.Length);
        }

        public void Disconnect () {
            if (state != State.RUNNING) return;
            state = State.STOPPING;
            outputStream.Close();
            client.Close();
            state = State.DISCONNECTED;
        }

        void process (string messages) {
            foreach (var message in messages.Split('\n')) {
                if (message.Length == 0) continue;
                switch (message.Split(' ')[0]) {
                    case "OK":
                        Console.WriteLine(message);
                        if (message.Contains("MIXER:Current/InCh/Fader/Level")) {
                            Console.WriteLine("We found it!");
                            Console.WriteLine(message.Split(' ').Last());
                        }
                        break;
                    case "OKm":
                        break;
                    case "NOTIFY":
                        break;
                    case "ERROR":
                        break;
                    default:
                        Console.WriteLine(message);
                        break;
                }
            }
        }

    }
}