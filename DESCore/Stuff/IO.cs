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
