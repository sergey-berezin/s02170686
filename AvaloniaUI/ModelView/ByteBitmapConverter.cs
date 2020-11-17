using System;
using System.IO;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Avalonia.Platform;
using Avalonia;

namespace ModelView
{
	public class UIDatabaseTypeConverters
	{
		public int Height { get; set; }
		public int Width { get; set; }
		public string SaveName { get; set; }

		public UIDatabaseTypeConverters(int height=30, int width=30,
										string save_name="UIDatabaseTypeCpnverter_tmp_file")
		{
			Height = height;
			Width = width;
			SaveName = save_name;
		}

		public Bitmap AvaloniaBitmapFromByteImage(byte[] byte_image)
		{
			using var image = new Image<Rgb24>(Width, Height);

			int counter = 0;
			for(int x = 0; x < image.Height; x++)
			{
				Span<Rgb24> pixelSpan = image.GetPixelRowSpan(x);
				for(int y = 0; y < image.Width; y++)
				{
					pixelSpan[y].R = byte_image[counter];
					pixelSpan[y].G = byte_image[counter + 1];
					pixelSpan[y].B = byte_image[counter + 2];

					counter += 3;
				}
			}

			MemoryStream bitmap_stream = new MemoryStream();
			image.SaveAsBmp(bitmap_stream);
			bitmap_stream.Position = 0;
			Bitmap bitmap = new Bitmap(bitmap_stream);

			return bitmap;
		}

		//byte image is sequence of sequences of 3 bytes (1-Red, 2-Green, 3-Blue)
		public byte[] ByteImageFromFile(string file_name, int channels=3)
		{
			using var image = Image.Load<Rgb24>(file_name);
			image.Mutate(x =>
            {
				SixLabors.ImageSharp.Size size = new SixLabors.ImageSharp.Size(Width, Height);
                x.Resize(size);
            });

			int counter = 0;
			byte[] byte_image = new byte[image.Height * image.Width * channels];

			for(int x = 0; x < image.Height; x++)
			{
				Span<Rgb24> pixelSpan = image.GetPixelRowSpan(x);
				for(int y = 0; y < image.Width; y++)
				{
					byte_image[counter] = pixelSpan[y].R;
					byte_image[counter + 1] = pixelSpan[y].G;
					byte_image[counter + 2] = pixelSpan[y].B;

					counter += 3;
				}
			}

			return byte_image;
		}
	}
}