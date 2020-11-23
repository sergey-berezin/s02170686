using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace ModelView
{
	public class AvaloniaUILabeledImage
	{
		public AvaloniaUILabeledImage(string full_name, int label, Bitmap bitmap = null) 
		{
			FullName = full_name;
			Label = label;
			AvaloniaBitmap = bitmap;
		}

		//name including absolute path
		public string FullName { get; set; }
		public string Name 
		{
			get { return Path.GetFileName(FullName); } 
		}
		public int Label { get; set; }
		public Bitmap AvaloniaBitmap { get; set; }

		public override string ToString()
		{
			return Path.GetFileName(FullName) + " " + Label.ToString();
		}
	}
}