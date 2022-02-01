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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TecWare.DE.Data
{
	#region -- class ObservableObject -------------------------------------------------

	/// <summary></summary>
	public class ObservableObject : INotifyPropertyChanged
	{
		/// <summary>Informiert über geänderte Objekte</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary></summary>
		/// <param name="propertyName"></param>
		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (String.IsNullOrEmpty(propertyName))
				throw new ArgumentNullException();

			OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		} // proc OnPropertyChanged

		/// <summary></summary>
		/// <param name="_"></param>
		/// <param name="args"></param>
		protected void OnPropertyChanged(object _, PropertyChangedEventArgs args)
			=> PropertyChanged?.Invoke(this, args);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		/// <param name="getPropertyName"></param>
		/// <returns></returns>
		protected bool Set<T>(ref T value, T newValue, Func<string> getPropertyName)
		{
			if (EqualityComparer<T>.Default.Equals(value, newValue))
				return false; // Keine Änderung

			value = newValue;
			OnPropertyChanged(getPropertyName());
			return true;
		} // func Set

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		protected bool Set<T>(ref T value, T newValue, [CallerMemberName] string propertyName = null)
		{
			return Set(ref value, newValue, () => propertyName);
		} // func Set

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="propertyExpression"></param>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		/// <returns></returns>
		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T value, T newValue)
		{
			if (propertyExpression == null)
				throw new ArgumentNullException("propertyExpression");

			return Set(ref value, newValue, () => ((PropertyInfo)((MemberExpression)(propertyExpression.Body)).Member).Name);
		} // func Set
	} // class ObservableObject

	#endregion
}
