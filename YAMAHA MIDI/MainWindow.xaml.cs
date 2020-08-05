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
using Sanford.Multimedia.Midi;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		InputDevice LS9_in;
		OutputDevice LS9_out;

		InputDevice DAW_in;
		OutputDevice DAW_out;

		public MainWindow () {
			InitializeComponent();
			InitializeIO();
			TestOutput();
		}

		public void InitializeIO () {
			LS9_in = new InputDevice(0); // Make sure to get and set properly as defined by GUI later
			LS9_in.SysExMessageReceived += HandleSysExMessage;
			LS9_out = new OutputDevice(0); // As above

			DAW_in = new InputDevice(1); // Ditto
			DAW_in.ChannelMessageReceived += HandleChannelMessage;
			DAW_out = new OutputDevice(1); // ''
		}

		private void HandleChannelMessage (object sender, ChannelMessageEventArgs e) {
			ChannelMessage channelMessage = e.Message;

			// Convert the channel and value and send the correct SysEx command to desk
		}

		private void HandleSysExMessage (object sender, SysExMessageEventArgs e) {
			SysExMessage sysExMessage = e.Message;
			// Now respond if the message matches the right message format for the CH 1-16 send level for MIX 1-6 to DAW_out
		}

		public void TestOutput () {
			ChannelMessageBuilder builder = new ChannelMessageBuilder();

			builder.Command = ChannelCommand.NoteOn;
			builder.MidiChannel = 0;
			builder.Data1 = 60;
			builder.Data2 = 127;
			builder.Build();

			LS9_out.Send(builder.Result);

			Thread.Sleep(1000);

			builder.Command = ChannelCommand.NoteOff;
			builder.Data2 = 0;
			builder.Build();

			LS9_out.Send(builder.Result);


			ChannelMessageBuilder ch1mix10db = new ChannelMessageBuilder();
			ch1mix10db.Command = ChannelCommand.Controller; // Control change?
			ch1mix10db.MidiChannel = 0;
			ch1mix10db.Data1 = 1; // idk about this one really, I think maybe the CC number?
			ch1mix10db.Data1 = 127; // Value, a.k.a. volume I think
			ch1mix10db.Build();
			DAW_out.Send(ch1mix10db.Result);
		}
	}
}
