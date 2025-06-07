using System.Collections;

namespace MakeCodeImageParserV3
{
    internal class ChunkedBitstream : IList<uint>, ICollection<uint>, IEnumerable<uint>, IEnumerable
    {
        private List<uint> chunks;

        private byte bits;
        private uint dataMask;

        public ChunkedBitstream(int bitsPerChunk)
        {
            chunks = new List<uint>();
            if ((bitsPerChunk < 1) || (bitsPerChunk > 32)) throw new ArgumentException("Bits per chunk must be between 1 and 32.");
            this.bits = (byte)bitsPerChunk;
            dataMask = (0xFFFFFFFF >> (32 - bitsPerChunk));
        }
        
        // #FIXME make sure stream length is multiple of bits per chunk?
        public ChunkedBitstream(Bitstream stream, int bitsPerChunk) : this(bitsPerChunk)
        {
            for(int i = 0; i < stream.Count / bitsPerChunk; i++)
            {
                Add(stream.GetData(i * bitsPerChunk, bitsPerChunk));
            }
        }
        public ChunkedBitstream(byte[] data, int bitsPerChunk) : this(new Bitstream(data), bitsPerChunk) { }
        public ChunkedBitstream(byte[] data, int bitsPerChunk, long totalBits) : this(new Bitstream(data, totalBits), bitsPerChunk) { }

        public byte Bits => bits;
        public int Count => chunks.Count;
        public uint DataMask => dataMask;
        public int TotalBits => chunks.Count * bits;

        public bool IsReadOnly => false;

        public uint this[int index] { get => GetChunk(index); set => SetChunk(index, value); }

        public byte GetBit(int bitIndex) { return GetBit(bitIndex / bits, bitIndex % bits); }
        public byte GetBit(int chunkIndex, int bitIndex)
        {
            return (byte)((chunks[chunkIndex] >> bitIndex) & 1);
        }

        // assumes bitValue is 1 or 0.
        public void SetBit(int bitIndex, byte bitValue) { SetBit(bitIndex / bits, bitIndex % bits, bitValue); }
        public void SetBit(int chunkIndex, int bitIndex, byte bitValue)
        {
            if (bitValue > 0)
                chunks[chunkIndex] = (byte)(chunks[chunkIndex] | (1U << bitIndex));
            else if (bitValue < 2)
                chunks[chunkIndex] = (byte)(chunks[chunkIndex] & ~(1U << bitIndex));
            else
                new ArgumentException("bitValue must be 0 or 1."); //#FIXME really throw? we could just not do the second if and assume.
        }

        public uint GetChunk(int chunkIndex)
        {
            return chunks[chunkIndex];
        }

        public void SetChunk(int chunkIndex, uint value)
        {
            chunks[chunkIndex] = CutByte(value);
        }

        public void Add(uint addition)
        {
            chunks.Add(CutByte(addition));
        }

        public bool Remove(uint item)
        {
            return chunks.Remove(item);
        }

        public uint CutByte(uint input)
        {
            return (input & dataMask);
        }

        public void SetSize(int targetSize)
        {
            int diff = targetSize - chunks.Count;
            if(diff > 0)
                Expand(diff);
            else if (diff < 0)
                Shrink(-diff);
        }

        public void Expand(int sizeIncrease)
        {
            if (sizeIncrease <= 0) return;
            chunks.AddRange(new uint[sizeIncrease]);
        }

        public void Shrink(int sizeDecrease)
        {
            if (sizeDecrease <= 0) return; 
            int startIndex = chunks.Count - sizeDecrease;
            if (startIndex < 0) startIndex = 0;
            chunks.RemoveRange(startIndex, sizeDecrease);
        }

        public Bitstream ToBitstream()
        {
            Bitstream bs = new();
            for(int i = 0; i < chunks.Count; i++)
            {
                bs.Add(chunks[i], bits);
            }
            return bs;
        }

        // #FIXME slow and inefficient, but simple. Optimize if it becomes an issue.
        public byte[] GetCompressedData()
        {
            return ToBitstream().ToByteArray();
        }

        public int IndexOf(uint chunk)
        {
            return chunks.IndexOf(chunk);
        }

        public void Insert(int index, uint chunk)
        {
            chunks.Insert(index, chunk);
        }

        public void RemoveAt(int index)
        {
            chunks.RemoveAt(index);
        }


        public void Clear()
        {
            chunks.Clear();
        }

        public bool Contains(uint item)
        {
            return chunks.Contains(item);
        }

        public void CopyTo(uint[] array, int arrayIndex)
        {
            chunks.CopyTo(array, arrayIndex);
        }


        public IEnumerator<uint> GetEnumerator()
        {
            return chunks.GetEnumerator();
        }

        // huh?
        IEnumerator IEnumerable.GetEnumerator()
        {
            return chunks.GetEnumerator();
        }
    }
}
