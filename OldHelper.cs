using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MakeCodeImageParserV3
{
    internal static class OldHelper
    {
        public static byte[] RLEFrame(byte[] data)
        {
            int count = 1;
            List<byte> outty = new List<byte>();
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] == data[i - 1])
                {
                    count++;
                    if (count == 256)
                    {
                        outty.Add(data[i - 1]);
                        outty.Add(255);
                        count = 1;
                    }
                }
                else
                {
                    outty.Add(data[i - 1]);
                    outty.Add((byte)count);
                    count = 1;
                }
            }
            outty.Add(data[^1]);
            outty.Add((byte)count);

            return outty.ToArray();
        }

        public static byte[] FlattenFrame(byte[,] frame)
        {
            int w = frame.GetLength(0);
            int h = frame.GetLength(1);
            byte[] outty = new byte[w * h];
            int findex = 0;
            for (int x = 0; x < w; ++x)
            {
                for (int y = 0; y < h; ++y)
                {
                    outty[findex++] = frame[x, y];
                }
            }
            return outty;
        }

        public static byte[,] Pack8To1(byte[,] imageBytes)
        {
            int w = imageBytes.GetLength(0);
            int h = imageBytes.GetLength(1);

            byte[,] outty = new byte[w / 8, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w / 8; x++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        outty[x, y] |= (byte)(imageBytes[x * 8 + i, y] << (7 - i));
                    }
                }
            }

            return outty;
        }

        public static byte[] RLESquis(byte[,] imageBytes)
        {
            return RLEFrame(FlattenFrame(Pack8To1(imageBytes)));
        }
    }
}
