using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace TouchFaders {
    class DataStructures {

        public static List<SolidColorBrush> bgColours = new List<SolidColorBrush>() {
            new SolidColorBrush(Color.FromRgb(1, 1, 253)),      // blue, default
			new SolidColorBrush(Color.FromRgb(255, 102, 1)),    // orange
			new SolidColorBrush(Color.FromRgb(153, 102, 1)),    // brown (gold)
			new SolidColorBrush(Color.FromRgb(102, 1, 153)),    // purple
			new SolidColorBrush(Color.FromRgb(1, 153, 255)),    // cyan
			new SolidColorBrush(Color.FromRgb(255, 102, 153)),  // pink
			new SolidColorBrush(Color.FromRgb(102, 1, 1)),      // bergundy (brown)
			new SolidColorBrush(Color.FromRgb(1, 102, 51))      // green
		};

        public static ObservableCollection<string> bgColourNames = new ObservableCollection<string>() {
            "blue",
            "orange",
            "gold",
            "purple",
            "cyan",
            "pink",
            "brown",
            "green"
        };

        public static Dictionary<string, SolidColorBrush> bgColourMap = Enumerable.Range(0, bgColourNames.Count).ToDictionary(i => bgColourNames[i], i => bgColours[i]);

        public static ObservableCollection<char> ChannelGroupChars = new ObservableCollection<char>() {
            ' ',
            'A',
            'B',
            'C',
            'D',
            'E',
            'F',
            'G',
            'H',
            'I',
            'J',
            'K',
            'L',
            'M',
            'N',
            'O',
            'P',
            'Q',
            'R',
            'S',
            'T',
            'U',
            'V',
            'W',
            'X',
            'Y',
            'Z',
            'a',
            'b',
            'c',
            'd',
            'e',
            'f',
            'g',
            'h'
        };
    }

    public class Data {
        public static event EventHandler sendLevelChanged;
        public static event EventHandler sendMuteChanged;
        public static event EventHandler channelNameChanged;
        public static event EventHandler channelLevelChanged;
        public static event EventHandler channelMuteChanged;
        public static event EventHandler channelPatchChanged;
        public static event EventHandler channelColourChanged;
        public static event EventHandler mixNameChanged;
        public static event EventHandler mixLevelChanged;
        public static event EventHandler mixMuteChanged;

        public List<Channel> channels { get; set; }
        public List<Mix> mixes { get; set; }

        public Data () {
            channels = (from channel in Enumerable.Range(1, MainWindow.instance.config.NUM_CHANNELS) select new Channel(channel)).ToList();
            mixes = (from mix in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select new Mix(mix)).ToList();
        }

        public class Channel {
            private string label;
            private int fader;
            private bool mute;
            private int port;
            private int colourIndex;

            public class NameArgs : EventArgs {
                public int channel;
                public string name;
            }

            public class LevelArgs : EventArgs {
                public int channel;
                public int level;
                public char linkGroup;
            }

            public class MuteArgs : EventArgs {
                public int channel;
                public bool muted;
            }

            public class PatchArgs : EventArgs {
                public int channel;
                public int patch;
            }

            public class ColourArgs : EventArgs {
                public int channel;
                public int bgColourId;
            }

            public int channel { get; set; }
            public string name { get => label; set { label = value; channelNameChanged?.Invoke(this, new NameArgs() { channel = channel, name = name }); } }
            public int level { get => fader; set { fader = value; channelLevelChanged?.Invoke(this, new LevelArgs() { channel = channel, level = level, linkGroup = linkGroup }); } }
            public bool muted { get => mute; set { mute = value; channelMuteChanged?.Invoke(this, new MuteArgs() { channel = channel, muted = muted }); } }
            public int patch { get => port; set { port = value; channelPatchChanged?.Invoke(this, new PatchArgs() { channel = channel, patch = patch }); } }
            public int bgColourId { get => colourIndex; set { colourIndex = value; channelColourChanged?.Invoke(this, new ColourArgs() { channel = channel, bgColourId = bgColourId }); } }
            public char linkGroup { get; set; }

            public List<Send> sends { get; set; }

            public Channel () { }

            public Channel (int channel) {
                this.channel = channel;
                name = $"ch{(channel < 10 ? " " : "")}{channel}";
                // Initialised to 0dB
                level = 823;
                // Initialised to unmuted (ON)
                muted = false;
                bgColourId = 0;
                linkGroup = ' ';
                patch = channel;
                sends = (from send in Enumerable.Range(1, MainWindow.instance.config.NUM_MIXES) select new Send()).ToList();
            }
        }

        public class SelectedChannel {
            private Channel currentChannel { get; set; }

            public int channelIndex { get; set; }
            public string name {
                get => currentChannel.name;
                set => currentChannel.name = value;
            }

            public int iconID { get; set; }
            public static List<Uri> iconURIs = new List<Uri>() {
                new Uri("Resources/00_kick.png", UriKind.Relative),
                new Uri("Resources/01_snare.png", UriKind.Relative),
                new Uri("Resources/02_hi-hat.png", UriKind.Relative),
                new Uri("Resources/03_tom.png", UriKind.Relative),
                new Uri("Resources/04_drumkit.png", UriKind.Relative),
                new Uri("Resources/05_perc.png", UriKind.Relative),
                new Uri("Resources/06_a.bass.png", UriKind.Relative),
                new Uri("Resources/07_strings.png", UriKind.Relative),
                new Uri("Resources/08_e.bass.png", UriKind.Relative),
                new Uri("Resources/09_a.guitar.png", UriKind.Relative),
                new Uri("Resources/10_e.guitar.png", UriKind.Relative),
                new Uri("Resources/11_bassamp.png", UriKind.Relative),
                new Uri("Resources/12_guitaramp.png", UriKind.Relative),
                new Uri("Resources/13_trumpet.png", UriKind.Relative),
                new Uri("Resources/14_trombone.png", UriKind.Relative),
                new Uri("Resources/15_saxophone.png", UriKind.Relative),
                new Uri("Resources/16_piano.png", UriKind.Relative),
                new Uri("Resources/17_organ.png", UriKind.Relative),
                new Uri("Resources/18_keyboard.png", UriKind.Relative),
                new Uri("Resources/19_male.png", UriKind.Relative),
                new Uri("Resources/20_female.png", UriKind.Relative),
                new Uri("Resources/21_choir.png", UriKind.Relative),
                new Uri("Resources/22_dynamic.png", UriKind.Relative),
                new Uri("Resources/23_condenser.png", UriKind.Relative),
                new Uri("Resources/24_wireless.png", UriKind.Relative),
                new Uri("Resources/25_podium.png", UriKind.Relative),
                new Uri("Resources/26_wedge.png", UriKind.Relative),
                new Uri("Resources/27_2way.png", UriKind.Relative),
                new Uri("Resources/28_in-ear.png", UriKind.Relative),
                new Uri("Resources/29_effector.png", UriKind.Relative),
                new Uri("Resources/30_media1.png", UriKind.Relative),
                new Uri("Resources/31_media2.png", UriKind.Relative),
                new Uri("Resources/32_vtr.png", UriKind.Relative),
                new Uri("Resources/33_mixer.png", UriKind.Relative),
                new Uri("Resources/34_pc.png", UriKind.Relative),
                new Uri("Resources/35_processor.png", UriKind.Relative),
                new Uri("Resources/36_audience.png", UriKind.Relative),
                new Uri("Resources/37_star1.png", UriKind.Relative),
                new Uri("Resources/38_star2.png", UriKind.Relative),
                new Uri("Resources/39_blank.png", UriKind.Relative)
            };
            public int bgColourID { get; set; }

            public int level { get { return currentChannel.level; } set { currentChannel.level = value; } }

            public SelectedChannel () {
                currentChannel = new Channel();
                channelIndex = 0;
                name = "ch 1";
                level = 823;
                iconID = 22;
                bgColourID = 0;
            }
        }

        public class Send {
            private int fader;
            private bool mute;

            public int level { get => fader; set { fader = value; sendLevelChanged?.Invoke(this, new EventArgs()); } }
            public bool muted { get => mute; set { mute = value; sendMuteChanged?.Invoke(this, new EventArgs()); } }

            public Send () {
                // Initalised to -10dB
                level = 623;
                // Initialised to unmuted (ON)
                muted = false;
            }
        }

        public class Mix {
            private string label;
            private int fader;
            private bool mute;

            public class NameArgs : EventArgs {
                public int mix;
                public string name;
            }

            public int mix { get; set; }
            public string name { get => label; set { label = value; mixNameChanged?.Invoke(this, new EventArgs()); } }
            public int level { get => fader; set { fader = value; mixLevelChanged?.Invoke(this, new EventArgs()); } }
            public bool muted { get => mute; set { mute = value; mixMuteChanged?.Invoke(this, new EventArgs()); } }
            public int bgColourId { get; set; }

            public Mix () { }

            public Mix (int mix) {
                this.mix = mix;
                name = $"MX{(mix < 10 ? " " : "")}{mix}";
                // Initialised to 0dB
                level = 823;
                // Initialised to unmuted (ON)
                muted = false;
                bgColourId = 0;
            }
        }

    }

}
