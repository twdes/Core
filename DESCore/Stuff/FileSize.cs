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
using System.Globalization;

namespace TecWare.DE.Stuff
{
	#region -- struct FileSize --------------------------------------------------------

	/// <summary>File size data type implementation</summary>
	public struct FileSize : IComparable, IComparable<FileSize>, IEquatable<FileSize>, IFormattable
	{
		private readonly long fileSize;

		/// <summary></summary>
		/// <param name="_fileSize"></param>
		public FileSize(long _fileSize)
		{
			fileSize = _fileSize;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> fileSize.GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is FileSize ? Equals((FileSize)obj) : false;

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(FileSize other)
			=> fileSize.Equals(other.fileSize);

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public int CompareTo(object obj)
			=> obj is FileSize ? CompareTo((FileSize)obj) : -1;

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(FileSize other)
			=> fileSize.CompareTo(other.fileSize);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(null, null);

		/// <summary></summary>
		/// <param name="format"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public string ToString(string format, IFormatProvider formatProvider = null)
			=> Format(fileSize, format, formatProvider) ?? fileSize.ToString(format, formatProvider);

		/// <summary>Core value of the file size in byte.</summary>
		public long Value => fileSize;

		/// <summary>Is the file size zero bytes.</summary>
		public bool IsEmpty => fileSize == 0;

		private static readonly string[] unitSuffix = new string[] { "Byte", "KiB", "MiB", "GiB", "TiB", "PiB" };

		internal static string Format(long fileSize, string format, IFormatProvider formatProvider)
		{
			int byteBase;

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

		/// <summary></summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static FileSize Parse(string s)
			=> Parse(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo);

		/// <summary></summary>
		/// <param name="s"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public static FileSize Parse(string s, IFormatProvider formatProvider)
			=> Parse(s, NumberStyles.Number, formatProvider);

		/// <summary></summary>
		/// <param name="s"></param>
		/// <param name="numberStyles"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public static FileSize Parse(string s, NumberStyles numberStyles, IFormatProvider formatProvider)
			=> TryParse(s, numberStyles, formatProvider, out var result)
				? result
				: throw new FormatException();

		/// <summary></summary>
		/// <param name="s"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public static bool TryParse(string s, out FileSize result)
			=> TryParse(s, NumberStyles.Number, NumberFormatInfo.CurrentInfo, out result);

		/// <summary></summary>
		/// <param name="s"></param>
		/// <param name="formatProvider"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public static bool TryParse(string s, IFormatProvider formatProvider, out FileSize result)
			=> TryParse(s, NumberStyles.Number, formatProvider, out result);

		/// <summary></summary>
		/// <param name="s"></param>
		/// <param name="numberStyles"></param>
		/// <param name="formatProvider"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public static bool TryParse(string s, NumberStyles numberStyles, IFormatProvider formatProvider, out FileSize result)
		{
			if (String.IsNullOrEmpty(s))
			{
				result = Empty;
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

			if (!Decimal.TryParse(s, numberStyles, formatProvider, out var value))
			{
				result = Empty;
				return false;
			}

			result = new FileSize(Convert.ToInt64(value * (1L << byteBase * 10)));
			return true;
		} // func TryParse

		/// <summary></summary>
		/// <param name="value"></param>
		public static implicit operator FileSize(int value)
			=> new FileSize(value);

		/// <summary></summary>
		/// <param name="value"></param>
		public static implicit operator FileSize(long value)
			=> new FileSize(value);

		/// <summary></summary>
		/// <param name="value"></param>
		public static implicit operator long(FileSize value)
			=> value.fileSize;

		/// <summary></summary>
		public static FileSize Empty { get; } = new FileSize();
	} // struct FileSize

	#endregion

	#region -- class FormatFileSizeConverter ------------------------------------------

	/// <summary></summary>
	public class FormatFileSizeConverter : IFormatProvider, ICustomFormatter
	{
		private readonly IFormatProvider formatProvider = null;

		private FormatFileSizeConverter()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="formatProvider"></param>
		public FormatFileSizeConverter(IFormatProvider formatProvider)
		{
			this.formatProvider = formatProvider;
		} // ctor

		object IFormatProvider.GetFormat(Type formatType)
			=> formatType == typeof(ICustomFormatter)
				? this
				: FormatProvider.GetFormat(formatType);

		string ICustomFormatter.Format(string format, object arg, IFormatProvider formatProvider)
		{
			// XiB, KiB, MiB, ...
			if (format != null && arg != null)
				return FileSize.Format(Convert.ToInt64(arg, formatProvider), format, formatProvider);

			return null;
		} // func ICustomFormatter.Format

		/// <summary></summary>
		public IFormatProvider FormatProvider => formatProvider ?? CultureInfo.CurrentUICulture;

		/// <summary>Default provider.</summary>
		public IFormatProvider Default { get; } = new FormatFileSizeConverter();
	} // class FormatFileSizeConverter

	#endregion
}
