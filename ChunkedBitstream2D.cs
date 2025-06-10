using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace MakeCodeImageParserV3
{
    internal class ChunkedBitstream2D
    {
        private ChunkedBitstream[] lines;
        private int lineCount;
        private int lineLength;

        private byte bitsPerChunk;
        private uint dataMask;


        public ChunkedBitstream2D(int bitsPerChunk, int lineCount, int lineLength)
        {
            if ((bitsPerChunk < 1) || (bitsPerChunk > 32)) throw new ArgumentException("Bits per chunk must be between 1 and 32.");

            lines = new ChunkedBitstream[lineCount];
            this.lineCount = lineCount;
            this.lineLength = lineLength;

            this.bitsPerChunk = (byte)bitsPerChunk;
            dataMask = (0xFFFFFFFF >> (32 - bitsPerChunk));

            for(int i = 0; i < lineCount; i++)
            {
                lines[i] = new ChunkedBitstream(bitsPerChunk, lineLength);
            }
        }

        // Test this
        public ChunkedBitstream2D(ChunkedBitstream[] data, bool flip)
        {
            lineCount = !flip ? data.Length : data[0].ChunkCount;
            lineLength = flip ? data.Length : data[0].ChunkCount;

            bitsPerChunk = data[0].BitsPerChunk;

            lines = new ChunkedBitstream[lineCount];

            for (int i = 0; i < lineCount; i++)
            {
                // if not flipping, just copy
                if (!flip)
                {
                    lines[i] = new ChunkedBitstream(data[i]);
                    continue;
                }

                // if flipping, copy bit by bit #FIXME test
                lines[i] = new ChunkedBitstream(bitsPerChunk, lineLength);
                for (int j = 0; j < lineLength; j++)
                {
                    lines[i][j] = data[j][i];
                }
            }
        }

        // #FIXME test
        public ChunkedBitstream2D(byte[,] data, byte bitsPerChunk): this(bitsPerChunk, data.GetLength(0), data.GetLength(1))
        {
            for(int i = 0; i < lineCount; i++)
            {
                for(int j = 0; j < lineLength; j++)
                {
                    lines[i][j] = data[i, j];
                }
            }
        }

        // #FIXME test
        public ChunkedBitstream2D(ChunkedBitstream flattened, int lineCount, int lineLength, bool endToEnd) : this(flattened.BitsPerChunk, lineCount, lineLength)
        {
            if (flattened.ChunkCount != lineLength * lineCount) throw new ArgumentException("flat input stream must be divisible into lines. count: " + flattened.ChunkCount + " given: " + lineCount + "x" + lineLength);

            for (int i = 0; i < flattened.ChunkCount; i++)
            {
                int lineIndex;
                int chunkIndex;
                if (endToEnd)
                {
                    lineIndex = i / lineLength;
                    chunkIndex = i % lineLength;
                }
                else
                {
                    lineIndex = i % lineCount;
                    chunkIndex = i / lineCount;
                }

                lines[lineIndex][chunkIndex] = flattened[i];
            }
        }

        public int LineCount => lineCount; 
        public int LineLength => lineLength;

        public uint this[int i, int j] { get => lines[i].GetChunk(j); set => lines[i].SetChunk(j, value); }

        public ChunkedBitstream this[int i] { get => lines[i]; set => SetLine(i, value); }

        public uint GetChunk(int i, int j)
        {
            return lines[i].GetChunk(j);
        }

        public void SetChunk(int i, int j, uint value)
        {
            lines[i].SetChunk(j, value);
        }

        public ChunkedBitstream GetLine(int i)
        {
            return lines[i];
        }

        public void SetLine(int i, ChunkedBitstream line)
        {
            if (line.BitsPerChunk != bitsPerChunk) throw new ArgumentException("bits per chunk must match");
            if (line.Count != lineLength) throw new ArgumentException("length of stream must match");

            lines[i] = line;
        }

        public ChunkedBitstream Flatten(bool endToEnd)
        {
            if(endToEnd)
            {
                ChunkedBitstream lined = new ChunkedBitstream(bitsPerChunk);
                for(int i = 0; i < lineCount; i++)
                {
                    lined.Append(lines[i]);
                }
                return lined;
            }

            ChunkedBitstream shredded = new ChunkedBitstream(bitsPerChunk);
            for(int  j = 0; j < lineLength; j++)
            {
                for(int i = 0; i < lineCount; i++) 
                {
                    shredded.Add(GetChunk(i, j));
                }
            }
            return shredded;
        }

        public ChunkedBitstream2D Flip()
        {
            return new ChunkedBitstream2D(lines, true);
        }

        public ChunkedBitstream2D PadLineLength(int multiple)
        {
            int remainder = lineLength % multiple;
            if (remainder == 0)
                return new ChunkedBitstream2D(lines, false);

            ChunkedBitstream2D output = new ChunkedBitstream2D(bitsPerChunk, lineCount, lineLength + multiple - remainder);
            for(int i = 0; i < lineCount; i++)
            {
                for(int j = 0; j < lineLength; j++)
                {
                    output[i, j] = GetChunk(i, j);
                }
            }
            return output;
        }

        public ChunkedBitstream2D PadLineCount(int multiple)
        {
            int remainder = lineCount % multiple;
            if (remainder == 0)
                return new ChunkedBitstream2D(lines, false);

            ChunkedBitstream2D output = new ChunkedBitstream2D(bitsPerChunk, lineCount + multiple - remainder, lineLength);
            for (int i = 0; i < lineCount; i++)
            {
                for (int j = 0; j < lineLength; j++)
                {
                    output[i, j] = GetChunk(i, j);
                }
            }
            return output;
        }

        public ChunkedBitstream2D Trim(int desiredLength)
        {
            ChunkedBitstream2D output = new ChunkedBitstream2D(bitsPerChunk, lineCount, desiredLength);
            for (int i = 0; i < lineCount; i++)
            {
                for (int j = 0; j < desiredLength; j++)
                {
                    output[i, j] = GetChunk(i, j);
                }
            }
            return output;
        }

        public ChunkedBitstream2D PackedSafe(int packCount, bool flipped)
        {
            if(!flipped)
            {
                return PadLineLength(packCount).Packed(packCount, flipped);
            }
            return PadLineCount(packCount).Packed(packCount, flipped);
        }

        public ChunkedBitstream2D Packed(int packCount, bool flipped)
        {
            int chunkBitsPacked = bitsPerChunk * packCount;
            if (chunkBitsPacked > 32) throw new ArgumentException("cannot pack more than 32 bits per chunk.");


            ChunkedBitstream2D packInput = new ChunkedBitstream2D(lines, flipped);
            ChunkedBitstream2D packOutput = new ChunkedBitstream2D(chunkBitsPacked, packInput.lineCount, packInput.lineLength / packCount);

            if ((packInput.lineLength % packCount) != 0) throw new ArgumentException("line length (" + packInput.lineLength + ") must be divisible by packCount (" + packCount + ")");

            for (int i = 0; i < packInput.lineCount; i++)
            {
                for(int j = 0; j < packInput.lineLength; j += packCount)
                {
                    uint chunk = packInput.GetChunk(i, j);
                    for(int q = 1; q < packCount; q++)
                    {
                        chunk |= (packInput.GetChunk(i, j + q) << (q * packInput.bitsPerChunk));
                    }
                    packOutput[i][j / packCount] = chunk;
                }
            }

            return flipped ? packOutput.Flip() : packOutput;
        }

        public ChunkedBitstream2D Unpacked(int unpackCount, bool flipped)
        {
            if ((bitsPerChunk % unpackCount) != 0) throw new ArgumentException("bits must be divisible by packCount");

            int chunkBitsUnpacked = bitsPerChunk / unpackCount;
            ChunkedBitstream2D packInput = new ChunkedBitstream2D(lines, flipped);
            ChunkedBitstream2D packOutput = new ChunkedBitstream2D(chunkBitsUnpacked, packInput.lineCount, packInput.lineLength * unpackCount);

            uint mask = (1U << chunkBitsUnpacked) - 1;

            for (int i = 0; i < packInput.lineCount; i++)
            {
                for (int j = 0; j < packInput.lineLength; j++)
                {
                    var chunkData = packInput.GetChunk(i, j);
                    for (int q = 0; q < unpackCount; q++)
                    {
                        packOutput[i][j * unpackCount + q] = ((chunkData >> (q * chunkBitsUnpacked)) & mask);
                    }
                }
            }

            return flipped ? packOutput.Flip() : packOutput;
        }

        public byte[,] ToSparseByteArray()
        {
            Console.WriteLine("outputting " + lineCount + "x" + lineLength + " byte array with " + bitsPerChunk + " bits per.");
            byte[,] output = new byte[lineCount, lineLength];

            for(int i = 0; i < lineCount; i++)
            {
                byte[] temp = lines[i].ToChunkedByteArray(1);
                for (int j = 0; j < lineLength; j++)
                {
                    output[i, j] = temp[j];
                }
            }

            return output;
        }

        public long TotalBits => lines.Sum(l => l.TotalBits);

        public override string ToString()
        {
            return $"LineCount: {lineCount}, lineLength: {lineLength}";
        }
    }
}
