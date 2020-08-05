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

		public MainWindow () {
			InitializeComponent();
			InitializeIO();
			TestOutput();
		}

		public void InitializeIO () {
			LS9_in = new InputDevice(0);
			LS9_out = new OutputDevice(0);
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


		}
	}
}
