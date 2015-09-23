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
