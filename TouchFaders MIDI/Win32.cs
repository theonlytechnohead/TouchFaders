using System;
using System.Runtime.InteropServices;

namespace TouchFaders_MIDI {
	class Win32 {

		[DllImport("Winmm.dll")]
		public static extern uint midiInGetNumDevs ();

		[DllImport("Winmm.dll")]
		public static extern uint midiOutGetNumDevs ();

		[StructLayout(LayoutKind.Sequential)]
		public struct MIDIINCAPS {
			public ushort wMid;
			public ushort wPid;
			public uint vDriverVersion;     // MMVERSION
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string szPname;
			public uint dwSupport;
		}

		public enum MMRESULT : uint {
			MMSYSERR_NOERROR = 0,
			MMSYSERR_ERROR = 1,
			MMSYSERR_BADDEVICEID = 2,
			MMSYSERR_NOTENABLED = 3,
			MMSYSERR_ALLOCATED = 4,
			MMSYSERR_INVALHANDLE = 5,
			MMSYSERR_NODRIVER = 6,
			MMSYSERR_NOMEM = 7,
			MMSYSERR_NOTSUPPORTED = 8,
			MMSYSERR_BADERRNUM = 9,
			MMSYSERR_INVALFLAG = 10,
			MMSYSERR_INVALPARAM = 11,
			MMSYSERR_HANDLEBUSY = 12,
			MMSYSERR_INVALIDALIAS = 13,
			MMSYSERR_BADDB = 14,
			MMSYSERR_KEYNOTFOUND = 15,
			MMSYSERR_READERROR = 16,
			MMSYSERR_WRITEERROR = 17,
			MMSYSERR_DELETEERROR = 18,
			MMSYSERR_VALNOTFOUND = 19,
			MMSYSERR_NODRIVERCB = 20,
			WAVERR_BADFORMAT = 32,
			WAVERR_STILLPLAYING = 33,
			WAVERR_UNPREPARED = 34
		}

		[DllImport("winmm.dll", SetLastError = true)]
		public static extern MMRESULT midiInGetDevCaps (UIntPtr uDeviceID, ref MIDIINCAPS caps, uint cbMidiInCaps);

		[StructLayout(LayoutKind.Sequential)]
		public struct MIDIOUTCAPS {
			public ushort wMid;
			public ushort wPid;
			public uint vDriverVersion;     //MMVERSION
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string szPname;
			public ushort wTechnology;
			public ushort wVoices;
			public ushort wNotes;
			public ushort wChannelMask;
			public uint dwSupport;
		}

		// values for wTechnology field of MIDIOUTCAPS structure
		private const ushort MOD_MIDIPORT = 1;     // output port
		private const ushort MOD_SYNTH = 2;        // generic internal synth
		private const ushort MOD_SQSYNTH = 3;      // square wave internal synth
		private const ushort MOD_FMSYNTH = 4;      // FM internal synth
		private const ushort MOD_MAPPER = 5;       // MIDI mapper
		private const ushort MOD_WAVETABLE = 6;    // hardware wavetable synth
		private const ushort MOD_SWSYNTH = 7;      // software synth

		// flags for dwSupport field of MIDIOUTCAPS structure
		private const uint MIDICAPS_VOLUME = 1;      // supports volume control
		private const uint MIDICAPS_LRVOLUME = 2;    // separate left-right volume control
		private const uint MIDICAPS_CACHE = 4;
		private const uint MIDICAPS_STREAM = 8;      // driver supports midiStreamOut directly

		[DllImport("winmm.dll", SetLastError = true)]
		public static extern MMRESULT midiOutGetDevCaps (UIntPtr uDeviceID, ref MIDIOUTCAPS caps, uint cbMidiOutCaps);

	}
}
