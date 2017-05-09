#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class WindowStream -------------------------------------------------------

	public sealed class WindowStream : Stream
	{
		private readonly Stream baseStream;
		private readonly long offset;
		private readonly bool writeAble;
		private readonly bool leaveOpen;

		private long position = 0;
		private long length;

		public WindowStream(Stream baseStream, long offset, long length, bool writeAble, bool leaveOpen)
		{
			this.baseStream = baseStream;
			if (offset < 0 && offset > baseStream.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			this.offset = offset;
			this.length = length;
			this.writeAble = writeAble;
			this.leaveOpen = leaveOpen;
			this.position = 0;

			if (CanSeek)
				Seek(0, SeekOrigin.Begin);
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (leaveOpen)
					baseStream.Flush();
				else
					baseStream.Dispose();
			}
			base.Dispose(disposing);
		} // proc Dispose


		public override void Flush()
			=> baseStream.Flush();

		public override int Read(byte[] buffer, int offset, int count)
		{
			var length = Length;
			if (count + position > length)
				count = unchecked((int)(length - position));

			if (count > 0)
			{
				var r = baseStream.Read(buffer, offset, count);
				position += r;
				return r;
			}
			else
				return 0;
		} // func Read

		public override long Seek(long offset, SeekOrigin origin)
		{
			long newPosition;
			switch(origin)
			{
				case SeekOrigin.Begin:
					newPosition = offset;
					break;
				case SeekOrigin.Current:
					newPosition = position + offset;
					break;
				case SeekOrigin.End:
					newPosition = Length - offset;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(origin));
			}

			if (newPosition < 0 || newPosition > Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			// update position
			baseStream.Seek(this.offset + newPosition, SeekOrigin.Begin);
			return position = newPosition;
		} // func Seek

		public override void SetLength(long value)
			=> this.length = value;

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (!CanWrite)
				throw new NotSupportedException();

			if (length >= 0 && count + position > length)
				throw new ArgumentOutOfRangeException(nameof(count));

			baseStream.Write(buffer, offset, count);
		} // proc Write

		public override bool CanRead => baseStream.CanRead;
		public override bool CanSeek => baseStream.CanSeek;
		public override bool CanWrite => writeAble && baseStream.CanWrite;

		public override long Length => length < 0 ? baseStream.Length - offset : length;
		public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
	} // class WindowStream

	#endregion

	public static partial class Procs
	{
		#region -- OpenStreamReader -------------------------------------------------------

		public static StreamReader OpenStreamReader(Stream src, Encoding @default)
		{
			// Ermittle die Codierung
			var encodingSource = @default;
			var detectEncoding = false;
			if (src.CanSeek)
			{
				byte[] bPreamble = new byte[4];
				int iReaded = src.Read(bPreamble, 0, 4);

				if (iReaded >= 3 && bPreamble[0] == 0xEF && bPreamble[1] == 0xBB && bPreamble[2] == 0xBF) // utf-8
					encodingSource = Encoding.UTF8;
				else if (iReaded == 4 && bPreamble[0] == 0x00 && bPreamble[1] == 0x00 && bPreamble[2] == 0xFE && bPreamble[3] == 0xFF) // utf-32 EB
				{
					encodingSource = Encoding.GetEncoding("utf-32"); // is a EL codepage, but the StreamReader should switch to EB
					detectEncoding = true;
				}
				else if (iReaded == 4 && bPreamble[0] == 0xFF && bPreamble[1] == 0xFE && bPreamble[2] == 0x00 && bPreamble[3] == 0x00) // utf-32 EL
					encodingSource = Encoding.GetEncoding("utf-32");
				else if (iReaded >= 2 && bPreamble[0] == 0xFE && bPreamble[1] == 0xFF) // utf-16 EB
					encodingSource = Encoding.BigEndianUnicode;
				else if (iReaded >= 2 && bPreamble[0] == 0xFF && bPreamble[1] == 0xFE) // utf-16 EL
					encodingSource = Encoding.Unicode;

				src.Seek(-iReaded, SeekOrigin.Current);
			}
			else
				detectEncoding = true;
			
			// Öffne den StreamReader
			return new StreamReader(src, encodingSource, detectEncoding);
		} // func OpenStreamReader

		#endregion

		#region -- ReadInArray ------------------------------------------------------------

		/// <summary>Liest die Daten eines Streams in ein Array.</summary>
		/// <param name="src">Stream dessen Daten in ein Array gelesen werden sollen.</param>
		/// <param name="bufferSize">Größe des Buffers, falls die Länge nicht ermittelt werden kann.</param>
		/// <returns>Die gelesenen Daten oder null im Falle eines Streams ohne Daten.</returns>
		public static byte[] ReadInArray(this Stream src, int bufferSize = 81920)
		{
			if (src is MemoryStream)
			{
				var tmp = (MemoryStream)src;
				return tmp.Length == 0 ? null : tmp.ToArray();
			}
			else if (src.CanSeek)
			{
				if (src.Length == 0)
					return null;
				else
				{
					var buffer = new byte[src.Length];
					src.Position = 0;
					src.Read(buffer, 0, (int)src.Length);
					return buffer;
				}
			}
			else
				using (MemoryStream dst = new MemoryStream(bufferSize))
				{
					src.CopyTo(dst, bufferSize);
					return dst.Length == 0 ? null : dst.ToArray();
				}
		} // func ReadInArray

		#endregion

		#region -- FileFilterToRegex ------------------------------------------------------

		public static string FileFilterToRegex(string filter)
		{
			if (String.IsNullOrEmpty(filter))
				return "*.";

			var sb = new StringBuilder("^");

			foreach (var c in filter)
			{
				if (c == '*')
					sb.Append("*.");
				else if (c == '?')
					sb.Append('.');
				else if (Char.IsLetterOrDigit(c))
					sb.Append(c);
				else
					sb.Append('\\').Append(c);
			}
			sb.Append('$');

			return sb.ToString();
		} // func FileFilterToRegex

		#endregion
	} // class Procs
}
