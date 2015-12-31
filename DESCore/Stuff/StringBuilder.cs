using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		public static StringBuilder WriteFreshLine(this StringBuilder sb)
		{
			if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
				sb.AppendLine();

			return sb;
		} // func WriteFreshLine

		public static StringBuilder WriteSeperator(this StringBuilder sb, string sHeader = null)
		{
			sb.WriteFreshLine();
			if (sHeader == null)
				sb.AppendLine(new string('-', 80));
			else
			{
				sb.Append("-- ");
				sb.Append(sHeader);
				int iRest = 76 - sHeader.Length;
				if (iRest > 0)
					sb.Append(' ').AppendLine(new string('-', iRest));
			}
			return sb;
		} // proc WriteSeperator

		public static StringBuilder WriteException(this StringBuilder sb, Exception e)
		{
			ExceptionFormatter.FormatPlainText(sb, e);
			return sb;
		} // func WriteException
	} // class Procs

	#region -- struct FileSize ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public struct FileSize : IComparable, IComparable<FileSize>, IEquatable<FileSize>, IFormattable
	{
		private readonly long fileSize;

		public FileSize(long _fileSize)
		{
			fileSize = _fileSize;
		} // ctor

		public override int GetHashCode()
			=> fileSize.GetHashCode();

		public override bool Equals(object obj)
			=> obj is FileSize ? Equals((FileSize)obj) : false;

		public bool Equals(FileSize other)
			=> fileSize.Equals(other.fileSize);

		public int CompareTo(object obj)
			=> obj is FileSize ? CompareTo((FileSize)obj) : -1;

		public int CompareTo(FileSize other)
			=> fileSize.CompareTo(other.fileSize);

		public override string ToString()
			=> ToString(null, null);

		public string ToString(string format, IFormatProvider formatProvider)
			=> Format(fileSize, format, formatProvider) ?? fileSize.ToString(format, formatProvider);

		public long Value => fileSize;

		internal static string Format(long fileSize, string format, IFormatProvider formatProvider)
		{
			var byteBase = 0;

			#region -- calculate the best unit --
			if (format == "XiB")
			{

				if (fileSize < 2048)
					byteBase = 0;
				else if (fileSize < 2097152)
					byteBase = 1;
				else if (fileSize < 1073741824)
					byteBase = 2;
				else if (fileSize < 1099511627776)
					byteBase = 3;
				else
					byteBase = 4;
			}
			else
			{
				switch (format)
				{
					case "Bytes":
						byteBase = 0;
						break;
					case "KiB":
						byteBase = 1;
						break;
					case "MiB":
						byteBase = 2;
						break;
					case "GiB":
						byteBase = 3;
						break;
					case "TiB":
						byteBase = 4;
						break;
					case "PiB":
						byteBase = 5;
						break;
					default:
						return null;
				}
			}
			#endregion

			switch (byteBase)
			{
				case 0:
					return fileSize.ToString("N0", formatProvider) + " Byte";
				case 1:
					return (fileSize / 1024).ToString("N0", formatProvider) + " KiB";
				case 2:
					return ((float)fileSize / 1048576).ToString("N1", formatProvider) + " MiB";
				case 3:
					return ((float)fileSize / 1073741824).ToString("N1", formatProvider) + " GiB";
				case 4:
					return ((float)fileSize / 1099511627776).ToString("N1", formatProvider) + " TiB";
				case 5:
					return ((float)fileSize / (1L << 50)).ToString("N1", formatProvider) + " PiB";
				default:
					return null;
			}
		} // func FormatFileSize

		public static implicit operator FileSize(int value)
			=> new FileSize(value);

		public static implicit operator FileSize(long value)
			=> new FileSize(value);

		public static implicit operator long(FileSize value)
			=> value.fileSize;
	} // struct FileSize

	#endregion

	#region -- class FormatFileSizeConverter --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class FormatFileSizeConverter : IFormatProvider, ICustomFormatter
	{
		private readonly IFormatProvider formatProvider = null;

		private FormatFileSizeConverter()
		{
		} // ctor

		public FormatFileSizeConverter(IFormatProvider formatProvider)
		{
			this.formatProvider = formatProvider;
		} // ctor

		object IFormatProvider.GetFormat(Type formatType)
		{
			if (formatType == typeof(ICustomFormatter))
				return this;
			else
				return FormatProvider.GetFormat(formatType);
		} // func IFormatProvider.GetFormat

		string ICustomFormatter.Format(string format, object arg, IFormatProvider formatProvider)
		{
			// XiB, KiB, MiB, ...
			if (format != null && arg != null)
				return FileSize.Format(Convert.ToInt64(arg, formatProvider), format, formatProvider);

			return null;
		} // func ICustomFormatter.Format

		public IFormatProvider FormatProvider => formatProvider ?? CultureInfo.CurrentUICulture;

		/// <summary>Default provider.</summary>
		public IFormatProvider Default { get; } = new FormatFileSizeConverter();
	} // class FormatFileSizeConverter

	#endregion
}
