using Melanchall.DryWetMidi.Devices;
using System.Net;

namespace TouchFaders_MIDI {
    class AudioConsole {

        public State state;
        public enum State {
            DISCONNECTED, STARTING, SYNCING, RUNNING, STOPPING
        }

        public Method method;
        public enum Method {
            MIDI, TCP, SCP
        }

        public void Connect (OutputDevice consoleIn, InputDevice consoleOut) {
            state = State.STARTING;
            method = Method.MIDI;
        }
        public void Connect (IPAddress console) {
            state = State.STARTING;
            method = Method.TCP;
        }
        public void Connect (SocketAddress console) {
            state = State.STARTING;
            method = Method.SCP;
        }

        public void Sync () {
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

        public void Disconnect () {
            state = State.STOPPING;
            switch (method) {
                case Method.MIDI:
                    break;
                case Method.TCP:
                    break;
                case Method.SCP:
                    break;
            }
            state = State.DISCONNECTED;
        }

    }
}