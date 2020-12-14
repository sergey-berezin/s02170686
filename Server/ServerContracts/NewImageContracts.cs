using System;

namespace ServerContracts
{
    public class NewImageContracts
    {
        public NewImageContracts(string name, string img_base64) 
		{
			Name = name;
			IncodedImageBase64 = img_base64;
		}

		public string Name { get; set; }
		public string IncodedImageBase64 { get; set; }

		// public override string ToString()
		// {
		// 	return Name;
		// }
    }
}