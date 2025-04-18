﻿#region -- copyright --
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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class WindowStream -----------------------------------------------------

	/// <summary>Special stream, the uses only an window of the base stream.</summary>
	public sealed class WindowStream : Stream
	{
		private readonly Stream baseStream;
		private readonly long offset;
		private readonly bool writeAble;
		private readonly bool leaveOpen;

		private long position = 0;
		private long length;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="baseStream">Stream to read from.</param>
		/// <param name="offset">Offset within the base stream.</param>
		/// <param name="length">Length to read/write.</param>
		/// <param name="writeAble">Are write operations allowed.</param>
		/// <param name="leaveOpen"></param>
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

		/// <inheritdoc/>>
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

		#endregion

		/// <inheritdoc/>
		public override void Flush()
			=> baseStream.Flush();

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count)
		{
			// check upper bound
			if (this.length >= 0)
			{
				var length = Length;
				if (count + position > length)
					count = unchecked((int)(length - position));
			}

			// read baseStream
			if (count > 0)
			{
				var r = baseStream.Read(buffer, offset, count);
				position += r;
				return r;
			}
			else
				return 0;
		} // func Read

		/// <summary></summary>
		/// <param name="offset"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
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

		/// <summary></summary>
		/// <param name="value"></param>
		public override void SetLength(long value)
			=> this.length = value;

		/// <summary></summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (!CanWrite)
				throw new NotSupportedException();

			if (length >= 0 && count + position > length)
				throw new ArgumentOutOfRangeException(nameof(count));

			baseStream.Write(buffer, offset, count);
		} // proc Write

		/// <summary></summary>
		public override bool CanRead => baseStream.CanRead;
		/// <summary></summary>
		public override bool CanSeek => baseStream.CanSeek;
		/// <summary></summary>
		public override bool CanWrite => writeAble && baseStream.CanWrite;

		/// <summary></summary>
		public override long Length => length < 0 ? baseStream.Length - offset : length;
		/// <summary></summary>
		public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
	} // class WindowStream

	#endregion

	/// <summary></summary>
	public static partial class Procs
	{
		private static readonly Regex remotePathRegex = new Regex(@"^\\\\(?<m>\w+)\\(?<s>\w+)\\(?<p>.*)", RegexOptions.Compiled | RegexOptions.Singleline);

		#region -- ReadInArray --------------------------------------------------------

		/// <summary></summary>
		/// <param name="src"></param>
		/// <param name="bufferSize"></param>
		/// <returns></returns>
		public static async Task<byte[]> ReadInArrayAsync(this Stream src, int bufferSize = 81920)
		{
			if (src is MemoryStream tmp)
			{
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
					await src.ReadAsync(buffer, 0, (int)src.Length);
					return buffer;
				}
			}
			else
			{
				using (var dst = new MemoryStream(bufferSize))
				{
					await src.CopyToAsync(dst, bufferSize);
					return dst.Length == 0 ? null : dst.ToArray();
				}
			}
		} // func ReadInArrayAsync

		/// <summary>Liest die Daten eines Streams in ein Array.</summary>
		/// <param name="src">Stream dessen Daten in ein Array gelesen werden sollen.</param>
		/// <param name="bufferSize">Größe des Buffers, falls die Länge nicht ermittelt werden kann.</param>
		/// <returns>Die gelesenen Daten oder null im Falle eines Streams ohne Daten.</returns>
		public static byte[] ReadInArray(this Stream src, int bufferSize = 81920)
		{
			if (src is MemoryStream tmp)
			{
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
			{
				using (var dst = new MemoryStream(bufferSize))
				{
					src.CopyTo(dst, bufferSize);
					return dst.Length == 0 ? null : dst.ToArray();
				}
			}
		} // func ReadInArray

		/// <summary></summary>
		/// <param name="src"></param>
		/// <param name="byteCount"></param>
		/// <param name="bufferSize"></param>
		public static async Task<long> SkipBytesAsync(this Stream src, long byteCount = Int64.MaxValue, int bufferSize = 81920)
		{
			var buffer = new byte[bufferSize];
			var skippedBytes = 0L;
			while (true)
			{
				var restSize = Math.Min(bufferSize, byteCount - skippedBytes);
				if (restSize > 0) // skip needed
				{
					var r = await src.ReadAsync(buffer, 0, (int)restSize);
					if (r <= 0) // eof reached
						break;
					skippedBytes += r;
				}
				else
					break;
			}
			return skippedBytes;
		} // func SkipBytes

		/// <summary></summary>
		/// <param name="src"></param>
		/// <param name="byteCount"></param>
		/// <param name="bufferSize"></param>
		public static long SkipBytes(this Stream src, long byteCount = Int64.MaxValue, int bufferSize = 81920)
		{
			var buffer = new byte[bufferSize];
			var skippedBytes = 0L;
			while (true)
			{
				var restSize = Math.Min(bufferSize, byteCount - skippedBytes);
				if (restSize > 0) // skip needed
				{
					var r = src.Read(buffer, 0, (int)restSize);
					if (r <= 0) // eof reached
						break;
					skippedBytes += r;
				}
				else
					break;
			}
			return skippedBytes;
		} // func SkipBytes

		#endregion

		#region -- FileFilterToRegex --------------------------------------------------

		/// <summary>Simple filter to regex.</summary>
		/// <param name="filter"></param>
		/// <returns></returns>
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

		/// <summary>Append the trailing directory seperator</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string IncludeTrailingBackslash(string path)
			=> String.IsNullOrEmpty(path) ? path : path[path.Length - 1] == Path.DirectorySeparatorChar ? path : path + Path.DirectorySeparatorChar;

		/// <summary>Remove the trailing directory seperator</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string ExcludeTrailingBackslash(string path)
			=> String.IsNullOrEmpty(path) ? path : path[path.Length - 1] == Path.DirectorySeparatorChar ? path.Substring(0, path.Length - 1) : path;

		/// <summary>Split a unc path in his parts.</summary>
		/// <param name="remotePath"></param>
		/// <param name="serverName"></param>
		/// <param name="shareName"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool TrySplitUncPath(string remotePath, out string serverName, out string shareName, out string path)
		{
			var m = remotePathRegex.Match(remotePath);
			if (m.Success)
			{
				serverName = m.Groups["m"].Value;
				shareName = m.Groups["s"].Value;
				path = m.Groups["p"].Value;
				return true;
			}
			else
			{
				serverName = null;
				shareName = null;
				path = null;
				return false;
			}
		} // func TrySplitUncPath

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="replaceChar"></param>
		/// <returns></returns>
		public static string FilterFileName(string name, char replaceChar = '_')
		{
			if (String.IsNullOrEmpty(name))
				return name;

			var invalidChars = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(name.Length);
			for (var i = 0;i< name.Length;i++)
			{
				var c = name[i];
				if (Array.IndexOf(invalidChars, c) >= 0)
					sb.Append(replaceChar);
				else
					sb.Append(c);
			}
			return sb.ToString();
		} // func FilterFileName

		private static string GetUniqueFileName(string path, string name, string extension, int idx)
			=> Path.Combine(path, idx == 0 ? name + extension : name + "." + idx.ToString() + extension);

		/// <summary>Create a unique file name.</summary>
		/// <param name="fileName">Filename</param>
		/// <returns></returns>
		public static string GetUniqueFileName(string fileName)
		{
			var idx = 0;

			string path = null;
			string extension = null;
			string name = null;

			while (File.Exists(fileName))
			{
				if (idx == 0)
				{
					path = Path.GetDirectoryName(fileName);
					extension = Path.GetExtension(fileName);
					name = Path.GetFileNameWithoutExtension(fileName);
				}
				fileName = GetUniqueFileName(path, name, extension, idx++);
			}

			return fileName;
		} // string GetUniqueFileName

		/// <summary>Create a unique file name.</summary>
		/// <param name="fileInfo">Filename</param>
		/// <returns></returns>
		public static FileInfo GetUniqueFileName(this FileInfo fileInfo)
			=> new FileInfo(GetUniqueFileName(fileInfo.FullName));

		/// <summary>Utf-8 encoder/decoder that not emits the utf-8 header.</summary>
		public static Encoding Utf8Encoding { get; } = new UTF8Encoding(false);
	} // class Procs
}
