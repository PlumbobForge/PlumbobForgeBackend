using System;
using System.Collections.Generic;

namespace Tiger;

internal class DBPFCompression
{
	private interface IMatchtracker
	{
		bool FindMatch(out int where);

		bool Nextmatch(out int where);

		void Addvalue(byte val);

		void Reset();
	}

	private class SingledepthMatchTracker : IMatchtracker
	{
		private uint mRunningValue;

		private int mInterval;

		private int mRollingInterval;

		private int mQueueLength;

		private uint[] mPendingValues;

		private int mPendingOffset;

		private int mDataLength;

		private int mWindowStart;

		private bool mInitialized;

		private uint[] mInsertedValues;

		private int mInsertLocation;

		private Dictionary<uint, int> mLookupTable = new Dictionary<uint, int>();

		public SingledepthMatchTracker(int blockinterval, int lookupstart, int windowlength)
		{
			mInterval = blockinterval;
			if (lookupstart > 0)
			{
				mPendingValues = new uint[lookupstart / blockinterval];
				mQueueLength = mPendingValues.Length * blockinterval;
			}
			else
			{
				mQueueLength = 0;
			}
			mInsertedValues = new uint[windowlength / blockinterval - lookupstart / blockinterval];
			mWindowStart = -(mInsertedValues.Length + lookupstart / blockinterval) * blockinterval - 4;
		}

		public void Reset()
		{
			mLookupTable.Clear();
			mRunningValue = 0u;
			mRollingInterval = 0;
			mWindowStart = -(mInsertedValues.Length + ((mPendingValues != null) ? mPendingValues.Length : 0)) * mInterval - 4;
			mDataLength = 0;
			mInitialized = false;
			mInsertLocation = 0;
			mPendingOffset = 0;
		}

		public bool Nextmatch(out int where)
		{
			where = 0;
			return false;
		}

		public void Addvalue(byte val)
		{
			if (mInitialized)
			{
				mRollingInterval++;
				if (mRollingInterval == mInterval)
				{
					mRollingInterval = 0;
					if (mWindowStart >= 0)
					{
						if (mInsertLocation == mInsertedValues.Length)
						{
							mInsertLocation = 0;
						}
						uint key = mInsertedValues[mInsertLocation];
						if (mLookupTable.TryGetValue(key, out var value) && value == mWindowStart)
						{
							mLookupTable.Remove(key);
						}
					}
					if (mPendingValues != null)
					{
						if (mDataLength > mQueueLength + 4)
						{
							uint num = mPendingValues[mPendingOffset];
							mInsertedValues[mInsertLocation] = num;
							mInsertLocation++;
							if (mInsertLocation > mInsertedValues.Length)
							{
								mInsertLocation = 0;
							}
							mLookupTable[num] = mDataLength - mQueueLength - 4;
						}
						mPendingValues[mPendingOffset] = mRunningValue;
						mPendingOffset++;
						if (mPendingOffset == mPendingValues.Length)
						{
							mPendingOffset = 0;
						}
					}
					else
					{
						mInsertedValues[mInsertLocation] = mRunningValue;
						mInsertLocation++;
						if (mInsertLocation > mInsertedValues.Length)
						{
							mInsertLocation = 0;
						}
						mLookupTable[mRunningValue] = mDataLength - 4;
					}
				}
			}
			else
			{
				mRollingInterval++;
				if (mRollingInterval == mInterval)
				{
					mRollingInterval = 0;
				}
				mInitialized = mDataLength == 3;
			}
			mRunningValue = (mRunningValue << 8) | val;
			mDataLength++;
			mWindowStart++;
		}

		public bool FindMatch(out int where)
		{
			return mLookupTable.TryGetValue(mRunningValue, out where);
		}
	}

	private class DeepMatchTracker : IMatchtracker
	{
		private int mBucketDepth;

		private uint mRunningValue;

		private int mInterval;

		private int mRollingInterval;

		private int mQueueLength;

		private uint[] mPendingValues;

		private int mPendingOffset;

		private int mDataLength;

		private int mWindowStart;

		private bool mInitialized;

		private uint[] mInsertedValues;

		private int mInsertLocation;

		private Dictionary<uint, List<int>> mLookupTable = new Dictionary<uint, List<int>>();

		private Stack<List<int>> mUnusedLists = new Stack<List<int>>();

		private List<int> mCurrentMatch;

		private int mCurrentMatchIndex;

		public DeepMatchTracker(int blockinterval, int lookupstart, int windowlength, int bucketdepth)
		{
			mInterval = blockinterval;
			if (lookupstart > 0)
			{
				mPendingValues = new uint[lookupstart / blockinterval];
				mQueueLength = mPendingValues.Length * blockinterval;
			}
			else
			{
				mQueueLength = 0;
			}
			mInsertedValues = new uint[windowlength / blockinterval - lookupstart / blockinterval];
			mWindowStart = -(mInsertedValues.Length + lookupstart / blockinterval) * blockinterval - 4;
			mBucketDepth = bucketdepth;
		}

		public void Reset()
		{
			mLookupTable.Clear();
			mRunningValue = 0u;
			mRollingInterval = 0;
			mWindowStart = -(mInsertedValues.Length + ((mPendingValues != null) ? mPendingValues.Length : 0)) * mInterval - 4;
			mDataLength = 0;
			mInitialized = false;
			mInsertLocation = 0;
			mPendingOffset = 0;
			mCurrentMatch = null;
		}

		public void Addvalue(byte val)
		{
			if (mInitialized)
			{
				mRollingInterval++;
				if (mRollingInterval == mInterval)
				{
					mRollingInterval = 0;
					List<int> value;
					if (mWindowStart > 0)
					{
						if (mInsertLocation == mInsertedValues.Length)
						{
							mInsertLocation = 0;
						}
						uint key = mInsertedValues[mInsertLocation];
						if (mLookupTable.TryGetValue(key, out value) && value[0] == mWindowStart)
						{
							value.RemoveAt(0);
							if (value.Count == 0)
							{
								mLookupTable.Remove(key);
								mUnusedLists.Push(value);
							}
						}
					}
					if (mPendingValues != null)
					{
						if (mDataLength > mQueueLength + 4)
						{
							uint num = mPendingValues[mPendingOffset];
							mInsertedValues[mInsertLocation] = num;
							mInsertLocation++;
							if (mInsertLocation > mInsertedValues.Length)
							{
								mInsertLocation = 0;
							}
							if (mLookupTable.TryGetValue(num, out value))
							{
								if (value.Count == mBucketDepth)
								{
									value.RemoveAt(0);
								}
							}
							else
							{
								value = ((mUnusedLists.Count <= 0) ? new List<int>() : mUnusedLists.Pop());
								mLookupTable[num] = value;
							}
							value.Add(mDataLength - mQueueLength - 4);
						}
						mPendingValues[mPendingOffset] = mRunningValue;
						mPendingOffset++;
						if (mPendingOffset == mPendingValues.Length)
						{
							mPendingOffset = 0;
						}
					}
					else
					{
						mInsertedValues[mInsertLocation] = mRunningValue;
						mInsertLocation++;
						if (mInsertLocation > mInsertedValues.Length)
						{
							mInsertLocation = 0;
						}
						if (mLookupTable.TryGetValue(mRunningValue, out value))
						{
							if (value.Count == mBucketDepth)
							{
								value.RemoveAt(0);
							}
						}
						else
						{
							value = ((mUnusedLists.Count <= 0) ? new List<int>() : mUnusedLists.Pop());
							mLookupTable[mRunningValue] = value;
						}
						value.Add(mDataLength - 4);
					}
				}
			}
			else
			{
				mRollingInterval++;
				if (mRollingInterval == mInterval)
				{
					mRollingInterval = 0;
				}
				mInitialized = mDataLength == 3;
			}
			mRunningValue = (mRunningValue << 8) | val;
			mDataLength++;
			mWindowStart++;
		}

		public bool Nextmatch(out int where)
		{
			if (mCurrentMatch != null && mCurrentMatchIndex < mCurrentMatch.Count)
			{
				where = mCurrentMatch[mCurrentMatchIndex];
				mCurrentMatchIndex++;
				return true;
			}
			where = -1;
			return false;
		}

		public bool FindMatch(out int where)
		{
			if (mLookupTable.TryGetValue(mRunningValue, out mCurrentMatch))
			{
				mCurrentMatchIndex = 1;
				where = mCurrentMatch[0];
				return true;
			}
			mCurrentMatch = null;
			where = -1;
			return false;
		}
	}

	private int mBruteForceLength;

	private IMatchtracker mTracker;

	private byte[] mData;

	private int mSequenceSource;

	private int mSequenceLength;

	private int mSequenceDest;

	private bool mSequenceFound;

	public DBPFCompression(int level)
	{
		mTracker = CreateTracker(level, out mBruteForceLength);
	}

	public DBPFCompression(int blockinterval, int lookupstart, int windowlength, int bucketdepth, int bruteforcelength)
	{
		mTracker = CreateTracker(blockinterval, lookupstart, windowlength, bucketdepth);
		mBruteForceLength = bruteforcelength;
	}

	public static bool Compress(byte[] data, out byte[] compressed)
	{
		Tiger.DBPFCompression dBPFCompression = new Tiger.DBPFCompression(5);
		compressed = dBPFCompression.Compress(data);
		return compressed != null;
	}

	public static bool Compress(byte[] data, out byte[] compressed, int level)
	{
		Tiger.DBPFCompression dBPFCompression = new Tiger.DBPFCompression(level);
		compressed = dBPFCompression.Compress(data);
		return compressed != null;
	}

	public byte[] Compress(byte[] data)
	{
		bool flag = false;
		List<byte[]> list = new List<byte[]>();
		int num = 0;
		int num2 = 0;
		if (data.Length < 16 || data.LongLength > uint.MaxValue)
		{
			return null;
		}
		mData = data;
		try
		{
			int num3 = 0;
			while (num < data.Length)
			{
				byte[] array;
				if (data.Length - num < 4)
				{
					array = new byte[data.Length - num + 1];
					array[0] = (byte)(0xFC | (data.Length - num));
					Array.Copy(data, num, array, 1, data.Length - num);
					list.Add(array);
					num += array.Length - 1;
					num2 += array.Length;
					flag = true;
					continue;
				}
				while (num > num3 - 3)
				{
					mTracker.Addvalue(data[num3++]);
				}
				mSequenceSource = 0;
				mSequenceLength = 0;
				mSequenceDest = int.MaxValue;
				mSequenceFound = false;
				do
				{
					for (int i = 0; i < 4; i++)
					{
						if (num3 < data.Length)
						{
							mTracker.Addvalue(data[num3++]);
						}
						FindSequence(num3 - 4);
					}
				}
				while (!mSequenceFound && num3 + 4 <= data.Length);
				if (!mSequenceFound)
				{
					mSequenceDest = mData.Length;
				}
				while (mSequenceDest - num >= 4)
				{
					int num4 = (mSequenceDest - num) & -4;
					if (num4 > 112)
					{
						num4 = 112;
					}
					array = new byte[num4 + 1];
					array[0] = (byte)(0xE0 | ((num4 >> 2) - 1));
					Array.Copy(data, num, array, 1, num4);
					list.Add(array);
					num += num4;
					num2 += array.Length;
				}
				if (!mSequenceFound)
				{
					continue;
				}
				array = null;
				while (mSequenceLength > 0)
				{
					int num5 = mSequenceLength;
					if (num5 > 1028)
					{
						num5 = 1028;
					}
					mSequenceLength -= num5;
					int num6 = mSequenceDest - mSequenceSource - 1;
					int num7 = mSequenceDest - num;
					mSequenceSource += num5;
					mSequenceDest += num5;
					if (num5 > 67 || num6 > 16383)
					{
						array = new byte[num7 + 4];
						array[0] = (byte)(0xC0 | num7 | ((num5 - 5 >> 6) & 0xC) | ((num6 >> 12) & 0x10));
						array[1] = (byte)((num6 >> 8) & 0xFF);
						array[2] = (byte)(num6 & 0xFF);
						array[3] = (byte)((num5 - 5) & 0xFF);
					}
					else if (num5 > 10 || num6 > 1023)
					{
						array = new byte[num7 + 3];
						array[0] = (byte)(0x80 | ((num5 - 4) & 0x3F));
						array[1] = (byte)(((num7 << 6) & 0xC0) | ((num6 >> 8) & 0x3F));
						array[2] = (byte)(num6 & 0xFF);
					}
					else
					{
						array = new byte[num7 + 2];
						array[0] = (byte)((num7 & 3) | ((num5 - 3 << 2) & 0x1C) | ((num6 >> 3) & 0x60));
						array[1] = (byte)(num6 & 0xFF);
					}
					if (num7 > 0)
					{
						Array.Copy(data, num, array, array.Length - num7, num7);
					}
					list.Add(array);
					num += num5 + num7;
					num2 += array.Length;
				}
			}
			if (num2 + 6 < data.Length)
			{
				byte[] array2;
				int num8;
				if (data.Length > 16777215)
				{
					array2 = new byte[num2 + 6 + ((!flag) ? 1 : 0)];
					array2[0] = 144;
					array2[1] = 251;
					array2[2] = (byte)(data.Length >> 24);
					array2[3] = (byte)(data.Length >> 16);
					array2[4] = (byte)(data.Length >> 8);
					array2[5] = (byte)data.Length;
					num8 = 6;
				}
				else
				{
					array2 = new byte[num2 + 5 + ((!flag) ? 1 : 0)];
					array2[0] = 16;
					array2[1] = 251;
					array2[2] = (byte)(data.Length >> 16);
					array2[3] = (byte)(data.Length >> 8);
					array2[4] = (byte)data.Length;
					num8 = 5;
				}
				for (int i = 0; i < list.Count; i++)
				{
					Array.Copy(list[i], 0, array2, num8, list[i].Length);
					num8 += list[i].Length;
				}
				if (!flag)
				{
					array2[array2.Length - 1] = 252;
				}
				return array2;
			}
			return null;
		}
		finally
		{
			mData = null;
			mTracker.Reset();
		}
	}

	private void FindSequence(int startindex)
	{
		int num = -mBruteForceLength;
		if (startindex < mBruteForceLength)
		{
			num = -startindex;
		}
		byte b = mData[startindex];
		int num2 = -1;
		while (num2 >= num && mSequenceLength < 1028)
		{
			byte b2 = mData[num2 + startindex];
			if (b2 == b)
			{
				int num3 = FindRunLength(startindex + num2, startindex);
				if (num3 > mSequenceLength && num3 >= 3 && (num3 >= 4 || num2 > -1024) && (num3 >= 5 || num2 > -16384))
				{
					mSequenceFound = true;
					mSequenceSource = startindex + num2;
					mSequenceLength = num3;
					mSequenceDest = startindex;
				}
			}
			num2--;
		}
		if (mSequenceLength >= 1028 || !mTracker.FindMatch(out var where))
		{
			return;
		}
		do
		{
			int num3 = FindRunLength(where, startindex);
			if (num3 >= 5)
			{
				mSequenceFound = true;
				mSequenceSource = where;
				mSequenceLength = num3;
				mSequenceDest = startindex;
			}
		}
		while (mSequenceLength < 1028 && mTracker.Nextmatch(out where));
	}

	private int FindRunLength(int src, int dst)
	{
		int num = src + 1;
		int i;
		for (i = dst + 1; i < mData.Length && mData[num] == mData[i] && i - dst < 1028; i++)
		{
			num++;
		}
		return i - dst;
	}

	private static IMatchtracker CreateTracker(int blockinterval, int lookupstart, int windowlength, int bucketdepth)
	{
		if (bucketdepth <= 1)
		{
			return new SingledepthMatchTracker(blockinterval, lookupstart, windowlength);
		}
		return new DeepMatchTracker(blockinterval, lookupstart, windowlength, bucketdepth);
	}

	private static IMatchtracker CreateTracker(int level, out int bruteforcelength)
	{
		switch (level)
		{
		case 0:
			bruteforcelength = 0;
			return CreateTracker(4, 0, 16384, 1);
		case 1:
			bruteforcelength = 0;
			return CreateTracker(2, 0, 32768, 1);
		case 2:
			bruteforcelength = 0;
			return CreateTracker(1, 0, 65536, 1);
		case 3:
			bruteforcelength = 0;
			return CreateTracker(1, 0, 131000, 2);
		case 4:
			bruteforcelength = 16;
			return CreateTracker(1, 16, 131000, 2);
		case 5:
			bruteforcelength = 16;
			return CreateTracker(1, 16, 131000, 5);
		case 6:
			bruteforcelength = 32;
			return CreateTracker(1, 32, 131000, 5);
		case 7:
			bruteforcelength = 32;
			return CreateTracker(1, 32, 131000, 10);
		case 8:
			bruteforcelength = 64;
			return CreateTracker(1, 64, 131000, 10);
		case 9:
			bruteforcelength = 128;
			return CreateTracker(1, 128, 131000, 20);
		default:
			return CreateTracker(5, out bruteforcelength);
		}
	}
}
