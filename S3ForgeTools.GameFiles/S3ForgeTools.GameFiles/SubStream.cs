using System;
using System.IO;

namespace S3ForgeTools.GameFiles;

public class SubStream : Stream
{
	private long _Offset;

	private long _Length;

	private long _Position;

	public Stream BaseStream { get; private set; }

	public override bool CanRead => GetCanRead();

	public override bool CanSeek => GetCanSeek();

	public override bool CanWrite => false;

	public override long Length => _Length;

	public override long Position
	{
		get
		{
			return _Position;
		}
		set
		{
			SetCurrentPosition(value);
		}
	}

	public SubStream(Stream BaseStream, long Offset, long Length)
	{
		this.BaseStream = BaseStream;
		_Offset = Offset;
		_Length = Length;
		_Position = 0L;
	}

	private void SetCurrentPosition(long Position)
	{
		if (BaseStream == null)
		{
			throw new ObjectDisposedException("BaseStream");
		}
		Seek(Position, SeekOrigin.Begin);
	}

	private bool GetCanRead()
	{
		if (BaseStream != null)
		{
			return BaseStream.CanRead;
		}
		return false;
	}

	private bool GetCanSeek()
	{
		if (BaseStream != null)
		{
			return BaseStream.CanSeek;
		}
		return false;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		if (BaseStream == null)
		{
			throw new ObjectDisposedException("BaseStream");
		}
		int num = 0;
		lock (BaseStream)
		{
			long position = BaseStream.Position;
			if (count > buffer.Length)
			{
				throw new ArgumentException();
			}
			if (count + _Offset > BaseStream.Length)
			{
				count = (int)(BaseStream.Length - _Offset);
			}
			else if (count + _Position > _Length)
			{
				count = (int)(_Length - _Position);
			}
			if (count <= 0)
			{
				return 0;
			}
			try
			{
				BaseStream.Position = _Offset + _Position;
				num = BaseStream.Read(buffer, offset, count);
			}
			finally
			{
				BaseStream.Position = position;
				_Position += num;
			}
		}
		return num;
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		if (BaseStream == null)
		{
			throw new ObjectDisposedException("BaseStream");
		}
		long num = origin switch
		{
			SeekOrigin.Begin => offset, 
			SeekOrigin.Current => _Position + offset, 
			SeekOrigin.End => _Length - offset, 
			_ => _Position, 
		};
		if (num < 0)
		{
			throw new ArgumentException();
		}
		if (num > _Length)
		{
			num = _Length;
		}
		_Position = num;
		return _Position;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		BaseStream = null;
	}

	public override void SetLength(long value)
	{
		throw new NotImplementedException();
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		throw new NotImplementedException();
	}

	public override void Flush()
	{
		throw new NotImplementedException();
	}
}
