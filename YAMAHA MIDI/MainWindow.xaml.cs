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

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public MainWindow () {
			InitializeComponent();
			InitializeIO();
		}

		protected override void OnClosed (EventArgs e) {

			base.OnClosed(e);
		}

		public void InitializeIO () {
			foreach (var outputDevice in OutputDevice.GetAll()) {
				Console.WriteLine(outputDevice.Name);
			}
			foreach (var inputDevice in InputDevice.GetAll()) {
				inputDevice.EventReceived += DAW_out_EventReceived;
				try {
					inputDevice.StartEventsListening();
				} catch {
					continue;
				}

			}/*
			using (var DAW_out = InputDevice.GetByName("DAW_out")) {
				DAW_out.EventReceived += DAW_out_EventReceived;
				DAW_out.StartEventsListening();
			}*/
		}

		private void DAW_out_EventReceived (object sender, MidiEventReceivedEventArgs e) {
			var DAW_out = (MidiDevice)sender;
			Console.WriteLine($"Event received from '{DAW_out.Name}' at {DateTime.Now}: {e.Event}");
		}

		private void TestOutput () {
			using (var DAW_in = OutputDevice.GetByName("DAW")) {
				SevenBitNumber cc = new SevenBitNumber(0); // 0-127
				SevenBitNumber value = new SevenBitNumber(97); // 0dB is right between 96 and 97 in REAPER
				DAW_in.SendEvent(new ControlChangeEvent(cc, value));
			}
		}

		private void sendMIDI_Click (object sender, RoutedEventArgs e) {
			TestOutput();
		}
	}
}
