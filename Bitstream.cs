
using System;
using System.Reflection;

namespace MakeCodeImageParserV3
{
    internal class Bitstream
    {
        private const int growFactor = 2;
        private const int shrinkFactor = 4;
        private const int startSize = 16;
 
        private uint[] data;
        private long totalBits;

        public Bitstream()
        {
            data = new uint[startSize];
            totalBits = 0;
        }

        public Bitstream(byte[] data) : this(data, data.Length * 8) { }
        public Bitstream(byte[] data, long totalBits) : this()
        {
            for (int i = 0; i < totalBits / 8; i++)
            {
                Add(data[i], 8);
            }

            int remainingBits = (int)(totalBits - this.totalBits);
            if (remainingBits > 0)
            {
                Add(data[data.Length - 1], remainingBits);
            }
        }

        public long TotalBits => totalBits;
        public long BitCapacity => ((long)data.Length * 32);


        public byte GetShortData(int index, int bitCount) { return GetShortData((long)index, bitCount); }
        public byte GetShortData(long index, int bitCount)
        {
            if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException();

            return (byte)GetData(index, bitCount);
        }

        public uint GetData(int index, int bitCount) { return GetData((long)index, bitCount); }

        public uint GetData(long index, int bitCount)
        {
            if (bitCount < 1 || bitCount > 32 || index < 0 || index + bitCount > totalBits)
                throw new ArgumentOutOfRangeException("index: " + index + ", bitCount: " + bitCount + ", totalBits: " + totalBits);

            int chunk1 = (int)(index / 32);
            int offset1 = (int)(index % 32);

            int chunk2 = (int)((index + bitCount - 1) / 32);
            int offset2 = (int)((index + bitCount - 1) % 32);

            uint mask = (bitCount == 32) ? 0xFFFFFFFFu : (1U << bitCount) - 1;

            if (chunk1 == chunk2)
            {
                uint rdata = data[chunk1] >> offset1;
                return (rdata & mask);
            }
            else
            {
                uint rdata1 = data[chunk1] >> offset1;
                uint rdata2 = data[chunk2] & ((1U << (offset2 + 1)) - 1);
                return ((rdata2 << (32 - offset1)) | rdata1) & mask;
            }
        }

        public void SetShortData(int index, int bitCount, byte element) { SetData((long)index, bitCount, element); }
        public void SetShortData(long index, int bitCount, byte element)
        {
            if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException();

            SetData(index, bitCount, element); 
        }

        public void SetData(int index, int bitCount, uint element) { SetData((long)index, bitCount, element); }
        public void SetData(long index, int bitCount, uint element)
        {
            if (bitCount < 1 || bitCount > 32 || index < 0 || index + bitCount > BitCapacity)
                throw new ArgumentOutOfRangeException($"bitCount ({bitCount}) should be [1, 32] and index ({index}) plus bitCount ({bitCount}) should be [0, {BitCapacity}]");

            int chunk1 = (int)(index / 32);
            int offset1 = (int)(index % 32);

            int chunk2 = (int)((index + (long)bitCount - 1) / 32);
            int offset2 = (int)((index + (long)bitCount - 1) % 32);

            if (chunk1 == chunk2)
            {
                if(bitCount == 32)
                {
                    // just copy over element on edge case where entire chunk is being replaced.
                    data[chunk1] = element;
                }
                else
                {
                    uint mask = ((1U << bitCount) - 1U) << offset1;
                    data[chunk1] &= ~mask;
                    data[chunk1] |= (element << offset1) & mask;
                }

            }
            else
            {
                uint mask1 = ~0U << offset1;
                data[chunk1] &= ~mask1;
                data[chunk1] |= (element << offset1) & mask1;

                uint mask2 = (1U << (offset2 + 1)) - 1;
                data[chunk2] &= ~mask2;
                data[chunk2] |= (element >> (32 - offset1)) & mask2;

            }
        }

        public void Add(byte data, int bitCount) { Add((uint)data, bitCount); }
        public void Add(int data, int bitCount) { Add((uint)data, bitCount); }
        public void Add(uint data, int bitCount)
        {
            if(totalBits + bitCount > BitCapacity)
            {
                Grow();
            }

            SetData(totalBits, bitCount, data);
            totalBits += bitCount;
        }

        public void AddReversed(byte data, int bitCount) { AddReversed((uint)data, bitCount); }
        public void AddReversed(int data, int bitCount) { AddReversed((uint)data, bitCount); }
        public void AddReversed(uint data, int bitCount)
        {
            Add(ReverseBits(data, bitCount), bitCount);
        }

        public void Remove(int bitCount) { Remove((long)bitCount); }
        public void Remove(long bitCount)
        {
            if (bitCount < 0 || bitCount > totalBits)
                throw new ArgumentOutOfRangeException();

            totalBits -= bitCount;
            if((totalBits * shrinkFactor < BitCapacity) && (data.Length > startSize))
            {
                Shrink();
            }
        }

        public void Append(Bitstream other)
        {
            long remainder = other.totalBits % 32;
            for(long i = 0; i < other.totalBits / 32; i++)
            {
                Add(other.GetData(i * 32, 32), 32);
            }
            if(remainder > 0)
            {
                Add(other.GetData(32 * (other.totalBits / 32), (int)remainder), (int)remainder);
            }
        }

        public Bitstream TakeSegment(long index, long transferBits)
        {
            Bitstream segment = new Bitstream();
            long remainder = transferBits % 32;
            for (long i = 0; i < transferBits / 32; i++)
            {
                segment.Add(GetData(i * 32 + index, 32), 32);
            }
            if (remainder > 0)
            {
                segment.Add(GetData(index + 32 * (transferBits / 32), (int)remainder), (int)remainder);
            }
            return segment;
        }

        private void Grow()
        {
            uint[] newData = new uint[data.Length * growFactor];
            Array.Copy(data, newData, data.Length);
            data = newData;
        }

        private void Shrink()
        {
            uint[] newData = new uint[data.Length / shrinkFactor];
            Array.Copy(data, newData, newData.Length);
            data = newData;
        }
        
        public void SetSize(long desiredBitSlots)
        {
            while(BitCapacity < desiredBitSlots)
            {
                Grow();
            }

            // just set to their desired. Assume anything beyond is fake.
            totalBits = desiredBitSlots;

            if ((totalBits * shrinkFactor < BitCapacity) && (data.Length > startSize))
            {
                Shrink();
            }
        }

        public void Clear()
        {
            totalBits = 0;
            data = new uint[startSize];
        }

        // #FIXME this should pad up to 7 bits to make the last byte full
        public byte[] ToByteArray()
        {
            List<byte> bytes = new();
            
            // add all full chunks
            for(int i = 0; i < totalBits / 32; i++)
            {
                bytes.Add((byte)(data[i]));
                bytes.Add((byte)(data[i] >> 8));
                bytes.Add((byte)(data[i] >> 16));
                bytes.Add((byte)(data[i] >> 24));
            }

            int remaining = (int)(totalBits % 32);

            // if we end on a non full byte, pad with some zeros.
            int bitsMissing = 8 - (int)(totalBits % 8);
            if(bitsMissing > 0)
                Add(0, bitsMissing);

            remaining = (int)(totalBits % 32);
            // add all full bytes left in unfilled chunk
            for (int i = 0; i < remaining / 8; i++)
            {
                bytes.Add((byte)(data[(totalBits / 32)] >> (8 * i)));
            }

            // add last bits in unfull byte. Should be impossible to reach with new padding code.
            remaining = (int)(totalBits % 8);
            if (remaining != 0) throw new Exception("implement this later.");

            return bytes.ToArray();
        }

        // one byte per bit.
        public byte[] ToLargeByteArray()
        {
            byte[] bytes = new byte[totalBits];
            for(long i = 0; i < totalBits; i++)
            {
                bytes[i] = GetShortData(i, 1);
            }
            return bytes;
        }

        public override string ToString()
        {
            string output = "";
            for(long i = 0; i < totalBits; i++)
                if (GetShortData(i, 1) > 0) output += '1';
                else output += "0";
            return output;
        }

        private uint ReverseBits(uint value, int bitCount)
        {
            uint reversed = 0;
            for (int i = 0; i < bitCount; i++)
            {
                reversed <<= 1;
                reversed |= (value >> i) & 1;
            }
            return reversed;
        }

        //#FIXME add GetBit (true/false)
    }
}
