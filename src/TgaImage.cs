using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using skwas.IO;

namespace skwas.Drawing
{

	/// <summary>
	/// Specifies the file format of the TGA file.
	/// </summary>
	public enum TgaFileVersion
	{
		/// <summary>
		/// TGA format v1.0, is a basic TGA format that does not have a footer, and does not support extensions and other custom data. This format is included for backwards compatibility. It is recommended to use Version2.  
		/// </summary>
		Version1 = 0,
		/// <summary>
		/// TGA format v2.0 is the latest tga format and supports extensions and custom data. The TgaImage class is designed to allow for custom data and extensions, and persists any data upon saving.
		/// </summary>
		Version2 = 1
	}

	/// <summary>
	/// Specifies the format of the color data for each pixel in the image.
	/// </summary>
	public enum TgaPixelFormat
	{
		/// <summary>
		/// No image data present.
		/// </summary>
		None,
		/// <summary>
		/// Indexed (using palette).
		/// </summary>
		Indexed = 1,
		/// <summary>
		/// True color.
		/// </summary>
		Rgb = 2,
		/// <summary>
		/// Greyscale.
		/// </summary>
		Greyscale = 3
	}

	/// <summary>
	/// Represents a TGA image.
	/// </summary>
	public sealed class TgaImage
		: IDisposable
	{
		// http://www.ludorg.net/amnesia/TGA_File_Format_Spec.html

		#region TGA header/footer

		private const int MinCompressedImageType = 8;

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi, Pack=1)]
			private struct TGA_HEADER
		{
			/// <summary>
			/// Size of ID field that follows 18 byte header (0 usually).
			/// </summary>
			public byte identsize;
			/// <summary>
			/// Type of colour map 0=none, 1=has palette.
			/// </summary>
			public byte colormaptype;
			/// <summary>
			/// Type of image 0=none, 1=indexed, 2=rgb, 3=grey, 9=indexedRLE, 10=rgbRLE, 11=greyRLE.
			/// </summary>
			public byte imagetype;          

			/// <summary>
			/// First colour map entry in palette.
			/// </summary>
			public short colormapstart;
			/// <summary>
			/// Number of colours in palette.
			/// </summary>
			public short colormaplength;
			/// <summary>
			/// Number of bits per palette entry 15, 16, 24, 32.
			/// </summary>
			public byte colormapbits;

			/// <summary>
			/// Image x origin.
			/// </summary>
			public short xstart;
			/// <summary>
			/// Image y origin.
			/// </summary>
			public short ystart;
			/// <summary>
			/// Image width in pixels.
			/// </summary>
			public short width;
			/// <summary>
			/// Image height in pixels.
			/// </summary>
			public short height; 
			/// <summary>
			/// Image bits per pixel 8, 16, 24, 32
			/// </summary>
			public byte bits;
			/// <summary>
			/// Image descriptor bits (vh flip bits).
			/// </summary>
			public byte descriptor;
    
			// pixel data follows header
  
			public byte NumberOfAlphaBits
			{
				get 
				{
					return (byte)(descriptor & 0xF);
				}
			}

			public bool BottomToTop
			{
				get
				{
					return (descriptor & 0x20) == 0;
				}
			}

			public bool RightToLeft
			{
				get
				{
					return (descriptor & 0x10) == 1;
				}
			}

			public TgaPixelFormat PixelFormat
			{
				get
				{
					return (TgaPixelFormat) (imagetype - 
						(imagetype > MinCompressedImageType ? MinCompressedImageType : 0)
					);
				}
			}

			public bool Compressed
			{
				get
				{
					return imagetype > MinCompressedImageType;
				}
			}

			public bool HasColorMap
			{
				get 
				{
					return colormaptype == 1;
				}
			}
		}


		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi, Pack=1)]
			private struct TGA_FOOTER
		{
			private const string TRUEVISION_SIG = "TRUEVISION-XFILE";

			public uint extensionAreaOffset;
			public uint developerDirectoryOffset;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst=16)]
			public byte[] signature;
			public byte reservedCharacter;
			public byte binaryZeroStringTerminator;

			public string Signature
			{
				get
				{
					return Encoding.ASCII.GetString(signature);
				}
			}

			public bool IsValidFooter
			{
				get
				{
					return Signature == TRUEVISION_SIG 
						&& (char)reservedCharacter == '.' 
						&& binaryZeroStringTerminator == 0;
				}
			}
		}

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi, Pack=1)]
		private struct TGA_EXTENSION
		{
			/// <summary>
			/// This field is a SHORT field which specifies the number of BYTES in the fixed-length portion of the Extension Area. For Version 2.0 of the TGA File Format, this number should be set to 495. If the number found in this field is not 495, then the file will be assumed to be of a version other than 2.0. If it ever becomes necessary to alter this number, the change will be controlled by Truevision, and will be accompanied by a revision to the TGA File Format with an accompanying change in the version number.
			/// </summary>
			public short extensionSize;
			/// <summary>
			/// This field is an ASCII field of 41 bytes where the last byte must be a null (binary zero). This gives a total of 40 ASCII characters for the name. If the field is used, it should contain the name of the person who created the image(author). If the field is not used, you may fill it with nulls or a series of blanks (spaces) terminated by a null. The 41st byte must always be a null.
			/// </summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=41)]
			public string authorName;
			/// <summary>
			/// This is an ASCII field consisting of 324 bytes which are organized as four lines of 80 characters, each followed by a null terminator. This field is provided, in addition to the original IMAGE ID field (in the original TGA format), because it was determined that a few developers had used the IMAGE ID field for their own purposes. This field gives the developer four lines of 80 characters each, to use as an Author Comment area. Each line is fixed to 81 bytes which makes access to the four lines easy. Each line must be terminated by a null. If you do not use all 80 available characters in the line, place the null after the last character and blank or null fill the rest of the line. The 81st byte of each of the four lines must be null.
			/// </summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=81)]
			public string authorComments1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=81)]
			public string authorComments2;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=81)]
			public string authorComments3;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=81)]
			public string authorComments4;

			public short dateTimeMonth;
			public short dateTimeDay;
			public short dateTimeYear;
			public short dateTimeHour;
			public short dateTimeMinute;
			public short dateTimeSecond;

			/// <summary>
			/// This field is an ASCII field of 41 bytes where the last byte must be a binary zero. This gives a total of 40 ASCII characters for the job name or the ID. If the field is used, it should contain a name or id tag which refers to the job with which the image was associated. This allows production companies (and others) to tie images with jobs by using this field as a job name (i.e., CITY BANK) or job id number (i.e., CITY023). If the field is not used, you may fill it with a null terminated series of blanks (spaces) or nulls. In any case, the 41st byte must be a null.
			/// </summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=41)]
			public string jobNameID;

			public short jobTimeHours;
			public short jobTimeMinutes;
			public short jobTimeSeconds;

			/// <summary>
			/// This field is an ASCII field of 41 bytes where the last byte must be a binary zero (null). This gives a total of 40 ASCII characters for the Software ID. The purpose of this field is to allow software to determine and record with what program a particular image was created. If the field is not used, you may fill it with a null terminated series of blanks (spaces) or nulls. The 41st byte must always be a null.
			/// </summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=41)]
			public string softwareID;
			
			/// <summary>
			/// This field consists of two sub-fields, a SHORT and an ASCII BYTE. The purpose of this field is to define the version of software defined by the "Software ID" field above. The SHORT contains the version number as a binary integer times 100. Therefore, software version 4.17 would be the integer value 417. This allows for two decimal positions of sub-version. The ASCII BYTE supports developers who also tag a release letter to the end. For example, if the version number is 1.17b, then the SHORT would contain 117. and the ASCII BYTE would contain "b". The organization is as follows:
			///			SHORT (Bytes 0 - 1): Version Number * 100
			///			BYTE (Byte 2): Version Letter
			/// If you do not use this field, set the SHORT to binary zero, and the BYTE to a space (" ").
			/// </summary>
			public short softwareVersionNumber;
			public byte softwareVersionLetter;

			/// <summary>
			/// This field contains a long value which is the key color in effect at the time the image is saved. The format is in A:R:G:B where ‘A’ (most significant byte) is the alpha channel key color (if you don’t have an alpha channel in your application, keep this byte zero [0]).
			/// The Key Color can be thought of as the ‘background color’ or ‘transparent color’. This is the color of the ‘non image’ area of the screen, and the same color that the screen would be cleared to if erased in the application. If you don’t use this field, set it to all zeros (0). Setting the field to all zeros is the same as selecting a key color of black.
			/// A good example of a key color is the ‘transparent color’ used in TIPSª for WINDOW loading/saving.
			/// </summary>
			public int keyColor;

			/// <summary>
			/// This field contains two SHORT sub-fields, which when taken together specify a pixel size ratio.
			/// </summary>
			public int pixelAspectRatio;

			/// <summary>
			/// This field contains two SHORT sub-fields, which when taken together in a ratio, provide a fractional gamma value.
			/// </summary>
			public int gammaValue;

			/// <summary>
			/// This field is a 4-byte field containing a single offset value. This is an offset from the beginning of the file to the start of the Color Correction table. This table may be written anywhere between the end of the Image Data field (field 8) and the start of the TGA File Footer. If the image has no Color Correction Table or if the Gamma Value setting is sufficient, set this value to zero and do not write a Correction Table anywhere.
			/// </summary>
			public int colorCorrectionOffset;
			 
			/// <summary>
			/// This field is a 4-byte field containing a single offset value. This is an offset from the beginning of the file to the start of the Postage Stamp Image. The Postage Stamp Image must be written after Field 25 (Scan Line Table) but before the start of the TGA File Footer. If no postage stamp is stored, set this field to the value zero (0).
			/// </summary>
			public int postageStampOffset;
			 
			/// <summary>
			/// This field is a 4-byte field containing a single offset value. This is an offset from the beginning of the file to the start of the Scan Line Table.
			/// </summary>
			public int scanLineOffset;
			 
			/// <summary>
			/// This single byte field contains a value which specifies the type of Alpha channel data contained in the file.
			/// </summary>
			public byte attributesType;

		}

		#endregion

		private bool _disposed;
		private Bitmap _bitmap;
		private TGA_HEADER _header;

		private TgaFileVersion _fileVersion;

		/// <summary>
		/// Gets the format of the color data for each pixel in the image.
		/// </summary>
		public TgaPixelFormat PixelFormat => _header.PixelFormat;

		/// <summary>
		/// Gets whether the color data is RLE encoded.
		/// </summary>
		public bool Compressed => _header.Compressed;

		/// <summary>
		/// Gets whether the pixels are stored from bottom to top.
		/// </summary>
		public bool BottomToTop => _header.BottomToTop;

		/// <summary>
		/// Gets whether the pixels are stored from right to left.
		/// </summary>
		public bool RightToLeft => _header.RightToLeft;

		/// <summary>
		/// Gets the number of alpha bits.
		/// </summary>
		public int NumberOfAlphaBits => _header.NumberOfAlphaBits;

		/// <summary>
		/// Gets the number of colors used.
		/// </summary>
		public ColorDepth ColorDepth => (ColorDepth)_header.bits;

		/// <summary>
		/// Gets whether the image has a palette.
		/// </summary>
		public bool HasPalette => _header.HasColorMap;

		/// <summary>
		/// Gets the number of bits per color in the palette.
		/// </summary>
		public int PaletteBits => _header.colormapbits;

		/// <summary>
		/// Gets the image width.
		/// </summary>
		public int Width => _header.width;

		/// <summary>
		/// Gets the image height.
		/// </summary>
		public int Height => _header.height;

		/// <summary>
		/// Gets the image size.
		/// </summary>
		public Size Size => new Size(Width, Height);

		#region .ctor/cleanup

		/// <summary>
		/// Initializes a new instance of <see cref="TgaImage"/> using specified file.
		/// </summary>
		/// <param name="path">The file name.</param>
		public TgaImage(string path)
		{
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				LoadBitmap(fs);
		}

		/// <summary>
		/// Initializes a new instance of <see cref="TgaImage"/> using specified stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		public TgaImage(Stream stream)
		{
			LoadBitmap(stream);
		}

		/// <summary>
		/// Initializes a new instance of <see cref="TgaImage"/> via an embedded resource.
		/// </summary>
		/// <param name="type">The type to use namespace from.</param>
		/// <param name="resource">The resource name.</param>
		public TgaImage(Type type, string resource)
		{
			LoadBitmap(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(type, resource));
		}

		/// <summary>
		/// Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.
		/// </summary>
		~TgaImage()
		{
			Dispose(false);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True when disposing.</param>
		private void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				_bitmap?.Dispose();
				_bitmap = null;
			}

			_disposed = true;
		}

		#endregion

		public static void ValidateStream(Stream stream)
		{
			ValidateStream(stream, false);
		}

		public static bool ValidateStream(Stream stream, bool fixCommonBugs)
		{
			if (stream == null) throw new ArgumentNullException();
			if (!stream.CanSeek) throw new ArgumentException("The stream does not support seeking.");
			if (!stream.CanRead) throw new ArgumentException("The stream does not support reading.");

			var currentPos = stream.Position;

			var reader = new BinaryReader(stream, Encoding.ASCII);

			// Read the header.
			var header = (TGA_HEADER)reader.ReadStruct(typeof(TGA_HEADER));

			if (header.identsize > 0)
				stream.Seek(header.identsize, SeekOrigin.Current);

			// Check if we know the image data type.
			if (!Enum.IsDefined(typeof(TgaPixelFormat), header.PixelFormat)) throw new Exception("Unknown image data type.");

			// Check if color map is needed by data type and load it if available.
			if (header.PixelFormat == TgaPixelFormat.Indexed)
				if (!header.HasColorMap)
					throw new Exception("No color map is defined.");
				else
					reader.ReadBytes((header.colormapbits / 8) * header.colormaplength);

			if (fixCommonBugs)
			{
				fixCommonBugs = false;
				if (!header.HasColorMap && (header.PixelFormat != TgaPixelFormat.Indexed))
				{
					if (!stream.CanWrite)
						throw new ArgumentException("Tga header contains a bug but can't be fixed since the stream does not support writing.");
					if ((header.colormapstart != 0) || (header.colormaplength != 0))
					{
						stream.Position = currentPos;
						header.colormapstart = 0;
						header.colormaplength = 0;
						var wr = new BinaryWriter(stream);
						wr.WriteStruct(header);
						fixCommonBugs = true;
					}
				}
			}

			stream.Position = currentPos;

			switch (header.bits)
			{
				case 32:
				case 24:
				case 16:
					return fixCommonBugs;
				case 8:
					// Color palettes are supported.
					if (header.HasColorMap && (header.PixelFormat == TgaPixelFormat.Indexed))
						return fixCommonBugs;

					// Greyscale is supported.
					if (header.PixelFormat == TgaPixelFormat.Greyscale)
						return fixCommonBugs;
					break;
			}
			throw new NotSupportedException("Stream does not appear to be a valid TGA stream.");
		}

		private void LoadBitmap(Stream stream)
		{
			ValidateStream(stream);

			var reader = new BinaryReader(stream);

			// Read the header.
			_header = (TGA_HEADER)reader.ReadStruct(typeof(TGA_HEADER));


			/*long t = stream.Position;
			BinaryWriter wr = new BinaryWriter(stream, Encoding.ASCII);
			if (!header.HasColorMap)
			{
				header.colormaplength = 0;
				header.colormapstart = 0;
				stream.Position = 0;
				wr.WriteStruct(header);
			}*/
//			stream.Position = t;


			if (_header.identsize > 0)
			{
				var imageId = reader.ReadBytes(_header.identsize);
				Debug.WriteLine("Has ImageID: " + Encoding.ASCII.GetString(imageId));
			}

			// Check if we know the image data type.
			if (!Enum.IsDefined(typeof(TgaPixelFormat), PixelFormat)) throw new Exception("Unknown image data type.");

			// Check if color map is needed by data type and load it if available.
			var colorMap = new byte[0];
			if (PixelFormat == TgaPixelFormat.Indexed)
				if (!HasPalette)
					throw new Exception("No color map is defined.");
				else
					colorMap = reader.ReadBytes((PaletteBits / 8) * _header.colormaplength);

			// Check if file has footer
			var tempPos = stream.Position;
			stream.Seek(-26, SeekOrigin.End);
			var footer = (TGA_FOOTER)reader.ReadStruct(typeof(TGA_FOOTER));
			stream.Position = tempPos;

			_fileVersion = TgaFileVersion.Version1;
			if (footer.IsValidFooter)
				_fileVersion = TgaFileVersion.Version2;

			_bitmap = null;
			switch (_header.bits)
			{
				case 32:
					_bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
					break;
				case 24:
					_bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
					break;
				case 16:
					_bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format16bppRgb555);
					break;
				case 8:
					_bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

					// Load palette from image data.
					if (HasPalette && PixelFormat == TgaPixelFormat.Indexed)
					{
						var bytesPerColor = PaletteBits / 8;
						for (var i = _header.colormapstart + bytesPerColor - 1; i < colorMap.Length; i += bytesPerColor)
						{
							var colorIndex = i/bytesPerColor;
							if (colorIndex < _bitmap.Palette.Entries.Length)
								_bitmap.Palette.Entries[colorIndex] = Color.FromArgb(colorMap[i], colorMap[i - 1], colorMap[i - 2]);
							else
								throw new Exception("Unexpected length of color palette.");
						}
					}
					else if (PixelFormat == TgaPixelFormat.Greyscale)
					{
						// Build a 256 color palette of grey colors.
						for (var i = 0; i < _bitmap.Palette.Entries.Length; i++)
							_bitmap.Palette.Entries[i] = Color.FromArgb(i, i, i);
					}
					break;
			}
			
			if (_bitmap == null)
				throw new NotSupportedException("The tga image format is not supported.");

			ReadPixels(_bitmap, 
				Compressed 
					? new TruevisionRleReader(reader.BaseStream, _bitmap.PixelFormat) 
					: reader,
				BottomToTop
			);


			if (_fileVersion == TgaFileVersion.Version2)
			{
				if (footer.developerDirectoryOffset > 0)
				{
					stream.Seek(footer.developerDirectoryOffset, SeekOrigin.Begin);
				}
				if (footer.extensionAreaOffset > 0)
				{
					//char[] trimChars = " \0".ToCharArray();

					stream.Seek(footer.extensionAreaOffset, SeekOrigin.Begin);
					var extension = (TGA_EXTENSION)reader.ReadStruct(typeof(TGA_EXTENSION));
//					Debug.WriteLine("Generated by software: " + extension.softwareID + " v." + (float)extension.softwareVersionNumber / 100);

				}
				var bytesToPreserve = reader.ReadBytes((int)(stream.Length - 26 - stream.Position));
//				Debug.WriteLine(System.Text.Encoding.ASCII.GetString(bytesToPreserve));
			}
		}

		private static unsafe void ReadPixels(Bitmap bitmap, BinaryReader reader, bool bottomToTop)
		{
			var width = bitmap.Width;
			var height = bitmap.Height;
			var bitsPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat);
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

			var ptrPixels = bitmapData.Scan0;

			// Get the pointer to the image bits.
			// This is the unsafe operation.
			var stride = bitmapData.Stride;

			byte* pBits;
			if (stride > 0)
				pBits = (byte*) ptrPixels.ToPointer();
			else
				pBits = (byte*) ptrPixels.ToPointer() + stride*(height - 1);

			// If order is inverted, flip.
			if (bottomToTop)
			{
				if (stride > 0)
					pBits = (byte*) ptrPixels.ToPointer() + stride*(height - 1);
				else
					pBits = (byte*) ptrPixels.ToPointer();
				stride = -stride;
			}			

			var bytesPerColor = bitsPerPixel / 8;

			for (var row = 0; row < height; ++row)
			{
				var rs = row*stride;
				for (var col = 0; col < width; ++col)
				{
					var p8bpp = pBits + rs + col*bytesPerColor;
					for (var i = 0; i < bytesPerColor; i++)
					{
						// Read next byte.
						*p8bpp = reader.ReadByte();
						// Move forward.
						p8bpp = p8bpp + 1;
					}
				}
			}

			// To commit the changes, unlock the portion of the bitmap.  
			bitmap.UnlockBits(bitmapData);
		}

		public Bitmap Bitmap
		{
			get 
			{
				if (_disposed)
					throw new ObjectDisposedException(GetType().Name);
				return _bitmap; 
			}
		}
	}
}
