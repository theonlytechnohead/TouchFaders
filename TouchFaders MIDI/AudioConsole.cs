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
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.MIDI;
        }
        public void Connect (IPAddress console) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.TCP;
        }
        public void Connect (SocketAddress console) {
            if (state != State.DISCONNECTED) return;
            state = State.STARTING;
            method = Method.SCP;
        }

        public void Sync () {
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

        public void Disconnect () {
            if (state != State.RUNNING) return;
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