using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using SharpOSC;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		OutputDevice DAW_in = OutputDevice.GetByName("DAW_in");
		InputDevice DAW_out = InputDevice.GetByName("DAW_out");

		public MainWindow () {
			InitializeComponent();
			InitializeIO();
		}

		protected override void OnClosed (EventArgs e) {
			DAW_in.Dispose();
			(DAW_out as IDisposable)?.Dispose();
			base.OnClosed(e);
		}

		public void InitializeIO () {
			DAW_in.EventSent += DAW_in_EventSent;
			DAW_out.EventReceived += DAW_out_EventReceived;
			DAW_out.StartEventsListening();
		}

		private void DAW_in_EventSent (object sender, MidiEventSentEventArgs e) {
			var DAW_in = (MidiDevice)sender;
			Console.WriteLine($"Event sent to '{DAW_in.Name}' as: {e.Event}");
		}

		private void DAW_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var DAW_out = (MidiDevice)sender;
			ControlChangeEvent midiEvent = (ControlChangeEvent)e.Event;
			Console.WriteLine($"Event received from '{DAW_out.Name}' as: {e.Event}");
			this.Dispatcher.Invoke(() => {
				testBar.Value = midiEvent.ControlValue;
				testSlider.ValueChanged -= Slider_ValueChanged; // Avoid feedback loop
				testSlider.Value = midiEvent.ControlValue; // Actually change the value
				testSlider.ValueChanged += Slider_ValueChanged; // Allow for value changes to update the value now
			});
		}

		private void TestOutput () {
			SevenBitNumber cc = new SevenBitNumber(0); // 0-127
			SevenBitNumber value = new SevenBitNumber(97); // 0dB is right between 96 and 97 in REAPER
			DAW_in.SendEvent(new ControlChangeEvent(cc, value));
		}

		private void TestMixerOutput () {
			using (var LS9_in = OutputDevice.GetByName("LS9")) {
				NormalSysExEvent sysExEvent = new NormalSysExEvent(); //			  Mix5		  Ch 1					  0 db  0 dB
				byte[] data = { 0xF0, 0x43, 0x10, 0x3E, 0x12, 0x01, 0x00, 0x43, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x37, 0xF7 };
				sysExEvent.Data = data;
			}
		}

		private void sendMIDI_Click (object sender, RoutedEventArgs e) {
			TestOutput();
		}

		private void Slider_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (sender == testSlider) {
				SevenBitNumber cc = new SevenBitNumber(0);
				SevenBitNumber value = new SevenBitNumber((byte)testSlider.Value);
				DAW_in.SendEvent(new ControlChangeEvent(cc, value));
			}
		}

		private void sendSysEx_Click (object sender, RoutedEventArgs e) {
			TestMixerOutput();
		}
	}
}
