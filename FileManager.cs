using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MakeCodeImageParserV3
{
    internal static class FileManager
    {
        public static void ExportImages(string directoryPath, string outputPath, int width, int height, int colorBits)
        {
            Regex numberRegex = new Regex(@"\d+");

            string[] imagePaths = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".gif"))
                .OrderBy(s => 
                { 
                    var match = numberRegex.Match(s); 
                    return match.Success ? int.Parse(match.Value) : int.MaxValue; 
                })
                .ToArray();

            using (var fs = new FileStream(outputPath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                // Write header
                bw.Write(imagePaths.Length);
                bw.Write(width);
                bw.Write(height);

                int completed = 0;
                foreach (var path in imagePaths)
                {
                    using (Bitmap bmp = new Bitmap(path))
                    {
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                Color col = bmp.GetPixel(x, y);
                                byte grayScaleValue = ColorToGrayscale(col);
                                bw.Write((byte)(grayScaleValue >> (8 - colorBits)));
                            }
                        }
                    }

                    completed++;
                    Console.WriteLine("Completed: " + completed + "/" + imagePaths.Length + ", path: " + path);
                }
            }
        }

        public static byte ColorToGrayscale(Color col)
        {
            return (byte)(0.2126 * col.R + 0.7152 * col.G + 0.0722 * col.B);
        }

        public static byte[][,] LoadImageData(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                int imageCount = br.ReadInt32();
                int width = br.ReadInt32();
                int height = br.ReadInt32();

                byte[][,] images = new byte[imageCount][,];

                for (int i = 0; i < imageCount; i++)
                {
                    images[i] = new byte[width, height];
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            images[i][x, y] = br.ReadByte();
                        }
                    }
                }

                return images;
            }
        }


        [STAThread]
        public static void ShowGrayscaleImagePopup(byte[,] pixelData, string title = "Image Viewer")
        {
            int width = pixelData.GetLength(0);
            int height = pixelData.GetLength(1);

            Console.WriteLine("Displaying (" + width + "x" + height + ") image.");

            Bitmap bmp = new Bitmap(width, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte value = pixelData[x, y];
                    Color gray = Color.FromArgb(value * 255, value * 255, value * 255);
                    bmp.SetPixel(x, y, gray);
                }
            }

            var form = new Form
            {
                Text = title,
                ClientSize = new Size(width, height),
                StartPosition = FormStartPosition.CenterScreen
            };

            var pictureBox = new PictureBox
            {
                Image = bmp,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill
            };

            form.Controls.Add(pictureBox);
            Application.Run(form);
        }
    }
}
