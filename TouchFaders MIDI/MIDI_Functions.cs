using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace TouchFaders_MIDI {
    class MIDI_Functions {

		OutputDevice console_in;
		InputDevice console_out;

		public Queue<NormalSysExEvent> queueSysEx;
		Timer queueTimer;


		/// <summary>
		/// Checks if MIDI is ready
		/// <para>
		/// <c>true</c> if MIDI is running and sync'd, <c>false</c> otherwise
		/// </para>
		/// </summary>
		public bool IsMIDIRunning {
			get {
				bool running = false;
				Dispatcher.CurrentDispatcher.Invoke(() => {
					running = MainWindow.instance.midiProgressBar.Value >= MainWindow.instance.midiProgressBar.Maximum;
				});
				return running;
			}
		}

		~MIDI_Functions () {
			(console_in as IDisposable)?.Dispose();
			(console_out as IDisposable)?.Dispose();
		}

		/// <summary>
		/// Called after Create
		/// </summary>
		public EventHandler OnCreated;

		public void Create (string outputDevice, string inputDevice) {
			queueSysEx = new Queue<NormalSysExEvent>();

			try {
				console_in = OutputDevice.GetByName(outputDevice);
				console_out = InputDevice.GetByName(inputDevice);
			} catch (ArgumentException ex) {
				MessageBox.Show($"Can't initialize {outputDevice} and {inputDevice}!\n{ex.Message}");
				Console.WriteLine(ex.Message);
				return;
			}
			console_out.EventReceived += Receive;
			try {
				console_out.StartEventsListening();
			} catch (MidiDeviceException ex) {
				Console.WriteLine($"Couldn't start listening to {inputDevice}");
				Console.WriteLine(ex.Message);
				return;
			}

			OnCreated?.Invoke(this, new EventArgs());
		}

		/// <summary>
		/// Called after Start
		/// </summary>
		public EventHandler OnStarted;

		public void Start () {
			queueTimer = new Timer(dequeueSysEx, null, 0, 8);

			OnStarted?.Invoke(this, new EventArgs());
		}

		public void Receive (object sender, MidiEventReceivedEventArgs eventArgs) {
			if (eventArgs.Event.EventType != MidiEventType.NormalSysEx)
				return;
			byte[] bytes = (eventArgs.Event as NormalSysExEvent).Data;
			byte[] commandBytes = { bytes[4], bytes[5], bytes[6], bytes[7], bytes[8] };
			int channelIndex = bytes[9] << 7;
			channelIndex += bytes[10];
			byte[] dataBytes = { bytes[11], bytes[12], bytes[13], bytes[14], bytes[15] };
			int data = ConvertDataBytes(dataBytes);
			SysExCommand command = new SysExCommand(commandBytes);
			SysExCommand.CommandType commandType = (SysExCommand.CommandType)(-1);
			foreach (KeyValuePair<SysExCommand.CommandType, SysExCommand> pair in MainWindow.instance.config.MIXER.commands) {
				if (command.DataCategory == pair.Value.DataCategory) {
					if (command.Element == pair.Value.Element) {
						commandType = pair.Key;
						break;
					}
				}
			}
			switch (commandType) {
				case SysExCommand.CommandType.kInputOn:
					break;
				case SysExCommand.CommandType.kInputFader:
					break;
				case SysExCommand.CommandType.kInputToMix:
					int mix = (command.Index - 5) / 3;
					break;
				case SysExCommand.CommandType.kGroupID_Input:
					break;
				case SysExCommand.CommandType.kPatchInInput:
					break;
				case SysExCommand.CommandType.kNameInputChannel:
					break;
				case SysExCommand.CommandType.kIconInputChannel:
					break;
				case SysExCommand.CommandType.kChannelSelected:
					MainWindow.instance.selectedChannel.channelIndex = data;
					break;
				default:
					return;
			}
		}

		/// <summary>
		/// Called after Stop
		/// </summary>
		public EventHandler OnStopped;

		public void Stop () {

			OnStopped?.Invoke(this, new EventArgs());
		}

		/// <summary>
		/// Called after Destroy
		/// </summary>
		public EventHandler OnDestroyed;

		public void Destroy () {

			OnDestroyed?.Invoke(this, new EventArgs());
		}

		///This region is for handling SysEx events that have been received in Receive
		#region SysExHandlers

		#endregion

		/// This region is for sending SysEx events to request parameters from the console
		#region SysExRequests

		#endregion

		/// This region is for sending SysEx events to change parameters on the console
		#region SysExChanges

		#endregion

		/// <summary>
		/// Dequeues a SysEx event and sends it to the console
		/// <para>Also, increments the <c>midiProgressBar</c></para>
		/// </summary>
		/// <param name="state"></param>
		void dequeueSysEx (object state) {
			if (queueSysEx.Count > 0) {
				try {
					NormalSysExEvent sysExEvent = queueSysEx.Dequeue();
					if (sysExEvent != null) {
						console_in.SendEvent(sysExEvent);
						//Dispatcher.Invoke(() => midiProgressBar.Value += 1);
					}
				} catch (MidiDeviceException ex) {
					Console.WriteLine($"Well shucks, {console_in.Name} don't work no more...");
					Console.WriteLine(ex.Message);
					MessageBox.Show(ex.Message);
				} catch (ObjectDisposedException) {
					Console.WriteLine($"Tried to use {console_in.Name} without initializing MIDI!");
					MessageBox.Show("Initialize MIDI first!");
				} catch (NullReferenceException) {
					Console.WriteLine($"Tried to use 'null' as a MIDI device!");
					MessageBox.Show("Output is null!");
				}
			}
		}

		#region Data processing helpers
		public static int ConvertDataBytes (byte[] dataArray) {
			string raw_MIDI = string.Concat(dataArray.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))); // convert the byte array to a string of 0's and 1's
			string decoded_MIDI = "";
			for (int i = 0; i < raw_MIDI.Length; i++) {
				if (i % 8 != 0) { // if it's not the 8th bit in a byte
					decoded_MIDI += raw_MIDI[i]; // add it to the new string, reforming the original 8-bit encoding
				}
			}
			decoded_MIDI = decoded_MIDI.Substring(3); // skip the first nibble-ish, it's a leftover artifact of the 7b/8b encoding
			int output = 0;
			for (int i = 0; i < decoded_MIDI.Length; i += 8) {
				output += Convert.ToByte(decoded_MIDI.Substring(i, 8), 2) << i;
			}
			return output;
		}

		public static string kNameShortToString (byte[] kNameShort) {
			string raw_MIDI = string.Concat(kNameShort.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))); // convert the byte array to a string of 0's and 1's
			string decoded_MIDI = "";
			for (int i = 0; i < raw_MIDI.Length; i++) {
				if (i % 8 != 0) { // if it's not the 8th bit in a byte
					decoded_MIDI += raw_MIDI[i]; // add it to the new string, reforming the original 8-bit encoding
				}
			}
			decoded_MIDI = decoded_MIDI.Substring(3); // skip the first nibble-ish, it's a leftover artifact of the 7b/8b encoding
			List<byte> list = new List<byte>();
			for (int i = 0; i < decoded_MIDI.Length; i += 8) {
				list.Add(Convert.ToByte(decoded_MIDI.Substring(i, 8), 2)); // convert segments 8 bits to a byte, and put in a list
			}
			return Encoding.ASCII.GetString(list.ToArray());
		}
		#endregion

	}
}
