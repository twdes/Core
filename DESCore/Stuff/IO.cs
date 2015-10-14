using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
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

		/// <summary>Liest die Daten eines Streams in einer Array.</summary>
		/// <param name="src">Stream dessen Daten in ein Array gelesen werden sollen.</param>
		/// <param name="bufferSize">Größe des Buffers, falls die Länge nicht ermittelt werden kann.</param>
		/// <returns>Die gelesenen Daten.</returns>
		public static byte[] ReadInArray(this Stream src, int bufferSize = 81920)
		{
			if (src is MemoryStream)
				return ((MemoryStream)src).ToArray();
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
	} // class Procs
}
