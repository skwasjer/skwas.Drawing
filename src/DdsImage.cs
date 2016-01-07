using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using skwas.IO;

namespace skwas.Drawing
{

	/// <summary>
	/// Summary description for TgaImage.
	/// </summary>
	public sealed class DdsImage : IDisposable
	{
		private enum ImageType
		{
			ColorMapped = 1,
			TrueColor = 2,
			GreyScale = 3
		}

		// http://msdn.microsoft.com/archive/default.asp?url=/archive/en-us/directx9_c/directx/graphics/reference/ddsfilereference/ddsfileformat.asp

		#region Dds header/footer

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[Flags]
		enum DDSD
		{
			CAPS = 0x00000001,
			HEIGHT = 0x00000002,
			WIDTH = 0x00000004,
			PITCH = 0x00000008,
			ALPHABITDEPTH = 0x00000080,
			PIXELFORMAT = 0x00001000,
			MIPMAPCOUNT = 0x00020000,
			LINEARSIZE = 0x00080000,
			DEPTH = 0x00800000
		}

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[Flags]
		enum DDPF
		{
			ALPHAPIXELS = 0x00000001,
			ALPHA = 0x00000002,
			FOURCC = 0x00000004,
			RGB = 0x00000040
		}

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[Flags]
		enum DDSCAPS
		{
			ALPHA = 0x00000002,
			COMPLEX = 0x00000008,
			TEXTURE = 0x00001000,
			MIPMAP = 0x00400000
		}

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[Flags]
		enum DDSCAPS2
		{
			CUBEMAP = 0x00000200,
			CUBEMAP_POSITIVEX = 0x00000400,
			CUBEMAP_NEGATIVEX = 0x00000800,
			CUBEMAP_POSITIVEY = 0x00001000,
			CUBEMAP_NEGATIVEY = 0x00002000,
			CUBEMAP_POSITIVEZ = 0x00004000,
			CUBEMAP_NEGATIVEZ = 0x00008000,
			VOLUME = 0x00200000
		}

		public enum DXT_FOURCC
		{
			Unknown = 0,
			DXT1 = 0x31545844,
			DXT2 = 0x32545844,
			DXT3 = 0x33545844,
			DXT4 = 0x34545844,
			DXT5 = 0x35545844
		}

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[StructLayout(LayoutKind.Sequential)]
		private struct DDSURFACEDESC2
		{
			/// <summary>
			/// Size of structure. This member must be set to 124.
			/// </summary>
			public int dwSize;
			/// <summary>
			/// Flags to indicate valid fields. Always include DDSD_CAPS, DDSD_PIXELFORMAT, DDSD_WIDTH, DDSD_HEIGHT.
			/// </summary>
			public DDSD dwFlags;
			/// <summary>
			/// Height of the main image in pixels 
			/// </summary>
			public int dwHeight;
			/// <summary>
			/// Width of the main image in pixels 
			/// </summary>
			public int dwWidth;
			/// <summary>
			/// For uncompressed formats, this is the number of bytes per scan line (DWORD> aligned) for the main image. dwFlags should include DDSD_PITCH in this case. For compressed formats, this is the total number of bytes for the main image. dwFlags should be include DDSD_LINEARSIZE in this case.
			/// </summary>
			public int dwPitchOrLinearSize;
			/// <summary>
			/// For volume textures, this is the depth of the volume. dwFlags should include DDSD_DEPTH in this case.
			/// </summary>
			public int dwDepth;
			/// <summary>
			/// For items with mipmap levels, this is the total number of levels in the mipmap chain of the main image. dwFlags should include DDSD_MIPMAPCOUNT in this case.
			/// </summary>
			public int dwMipMapCount;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved0;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved01;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved02;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved03;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved04;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved05;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved06;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved07;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved08;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved09;
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public int dwReserved010;

			/// <summary>
			/// 32-byte value that specifies the pixel format structure.
			/// </summary>
			public DDPIXELFORMAT ddpfPixelFormat;
			/// <summary>
			/// 16-byte value that specifies the capabilities structure.
			/// </summary>
			public DDCAPS2 ddsCaps;
			public int dwReserved2;

			public bool IsValid()
			{
				return (dwSize == 124) && ((dwFlags & (DDSD.WIDTH | DDSD.HEIGHT | DDSD.PIXELFORMAT | DDSD.CAPS)) != 0) && ddpfPixelFormat.IsValid();
			}
		}

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[StructLayout(LayoutKind.Sequential)]
		private struct DDPIXELFORMAT
		{
			/// <summary>
			/// Size of structure. This member must be set to 32. 
			/// </summary>
			public int dwSize;
			/// <summary>
			/// Flags to indicate valid fields. Uncompressed formats will usually use DDPF_RGB to indicate an RGB format, while compressed formats will use DDPF_FOURCC with a four-character code.
			/// </summary>
			public DDPF dwFlags;
			/// <summary>
			/// This is the four-character code for compressed formats. dwFlags should include DDPF_FOURCC in this case. For DXTn compression, this is set to "DXT1", "DXT2", "DXT3", "DXT4", or "DXT5". 
			/// </summary>
			public DXT_FOURCC dwFourCC;
			/// <summary>
			/// For RGB formats, this is the total number of bits in the format. dwFlags should include DDPF_RGB in this case. This value is usually 16, 24, or 32. For A8R8G8B8, this value would be 32.
			/// </summary>
			public int dwRGBBitCount;
			//For RGB formats, these three fields contain the masks for the red, green, and blue channels. For A8R8G8B8, these values would be 0x00ff0000, 0x0000ff00, and 0x000000ff respectively.
			public int dwRBitMask;
			public int dwGBitMask;
			public int dwBBitMask;
			/// <summary>
			/// For RGB formats, this contains the mask for the alpha channel, if any. dwFlags should include DDPF_ALPHAPIXELS in this case. For A8R8G8B8, this value would be 0xff000000. 
			/// </summary>
			public int dwRGBAlphaBitMask;

			public bool IsValid()
			{
				return (dwSize == 32);
			}

			public string FOURCC
			{
				get
				{
					return Enum.IsDefined(typeof (DXT_FOURCC), (int) dwFourCC)
						? dwFourCC.ToString()
						: Encoding.ASCII.GetString(BitConverter.GetBytes((int) dwFourCC));
				}
			}
		}

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[StructLayout(LayoutKind.Sequential)]
		private struct DDCAPS2
		{
			/// <summary>
			/// DDS files should always include DDSCAPS_TEXTURE. If the file contains mipmaps, DDSCAPS_MIPMAP should be set. For any DDS file with more than one main surface, such as a mipmaps, cubic environment map, or volume texture, DDSCAPS_COMPLEX should also be set.
			/// </summary>
			public DDSCAPS dwCaps1;
			/// <summary>
			/// For cubic environment maps, DDSCAPS2_CUBEMAP should be included as well as one or more faces of the map (DDSCAPS2_CUBEMAP_POSITIVEX, DDSCAPS2_CUBEMAP_NEGATIVEX, DDSCAPS2_CUBEMAP_POSITIVEY, DDSCAPS2_CUBEMAP_NEGATIVEY, DDSCAPS2_CUBEMAP_POSITIVEZ, DDSCAPS2_CUBEMAP_NEGATIVEZ). For volume textures, DDSCAPS2_VOLUME should be included.
			/// </summary>
			public DDSCAPS2 dwCaps2;
			public int Reserved0;
			public int Reserved1;
		}

		[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
		[StructLayout(LayoutKind.Sequential)]
		private struct DDSHEADER
		{
			/// <summary>
			/// Magic 'DDS ' = 0x20534444
			/// </summary>
			public int magic;
			public DDSURFACEDESC2 surfaceDesc;

			public bool IsValid()
			{
				return (magic == 0x20534444) && (surfaceDesc.IsValid());
			}
		}


		#endregion

		private bool _disposed;
		private Bitmap _bitmap;
		private DXT_FOURCC _fourcc;

		public DXT_FOURCC FourCC
		{
			get { return _fourcc; }
		}

		public DdsImage(string path)
		{
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				LoadBitmap(fs);
			}
		}

		public DdsImage(Stream stream)
		{
			LoadBitmap(stream);
		}

		public DdsImage(Type type, string resource)
		{
			LoadBitmap(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(type, resource));
		}

		/// <summary>
		/// Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.
		/// </summary>
		~DdsImage()
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


		private void LoadBitmap(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException();
			if (!stream.CanSeek) throw new ArgumentException("The stream does not support seeking.");
			if (!stream.CanRead) throw new ArgumentException("The stream does not support reading.");

			var reader = new BinaryReader(stream, Encoding.ASCII);

			// Read the header.
			var header = (DDSHEADER)reader.ReadStruct(typeof(DDSHEADER));
			if (!header.IsValid())
				throw new ArgumentException("Invalid header. Stream does not appear to be a valid DDS-file.", nameof(stream));

			_fourcc = DXT_FOURCC.Unknown;
			if ((header.surfaceDesc.ddpfPixelFormat.dwFlags & DDPF.RGB) == DDPF.RGB)
			{
				_bitmap = LoadRGB(header.surfaceDesc, stream);
			}
			else if ((header.surfaceDesc.ddpfPixelFormat.dwFlags & DDPF.FOURCC) == DDPF.FOURCC)
			{
				_fourcc = header.surfaceDesc.ddpfPixelFormat.dwFourCC;
				IDXTDecoder decoder;
				switch (header.surfaceDesc.ddpfPixelFormat.dwFourCC)
				{
					case DXT_FOURCC.DXT1:
						decoder = new DXT1Decoder();
						break;
					case DXT_FOURCC.DXT3:
						decoder = new DXT3Decoder();
						break;
					case DXT_FOURCC.DXT5:
						decoder = new DXT5Decoder();
						break;
					default:
						throw new NotSupportedException(string.Format("FOURCC {0} is not supported.", header.surfaceDesc.ddpfPixelFormat.dwFourCC));
				}
				_bitmap = decoder.Decode(header.surfaceDesc, stream);
			}
		}

		private Bitmap LoadRGB(DDSURFACEDESC2 desc, Stream stream)
		{
			throw new Exception("The method or operation is not implemented.");
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

		private class DXT1Decoder
			: DXTDecoder<DXT1Block>
		{
			public override int BytesPerBlock
			{
				get { return 8; }
			}

			public override bool IsDXT1
			{
				get { return true; }
			}

			protected override void Setup(DXTBlockWrapper<DXT1Block> pBlock)
			{
				base.Setup(pBlock);
				GetBlockColors(m_pBlock, m_colors, IsDXT1);
			}

			protected override void SetY(int y)
			{
				base.SetY(y);
				m_colorRow = m_pBlock.Value.color.row[y];
			}
		}


		private class DXT3Decoder
			: DXTDecoder<DXT3Block>
		{
			protected uint m_alphaRow;

			public override int BytesPerBlock
			{
				get { return 16; }
			}

			public override bool IsDXT1
			{
				get { return true; }
			}

			protected override void Setup(DXTBlockWrapper<DXT3Block> pBlock)
			{
				base.Setup(pBlock);
				GetBlockColors(m_pBlock, m_colors, IsDXT1);

			}

			protected override void SetY(int y)
			{
				base.SetY(y);
				m_colorRow = m_pBlock.Value.color.row[y];

				m_alphaRow = m_pBlock.Value.alpha.row[y];
			}

			protected override Color8888 GetColor(int x, int y)
			{
				var color = base.GetColor(x, y);
				var bits = (m_alphaRow >> (x * 4)) & 0xF;
				color.a = (byte)((bits * 0xFF) / 0xF);
				return color;
			}
		}


		private class DXT5Decoder 
			: DXTDecoder<DXT5Block>
		{
			protected uint[] m_alphas = new uint[8];
			protected uint m_alphaBits;
			int m_offset;

			public override int BytesPerBlock
			{
				get { return 16; }
			}

			public override bool IsDXT1
			{
				get { return true; }
			}

			protected override void Setup(DXTBlockWrapper<DXT5Block> pBlock)
			{
				base.Setup(pBlock);
				GetBlockColors(m_pBlock, m_colors, IsDXT1);

				var block = m_pBlock.Value.alpha;
				m_alphas[0] = block.alpha[0];
				m_alphas[1] = block.alpha[1];
				if (m_alphas[0] > m_alphas[1])
				{
					// 8 alpha block
					for (var i = 0; i < 6; i++)
					{
						m_alphas[i + 2] = (uint)((6 - i) * m_alphas[0] + (1 + i) * m_alphas[1] + 3) / 7;
					}
				}
				else
				{
					// 6 alpha block
					for (var i = 0; i < 4; i++)
					{
						m_alphas[i + 2] = (uint)((4 - i) * m_alphas[0] + (1 + i) * m_alphas[1] + 2) / 5;
					}
					m_alphas[6] = 0;
					m_alphas[7] = 0xFF;
				}
			}

			protected override void SetY(int y)
			{
				base.SetY(y);
				m_colorRow = m_pBlock.Value.Color.row[y];

				var i = y / 2;
				var block = m_pBlock.Value.alpha;
				m_alphaBits = (uint)(block.data[0 + i * 3]) | ((uint)(block.data[1 + i * 3]) << 8) | ((uint)(block.data[2 + i * 3]) << 16);
				m_offset = (y & 1) * 12;
			}

			protected override Color8888 GetColor(int x, int y)
			{
				var color = base.GetColor(x, y);
				var bits = (m_alphaBits >> (x * 3 + m_offset)) & 7;
				color.a = (byte)m_alphas[bits];
				return color;
			}
		}

		private interface IDXTDecoder
		{
			int BytesPerBlock { get; }
			bool IsDXT1 { get; }
			Bitmap Decode(DDSURFACEDESC2 desc, Stream stream);
		}

		private abstract class DXTDecoder<TBlock> : IDXTDecoder
			where TBlock : IDXTBlock
		{
			public abstract int BytesPerBlock { get; }
			public abstract bool IsDXT1 { get; }			

			protected Color8888[] m_colors = new Color8888[4];
			protected DXTBlockWrapper<TBlock> m_pBlock;
			protected uint m_colorRow;

			protected virtual void Setup(DXTBlockWrapper<TBlock> pBlock)
			{
				m_pBlock = pBlock;
			}

			protected void GetBlockColors(DXTBlockWrapper<TBlock> block, Color8888[] colors, bool isDXT1)
			{
				int i;
				for (i = 0; i < 2; i++)
				{
					colors[i].a = 0xff;
					colors[i].r = (byte)(block.Colors[i].R * 0xff / 0x1f);
					colors[i].g = (byte)(block.Colors[i].G * 0xff / 0x3f);
					colors[i].b = (byte)(block.Colors[i].B * 0xff / 0x1f);
				}

				//			WORD *wCol = (WORD *)block.colors;
				//			if (wCol[0] > wCol[1] || !isDXT1) {
				if (block.Value.Color.colors[0].clr565 > block.Value.Color.colors[1].clr565 || !isDXT1)
				{
					// 4 color block
					for (i = 0; i < 2; i++)
					{
						colors[i + 2].a = 0xff;
						colors[i + 2].r = (byte)(((ushort)(colors[0].r) * (2 - i) + (ushort)(colors[1].r) * (1 + i)) / 3);
						colors[i + 2].g = (byte)(((ushort)(colors[0].g) * (2 - i) + (ushort)(colors[1].g) * (1 + i)) / 3);
						colors[i + 2].b = (byte)(((ushort)(colors[0].b) * (2 - i) + (ushort)(colors[1].b) * (1 + i)) / 3);
					}
				}
				else
				{
					// 3 color block, number 4 is transparent
					colors[2].a = 0xff;
					colors[2].r = (byte)(((ushort)(colors[0].r) + (ushort)(colors[1].r)) / 2);
					colors[2].g = (byte)(((ushort)(colors[0].g) + (ushort)(colors[1].g)) / 2);
					colors[2].b = (byte)(((ushort)(colors[0].b) + (ushort)(colors[1].b)) / 2);

					colors[3].a = 0x00;
					colors[3].g = 0x00;
					colors[3].b = 0x00;
					colors[3].r = 0x00;
				}
			}

			protected virtual void SetY(int y)
			{
				//m_colorRow = m_pBlock.Block.Color.row[y];
			}

			protected virtual Color8888 GetColor(int x, int y)
			{
				var bits = (m_colorRow >> (x * 2)) & 3;
				return m_colors[bits];
			}


			protected unsafe virtual void DecodeDXTBlock(byte* pbDst, DXTBlockWrapper<TBlock> block, int dstPitch, int bw, int bh)
			{
				Setup(block);
				for (var y = 0; y < bh; y++)
				{
					var dst = pbDst + y * dstPitch;
					SetY(y);
					for (var x = 0; x < bw; x++)
					{
						var color = GetColor(x, y);
						*dst = color.r;
						dst++;
						*dst = color.g;
						dst++;
						*dst = color.b;
						dst++;
						*dst = color.a;
						dst++;
					}
				}
			}


			public Bitmap Decode(DDSURFACEDESC2 desc, Stream stream)
			{
				var width = (int)desc.dwWidth & ~3;
				var height = (int)desc.dwHeight & ~3;

				// allocate a 32-bit dib
				var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				var bpp = 4; // bytes per pixel
				var line = width * bpp;

				var reader = new BinaryReader(stream);
								
				var blocks = new DXTBlockWrapper<TBlock>[(width + 3) / 4];
				var widthRest = (int)width & 3;
				var heightRest = (int)height & 3;
				var inputLine = (width + 3) / 4;
				var y = 0;

				var bitmapData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
				
				var ptrPixels = bitmapData.Scan0;
				unsafe
				{
					// Get the pointer to the image bits.
					var stride = bitmapData.Stride;

					byte *pBits;
					if (stride > 0)
						pBits = (byte*)ptrPixels.ToPointer();
					else
						pBits = (byte*)ptrPixels.ToPointer() + stride * (height - 1);

/*					if (false)
					{
						if (stride > 0)
							pBits = (byte*)ptrPixels.ToPointer() + stride * (height - 1);
						else
							pBits = (byte*)ptrPixels.ToPointer();
						stride = -stride;
					}
*/

					if (height >= 4)
					{
						for (; y < height; y += 4)
						{
							for (var j = 0; j < blocks.Length; j++)
							{
								var streamPos = stream.Position;
								blocks[j] = new DXTBlockWrapper<TBlock>((TBlock)reader.ReadStruct(typeof(TBlock)));
								stream.Position = streamPos + BytesPerBlock;
							}
							
							var pbDst = pBits + y * stride;
//							pbDst = pBits + (height - y - 1) * stride;

							var blockPos = 0;
							if (width >= 4)
							{
								for (var x = 0; x < width; x += 4)
								{
									DecodeDXTBlock(pbDst, blocks[blockPos++], stride, 4, 4);
									//pbSrc += INFO::bytesPerBlock;
									pbDst += 4 * 4;
								}
							}
							if (widthRest > 0)
							{
								DecodeDXTBlock(pbDst, blocks[blockPos++], stride, widthRest, 4);
							}
						}
					}
					if (heightRest > 0)
					{
						for (var j = 0; j < blocks.Length; j++)
						{
							var streamPos = stream.Position;
							blocks[j] = new DXTBlockWrapper<TBlock>((TBlock)reader.ReadStruct(typeof(TBlock)));
							stream.Position = streamPos + BytesPerBlock;
						}

						var pbDst = pBits + y * stride;
						//							pbDst = pBits + (height - y - 1) * stride;

						var blockPos = 0;
						if (width >= 4)
						{
							for (var x = 0; x < width; x += 4)
							{
								DecodeDXTBlock(pbDst, blocks[blockPos++], stride, 4, heightRest);
								// pbSrc += INFO::bytesPerBlock;
								pbDst += 4 * 4;
							}
						}
						if (widthRest > 0)
						{
							DecodeDXTBlock(pbDst, blocks[blockPos++], stride, widthRest, heightRest);
						}
					}
				} // unsafe
				bmp.UnlockBits(bitmapData);

				return bmp;
			}
		}
	}





	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct Color8888
	{
		public byte b;
		public byte g;
		public byte r;
		public byte a;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct Color888
	{
		public byte b;
		public byte g;
		public byte r;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct Color565
	{
		public ushort clr565;

//		public ushort b1; // 5
//		public ushort g; //6;
//		public ushort r; //5
		public ushort b
		{
			get { return (ushort)(clr565 >> 11); }
		}
		public ushort g
		{
			get { return (ushort)((clr565 >> 5) & 0x3F); }
		}
		public ushort r
		{
			get { return (ushort)(clr565 & 0x1F); }
		}

		internal Color ToColor()
		{
			return Color.FromArgb(r, g, b);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXTColBlock
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.Struct)]
		public Color565[] colors;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] row;
	}	

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXTAlphaBlockExplicit
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public ushort[] row;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXTAlphaBlock3BitLinear
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public byte[] alpha;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] data;
	}


	interface IDXTBlock
	{
		DXTColBlock Color { get; }
	}
	
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXT1Block : IDXTBlock
	{
		[MarshalAs(UnmanagedType.Struct)]
		public DXTColBlock color;

		public DXTColBlock Color
		{
			get { return color; }
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXT3Block : IDXTBlock
	{		// also used by dxt2
		[MarshalAs(UnmanagedType.Struct)]
		public DXTAlphaBlockExplicit alpha;
		[MarshalAs(UnmanagedType.Struct)]
		public DXTColBlock color;

		public DXTColBlock Color
		{
			get { return color; }
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal class DXT5Block : IDXTBlock
	{	// also used by dxt4
		[MarshalAs(UnmanagedType.Struct)]
		public DXTAlphaBlock3BitLinear alpha;
		[MarshalAs(UnmanagedType.Struct)]
		public DXTColBlock color;

		public DXTColBlock Color
		{
			get { return color; }
		}
	}

	internal class DXTBlockWrapper<Block>
		where Block : IDXTBlock
	{
		public Color[] Colors;

		public Block Value;

		public DXTBlockWrapper(Block value)
		{
			Value = value;
			Colors = new Color[value.Color.colors.Length];
			for (var i = 0; i < Colors.Length; i++)
				Colors[i] = value.Color.colors[i].ToColor();
		}
	}

	/*
	internal abstract class DXT_INFO<DXTBlock>
	{
		private DXTBlock _block;

		public DXTBlock Block
		{
			get { return _block; }
			set { _block = value; }
		}

		public abstract bool isDXT1 { get; }
		public abstract int bytesPerBlock { get; }
	}

	internal class DXT_INFO_1 : DXT_INFO<DXT1Block>
	{
		public override bool isDXT1
		{
			get { return true; }
		}

		public override int bytesPerBlock
		{
			get { return 8; }
		}
	}

	internal class DXT_INFO_3 : DXT_INFO<DXT3Block>
	{
		public override bool isDXT1
		{
			get { return true; }
		}

		public override int bytesPerBlock
		{
			get { return 16; }
		}
	}

	internal class DXT_INFO_5 : DXT_INFO<DXT5Block>
	{
		public override bool isDXT1
		{
			get { return true; }
		}

		public override int bytesPerBlock
		{
			get { return 16; }
		}
	}

	internal abstract class DXT_BLOCKDECODER_BASE<INFO, DXTBlock>
		where INFO : DXT_INFO<DXTBlock>
		where DXTBlock : IDXTBlock
	{
		protected Color8888[] m_colors = new Color8888[4];
		protected INFO m_pBlock;
		protected uint m_colorRow;

		public virtual void Setup(DXTBlock pBlock)
		{
			m_pBlock.Block = pBlock;
			GetBlockColors(m_pBlock.Block.Color, m_colors, m_pBlock.isDXT1);
		}

		private void GetBlockColors(DXTColBlock block, Color8888[] colors, bool isDXT1)
		{
			int i;
			for (i = 0; i < 2; i++)
			{
				colors[i].a = 0xff;
				colors[i].r = (byte)(block.colors[i].r * 0xff / 0x1f);
				colors[i].g = (byte)(block.colors[i].g * 0xff / 0x3f);
				colors[i].b = (byte)(block.colors[i].b * 0xff / 0x1f);
			}

//			WORD *wCol = (WORD *)block.colors;
//			if (wCol[0] > wCol[1] || !isDXT1) {
			if (block.colors[0].b > block.colors[0].g || !isDXT1)
			{
				// 4 color block
				for (i = 0; i < 2; i++)
				{
					colors[i + 2].a = 0xff;
					colors[i + 2].r = (byte)(((ushort)(colors[0].r) * (2 - i) + (ushort)(colors[1].r) * (1 + i)) / 3);
					colors[i + 2].g = (byte)(((ushort)(colors[0].g) * (2 - i) + (ushort)(colors[1].g) * (1 + i)) / 3);
					colors[i + 2].b = (byte)(((ushort)(colors[0].b) * (2 - i) + (ushort)(colors[1].b) * (1 + i)) / 3);
				}
			}
			else
			{
				// 3 color block, number 4 is transparent
				colors[2].a = 0xff;
				colors[2].r = (byte)(((ushort)(colors[0].r) + (ushort)(colors[1].r)) / 2);
				colors[2].g = (byte)(((ushort)(colors[0].g) + (ushort)(colors[1].g)) / 2);
				colors[2].b = (byte)(((ushort)(colors[0].b) + (ushort)(colors[1].b)) / 2);

				colors[3].a = 0x00;
				colors[3].g = 0x00;
				colors[3].b = 0x00;
				colors[3].r = 0x00;
			}
		}

		public virtual void SetY(int y) 
		{
			m_colorRow = m_pBlock.Block.Color.row[y];
		}

		public virtual Color8888 GetColor(int x, int y) 
		{
			uint bits = (m_colorRow >> (x * 2)) & 3;
			return m_colors[bits];
		}
	}

	internal class DXT_BLOCKDECODER_1 : DXT_BLOCKDECODER_BASE<DXT_INFO_1, DXT1Block> 
	{
		public DXT_BLOCKDECODER_1()
		{
		}
	}

	internal class DXT_BLOCKDECODER_3 : DXT_BLOCKDECODER_BASE<DXT_INFO_3, DXT3Block>
	{
		protected uint m_alphaRow;

		public DXT_BLOCKDECODER_3()
		{
		}
		
		public override void  SetY(int y)
		{
 			base.SetY(y);
			m_alphaRow = this.m_pBlock.Block.alpha.row[y];
		}

		public override Color8888 GetColor(int x, int y)
		{
			Color8888 color = base.GetColor(x, y);
			uint bits = (m_alphaRow >> (x * 4)) & 0xF;
			color.a = (byte)((bits * 0xFF) / 0xF);
			return color;
		}
	}

	internal class DXT_BLOCKDECODER_5 : DXT_BLOCKDECODER_BASE<DXT_INFO_5, DXT5Block>
	{
		protected uint[] m_alphas = new uint[8];
		protected uint m_alphaBits;
		int m_offset;

		public override void Setup(DXT5Block pBlock)
		{
			base.Setup(pBlock);

			DXTAlphaBlock3BitLinear block = m_pBlock.Block.alpha;
			m_alphas[0] = block.alpha[0];
			m_alphas[1] = block.alpha[1];
			if (m_alphas[0] > m_alphas[1])
			{
				// 8 alpha block
				for (int i = 0; i < 6; i++)
				{
					m_alphas[i + 2] = (uint)((6 - i) * m_alphas[0] + (1 + i) * m_alphas[1] + 3) / 7;
				}
			}
			else
			{
				// 6 alpha block
				for (int i = 0; i < 4; i++)
				{
					m_alphas[i + 2] = (uint)((4 - i) * m_alphas[0] + (1 + i) * m_alphas[1] + 2) / 5;
				}
				m_alphas[6] = 0;
				m_alphas[7] = 0xFF;
			}
		}

		public override void SetY(int y)
		{
			base.SetY(y);
			int i = y / 2;
			DXTAlphaBlock3BitLinear block = m_pBlock.Block.alpha;
			m_alphaBits = (uint)(block.data[0 + i * 3]) | ((uint)(block.data[1 + i * 3]) << 8) | ((uint)(block.data[2 + i * 3]) << 16);
			m_offset = (y & 1) * 12;
		}

		public override Color8888 GetColor(int x, int y)
		{
			Color8888 color = base.GetColor(x, y);
			uint bits = (m_alphaBits >> (x * 3 + m_offset)) & 7;
			color.a = (byte)m_alphas[bits];
			return color;
		}
	}	
	 * */
}
