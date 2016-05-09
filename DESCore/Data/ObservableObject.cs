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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class ObservableObject : INotifyPropertyChanged
	{
		/// <summary>Informiert über geänderte Objekte</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string sPropertyName)
		{
			if (String.IsNullOrEmpty(sPropertyName))
				throw new ArgumentNullException();

			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
		} // proc OnPropertyChanged

		protected bool Set<T>(ref T value, T newValue, Func<string> getPropertyName)
		{
			if (EqualityComparer<T>.Default.Equals(value, newValue))
				return false; // Keine Änderung

			value = newValue;
			OnPropertyChanged(getPropertyName());
			return true;
		} // func Set

		protected bool Set<T>(ref T value, T newValue, [CallerMemberName] string sPropertyName = null)
		{
			return Set(ref value, newValue, () => sPropertyName);
		} // func Set

		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T value, T newValue)
		{
			if (propertyExpression == null)
				throw new ArgumentNullException("propertyExpression");

			return Set(ref value, newValue, () => ((PropertyInfo)((MemberExpression)(propertyExpression.Body)).Member).Name);
		} // func Set
	} // class ObservableObject
}
