using System;
using System.Collections.Generic;
using System.IO;

namespace s3pi.Interfaces;

public abstract class AResourceIndexEntry : AResourceKey, IResourceIndexEntry, IApiVersion, IContentFields, IResourceKey, IEqualityComparer<IResourceKey>, IEquatable<IResourceKey>, IComparable<IResourceKey>
{
	public EventHandler ResourceIndexEntryChanged;

	public override List<string> ContentFields => AApiVersionedFields.GetContentFields(requestedApiVersion, GetType());

	[ElementPriority(5)]
	public abstract uint Chunkoffset { get; set; }

	[ElementPriority(6)]
	public abstract uint Filesize { get; set; }

	[ElementPriority(7)]
	public abstract uint Memsize { get; set; }

	[ElementPriority(8)]
	public abstract ushort Compressed { get; set; }

	[ElementPriority(9)]
	public abstract ushort Unknown2 { get; set; }

	public abstract Stream Stream { get; }

	public abstract bool IsDeleted { get; set; }

	public AResourceIndexEntry()
		: base(0, null)
	{
		handler = (EventHandler)Delegate.Combine(handler, new EventHandler(OnResourceIndexEntryChanged));
	}

	private void OnResourceIndexEntryChanged(object sender, EventArgs e)
	{
		if (ResourceIndexEntryChanged != null)
		{
			ResourceIndexEntryChanged(sender, e);
		}
	}
}
