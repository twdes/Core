using System;
using System.Globalization;

namespace TecWare.DE.Stuff
{
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

		public bool IsEmpty => fileSize == 0;

		private static readonly string[] unitSuffix = new string[] { "Byte", "KiB", "MiB", "GiB", "TiB", "PiB" };

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
				byteBase = Array.IndexOf(unitSuffix, format);
				if (byteBase == -1)
					return null;
			}
			#endregion

			switch (byteBase)
			{
				case 0:
					return fileSize.ToString("N0", formatProvider) + " Byte";
				case 1:
					return (fileSize >> 10).ToString("N0", formatProvider) + " KiB";
				default:
					return ((float)fileSize / (1L << (byteBase * 10))).ToString("N1", formatProvider) + " " + unitSuffix[byteBase];
			}
		} // func FormatFileSize

		public static FileSize Parse(string s)
			=> Parse(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo);

		public static FileSize Parse(string s, IFormatProvider formatProvider)
			=> Parse(s, NumberStyles.Number, formatProvider);

		public static FileSize Parse(string s, NumberStyles numberStyles, IFormatProvider formatProvider)
		{
			FileSize result;
			if (TryParse(s, numberStyles, formatProvider, out result))
				return result;
			else
				throw new FormatException();
		} // func Parse

		public static bool TryParse(string s, out FileSize result)
			=> TryParse(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo, out result);

		public static bool TryParse(string s, IFormatProvider formatProvider, out FileSize result)
			=> TryParse(s, NumberStyles.Number, formatProvider, out result);

		public static bool TryParse(string s, NumberStyles numberStyles, IFormatProvider formatProvider, out FileSize result)
		{
			if (String.IsNullOrEmpty(s))
			{
				result = FileSize.Empty;
				return false;
			}

			// find the suffix
			var byteBase = Array.FindIndex(unitSuffix, c => s.EndsWith(c, StringComparison.OrdinalIgnoreCase));
			if (byteBase == -1) // try byte
				byteBase = 0;
			else // truncate the unit
			{
				var sl = unitSuffix[byteBase].Length;
				s = s.Substring(0, s.Length - sl).TrimEnd();
			}

			decimal value;
			if (!Decimal.TryParse(s, numberStyles, formatProvider, out value))
			{
				result = FileSize.Empty;
				return false;
			}

			result = new FileSize(Convert.ToInt64(value * (1L << byteBase * 10)));
			return true;
		} // func TryParse

		public static implicit operator FileSize(int value)
			=> new FileSize(value);

		public static implicit operator FileSize(long value)
			=> new FileSize(value);

		public static implicit operator long(FileSize value)
			=> value.fileSize;

		public static FileSize Empty { get; } = new FileSize();
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
