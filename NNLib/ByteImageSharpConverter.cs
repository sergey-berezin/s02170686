using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NNLib
{
    public class ByteImageSharpConverter
    {
        public int Height { get; set; }
        public int Width { get; set; }

        public ByteImageSharpConverter(int width=28, int height=28)
        {
            Height = height;
            Width = width;
        }

		public Image<Rgb24> ImageSharpFromByteImage(byte[] byte_image)
		{
			Image<Rgb24> image = new Image<Rgb24>(Width, Height);

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

			return image;
		}

        public byte[] ByteImageFromImageSharp(Image<Rgb24> image, int channels=3)
		{
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