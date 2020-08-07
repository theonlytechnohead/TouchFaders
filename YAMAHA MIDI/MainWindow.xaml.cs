using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Windows;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using SharpOSC;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		UDPSender oscIn = new UDPSender("127.0.0.1", 55555);
		UDPListener oscOut;

		OutputDevice LS9_in = OutputDevice.GetByName("LS9");
		InputDevice LS9_out = InputDevice.GetByName("LS9");

		Timer activeSensingTimer;

		public MainWindow () {
			InitializeComponent();
			InitializeIO();
			oscOut = new UDPListener(55554, parseOSCMessage);
		}

		#region setupMIDI
		public void InitializeIO () {
			LS9_in.EventSent += LS9_in_EventSent;
			LS9_out.EventReceived += LS9_out_EventReceived;
			try {
				LS9_out.StartEventsListening();
			} catch (MidiDeviceException e) {
				Console.WriteLine("Couldn't start listening to LS9");
			}
			if (LS9_out.IsListeningForEvents) {
				ResetEvent systemReset = new ResetEvent();
				try {
					LS9_in.SendEvent(systemReset);
				} catch (MidiDeviceException e) {
					Console.WriteLine("Couldn't send system reset MIDI event to LS9");
				}
				activeSensingTimer = new Timer(sendActiveSense, null, 0, 350);
			}
		}

		void sendActiveSense (object state) {
			ActiveSensingEvent activeSense = new ActiveSensingEvent();
			try {
				LS9_in.SendEvent(activeSense); // MIDI should now be initialized with the desk
			} catch (MidiDeviceException e) {
				Console.WriteLine("Couldn't send active sensing MIDI event to LS9");
			}
		}
		#endregion

		#region SysExMIDIHelpers
		bool CheckSysEx (byte[] bytes) {
			if (bytes.Length == 18) {
				return false;
			}
			byte sysExStart = bytes[0];     // Signals start of SysEx, 0xF0
			byte manufacturerID = bytes[1]; // YAMAHA is 0x43
			byte deviceNumber = bytes[2];   // device number is 0x1n where n is 0-15
			byte groupID = bytes[3];        // Digital mixer is 0x3E
			byte modelID = bytes[4];        // LS9 is 0x12
			byte dataCategory = bytes[5];   // kInputToMix is in 0x01
			byte elementMSB = bytes[6];     // kInputToMix has MSB 0x00
			byte elementLSB = bytes[7];     // kInputToMix has LSB 0x43
			byte indexMSB = bytes[8];       // index MSB is for the Mix ...
			byte indexLSB = bytes[9];       // ... as on the desk, MIX 0-5
			byte channelMSB = bytes[10];    // Channel MSB per channel
			byte channelLSB = bytes[11];    // Channel LSB with a 0 in the 8th bit
			byte data5 = bytes[12];         // Data bytes start
			byte data4 = bytes[13];         // ''
			byte data3 = bytes[14];         // ''
			byte data2 = bytes[15];         // ''
			byte data1 = bytes[16];         // ''
			byte sysExEnd = bytes[17];      // End of SysEx message, 0xF7

			if (manufacturerID == 0x43 &&
				deviceNumber == 0x10 &&
				groupID == 0x3E &&
				modelID == 0x12 &&
				dataCategory == 0x01 &&
				elementMSB == 0x00 &&
				elementLSB == 0x43) {
				int index = indexMSB << 7;
				index += indexLSB;
				if (0x05 <= index && index <= 0x14) {
					return true;
				}
			}
			return false;
		}

		(int, int, int) ConvertByteArray (byte[] bytes) {
			byte mixMSB = bytes[8];
			byte mixLSB = bytes[9];
			int mix = mixMSB << 7;
			mix += mixLSB;

			byte channelMSB = bytes[10];
			byte channelLSB = bytes[11];
			int channel = channelMSB << 7;
			channel += channelLSB;

			byte valueMSB = bytes[15];
			byte valueLSB = bytes[16];
			int value = valueMSB << 7;
			value += valueLSB;

			return (mix, channel, value);
		}
		#endregion

		#region MIDI
		void LS9_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var LS9_out = (MidiDevice)sender;
			SysExEvent midiEvent = (SysExEvent)e.Event;
			Console.WriteLine($"Event received from '{LS9_out.Name}' as: {e.Event}");
			if (CheckSysEx(midiEvent.Data)) {
				(int mix, int channel, int value) = ConvertByteArray(midiEvent.Data);
				sendOSCMessage(16 * mix + channel, value);
				/*this.Dispatcher.Invoke(() => {
					testBar.Value = value;
					testSlider.ValueChanged -= testSlider_ValueChanged; // Avoid feedback loop
					testSlider.Value = value; // Actually change the value
					testSlider.ValueChanged += testSlider_ValueChanged; // Allow for value changes to update the value now
				});*/
			}
		}

		void TestMixerOutput () {
			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //			  Mix1		  Ch 1					  0 db  0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x37, 0xF7 };
			sysExEvent.Data = data;
			try {
				LS9_in.SendEvent(sysExEvent);
			} catch (MidiDeviceException ex) {
				Console.WriteLine("Well shucks, LS9_in don't work no more...");
				Console.WriteLine(ex.Message);
			}
		}

		void SendSysEx (int mix, int channel, float value) {
			byte mixLSB = (byte)(mix);
			byte mixMSB = (byte)(mix >> 8);

			byte channelLSB = (byte)(channel);
			byte channelMSB = (byte)(channel >> 8);

			int value_int = Convert.ToInt32(value * 16383);
			byte valueMSB = (byte)(value_int);
			byte valueLSB = (byte)(value_int >> 8);

			NormalSysExEvent sysExEvent = new NormalSysExEvent(); //				Mix					Ch							  0 db		0 dB
			byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, mixMSB, mixLSB, channelMSB, channelLSB, 0x00, 0x00, 0x00, valueMSB, valueLSB, 0xF7 };
			sysExEvent.Data = data;

			try {
				LS9_in.SendEvent(sysExEvent);
			} catch (MidiDeviceException ex) {
				Console.WriteLine("Well shucks, LS9_in don't work no more...");
				Console.WriteLine(ex.Message);
			}
		}

		void LS9_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var LS9_in = (MidiDevice)sender;
			Console.WriteLine($"Event sent to '{LS9_in.Name}' as: {e.Event}");
		}
		#endregion

		#region OSC
		void parseOSCMessage (OscPacket packet) {
			if (packet is OscBundle) {
				OscBundle messageBundle = (OscBundle)packet;
				foreach (OscMessage message in messageBundle.Messages) {
					handleOSCMessage(message);
				}
			} else {
				OscMessage message = (OscMessage)packet;
				handleOSCMessage(message);
			}
		}

		void handleOSCMessage (OscMessage message) {
			Console.WriteLine($"Received a message: {message.Address} {message.Arguments[0]}");
			if (message.Address.Contains("/iem/fader")) {
				int fader = int.Parse(String.Join("", message.Address.Where(char.IsDigit)));
				int channel = fader % 16;
				int mix = (fader - channel) / 16;
				float value = (float)message.Arguments[0];
				SendSysEx(mix, channel, value);
				if (fader == 1) {
					this.Dispatcher.Invoke(() => {
						testBar.Value = value * 100f;
						testSlider.ValueChanged -= testSlider_ValueChanged;
						testSlider.Value = value * 100f;
						testSlider.ValueChanged += testSlider_ValueChanged;
					});
				}
			} else if (message.Address.Contains("/iem/db")) {
				int fader = int.Parse(String.Join("", message.Address.Where(char.IsDigit)));
				int channel = fader % 16;
				int mix = (fader - channel) / 16;
				string value = (string)message.Arguments[0];
				this.Dispatcher.Invoke(() => {
					dbLabel.Content = value;
				});
			}
		}

		void sendOSCMessage (int channel, int value) {
			float faderPos = value / 16383; // convert from 14-bt to 0-1 float
			OscMessage message = new OscMessage($"/iem/fader{channel}", faderPos);
			oscIn.Send(message);
		}
		#endregion

		#region UIEvents
		private void initializeIOButton_Click (object sender, RoutedEventArgs e) {
			InitializeIO();
		}

		private void disposeSensingTimerButton_Click (object sender, RoutedEventArgs e) {
			(activeSensingTimer as IDisposable)?.Dispose();
			Console.WriteLine("Disposed active sensing timer");
		}

		void testSlider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			OscMessage message = new OscMessage("/iem/fader1", (float)(testSlider.Value / 100f));
			oscIn.Send(message);
		}

		void sendOSC_Click (object sender, RoutedEventArgs e) {
			OscMessage message = new OscMessage("/iem/fader1", 0.76f); // 0dB is approx. 0.76f in range 0-100f for REAPER as configured
			oscIn.Send(message);
		}

		void sendSysEx_Click (object sender, RoutedEventArgs e) {
			TestMixerOutput();
		}
		#endregion

		protected override void OnClosed (EventArgs e) {
			(activeSensingTimer as IDisposable)?.Dispose();
			(LS9_in as IDisposable)?.Dispose();
			(LS9_out as IDisposable)?.Dispose();
			base.OnClosed(e);
		}
	}
}
