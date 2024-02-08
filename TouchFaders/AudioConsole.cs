﻿using Melanchall.DryWetMidi.Devices;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TouchFaders {
    class AudioConsole {

        public static State state;
        public enum State {
            DISCONNECTED, STARTING, SYNCING, RUNNING, STOPPING
        }

        static OutputDevice input;
        static InputDevice output;

        static TcpClient client;
        static NetworkStream outputStream;
        static TcpListener listener;
        static NetworkStream inputStream;
        static TcpClient consoleClient;

        /// <summary>
        /// Uses native SCP commands over a TCP connection for modern consoles
        /// </summary>
        /// <param name="host">String containing any valid interpretation of an IP address to parse</param>
        public static void Connect (string host) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;

            IPAddress address;
            if (!IPAddress.TryParse(host, out address)) {
                return;
            }
            IPEndPoint console = new IPEndPoint(address, TCP_Functions.RCP_PORT);
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
            // TODO: perform sync
            state = State.RUNNING;
        }

        public static void Send (string message) {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            outputStream.Write(buffer, 0, buffer.Length);
        }

        public static void Disconnect () {
            if (state != State.RUNNING) return;
            state = State.STOPPING;
            outputStream.Close();
            client.Close();
            state = State.DISCONNECTED;
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