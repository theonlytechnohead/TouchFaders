using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TouchFaders_MIDI {
    class AudioConsole {

        public static State state;
        public enum State {
            DISCONNECTED, STARTING, SYNCING, RUNNING, STOPPING
        }

        public static Method method;
        public enum Method {
            MIDI, TCP, SCP
        }

        static OutputDevice input;
        static InputDevice output;

        static TcpClient client;
        static NetworkStream outputStream;
        static TcpListener listener;
        static NetworkStream inputStream;
        static TcpClient consoleClient;

        public static void Connect (OutputDevice consoleIn, InputDevice consoleOut) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.MIDI;

            input = consoleIn;
            output = consoleOut;

            output.EventReceived += delegate (object sender, MidiEventReceivedEventArgs args) {
                process(args.Event);
            };
        }

        public static void Connect (IPAddress console) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.TCP;

            listener = new TcpListener(IPAddress.Any, 12300);
            listener.Start();
            // TODO: refactor and check this whole protocol
            Task.Run(() => {
                while (true) {
                    if (consoleClient == null || !consoleClient.Connected) {
                        consoleClient = listener.AcceptTcpClient();
                        inputStream = consoleClient.GetStream();

                        byte[] buffer = new byte[consoleClient.ReceiveBufferSize];
                        int bytesRead = inputStream.Read(buffer, 0, consoleClient.ReceiveBufferSize);

                        byte[] ackMessage = { 0x00, 0x00, 0x00, 0x10, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff };
                        inputStream.Write(ackMessage, 0, ackMessage.Length);

                        process(buffer);
                    }
                }
            });

            IPEndPoint endpoint = new IPEndPoint(console, 12300);
            client = new TcpClient(endpoint);
            outputStream = client.GetStream();

            byte[] initDataA = new byte[] { 0x00, 0x00, 0x00, 0x10, 0x20, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x03 };
            outputStream.Write(initDataA, 0, initDataA.Length);
            byte[] receiveBufferA = new byte[client.ReceiveBufferSize];
            int receivedA = outputStream.Read(receiveBufferA, 0, receiveBufferA.Length);

            byte[] initDataB = new byte[] { 0x00, 0x00, 0x00, 0x10, 0x23, 0x00, 0x00, 0x00, 0x19, 0xe7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            outputStream.Write(initDataB, 0, initDataB.Length);
            byte[] receiveBufferB = new byte[client.ReceiveBufferSize];
            int receivedB = outputStream.Read(receiveBufferB, 0, receiveBufferB.Length);
        }
        public static void Connect (string host) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.SCP;

            IPEndPoint console = new IPEndPoint(IPAddress.Parse(host), 49280);
            client = new TcpClient();
            client.Connect(console);
            outputStream = client.GetStream();

            byte[] buffer = Encoding.UTF8.GetBytes("devinfo productname\n");
            outputStream.Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes("scpmode sstype \"text\"\n");
            outputStream.Write(buffer, 0, buffer.Length);

            Task.Run(() => {
                while (true) {
                    byte[] buffer = new byte[client.ReceiveBufferSize];
                    int bytes = outputStream.Read(buffer, 0, buffer.Length);

                    string message = Encoding.UTF8.GetString(buffer, 0, bytes);
                    process(message);
                }
            });
        }

        public static void Sync () {
            if (state != State.RUNNING) return;
            state = State.SYNCING;
            switch (method) {
                case Method.MIDI:
                    break;
                case Method.TCP:
                    break;
                case Method.SCP:
                    break;
            }
            state = State.RUNNING;
        }

        public static void Send (MidiEvent midi) {
            input.SendEvent(midi);
        }
        public static void Send (byte[] bytes) {
            outputStream.Write(bytes, 0, bytes.Length);
        }
        public static void Send (string message) {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            outputStream.Write(buffer, 0, buffer.Length);
        }

        public static void Disconnect () {
            if (state != State.RUNNING) return;
            state = State.STOPPING;
            switch (method) {
                case Method.MIDI:
                    byte[] closeDataZ = new byte[] { 0x00, 0x00, 0x00, 0x10, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff };
                    outputStream.Write(closeDataZ, 0, closeDataZ.Length);
                    input.Dispose();
                    output.Dispose();
                    break;
                case Method.TCP:
                    outputStream.Close();
                    client.Close();
                    inputStream.Close();
                    consoleClient.Close();
                    listener.Stop();
                    break;
                case Method.SCP:
                    outputStream.Close();
                    client.Close();
                    break;
            }
            state = State.DISCONNECTED;
        }

        static void process (MidiEvent midi) {

        }
        static void process (byte[] bytes) {

        }
        static void process (string message) {
            string[] messages = message.Split('\n');
            foreach (var m in messages) {
                if (m.Length == 0) continue;
                if (m.Contains("OK devinfo productname")) {
                    System.Console.WriteLine($"Found a thing! {m}");
                }
            }
        }

    }
}