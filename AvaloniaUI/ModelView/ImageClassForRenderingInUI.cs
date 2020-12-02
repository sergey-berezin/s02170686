using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace ModelView
{
	public class AvaloniaUILabeledImage
	{
		public AvaloniaUILabeledImage(string name, int label, Bitmap bitmap = null) 
		{
			Name = name;
			Label = label;
			AvaloniaBitmap = bitmap;
		}

		//name including absolute path
		public string Name { get; set; }
		public int Label { get; set; }
		public Bitmap AvaloniaBitmap { get; set; }

		public override string ToString()
		{
			return Path.GetFileName(Name) + " " + Label.ToString();
		}
	}
}