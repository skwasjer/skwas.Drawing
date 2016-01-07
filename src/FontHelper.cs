using System;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;

namespace skwas.Drawing
{
	public static class FontHelper
	{
		// This is used to correct a bug in GDI+. 
		// Private fonts return garbage in GetKerningPairs if they are not installed on the system.
		// Also, they don't print correctly.
		[DllImport("gdi32.dll")]
		private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, int cbFont, IntPtr pdv, [In] ref uint pcFonts);

		[DllImport("gdi32.dll")]
		private static extern IntPtr AddFontResourceEx(string filename, int flags, IntPtr pdv);

		private static uint _dummy;
		private static PrivateFontCollection _fc = new PrivateFontCollection();
		private static Collection<string> _fontNames = new Collection<string>();

		public static Collection<string> FontNames
		{
			get { return _fontNames; }
		}

		public static PrivateFontCollection PrivateFontCollection
		{
			get { return _fc; }
		}

		/// <summary>
		/// Adds a private font from one or more files. Supported font files are .fon, .fnt, .ttf, .ttc, .fot, .otf, .mmm, .pfb, .pfm. For fonts whose information comes from more than one file, seperate each file with a pipeline (|).
		/// </summary>
		/// <param name="filename"></param>
		public static void AddFont(string filename)
		{
			AddFontResourceEx(filename, 0x10, IntPtr.Zero);   // FR_PRIVATE
		}

		public static void AddMemoryFont(string fontName, byte[] fontData)
		{
			if (_fontNames.Contains(fontName))
				throw new ArgumentException("Specified font is already added to memory table.");
			_fontNames.Add(fontName);
			InternalAddMemoryFont(fontData);
		}

		private static unsafe void InternalAddMemoryFont(byte[] fontData)
		{
			fixed (byte* pFontData = fontData)
			{
				_fc.AddMemoryFont((IntPtr)pFontData, fontData.Length);
				AddFontMemResourceEx((IntPtr)pFontData, fontData.Length, IntPtr.Zero, ref _dummy);
			}
		}
	}
}
