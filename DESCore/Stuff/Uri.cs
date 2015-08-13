using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DES.Stuff
{
	public static partial class UriHelper
	{
		public static string GetFileName(this Uri uri)
		{
			return Path.GetFileName(uri.AbsolutePath);
		} // func GetFileName
	} // class Procs
}
