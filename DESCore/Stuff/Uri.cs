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
		public static IEnumerable<string> ParseMultiValueHeader(string value)
		{
			return from c in value.Split(',')
						 let t = c.Trim()
						 where t.Length > 0
						 select t;
		} // func ParseMultiValueHeader

		public static string GetFileName(this Uri uri)
		{
			return Path.GetFileName(uri.AbsolutePath);
		} // func GetFileName
	} // class Procs
}
