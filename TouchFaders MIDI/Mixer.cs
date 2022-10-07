﻿using System.Collections.Generic;

namespace TouchFaders_MIDI {

	public class SysExCommand {
		private byte[] dataBytes;
		public CommandType type;

		public int DataCategory {
			get {
				return dataBytes[0];
			}
		}

		public byte DataCategoryByte {
			get {
				return dataBytes[0];
			}
		}

		public int Element {
			get {
				int element = dataBytes[1] << 7;
				return element + dataBytes[2];
			}
		}

		public byte ElementMSB {
			get => dataBytes[1];
		}

		public byte ElementLSB {
			get => dataBytes[2];
		}

		public int Index {
			get {
				int index = dataBytes[3] << 7;
				return index + dataBytes[4];
			}
		}

		public byte IndexMSB {
			get => dataBytes[3];
		}

		public byte IndexLSB {
			get => dataBytes[4];
		}

		public SysExCommand (byte[] data) {
			dataBytes = data;
		}

		public enum CommandType {
			kInputOn,
			kInputFader,
			kInputToMix,
			kGroupID_Input,
			kPatchInInput,
			kNameInputChannel,
			kIconInputChannel,
			kChannelSelected
		}
	}

	public class Mixer {

		public enum Type {
			LS9, QL, CL
		}

		public Mixer () { model = "NONE"; type = Type.LS9; channelCount = 0; mixCount = 0; id = 0; commands = LS9_commands; }
		private Mixer (string value, int channels, int mixes, byte midi_id, Type type) {
			model = value;
			channelCount = channels;
			mixCount = mixes;
			id = midi_id;
			switch (type) {
				case Type.LS9:
					commands = LS9_commands;
					break;
				case Type.QL:
				case Type.CL:
					commands = QL_CL_commands;
					break;
			}
		}

		public string model { get; set; }
		private Type t;
		public Type type {
			get => t; set {
				t = value; switch (type) {
					case Type.LS9:
						commands = LS9_commands;
						break;
					case Type.QL:
					case Type.CL:
						commands = QL_CL_commands;
						break;
				}
			}
		}
		public int channelCount { get; set; }
		public int mixCount { get; set; }
		public byte id { get; set; }
		public Dictionary<SysExCommand.CommandType, SysExCommand> commands;

		public override string ToString () {
			return $"{model}, ch{channelCount}×{mixCount}";
		}

		public override bool Equals (object obj) {
			if (obj.GetType() != typeof(Mixer)) return false;
			Mixer other = obj as Mixer;
			if (model != other.model) return false;
			if (channelCount != other.channelCount) return false;
			if (mixCount != other.mixCount) return false;
			return true;
		}

		public override int GetHashCode () {
			int hashCode = -316074491;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(model);
			hashCode = hashCode * -1521134295 + channelCount.GetHashCode();
			hashCode = hashCode * -1521134295 + mixCount.GetHashCode();
			return hashCode;
		}

		private static readonly Dictionary<SysExCommand.CommandType, SysExCommand> LS9_commands = new Dictionary<SysExCommand.CommandType, SysExCommand>() {
					{ SysExCommand.CommandType.kInputOn , new SysExCommand(new byte[] { 0x01, 0x00, 0x31, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kInputFader , new SysExCommand(new byte[] { 0x01, 0x00, 0x33, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kInputToMix, new SysExCommand(new byte[] { 0x01, 0x00, 0x43, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kGroupID_Input , new SysExCommand(new byte[] { 0x01, 0x01, 0x06, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kPatchInInput, new SysExCommand(new byte[] { 0x01, 0x01, 0x0B, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kNameInputChannel, new SysExCommand(new byte[] { 0x01, 0x01, 0x14, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kIconInputChannel, new SysExCommand(new byte[] { 0x01, 0x01, 0x15, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kChannelSelected, new SysExCommand(new byte[] {0x02, 0x39, 0x00, 0x10, 0x00}) }
				};

		private static readonly Dictionary<SysExCommand.CommandType, SysExCommand> QL_CL_commands = new Dictionary<SysExCommand.CommandType, SysExCommand>() {
					{ SysExCommand.CommandType.kInputOn , new SysExCommand(new byte[] { 0x01, 0x00, 0x35, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kInputFader , new SysExCommand(new byte[] { 0x01, 0x00, 0x37, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kInputToMix, new SysExCommand(new byte[] { 0x01, 0x00, 0x49, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kGroupID_Input , new SysExCommand(new byte[] { 0x01, 0x01, 0x0F, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kPatchInInput, new SysExCommand(new byte[] { 0x01, 0x01, 0x14, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kNameInputChannel, new SysExCommand(new byte[] { 0x01, 0x01, 0x1D, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kIconInputChannel, new SysExCommand(new byte[] { 0x01, 0x01, 0x1E, 0x00, 0x00}) },
					{ SysExCommand.CommandType.kChannelSelected, new SysExCommand(new byte[] {0x02, 0x39, 0x00, 0x10, 0x00}) }
				};

		public static Mixer LS932 => new Mixer("LS9-32", 64, 16, 0x12, Type.LS9);
		public static Mixer LS916 => new Mixer("LS9-16", 32, 16, 0x12, Type.LS9);

		public static Mixer QL5 => new Mixer("QL5", 64, 16, 0x19, Type.QL);
		public static Mixer QL1 => new Mixer("QL1", 32, 16, 0x19, Type.QL);

		public static Mixer CL5 => new Mixer("CL5", 72, 24, 0x19, Type.CL);
		public static Mixer CL3 => new Mixer("CL3", 64, 24, 0x19, Type.CL);
		public static Mixer CL1 => new Mixer("CL1", 48, 24, 0x19, Type.CL);

	}

}
