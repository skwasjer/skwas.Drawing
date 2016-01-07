using System;
using System.Drawing;
using System.Windows.Forms;

namespace skwas.Drawing
{
	/// <summary>
	/// Helpers for custom painting.
	/// </summary>
	public static class DrawHelpers
	{
		/// <summary>
		/// Measures if the specified file name fits in the <paramref name="proposedSize"/> when painted using <paramref name="font"/>. Returns a shorter file name with ellipsis if it does not fit, or the original otherwise.
		/// </summary>
		/// <param name="filename">The file name to measure.</param>
		/// <param name="font">The font to measure with.</param>
		/// <param name="proposedSize">The available space for painting the file name.</param>
		/// <returns></returns>
		public static string GetPathEllipsisString(string filename, Font font, Size proposedSize)
		{
			// First, clone the filename byval. The MeasureText method below will replace the contents of the string that we pass, and we don't want the global original value to change.
			var path = string.Copy(filename);

			// Replace the path string with ellipsis if needed.
			TextRenderer.MeasureText(path, font, proposedSize, TextFormatFlags.NoPadding | TextFormatFlags.PathEllipsis | TextFormatFlags.ModifyString);
			// If an ellipses was added, the string should now contain a null terminating character. Trim the string to here...
			return path.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)[0];
		}
	}
}
