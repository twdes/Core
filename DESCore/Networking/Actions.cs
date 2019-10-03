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
using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DE.Networking
{
	#region -- class DEActionParam ----------------------------------------------------

	/// <summary>Parameter for the action</summary>
	public sealed class DEActionParam
	{
		private readonly string parameterName;
		private readonly Type type;

		/// <summary>Parameter for the action</summary>
		/// <param name="parameterName">Name of the parameter</param>
		/// <param name="type">Target datatype</param>
		public DEActionParam(string parameterName, Type type)
		{
			this.parameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
			this.type = type ?? throw new ArgumentNullException(nameof(type));
		} // ctor

		/// <summary>Name of the parameter</summary>
		public string Name => parameterName;
		/// <summary>Target datatype</summary>
		public Type Type => type;
	} // class DEActionParam

	#endregion

	#region -- class DEAction ---------------------------------------------------------

	/// <summary>Action definiton for the client implementation.</summary>
	public sealed class DEAction
	{
		private readonly string path;
		private readonly string actionName;
		private readonly DEActionParam[] parameters;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private DEAction(string actionName, string path, params DEActionParam[] parameters)
		{
			this.actionName = actionName ?? throw new ArgumentNullException(nameof(actionName));
			this.path = path;
			this.parameters = parameters ?? Array.Empty<DEActionParam>();
		} // ctor

		#endregion

		#region -- ToQuery, Execute ---------------------------------------------------

		private StringBuilder GetRequest(StringBuilder sb)
		{
			if (!String.IsNullOrEmpty(path))
			{
				sb.Append(path);
				if (sb[sb.Length - 1] != '/')
					sb.Append('/');
			}
			sb.Append("?action=")
				.Append(actionName);

			return sb;
		} // func GetRequest

		private IEnumerable<PropertyValue> GetPropertyValuesByName(IPropertyReadOnlyDictionary arguments)
		{
			foreach (var param in parameters)
			{
				if (arguments.TryGetProperty(param.Name, out var value))
					yield return new PropertyValue(param.Name, Procs.ChangeType(value, param.Type));
			}
		} // func GetPropertyValuesByName

		private StringBuilder ToQueryByName(StringBuilder sb, IPropertyReadOnlyDictionary arguments)
		{
			HttpStuff.MakeUriArguments(sb, true, GetPropertyValuesByName(arguments));
			return sb;
		} // proc ToQuery

		private IEnumerable<PropertyValue> GetPropertyValuesByPosition(object[] arguments)
		{
			for (var i = 0; i < parameters.Length; i++)
			{
				var value = arguments != null && i < arguments.Length ? arguments[i] : null;
				if (value != null)
					yield return new PropertyValue(parameters[i].Name, Procs.ChangeType(value, parameters[i].Type));
			}
		} // func GetPropertyValuesByPosition

		private StringBuilder ToQueryByPosition(StringBuilder sb, object[] arguments)
		{
			HttpStuff.MakeUriArguments(sb, true, GetPropertyValuesByPosition(arguments));
			return sb;
		} // func ToQueryByPosition

		/// <summary>Create a query uri.</summary>
		/// <param name="arguments">Arguments to fill in.</param>
		/// <returns></returns>
		public string ToQuery(IPropertyReadOnlyDictionary arguments)
			=> ToQueryByName(GetRequest(new StringBuilder()), arguments).ToString();

		/// <summary>Create a query uri.</summary>
		/// <param name="arguments">Arguments to fill in.</param>
		/// <returns></returns>
		public string ToQuery(params object[] arguments)
			=> ToQueryByPosition(GetRequest(new StringBuilder()), arguments).ToString();

		/// <summary>Execute a action on http client.</summary>
		/// <param name="http"></param>
		/// <param name="arguments">Arguments to fill in.</param>
		/// <returns></returns>
		public Task<LuaTable> ExecuteAsync(DEHttpClient http, IPropertyReadOnlyDictionary arguments)
			=> http.GetTableAsync(ToQuery(arguments));

		/// <summary>Execute a action on http client.</summary>
		/// <param name="http">Http client to use.</param>
		/// <param name="arguments">Arguments to fill in.</param>
		/// <returns></returns>
		public Task<LuaTable> ExecuteAsync(DEHttpClient http, params object[] arguments)
			=> http.GetTableAsync(ToQuery(arguments));

		#endregion

		/// <summary>Name of the action.</summary>
		public string Name => actionName;

		#region -- Create -------------------------------------------------------------

		/// <summary>Create action definition.</summary>
		/// <param name="actionName"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static DEAction Create(string actionName, params DEActionParam[] parameters)
			=> new DEAction(actionName, null, parameters);

		/// <summary>Create action definition.</summary>
		/// <param name="actionName"></param>
		/// <param name="path"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static DEAction Create(string actionName, string path, params DEActionParam[] parameters)
			=> new DEAction(actionName, path, parameters);

		#endregion
	} // class DEAction

	#endregion

}
