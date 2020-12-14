using System;

namespace ServerContracts
{
    public class ProcessedImageContracts
    {

		// id to be self defined ???
        public ProcessedImageContracts(string name, int label, string img_base64) 
		{
			Name = name;
			Label = label;
			IncodedImageBase64 = img_base64;
		}

		public string Name { get; set; }
		public int Label { get; set; }
		public string IncodedImageBase64 { get; set; }

		// public override string ToString()
		// {
		// 	return Name + " " + Label.ToString();
		// }
    }
}
