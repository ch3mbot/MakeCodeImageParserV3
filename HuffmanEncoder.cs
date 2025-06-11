using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace MakeCodeImageParserV3
{
    internal class HuffmanNode
    {
        public uint? Symbol;
        public int Frequency;
        public HuffmanNode Left, Right;

        public bool IsLeaf => Symbol.HasValue;
    }

    internal static class HuffmanEncoder
    {

        public static Dictionary<uint, int> BuildFrequencyTable(ChunkedBitstream[] data)
        {
            int bitsPerChunk = data[0].BitsPerChunk;

            var freq = new Dictionary<uint, int>();

            if (bitsPerChunk < 1 || bitsPerChunk > 31) throw new ArgumentException("bitsPerChunk must be between 1 and 32.");

            uint mask = uint.MaxValue >> 1;
            if(bitsPerChunk < 31)
            {
                mask = (uint)((1UL << bitsPerChunk) - 1);
            }

            freq[uint.MaxValue >> 1] = 0;

            foreach (ChunkedBitstream stream in data)
            {
                for(int i = 0; i < stream.ChunkCount; i++)
                {
                    uint masked = stream[i] & mask;
                    if (!freq.ContainsKey(masked))
                        freq[masked] = 0;
                    freq[masked]++;
                }

                // Add one instance of null terminator
                freq[uint.MaxValue >> 1]++;
            }

            return freq;
        }

        public static HuffmanNode BuildHuffmanTree(Dictionary<uint, int> frequencies)
        {
            var pq = new PriorityQueue<HuffmanNode, int>();

            foreach (var kvp in frequencies)
            {
                HuffmanNode newNode = new HuffmanNode
                {
                    Symbol = kvp.Key,
                    Frequency = kvp.Value
                };

                pq.Enqueue(newNode, newNode.Frequency);
            }

            while (pq.Count > 1)
            {
                HuffmanNode left = pq.Dequeue();
                HuffmanNode right = pq.Dequeue();
                HuffmanNode parent = new HuffmanNode
                {
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };

                pq.Enqueue(parent, parent.Frequency);
            }

            return pq.Dequeue();
        }

        public static void GenerateCodes(HuffmanNode node, Dictionary<uint, (uint code, int length)> codes, uint code = 0, int length = 0)
        {
            if (node.IsLeaf)
            {
                codes[node.Symbol.Value] = (code, length);
                return;
            }

            if (node.Left != null)
                GenerateCodes(node.Left, codes, (code << 1) | 0, length + 1);
            if (node.Right != null)
                GenerateCodes(node.Right, codes, (code << 1) | 1, length + 1);
        }

        public static Bitstream EncodeSingleStream(ChunkedBitstream data, Dictionary<uint, (uint code, int length)> codes)
        {
            int bitsPerChunk = data.BitsPerChunk;

            Bitstream output = new Bitstream();

            uint mask = uint.MaxValue >> 1;
            if (bitsPerChunk < 31)
            {
                mask = (uint)((1UL << bitsPerChunk) - 1);
            }

            output.Add((byte)data.ChunkCount, 8);
            for (int i = 0; i < data.ChunkCount; i++) 
            {
                uint masked = data[i] & mask;
                (uint code, int length) = codes[masked];
                output.AddReversed(code, length);
            }

            // Add null terminator
            (uint terminatorCode, int terminatorLength) = codes[uint.MaxValue >> 1];
            output.Add(terminatorCode, terminatorLength);

            return output;
        }

        public static Bitstream EncodeStreamArray(ChunkedBitstream[] data, Dictionary<uint, (uint code, int length)> codes)
        {
            int bitsPerChunk = data[0].BitsPerChunk;

            Bitstream output = new Bitstream();
            uint mask = uint.MaxValue >> 1;
            if (bitsPerChunk < 31)
            {
                mask = (uint)((1UL << bitsPerChunk) - 1);
            }

            for(int s = 0; s < data.Length; s++)
            {
                output.Append(EncodeSingleStream(data[s], codes));
                // #FIXME remove if bad
                /*
                output.Add((byte)data[s].ChunkCount, 8);
                for (int i = 0; i < data[s].ChunkCount; i++)
                {
                    uint masked = data[s][i] & mask;
                    (uint code, int length) = codes[masked];
                    output.Add(code, length);
                }
                */
            }

            return output;
        }

        public static Dictionary<uint, int> GetCodeLengths(HuffmanNode root)
        {
            var lengths = new Dictionary<uint, int>();
            void Traverse(HuffmanNode node, int depth)
            {
                if (node.IsLeaf)
                {
                    lengths[node.Symbol.Value] = depth;
                    return;
                }

                if (node.Left != null) Traverse(node.Left, depth + 1);
                if (node.Right != null) Traverse(node.Right, depth + 1);
            }

            Traverse(root, 0);
            return lengths;
        }

        public static Dictionary<uint, (uint code, int length)> AssignCanonicalCodes(Dictionary<uint, int> lengths)
        {
            // Sort symbols first by length, then by symbol value
            var sortedSymbols = lengths.OrderBy(p => p.Value).ThenBy(p => p.Key).ToList();
            var result = new Dictionary<uint, (uint code, int length)>();

            uint code = 0;
            int prevLen = 0;

            foreach (var (symbol, length) in sortedSymbols)
            {
                code <<= (length - prevLen); // Shift if length increases
                result[symbol] = (code, length);
                code++;
                prevLen = length;
            }

            return result;
        }

        public static Bitstream WriteCodebook(Dictionary<uint, int> lengths)
        {
            Bitstream output = new Bitstream();

            // 32 bits for number of lengths
            //writer.Write(lengths.Count);
            output.Add(lengths.Count, 32);

            foreach (var kvp in lengths.OrderBy(k => k.Key))
            {
                //writer.Write(kvp.Key);     // 32-bit symbol
                output.Add(kvp.Key, 32);
                //writer.Write((byte)kvp.Value); // Length: 1 byte is enough (Huffman trees rarely exceed depth 32)
                output.Add(kvp.Value, 8);
            }

            return output;
        }

        public static Dictionary<uint, int> ReadCodebook(Bitstream bs)
        {
            //int count = reader.ReadInt32();

            long index = 0;
            uint count = bs.GetData(index, 32);
            index += 32;

            Dictionary<uint, int> lengths = new();
            for (int i = 0; i < count; i++)
            {
                //uint symbol = reader.ReadUInt32();
                uint symbol = bs.GetData(index, 32);
                index += 32;
                //byte length = reader.ReadByte();
                byte length = bs.GetShortData(index, 8);
                index += 8;
                lengths[symbol] = length;
            }
            return lengths;
        }

        //#FIXME add variable terminator symbol?
        public static ChunkedBitstream Decode(Bitstream input, Dictionary<uint, (uint code, int length)> encoding, int bitsPerChunk, out long finalOffset, long offset = 0)
        {
            // Build reverse lookup table: (code, length) → symbol
            var reverse = encoding.ToDictionary(kvp => (kvp.Value.code, kvp.Value.length), kvp => kvp.Key);
            int maxLen = encoding.Values.Max(c => c.length);

            ChunkedBitstream result = new ChunkedBitstream(bitsPerChunk);
            int bitBuffer = 0, bitCount = 0;

            // offset some number of bits.
            long index = offset;

            // read header for count in this run
            byte valueCount = input.GetShortData(offset, 8);
            index += 8;

            while (true)
            {
                bitBuffer = (bitBuffer << 1) | input.GetShortData(index, 1);
                bitCount++;
                index++;

                if (bitCount > maxLen) continue;

                uint maskedCode = (uint)(bitBuffer & ((1 << bitCount) - 1));
                var key = (maskedCode, bitCount);

                if (reverse.TryGetValue(key, out uint symbol))
                {
                    if (symbol == (uint.MaxValue >> 1))
                        break;

                    result.Add(symbol);
                    bitBuffer = 0;
                    bitCount = 0;
                }
            }


            finalOffset = index;

            return result;
        }
    }
}
