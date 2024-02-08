using Rug.Osc;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace TouchFaders {
    public class oscDevice {

        public const string CONNECT = "connect";
        public const string DISCONNECT = "disconnect";

        public const string CHANNEL = "channel";
        public const string MIX = "mix";
        public const string NAME = "name";
        public const string PATCH = "patch";
        public const string MUTE = "mute";

        public const string COLOUR = "colour";

        public string deviceName;
        private int currentMix;

        private OscAddressManager osc;

        private OscReceiver input = null;
        private OscSender output = null;

        Thread listenThread;

        public oscDevice (string name, IPAddress address, int sendPort, int receivePort) {
            deviceName = name;

            osc = new OscAddressManager();

            input = new OscReceiver(receivePort);
            input.Connect();
            listenThread = new Thread(new ThreadStart(ListenLoop));
            output = new OscSender(address, sendPort);
            output.Connect();

            AttachPatterns();

            listenThread.Start();
        }

        public void Close () {
            input.Dispose();
            output.Dispose();
            listenThread.Join();
        }

        void ListenLoop () {
            try {
                while (input.State != OscSocketState.Closed) {
                    if (input.State == OscSocketState.Connected) {
                        bool received = input.TryReceive(out OscPacket packet);
                        if (received) {
                            switch (osc.ShouldInvoke(packet)) {
                                case OscPacketInvokeAction.Invoke:
                                    osc.Invoke(packet);
                                    break;
                                case OscPacketInvokeAction.DontInvoke:
                                    //Console.WriteLine($"Couldn't handle OSC: {packet}");
                                    break;
                                case OscPacketInvokeAction.HasError:
                                    //Console.WriteLine($"OSC packet has error: {packet.Error} {packet}");
                                    break;
                                case OscPacketInvokeAction.Pospone:
                                    //Console.WriteLine($"OSC packet was postponed: {packet}");
                                    break;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                if (input.State == OscSocketState.Connected) {
                    Console.WriteLine("Exception in listen loop (to follow):");
                    Console.WriteLine(ex.Message);
                }
            }
        }

        void AttachPatterns () {
            osc.Attach($"/{CONNECT}", new OscMessageEvent((OscMessage message) => {
                output.Send(new OscMessage($"/{CONNECT}/{CONNECT}", 1));
            }));
            osc.Attach($"/{DISCONNECT}", new OscMessageEvent((OscMessage message) => {
                // nothing to do
            }));
            osc.Attach($"/{MIX}[0-9]", new OscMessageEvent((OscMessage message) => {
                string mix = message.Address.Split('/')[1];
                currentMix = int.Parse(String.Join("", mix.Where(char.IsDigit)));
                Refresh();
            }));
            osc.Attach($"/{MIX}[0-9]/{CHANNEL}[0-9]", new OscMessageEvent((OscMessage message) => {
                string mix = message.Address.Split('/')[1];
                if (int.Parse(String.Join("", mix.Where(char.IsDigit))) == currentMix) {
                    int channel = int.Parse(String.Join("", message.Address.Split('/')[2].Where(char.IsDigit)));
                    int value = (int)message[0];
                    value = Math.Max(0, Math.Min(value, 1023));
                    MainWindow.instance.SendFaderValue(currentMix, channel, value, this);
                }
            }));
            osc.Attach($"/{MIX}[0-9]/{CHANNEL}[0-9]/{MUTE}", new OscMessageEvent((OscMessage message) => {
                string mix = message.Address.Split('/')[1];
                if (int.Parse(String.Join("", mix.Where(char.IsDigit))) == currentMix) {
                    int channel = int.Parse(String.Join("", message.Address.Split('/')[2].Where(char.IsDigit)));
                    bool muted = false;
                    if ((int)message[0] == 1) {
                        muted = true;
                    }
                    MainWindow.instance.SendChannelMute(currentMix, channel, muted, this);
                }
            }));
        }

        public void Refresh () {
            SendChannelStrips();
        }

        public void SendChannelStrips () {
            for (int channel = 1; channel <= MainWindow.instance.config.NUM_CHANNELS; channel++) {
                SendChannelStrip(channel);
                Thread.Sleep(3);
            }
        }

        public void SendChannelStrip (int channel) {
            int level = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].level;
            bool sendMuted = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].muted;
            string name = MainWindow.instance.data.channels[channel - 1].name;
            bool channelMuted = MainWindow.instance.data.channels[channel - 1].muted;
            string patch = "IN " + MainWindow.instance.data.channels[channel - 1].patch;
            int colourIndex = MainWindow.instance.data.channels[channel - 1].bgColourId;
            OscMessage message = new OscMessage($"/{MIX}{currentMix}/{CHANNEL}{channel}", level, sendMuted, name, channelMuted, patch, colourIndex);
            output.Send(message);
        }

        public void SendSendLevels () {
            for (int channel = 1; channel <= MainWindow.instance.config.NUM_CHANNELS; channel++) {
                int level = MainWindow.instance.data.channels[channel - 1].sends[currentMix - 1].level;
                sendOSCMessage(currentMix, channel, level);
                Thread.Sleep(3);
            }
        }

        public void SendChannelNames () {
            for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
                SendChannelName(channel, MainWindow.instance.data.channels[channel - 1].name);
                Thread.Sleep(3);
            }
        }

        public void SendChannelName (int channel, string name) {
            OscMessage message = new OscMessage($"/{NAME}{channel}", name);
            output.Send(message);
        }

        public void SendChannelPatches () {
            for (int patch = 1; patch <= MainWindow.instance.data.channels.Count; patch++) {
                SendChannelPatch(patch, patch);
                Thread.Sleep(3);
            }
        }

        public void SendChannelPatch (int channel, int patch) {
            string patchIn = "IN " + MainWindow.instance.data.channels[patch - 1].patch;
            OscMessage message = new OscMessage($"/{PATCH}{channel}", patchIn);
            output.Send(message);
        }

        public void SendSendMutes () {
            for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
                SendSendMute(currentMix, channel);
            }
        }

        public void SendSendMute (int mix, int channel) {
            bool muted = MainWindow.instance.data.channels[channel - 1].sends[mix - 1].muted;
            SendSendMute(mix, channel, muted);
        }

        public void SendSendMute (int mix, int channel, bool muted) {
            OscMessage message = new OscMessage($"/{MIX}{mix}/{CHANNEL}{channel}/{MUTE}", muted ? 1 : 0);
            output.Send(message);
        }

        public void SendChannelMutes () {
            for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
                bool muted = MainWindow.instance.data.channels[channel - 1].muted;
                SendChannelMute(channel, muted);
            }
        }

        public void SendChannelMute (int channel, bool muted) {
            OscMessage message = new OscMessage($"/{CHANNEL}{channel}/{MUTE}", muted);
            output.Send(message);
        }

        public void SendChannelColours () {
            for (int channel = 1; channel <= MainWindow.instance.data.channels.Count; channel++) {
                int colourIndex = MainWindow.instance.data.channels[channel - 1].bgColourId;
                SendChannelColour(channel, colourIndex);
            }
        }

        public void SendChannelColour (int channel, int colourIndex) {
            OscMessage message = new OscMessage($"/{CHANNEL}{channel}/{COLOUR}", colourIndex);
            output.Send(message);
        }

        public void SendDisconnect () {
            OscMessage message = new OscMessage($"/{DISCONNECT}");
            output.Send(message);
        }

        public void sendOSCMessage (int mix, int channel, int value) {
            //Console.WriteLine($"Sending OSC: /mix{mix}/fader{channel} {value}");
            OscMessage message = new OscMessage($"/{MIX}{mix}/{CHANNEL}{channel}", value);
            output.Send(message);
        }
    }
}
