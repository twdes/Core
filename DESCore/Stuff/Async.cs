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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class Procs ------------------------------------------------------------

	public static partial class Procs
	{
		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="printException"></param>
		public static void Silent(this Task task, Action<Exception> printException = null)
		{
			task.ContinueWith(
				t => 
				{
					if (printException != null)
						printException.Invoke(t.Exception);
					else
						Debug.Print(t.Exception.GetInnerException().ToString());
				},
				TaskContinuationOptions.OnlyOnFaulted
			);
		} // proc Silent
	} // class Procs

	#endregion
}
