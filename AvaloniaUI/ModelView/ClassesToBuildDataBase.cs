using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ModelView.DataBaseClasses
{
	class ProcessedImageDB
	{
		[Key]
		public int ImageId { get; set; }
		public string ImageName { get; set; }
		public int ImageLabel { get; set; }
		public ICollection<ImageDetailDB> AdditionalInfo { get; set; }
	}

	//how does he store byte image
	class ImageDetailDB
	{
		[Key]
		public int ImageDetailId { get; set; }

		//byte sequence represents colored image
		public byte[] ByteImage { get; set; }
		public ICollection<ProcessedImageDB> PrimaryInfo { get; set; } 
	}

	class MyContext : DbContext
	{
		public DbSet<ProcessedImageDB> ProcessedImages { get; set; }
		public DbSet<ImageDetailDB> ImageDetails { get; set;}

		protected override void OnConfiguring(DbContextOptionsBuilder o)
			=> o.UseSqlite("Data Source=../ModelView/DataBase.db");
	}
}