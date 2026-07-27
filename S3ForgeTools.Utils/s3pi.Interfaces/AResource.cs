using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using s3pi.Settings;

namespace s3pi.Interfaces;

public abstract class AResource : AApiVersionedFields, IResource, IApiVersion, IContentFields
{
	public abstract class DependentList<T> : AHandlerList<T>, IGenericAdd, IList, ICollection, IEnumerable where T : IEquatable<T>
	{
		protected EventHandler elementHandler;

		protected DependentList(EventHandler handler)
			: this(handler, -1L)
		{
		}

		protected DependentList(EventHandler handler, long size)
			: base(handler, size)
		{
		}

		protected DependentList(EventHandler handler, IList<T> ilt)
			: this(handler, -1L, ilt)
		{
		}

		protected DependentList(EventHandler handler, long size, IList<T> ilt)
			: base(handler, size, ilt)
		{
		}

		protected DependentList(EventHandler handler, Stream s)
			: this(handler, -1L, s)
		{
		}

		protected DependentList(EventHandler handler, long size, Stream s)
			: base((EventHandler)null, size)
		{
			elementHandler = handler;
			Parse(s);
			base.handler = handler;
		}

		protected virtual void Parse(Stream s)
		{
			base.Clear();
			bool inc = true;
			for (uint num = ReadCount(s); num != 0; num = (uint)(num - (inc ? 1 : 0)))
			{
				base.Add(CreateElement(s, out inc));
			}
		}

		protected virtual uint ReadCount(Stream s)
		{
			return new BinaryReader(s).ReadUInt32();
		}

		protected abstract T CreateElement(Stream s);

		protected virtual T CreateElement(Stream s, out bool inc)
		{
			inc = true;
			return CreateElement(s);
		}

		public virtual void UnParse(Stream s)
		{
			WriteCount(s, (uint)base.Count);
			using Enumerator enumerator = GetEnumerator();
			while (enumerator.MoveNext())
			{
				T current = enumerator.Current;
				WriteElement(s, current);
			}
		}

		protected virtual void WriteCount(Stream s, uint count)
		{
			new BinaryWriter(s).Write(count);
		}

		protected abstract void WriteElement(Stream s, T element);

		public abstract void Add();

		public virtual bool Add(params object[] fields)
		{
			if (fields == null)
			{
				return false;
			}
			Type type = typeof(T);
			if (fields.Length == 1 && type.IsAssignableFrom(fields[0].GetType()) && !typeof(AHandlerElement).IsAssignableFrom(type))
			{
				base.Add((T)fields[0]);
				return true;
			}
			if (type.IsAbstract)
			{
				type = GetElementType(fields);
			}
			Type[] array = new Type[2 + fields.Length];
			array[0] = typeof(int);
			array[1] = typeof(EventHandler);
			for (int i = 0; i < fields.Length; i++)
			{
				array[2 + i] = fields[i].GetType();
			}
			object[] array2 = new object[2 + fields.Length];
			array2[0] = 0;
			array2[1] = elementHandler;
			Array.Copy(fields, 0, array2, 2, fields.Length);
			ConstructorInfo constructor = type.GetConstructor(array);
			if (constructor == null)
			{
				return false;
			}
			base.Add((T)type.GetConstructor(array).Invoke(array2));
			return true;
		}

		protected virtual Type GetElementType(params object[] fields)
		{
			throw new NotImplementedException();
		}

		void IList.Clear()
		{
			Clear();
		}

		void IList.RemoveAt(int P_0)
		{
			RemoveAt(P_0);
		}
	}

	public class TGIBlock : AResourceKey, IEquatable<TGIBlock>
	{
		public enum Order
		{
			TGI,
			TIG,
			GTI,
			GIT,
			ITG,
			IGT
		}

		private const int recommendedApiVersion = 1;

		private string order = "TGI";

		public override List<string> ContentFields => AApiVersionedFields.GetContentFields(requestedApiVersion, GetType());

		public override int RecommendedApiVersion => 1;

		public string Value => ToString();

		private void ok(string v)
		{
			ok((Order)Enum.Parse(typeof(Order), v));
		}

		private void ok(Order v)
		{
			if (!Enum.IsDefined(typeof(Order), v))
			{
				throw new ArgumentException("Invalid value " + v, "order");
			}
		}

		public TGIBlock(int APIversion, EventHandler handler, TGIBlock basis)
			: this(APIversion, handler, basis.order, basis)
		{
		}

		public TGIBlock(int APIversion, EventHandler handler, uint resourceType, uint resourceGroup, ulong instance)
			: base(APIversion, handler, resourceType, resourceGroup, instance)
		{
		}

		public TGIBlock(int APIversion, EventHandler handler, string order, uint resourceType, uint resourceGroup, ulong instance)
			: this(APIversion, handler, resourceType, resourceGroup, instance)
		{
			ok(order);
			this.order = order;
		}

		public TGIBlock(int APIversion, EventHandler handler, Order order, uint resourceType, uint resourceGroup, ulong instance)
			: this(APIversion, handler, resourceType, resourceGroup, instance)
		{
			ok(order);
			this.order = string.Concat(order);
		}

		public TGIBlock(int APIversion, EventHandler handler, IResourceKey rk)
			: base(APIversion, handler, rk)
		{
		}

		public TGIBlock(int APIversion, EventHandler handler, string order, IResourceKey rk)
			: this(APIversion, handler, rk)
		{
			ok(order);
			this.order = order;
		}

		public TGIBlock(int APIversion, EventHandler handler, Order order, IResourceKey rk)
			: this(APIversion, handler, rk)
		{
			ok(order);
			this.order = string.Concat(order);
		}

		public TGIBlock(int APIversion, EventHandler handler, Stream s)
			: base(APIversion, handler)
		{
			Parse(s);
		}

		public TGIBlock(int APIversion, EventHandler handler, string order, Stream s)
			: base(APIversion, handler)
		{
			ok(order);
			this.order = order;
			Parse(s);
		}

		public TGIBlock(int APIversion, EventHandler handler, Order order, Stream s)
			: base(APIversion, handler)
		{
			ok(order);
			this.order = string.Concat(order);
			Parse(s);
		}

		protected void Parse(Stream s)
		{
			BinaryReader binaryReader = new BinaryReader(s);
			string text = order;
			for (int i = 0; i < text.Length; i++)
			{
				switch (text[i])
				{
				case 'T':
					resourceType = binaryReader.ReadUInt32();
					break;
				case 'G':
					resourceGroup = binaryReader.ReadUInt32();
					break;
				case 'I':
					instance = binaryReader.ReadUInt64();
					break;
				}
			}
		}

		public void UnParse(Stream s)
		{
			BinaryWriter binaryWriter = new BinaryWriter(s);
			string text = order;
			for (int i = 0; i < text.Length; i++)
			{
				switch (text[i])
				{
				case 'T':
					binaryWriter.Write(resourceType);
					break;
				case 'G':
					binaryWriter.Write(resourceGroup);
					break;
				case 'I':
					binaryWriter.Write(instance);
					break;
				}
			}
		}

		public override AHandlerElement Clone(EventHandler handler)
		{
			return new TGIBlock(requestedApiVersion, handler, this);
		}

		public bool Equals(TGIBlock other)
		{
			return Equals((IResourceKey)other);
		}
	}

	public class CountedTGIBlockList : DependentList<TGIBlock>
	{
		private uint origCount;

		private string order = "TGI";

		public string Value
		{
			get
			{
				string text = "";
				for (int i = 0; i < base.Count; i++)
				{
					text += $"0x{i:X8}: {this[i].Value}\n";
				}
				return text;
			}
		}

		public CountedTGIBlockList(EventHandler handler)
			: this(handler, -1L, "TGI")
		{
		}

		public CountedTGIBlockList(EventHandler handler, IList<TGIBlock> ilt)
			: this(handler, -1L, "TGI", ilt)
		{
		}

		public CountedTGIBlockList(EventHandler handler, uint count, Stream s)
			: this(handler, -1L, "TGI", count, s)
		{
		}

		public CountedTGIBlockList(EventHandler handler, TGIBlock.Order order)
			: this(handler, -1L, order)
		{
		}

		public CountedTGIBlockList(EventHandler handler, TGIBlock.Order order, IList<TGIBlock> ilt)
			: this(handler, -1L, order, ilt)
		{
		}

		public CountedTGIBlockList(EventHandler handler, TGIBlock.Order order, uint count, Stream s)
			: this(handler, -1L, order, count, s)
		{
		}

		public CountedTGIBlockList(EventHandler handler, string order)
			: this(handler, -1L, order)
		{
		}

		public CountedTGIBlockList(EventHandler handler, string order, IList<TGIBlock> ilt)
			: this(handler, -1L, order, ilt)
		{
		}

		public CountedTGIBlockList(EventHandler handler, string order, uint count, Stream s)
			: this(handler, -1L, order, count, s)
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size)
			: this(handler, size, "TGI")
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, IList<TGIBlock> ilt)
			: this(handler, size, "TGI", ilt)
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, uint count, Stream s)
			: this(handler, size, "TGI", count, s)
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, TGIBlock.Order order)
			: this(handler, size, string.Concat(order))
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, TGIBlock.Order order, IList<TGIBlock> ilt)
			: this(handler, size, string.Concat(order), ilt)
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, TGIBlock.Order order, uint count, Stream s)
			: this(handler, size, string.Concat(order), count, s)
		{
		}

		public CountedTGIBlockList(EventHandler handler, long size, string order)
			: base(handler, size)
		{
			this.order = order;
		}

		public CountedTGIBlockList(EventHandler handler, long size, string order, IList<TGIBlock> ilt)
			: base(handler, size, ilt)
		{
			this.order = order;
		}

		public CountedTGIBlockList(EventHandler handler, long size, string order, uint count, Stream s)
			: base((EventHandler)null, size)
		{
			origCount = count;
			this.order = order;
			elementHandler = handler;
			Parse(s);
			base.handler = handler;
		}

		protected override TGIBlock CreateElement(Stream s)
		{
			return new TGIBlock(0, elementHandler, order, s);
		}

		protected override void WriteElement(Stream s, TGIBlock element)
		{
			element.UnParse(s);
		}

		protected override uint ReadCount(Stream s)
		{
			return origCount;
		}

		protected override void WriteCount(Stream s, uint count)
		{
		}

		public override void Add()
		{
			base.Add(new TGIBlock(0, elementHandler, order, 0u, 0u, 0uL));
		}

		public override void Add(TGIBlock item)
		{
			base.Add(new TGIBlock(0, elementHandler, order, item));
		}

		public void Add(IResourceKey rk)
		{
			base.Add(new TGIBlock(0, elementHandler, order, rk));
		}

		public override void Insert(int index, TGIBlock item)
		{
			base.Insert(index, new TGIBlock(0, elementHandler, order, item));
		}
	}

	public class TGIBlockList : DependentList<TGIBlock>
	{
		public string Value
		{
			get
			{
				string text = "";
				for (int i = 0; i < base.Count; i++)
				{
					text += $"0x{i:X8}: {this[i].Value}\n";
				}
				return text;
			}
		}

		public TGIBlockList(EventHandler handler)
			: base(handler)
		{
		}

		public TGIBlockList(EventHandler handler, IList<TGIBlock> ilt)
			: base(handler, ilt)
		{
		}

		public TGIBlockList(EventHandler handler, Stream s, long tgiPosn, long tgiSize)
			: base((EventHandler)null)
		{
			elementHandler = handler;
			Parse(s, tgiPosn, tgiSize);
			base.handler = handler;
		}

		protected override TGIBlock CreateElement(Stream s)
		{
			return new TGIBlock(0, elementHandler, s);
		}

		protected override void WriteElement(Stream s, TGIBlock element)
		{
			element.UnParse(s);
		}

		protected void Parse(Stream s, long tgiPosn, long tgiSize)
		{
			bool flag = true;
			if (flag && tgiPosn != s.Position)
			{
				throw new InvalidDataException($"Position of TGIBlock read: 0x{tgiPosn:X8}, actual: 0x{s.Position:X8}");
			}
			if (tgiSize > 0)
			{
				Parse(s);
			}
			if (flag && tgiSize != s.Position - tgiPosn)
			{
				throw new InvalidDataException($"Size of TGIBlock read: 0x{tgiSize:X8}, actual: 0x{s.Position - tgiPosn:X8}; at 0x{s.Position:X8}");
			}
		}

		public void UnParse(Stream s, long ptgiO)
		{
			BinaryWriter binaryWriter = new BinaryWriter(s);
			long position = s.Position;
			UnParse(s);
			long position2 = s.Position;
			s.Position = ptgiO;
			binaryWriter.Write((uint)(position - ptgiO - 4));
			binaryWriter.Write((uint)(position2 - position));
			s.Position = position2;
		}

		public override void Add()
		{
			Add(new TGIBlock(0, elementHandler, 0u, 0u, 0uL));
		}
	}

	protected Stream stream = null;

	protected bool dirty = false;

	public override List<string> ContentFields => AApiVersionedFields.GetContentFields(requestedApiVersion, GetType());

	public virtual Stream Stream
	{
		get
		{
			if (dirty || s3pi.Settings.Settings.AsBytesWorkaround)
			{
				stream = UnParse();
				dirty = false;
			}
			stream.Position = 0L;
			return stream;
		}
	}

	public virtual byte[] AsBytes
	{
		get
		{
			if (Stream is MemoryStream memoryStream)
			{
				return memoryStream.ToArray();
			}
			stream.Position = 0L;
			return new BinaryReader(stream).ReadBytes((int)stream.Length);
		}
	}

	public event EventHandler ResourceChanged;

	protected AResource(int APIversion, Stream s)
	{
		requestedApiVersion = APIversion;
		stream = s;
	}

	protected abstract Stream UnParse();

	protected virtual void OnResourceChanged(object sender, EventArgs e)
	{
		dirty = true;
		if (this.ResourceChanged != null)
		{
			this.ResourceChanged(sender, e);
		}
	}
}
