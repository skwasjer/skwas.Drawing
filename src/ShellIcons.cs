using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace skwas.Drawing
{
	/// <summary>
	/// Provides access to shell icons.
	/// </summary>
	public static class ShellIcons
	{
		#region Interop

		private const uint SHGFI_ICON = 0x100;

		[DllImport("shell32.dll")]
		private static extern IntPtr SHGetFileInfo(
			string pszPath,
			uint dwFileAttributes,
			ref SHFILEINFO psfi,
			uint cbSizeFileInfo,
			uint uFlags);

		[StructLayout(LayoutKind.Sequential)]
		private struct SHFILEINFO
		{
			public IntPtr hIcon;
			public IntPtr iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};

		#endregion

		/// <summary>
		/// Gets the icon for specified file.
		/// </summary>
		/// <param name="path">The path to the file.</param>
		/// <param name="iconSize"></param>
		/// <returns>An icon or null if the icon could not be found or the file does not exist.</returns>
		public static Icon GetFileIcon(string path, IconSize iconSize = IconSize.Small)
		{
			var shinfo = new SHFILEINFO();
			var hImg = SHGetFileInfo(
				path, 
				0, 
				ref shinfo,
				(uint)Marshal.SizeOf(shinfo),
				SHGFI_ICON | (uint)iconSize
			);

			return shinfo.hIcon == IntPtr.Zero ? null : Icon.FromHandle(shinfo.hIcon);
		}
	}

	/// <summary>
	/// The size of the icon.
	/// </summary>
	public enum IconSize
	{
		/// <summary>
		/// Large icon.
		/// </summary>
		Large,	// SHGFI_LARGEICON
		/// <summary>
		/// Small icon.
		/// </summary>
		Small	// SHGFI_SMALLICON
	}
}
