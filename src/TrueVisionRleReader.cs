using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace skwas.Drawing
{
	/// <summary>
	/// Reader for Truevision Run Length Encoding, as used by TGA file format.
	/// </summary>
    public class TruevisionRleReader : BinaryReader
	{
		private byte[] _buffer;
		private int _position;
		private readonly int _bytesPerPixel;

		/// <summary>
		/// Initializes a new instance of <see cref="TruevisionRleReader"/> using specified pixel<paramref name="format"/>.
		/// </summary>
		/// <param name="stream">The RLE encoded stream.</param>
		/// <param name="format">The pixel format of the TGA image.</param>
		public TruevisionRleReader(Stream stream, PixelFormat format) 
			: this(stream, format, Encoding.ASCII)
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="TruevisionRleReader"/> using specified pixel<paramref name="format"/>.
		/// </summary>
		/// <param name="stream">The RLE encoded stream.</param>
		/// <param name="format">The pixel format of the TGA image.</param>
		/// <param name="encoding">The encoding to use when reading from the stream.</param>
		public TruevisionRleReader(Stream stream, PixelFormat format, Encoding encoding)
			: base(stream, encoding)
		{
			var bitsPerPixel = Image.GetPixelFormatSize(format);
			_bytesPerPixel = bitsPerPixel / 8;
		}

		#region Overrides of BinaryReader

		/// <summary>
		/// Reads the next (decoded) byte from the stream.
		/// </summary>
		/// <returns></returns>
		public override byte ReadByte()
		{
			// When buffer is empty, set up the buffer from stream.
			if (_buffer == null)
			{
				_position = 0;

				var packet = base.ReadByte();
				// Decode the packet.
				var isRawPacket = (packet & 0x80) == 0;
				var pixelCount = (packet & 0x7F) + 1;

				_buffer = new byte[_bytesPerPixel*pixelCount];
				if (isRawPacket)
				{
					// Read raw pixels.
					for (var i = 0; i < _buffer.Length; i++)
						_buffer[i] = base.ReadByte();
				}
				else
				{
					// Read a single pixel from stream (bytesPerPixel).
					for (var i = 0; i < _bytesPerPixel; i++)
						_buffer[i] = base.ReadByte();

					// Duplicate the first pixel until buffer is full.
					for (var i = _bytesPerPixel; i < _buffer.Length; i++)
						_buffer[i] = _buffer[i % _bytesPerPixel];
				}
			}

			// While still a valid position, return the next pixel from buffer.
			var retVal = _buffer[_position++];
			// Once we pass the bounds of buffer, we need to read next packet.
			if (_position >= _buffer.Length) _buffer = null;

			return retVal;
		}

		#endregion
	}
}
