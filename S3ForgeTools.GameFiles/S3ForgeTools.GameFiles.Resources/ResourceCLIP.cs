using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Sims3.SimIFace;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceCLIP
{
	public class ClipRuleEntry
	{
		public ushort FrameIndex { get; set; }

		public ushort SignBits { get; set; }

		public Vector3 Translation { get; set; }

		public Quaternion Rotation { get; set; }

		public Vector3 Scale { get; set; }

		public string ExportText()
		{
			return string.Format("TR:{1}, ROT:{2}", FrameIndex, Translation, Rotation);
		}
	}

	public class ClipRule
	{
		private uint Size;

		private Dictionary<uint, string> HashTable;

		public uint RuleDataOffset { get; set; }

		public uint NameHash { get; set; }

		public float MovementOffset { get; set; }

		public float MovementScale { get; set; }

		public ushort RuleFrameCount { get; set; }

		public ushort FrameType { get; set; }

		public List<ClipRuleEntry> Entries { get; private set; }

		public ClipRule()
		{
			Entries = new List<ClipRuleEntry>();
			HashTable = null;
		}

		public ClipRule(Dictionary<uint, string> HashTable)
			: this()
		{
			this.HashTable = HashTable;
		}

		public void CalculateSize(uint Offset)
		{
			Size = Offset - RuleDataOffset;
			float num = (float)Size / (float)(int)RuleFrameCount;
			switch (FrameType)
			{
			case 259:
				if ((double)num != 10.0)
				{
					throw new DataMisalignedException();
				}
				break;
			case 267:
				if (!float.IsNaN(num))
				{
					throw new DataMisalignedException();
				}
				break;
			case 274:
				if ((double)num != 8.0)
				{
					throw new DataMisalignedException();
				}
				break;
			case 524:
				if (!float.IsNaN(num))
				{
					throw new DataMisalignedException();
				}
				break;
			case 529:
				if (!float.IsNaN(num))
				{
					throw new DataMisalignedException();
				}
				break;
			case 532:
				if ((double)num != 12.0)
				{
					throw new DataMisalignedException();
				}
				break;
			case 1797:
				if ((double)num != 6.0)
				{
					throw new DataMisalignedException();
				}
				break;
			case 1801:
				if (!float.IsNaN(num))
				{
					throw new DataMisalignedException();
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		public void Import(BinaryReader Reader, List<float> Floats)
		{
			RuleDataOffset = Reader.ReadUInt32();
			NameHash = Reader.ReadUInt32();
			MovementOffset = Reader.ReadSingle();
			MovementScale = Reader.ReadSingle();
			RuleFrameCount = Reader.ReadUInt16();
			FrameType = Reader.ReadUInt16();
			string text = $"0x{NameHash:x8}";
			if (HashTable != null && HashTable.ContainsKey(NameHash))
			{
				text = HashTable[NameHash];
			}
			switch (FrameType)
			{
			default:
				throw new NotImplementedException();
			case 259:
			case 267:
			case 274:
			case 524:
			case 529:
			case 532:
			case 1797:
			case 1801:
			{
				if (FrameType == 267)
				{
					Console.WriteLine("Data, Frame {0:x4}, Bone {5}, Disp {3}, Scale {4}", FrameType, RuleFrameCount, RuleDataOffset, MovementOffset, MovementScale, text);
				}
				else if (FrameType == 529)
				{
					Console.WriteLine("Data, Frame {0:x4}, Bone {5}, Disp {3}, Scale {4}", FrameType, RuleFrameCount, RuleDataOffset, MovementOffset, MovementScale, text);
				}
				else if (FrameType == 1801)
				{
					Console.WriteLine("Data, Frame {0:x4}, Bone {5}, Disp {3}, Scale {4}", FrameType, RuleFrameCount, RuleDataOffset, MovementOffset, MovementScale, text);
				}
				long position = Reader.BaseStream.Position;
				Reader.BaseStream.Position = RuleDataOffset;
				for (int i = 0; i < RuleFrameCount; i++)
				{
					ClipRuleEntry clipRuleEntry = CreateEntry(Reader, FrameType, MovementOffset, MovementScale, Floats);
					Entries.Add(clipRuleEntry);
					if (clipRuleEntry.SignBits > 15)
					{
						Console.WriteLine("RULE: {0:x4} {1:x4} {2}", FrameType, clipRuleEntry.SignBits, text);
					}
				}
				Reader.BaseStream.Position = position;
				break;
			}
			}
		}

		private ClipRuleEntry CreateEntry(BinaryReader Reader, ushort FrameType, float Offset, float Scale, List<float> Floats)
		{
			ClipRuleEntry clipRuleEntry = new ClipRuleEntry();
			clipRuleEntry.FrameIndex = Reader.ReadUInt16();
			clipRuleEntry.SignBits = Reader.ReadUInt16();
			switch (FrameType)
			{
			case 259:
			{
				float num = Floats[Reader.ReadUInt16()] * Scale + Offset;
				float num2 = Floats[Reader.ReadUInt16()] * Scale + Offset;
				float num3 = Floats[Reader.ReadUInt16()] * Scale + Offset;
				if ((clipRuleEntry.SignBits & 1) == 1)
				{
					num = 0f - num;
				}
				if ((clipRuleEntry.SignBits & 2) == 2)
				{
					num2 = 0f - num2;
				}
				if ((clipRuleEntry.SignBits & 4) == 4)
				{
					num3 = 0f - num3;
				}
				if (clipRuleEntry.SignBits > 7)
				{
					throw new NotImplementedException();
				}
				clipRuleEntry.Translation = new Vector3(num, num2, num3);
				break;
			}
			case 267:
				throw new NotImplementedException();
			case 274:
			{
				uint num5 = Reader.ReadUInt32();
				float num = (float)(num5 & 0x3FF) / 1023f * Scale + Offset;
				float num2 = (float)((num5 & 0xFFC00) >> 10) / 1023f * Scale + Offset;
				float num3 = (float)((num5 & 0x3FF00000) >> 20) / 1023f * Scale + Offset;
				if ((clipRuleEntry.SignBits & 1) == 1)
				{
					num = 0f - num;
				}
				if ((clipRuleEntry.SignBits & 2) == 2)
				{
					num2 = 0f - num2;
				}
				if ((clipRuleEntry.SignBits & 4) == 4)
				{
					num3 = 0f - num3;
				}
				if (clipRuleEntry.SignBits > 7)
				{
				}
				clipRuleEntry.Translation = new Vector3(num, num2, num3);
				break;
			}
			case 524:
				throw new NotImplementedException();
			case 529:
				throw new NotImplementedException();
			case 532:
			{
				float num = (float)(Reader.ReadUInt16() & 0xFFF) / 4095f * Scale + Offset;
				float num2 = (float)(Reader.ReadUInt16() & 0xFFF) / 4095f * Scale + Offset;
				float num3 = (float)(Reader.ReadUInt16() & 0xFFF) / 4095f * Scale + Offset;
				float num4 = (float)(Reader.ReadUInt16() & 0xFFF) / 4095f * Scale + Offset;
				if ((clipRuleEntry.SignBits & 1) == 1)
				{
					num = 0f - num;
				}
				if ((clipRuleEntry.SignBits & 2) == 2)
				{
					num2 = 0f - num2;
				}
				if ((clipRuleEntry.SignBits & 4) == 4)
				{
					num3 = 0f - num3;
				}
				if ((clipRuleEntry.SignBits & 8) == 8)
				{
					num4 = 0f - num4;
				}
				if (clipRuleEntry.SignBits > 15)
				{
				}
				clipRuleEntry.Rotation = new Quaternion(num, num2, num3, num4);
				break;
			}
			case 1797:
			{
				byte b = Reader.ReadByte();
				byte b2 = Reader.ReadByte();
				break;
			}
			case 1801:
				throw new NotImplementedException();
			default:
				throw new NotImplementedException();
			}
			return clipRuleEntry;
		}

		public override string ToString()
		{
			return $"Joint Rule -- Bone {NameHash} " + $"Offset {MovementOffset}, Scale {MovementScale} " + $"Frame type {LookupFrameType(FrameType):x} for {RuleFrameCount} frames";
		}

		private string LookupFrameType(ushort FrameType)
		{
			return FrameType switch
			{
				259 => "Ind-T", 
				267 => "Nul-T", 
				274 => "Pak-T", 
				524 => "Nul-R", 
				529 => "Ind-R", 
				532 => "Pak-R", 
				_ => $"UNK-? 0x{FrameType:x4}", 
			};
		}
	}

	public class ClipEvent
	{
		public uint EventType { get; private set; }

		public uint ID { get; private set; }

		public float TimeCode { get; private set; }

		public float UnknownA { get; private set; }

		public float UnknownB { get; private set; }

		public uint UnknownC { get; private set; }

		public string EventName { get; private set; }

		public int FrameNumber => (int)((double)TimeCode * 30.0);

		internal ClipEvent(uint EventType)
		{
			this.EventType = EventType;
		}

		public virtual void Import(BinaryReader Reader)
		{
			ushort num = Reader.ReadUInt16();
			if (num != 49636)
			{
				throw new InvalidDataException();
			}
			ID = Reader.ReadUInt32();
			TimeCode = Reader.ReadSingle();
			UnknownA = Reader.ReadSingle();
			UnknownB = Reader.ReadSingle();
			UnknownC = Reader.ReadUInt32();
			int namelen = Reader.ReadInt32() + 1;
			EventName = ReadNullASCIIString(Reader, namelen);
			while (Reader.BaseStream.Position % 4 != 0)
			{
				Reader.ReadByte();
			}
		}

		public virtual void ImportText(string Value)
		{
		}

		public virtual string ExportText()
		{
			return $"ID:0x:{ID:x4}, FR:{FrameNumber}, UA:{UnknownA}, UB:{UnknownB}, UC:0x{UnknownC:x4}, NAME:{EventName}";
		}

		public override string ToString()
		{
			return $"Event {ID}={EventName} at {FrameNumber}";
		}
	}

	public class ClipEventAttach : ClipEvent
	{
		public uint PropHash { get; set; }

		public uint ObjectHash { get; set; }

		public uint SlotHash { get; set; }

		public uint Unknown { get; set; }

		private Matrix44 Matrix { get; set; }

		public ClipEventAttach()
			: base(1u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			PropHash = Reader.ReadUInt32();
			ObjectHash = Reader.ReadUInt32();
			SlotHash = Reader.ReadUInt32();
			Unknown = Reader.ReadUInt32();
			Matrix = new Matrix44(Reader);
		}

		public override string ToString()
		{
			return $"Attach Object {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV1, " + base.ExportText() + $", prop:0x{PropHash:x4}, obj:0x{ObjectHash:x4}, slot:0x{SlotHash:x4}, U1:0x{Unknown:x4}";
		}
	}

	public class ClipEventUnParent : ClipEvent
	{
		public uint ObjectHash { get; set; }

		public ClipEventUnParent()
			: base(2u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			ObjectHash = Reader.ReadUInt32();
		}

		public override string ToString()
		{
			return $"Un-Parent {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV2, " + base.ExportText() + $", obj:0x{ObjectHash:x4}";
		}
	}

	public class ClipEventPlaySound : ClipEvent
	{
		public string SoundName { get; set; }

		public ClipEventPlaySound()
			: base(3u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			SoundName = ReadNullASCIIString(Reader, 128);
		}

		public override string ToString()
		{
			return $"Play Sound {base.ToString()} - {SoundName}";
		}

		public override string ExportText()
		{
			return "EV3, " + base.ExportText() + $", SND:{SoundName}";
		}
	}

	public class ClipEventSACS : ClipEvent
	{
		public ClipEventSACS()
			: base(4u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
		}

		public override string ToString()
		{
			return $"SACS Script {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV4, " + base.ExportText();
		}
	}

	public class ClipEventPlayEffect : ClipEvent
	{
		public uint Unknown1 { get; set; }

		public uint Unknown2 { get; set; }

		public uint EffectHash { get; set; }

		public uint ActorHash { get; set; }

		public uint SlotHash { get; set; }

		public uint Unknown3 { get; set; }

		public ClipEventPlayEffect()
			: base(5u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			Unknown1 = Reader.ReadUInt32();
			Unknown2 = Reader.ReadUInt32();
			EffectHash = Reader.ReadUInt32();
			ActorHash = Reader.ReadUInt32();
			SlotHash = Reader.ReadUInt32();
			Unknown3 = Reader.ReadUInt32();
		}

		public override string ToString()
		{
			return $"Play Effect {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV5, " + base.ExportText() + $", U1:{Unknown1}, U2:0x{Unknown2:x4}, Effect:0x{EffectHash:x4}, Actor:0x{ActorHash:x4}, Slot:0x{SlotHash:x4}, U3:0x{Unknown3:x4}";
		}
	}

	public class ClipEventVisibility : ClipEvent
	{
		public float Visibility { get; set; }

		public ClipEventVisibility()
			: base(6u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			Visibility = Reader.ReadSingle();
		}

		public override string ToString()
		{
			return $"Set Visibility {base.ToString()} -- {Visibility}";
		}

		public override string ExportText()
		{
			return "EV6, " + base.ExportText() + $", VIS:{Visibility}";
		}
	}

	public class ClipEventDestroyProp : ClipEvent
	{
		public uint ActorHash { get; set; }

		public ClipEventDestroyProp()
			: base(9u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			ActorHash = Reader.ReadUInt32();
		}

		public override string ToString()
		{
			return $"Destroy Prop {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV9, " + base.ExportText() + $", Actor:0x{ActorHash:x4}";
		}
	}

	public class ClipEventStopEffect : ClipEvent
	{
		public uint EffectHash { get; set; }

		public uint Unknown1 { get; set; }

		public ClipEventStopEffect()
			: base(10u)
		{
		}

		public override void Import(BinaryReader Reader)
		{
			base.Import(Reader);
			EffectHash = Reader.ReadUInt32();
			Unknown1 = Reader.ReadUInt32();
		}

		public override string ToString()
		{
			return $"Stop Effect {base.ToString()}";
		}

		public override string ExportText()
		{
			return "EV10, " + base.ExportText() + $", Effect:0x{EffectHash:x4}, U1:0x{Unknown1:x4}";
		}
	}

	private Dictionary<uint, string> HashTable;

	public string FileName { get; private set; }

	public string Name { get; private set; }

	public float[] EndData { get; private set; }

	public uint Unknown1 { get; private set; }

	public uint Unknown2 { get; private set; }

	public string ActorName { get; private set; }

	public List<Tuple<uint, string, string>> SlotTable { get; private set; }

	public ClipEvent[] ClipTable { get; private set; }

	public ClipRule[] Rules { get; private set; }

	public List<float> FloatList { get; private set; }

	public ResourceCLIP(Stream Source, Dictionary<uint, string> Table = null)
	{
		HashTable = Table;
		if (HashTable == null)
		{
			HashTable = new Dictionary<uint, string>();
		}
		FloatList = new List<float>();
		SlotTable = new List<Tuple<uint, string, string>>();
		Import(Source);
	}

	private void Import(Stream Source)
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		uint num = binaryReader.ReadUInt32();
		if (num != 1797309683)
		{
			throw new InvalidDataException();
		}
		uint num2 = binaryReader.ReadUInt32();
		uint num3 = binaryReader.ReadUInt32();
		uint num4 = (uint)(int)binaryReader.BaseStream.Position + binaryReader.ReadUInt32();
		uint num5 = (uint)(int)binaryReader.BaseStream.Position + binaryReader.ReadUInt32();
		uint num6 = (uint)(int)binaryReader.BaseStream.Position + binaryReader.ReadUInt32();
		uint num7 = (uint)(int)binaryReader.BaseStream.Position + binaryReader.ReadUInt32();
		Unknown1 = binaryReader.ReadUInt32();
		Unknown2 = binaryReader.ReadUInt32();
		uint num8 = binaryReader.ReadUInt32();
		byte[] array = binaryReader.ReadBytes(16);
		binaryReader.BaseStream.Position = num8;
		EndData = new float[4];
		EndData[0] = binaryReader.ReadSingle();
		EndData[1] = binaryReader.ReadSingle();
		EndData[2] = binaryReader.ReadSingle();
		EndData[3] = binaryReader.ReadSingle();
		Stream source = new SubStream(Source, num4, num3);
		ImportClip(source);
		Stream source2 = new SubStream(Source, num5, num6 - num5);
		ImportSlotTable(source2);
		binaryReader.BaseStream.Position = num6;
		int namelen = (int)(num7 - num6);
		ActorName = ReadNullASCIIString(binaryReader, namelen);
		binaryReader.BaseStream.Position = num7;
		string @string = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
		if (@string != "=CE=")
		{
			throw new InvalidDataException();
		}
		uint num9 = binaryReader.ReadUInt32();
		if (num9 != 259)
		{
			throw new InvalidDataException();
		}
		uint num10 = binaryReader.ReadUInt32();
		uint num11 = binaryReader.ReadUInt32();
		uint num12 = binaryReader.ReadUInt32();
		ClipTable = new ClipEvent[num10];
		for (int i = 0; i < num10; i++)
		{
			ClipEvent clipEvent = binaryReader.ReadUInt16() switch
			{
				1 => new ClipEventAttach(), 
				2 => new ClipEventUnParent(), 
				3 => new ClipEventPlaySound(), 
				4 => new ClipEventSACS(), 
				5 => new ClipEventPlayEffect(), 
				6 => new ClipEventVisibility(), 
				9 => new ClipEventDestroyProp(), 
				10 => new ClipEventStopEffect(), 
				_ => throw new InvalidDataException(), 
			};
			ClipTable[i] = clipEvent;
			clipEvent.Import(binaryReader);
		}
		Source.Close();
	}

	private void ImportClip(Stream Source)
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		string @string = Encoding.ASCII.GetString(binaryReader.ReadBytes(8));
		if (@string != "_pilC3S_")
		{
			throw new InvalidDataException();
		}
		uint num = binaryReader.ReadUInt32();
		if (num != 2)
		{
			throw new InvalidDataException();
		}
		uint num2 = binaryReader.ReadUInt32();
		float num3 = binaryReader.ReadSingle();
		ushort num4 = binaryReader.ReadUInt16();
		ushort num5 = binaryReader.ReadUInt16();
		uint num6 = binaryReader.ReadUInt32();
		uint num7 = binaryReader.ReadUInt32();
		uint num8 = binaryReader.ReadUInt32();
		uint num9 = binaryReader.ReadUInt32();
		uint num10 = binaryReader.ReadUInt32();
		uint num11 = binaryReader.ReadUInt32();
		binaryReader.BaseStream.Position = num10;
		Name = ReadNullASCIIString(binaryReader, (int)(num11 - num10 + 1));
		binaryReader.BaseStream.Position = num11;
		FileName = ReadNullASCIIString(binaryReader, (int)(num9 - num11 + 1));
		binaryReader.BaseStream.Position = num9;
		for (int i = 0; i < num7; i++)
		{
			FloatList.Add(binaryReader.ReadSingle());
		}
		binaryReader.BaseStream.Position = num8;
		Rules = new ClipRule[num6];
		for (int i = 0; i < num6; i++)
		{
			Rules[i] = new ClipRule(HashTable);
			Rules[i].Import(binaryReader, FloatList);
			if (i > 0)
			{
				Rules[i - 1].CalculateSize(Rules[i].RuleDataOffset);
			}
		}
	}

	private void ImportSlotTable(Stream Source)
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		uint num = binaryReader.ReadUInt32();
		uint[] array = new uint[num];
		uint num2 = (uint)binaryReader.BaseStream.Position;
		for (int i = 0; i < num; i++)
		{
			array[i] = binaryReader.ReadUInt32() + num2;
		}
		uint[] array2 = array;
		foreach (uint num3 in array2)
		{
			binaryReader.BaseStream.Position = num3;
			uint num4 = binaryReader.ReadUInt32();
			uint num5 = binaryReader.ReadUInt32();
			uint[] array3 = new uint[num5];
			uint num6 = (uint)binaryReader.BaseStream.Position;
			for (int i = 0; i < num5; i++)
			{
				array3[i] = binaryReader.ReadUInt32() + num6;
			}
			uint[] array4 = array3;
			foreach (uint num7 in array4)
			{
				binaryReader.BaseStream.Position = num7;
				uint item = binaryReader.ReadUInt32();
				string item2 = ReadNullASCIIString(binaryReader, 512);
				string item3 = ReadNullASCIIString(binaryReader, 512);
				SlotTable.Add(new Tuple<uint, string, string>(item, item2, item3));
			}
		}
	}

	private static string ReadNullASCIIString(BinaryReader Reader, int namelen)
	{
		byte[] array = Reader.ReadBytes(namelen);
		StringBuilder stringBuilder = new StringBuilder(namelen);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			if (b == 0 || b == 126)
			{
				break;
			}
			stringBuilder.Append((char)b);
		}
		return stringBuilder.ToString();
	}

	public void ExportToBlender(string OutputName, Dictionary<uint, string> HashTable = null)
	{
		ExportMainToBlender(Path.ChangeExtension(OutputName, ".BA"));
		ExportEventTableToBlender(Path.ChangeExtension(OutputName, ".BA0"));
		ExportClipToBlender(Path.ChangeExtension(OutputName, ".BA1"), HashTable);
	}

	private void ExportMainToBlender(string OutputName)
	{
		List<string> list = new List<string>();
		list.Add("#CLIP Import/Export Master File");
		list.Add("#The only user editable value in this file is the ActorName");
		list.Add("#And possibly the slot table");
		list.Add($"Name={Name}");
		list.Add($"FileName={FileName}");
		list.Add($"ActorName={ActorName}");
		list.Add($"Unknown1={Unknown1}");
		list.Add($"Unknown2={Unknown2}");
		list.Add($"End0={EndData[0]} {EndData[1]} {EndData[2]} {EndData[3]}");
		list.Add(string.Empty);
		list.Add("#CLIP Import/Export Slot Table File");
		foreach (Tuple<uint, string, string> item in SlotTable)
		{
			list.Add($"{item.Item1}:{item.Item2}={item.Item3}");
		}
		File.WriteAllLines(OutputName, list);
	}

	private void ExportEventTableToBlender(string OutputName)
	{
		List<string> list = new List<string>();
		list.Add("#CLIP Import/Export Event Table File");
		IOrderedEnumerable<ClipEvent> orderedEnumerable = ClipTable.OrderBy((ClipEvent Item) => Item.FrameNumber);
		foreach (ClipEvent item in orderedEnumerable)
		{
			list.Add(item.ExportText());
		}
		File.WriteAllLines(OutputName, list);
	}

	private void ExportClipToBlender(string OutputName, Dictionary<uint, string> HashTable = null)
	{
		List<string> list = new List<string>();
		list.Add("#CLIP Import/Export Animation Clip File");
		List<Tuple<ushort, string, string>> list2 = new List<Tuple<ushort, string, string>>();
		ClipRule[] rules = Rules;
		foreach (ClipRule clipRule in rules)
		{
			string text = $"0x{clipRule.NameHash:x4}";
			if (HashTable != null && HashTable.ContainsKey(clipRule.NameHash))
			{
				text = HashTable[clipRule.NameHash];
			}
			foreach (ClipRuleEntry entry in clipRule.Entries)
			{
				list2.Add(new Tuple<ushort, string, string>(entry.FrameIndex, text, $"FR:{entry.FrameIndex}, BONE:{text}, {entry.ExportText()}"));
			}
		}
		IEnumerable<ushort> source = from Item in list2
			orderby Item.Item1
			select Item.Item1;
		int num = source.Distinct().Count();
		IEnumerable<string> enumerable = from Item in list2
			orderby Item.Item1, Item.Item2
			select Item.Item3;
		int num2 = enumerable.Count();
		foreach (string item in enumerable)
		{
			list.Add(item);
		}
		File.WriteAllLines(OutputName, list);
	}
}
