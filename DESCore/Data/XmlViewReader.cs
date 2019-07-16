using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- class XmlViewDataReader ------------------------------------------------

	/// <summary></summary>
	public abstract class XmlViewDataReader : IEnumerable<IDataRow>
	{
		#region -- class XmlViewDataColumn --------------------------------------------

		/// <summary></summary>
		private sealed class XmlViewDataColumn : IDataColumn
		{
			private readonly string name;
			private readonly Type dataType;
			private readonly PropertyDictionary attributes;

			#region -- Ctor/Dtor ------------------------------------------------------

			public XmlViewDataColumn(string name, Type dataType, PropertyDictionary attributes)
			{
				this.name = name;
				this.dataType = dataType;
				this.attributes = attributes;
			} // ctor

			#endregion

			#region -- IDataColumn ----------------------------------------------------

			public string Name => name;
			public Type DataType => dataType;
			public IPropertyEnumerableDictionary Attributes => attributes;

			#endregion
		} // class XmlViewDataColumn

		#endregion

		#region -- class XmlViewDataRow -----------------------------------------------

		private sealed class XmlViewDataRow : DynamicDataRow
		{
			private readonly XmlViewDataEnumerator enumerator;
			private readonly object[] columnValues;

			#region -- Ctor/Dtor ------------------------------------------------------

			public XmlViewDataRow(XmlViewDataEnumerator enumerator, object[] columnValues)
			{
				this.enumerator = enumerator;
				this.columnValues = columnValues;
			} // ctor

			#endregion

			#region -- IDataRow -------------------------------------------------------

			public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
			public override object this[int index] => columnValues[index];

			public override bool IsDataOwner => true;

			#endregion
		} // class XmlViewDataRow

		#endregion

		#region -- class XmlViewDataEnumerator ----------------------------------------

		/// <summary></summary>
		private sealed class XmlViewDataEnumerator : IEnumerator<IDataRow>, IDataColumns
		{
			#region -- enum ReadingState ----------------------------------------------

			/// <summary></summary>
			private enum ReadingState
			{
				/// <summary>Nothing read until now</summary>
				Unread,
				/// <summary>Read first row</summary>
				FetchFirstRow,
				/// <summary>Fetch more rows</summary>
				FetchRows,
				/// <summary>Done</summary>
				Complete,
			} // enum ReadingState

			#endregion

			private readonly XName xnView = "view";
			private readonly XName xnFields = "fields";
			private readonly XName xnRows = "rows";
			private readonly XName xnRow = "r";

			private readonly XmlViewDataReader owner;
			private bool disposeXml = true;
			private XmlReader xml;
			private XmlViewDataColumn[] columns;
			private ReadingState state;
			private XmlViewDataRow currentRow;

			#region -- Ctor/Dtor ------------------------------------------------------

			public XmlViewDataEnumerator(XmlViewDataReader owner)
			{
				this.owner = owner;
			} // ctor

			public void Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
			{
				if (disposing)
					xml?.Dispose();
			} // proc Dispose

			#endregion

			#region -- IEnumerator ----------------------------------------------------

			private bool MoveNext(bool headerOnly)
			{
				switch (state)
				{
					#region -- ReadingState.Unread --
					case ReadingState.Unread:
						// open the xml stream
						xml = owner.CreateXmlReader(out disposeXml);

						xml.Read();
						if (xml.NodeType == XmlNodeType.XmlDeclaration)
							xml.Read();

						if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnView.LocalName)
							throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

						xml.Read();
						if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnFields.LocalName)
							throw new InvalidDataException($"Expected \"{xnFields}\", read \"{xml.LocalName}\".");

						var viewColumns = new List<XmlViewDataColumn>();
						var fields = (XElement)XNode.ReadFrom(xml);
						foreach (var field in fields.Elements())
						{
							var columnName = field.Name.LocalName;
							var columnDataType = LuaType.GetType(field.GetAttribute("type", "string"), lateAllowed: false).Type;
							var columnId = field.GetAttribute("field", String.Empty);

							var attributes = new PropertyDictionary();

							// add colum id
							if (!String.IsNullOrEmpty(columnId))
								attributes.SetProperty("field", typeof(string), columnId);

							foreach (var c in field.Elements("attribute"))
							{
								if (c.IsEmpty)
									continue;

								var attributeName = c.GetAttribute("name", String.Empty);
								if (String.IsNullOrEmpty(attributeName))
									continue;

								attributes.SetProperty(attributeName, LuaType.GetType(c.GetAttribute("type", "string"), lateAllowed: false).Type, c.Value);
							} // foreach c

							viewColumns.Add(new XmlViewDataColumn(columnName, columnDataType, attributes));
						} // foreach field

						if (viewColumns.Count < 1)
							throw new InvalidDataException("No header found.");
						columns = viewColumns.ToArray();

						state = ReadingState.FetchFirstRow;
						if (headerOnly)
							return true;
						else
							goto case ReadingState.FetchFirstRow;
					#endregion
					#region -- ReadingState.FetchFirstRow --
					case ReadingState.FetchFirstRow:
						if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnRows.LocalName)
							throw new InvalidDataException($"Expected \"{xnRows}\", read \"{xml.LocalName}\".");

						if (xml.IsEmptyElement)
						{
							xml.Read();
							if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnView.LocalName)
								throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

							xml.Read();
							if (!xml.EOF)
								throw new InvalidDataException("Unexpected eof.");

							state = ReadingState.Complete;
							goto case ReadingState.Complete;
						} // if xml.IsEmptyElement
						else
						{
							xml.Read();
							state = ReadingState.FetchRows;
							goto case ReadingState.FetchRows;
						}
					#endregion
					#region -- ReadingState.FetchRows --
					case ReadingState.FetchRows:
						if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnRow.LocalName)
							throw new InvalidDataException($"Expected \"r\", read \"{xml.LocalName}\".");

						var values = new object[columns.Length];

						if (!xml.IsEmptyElement)
						{
							var rowData = (XElement)XNode.ReadFrom(xml);
							foreach (var column in rowData.Elements())
							{
								var columnIndex = Array.FindIndex(columns, c => String.Compare(c.Name, column.Name.LocalName, StringComparison.OrdinalIgnoreCase) == 0);
								if (columnIndex != -1)
									values[columnIndex] = Procs.ChangeType(column.Value, columns[columnIndex].DataType);
							}
						} // if xml.IsEmptyElement
						else
							// Without a call to XNode.ReadFrom() it's necessary to read to the next node.
							xml.Read();

						currentRow = new XmlViewDataRow(this, values);

						if (xml.NodeType == XmlNodeType.Element && xml.LocalName == xnRow.LocalName)
							return true;

						if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnRows.LocalName)
							throw new InvalidDataException($"Expected \"{xnRows}\", read \"{xml.LocalName}\".");

						xml.Read();
						if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnView.LocalName)
							throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

						xml.Read();
						if (!xml.EOF)
							throw new InvalidDataException("Unexpected eof.");

						state = ReadingState.Complete;
						return true;
					#endregion
					case ReadingState.Complete:
						return false;
					default:
						throw new InvalidOperationException("The state of the object is invalid.");
				} // switch state
			} // func MoveNext 

			public bool MoveNext()
				=> MoveNext(false);

			void IEnumerator.Reset()
			{
				if (disposeXml)
					xml?.Dispose();
				xml = null;
				columns = null;
				currentRow = null;
				state = ReadingState.Unread;
			} // proc Reset

			public IDataRow Current => currentRow;
			object IEnumerator.Current => Current;

			#endregion

			#region -- IDataColumns ---------------------------------------------------

			public IReadOnlyList<IDataColumn> Columns
			{
				get
				{
					if (state == ReadingState.Unread)
						MoveNext(true);
					return columns;
				}
			} // prop Columns

			#endregion
		} // class ViewDataEnumerator

		#endregion

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public XmlViewDataReader()
		{
		} // ctor

		/// <summary>Create reader source</summary>
		/// <param name="doDispose"></param>
		/// <returns></returns>
		protected abstract XmlReader CreateXmlReader(out bool doDispose);

		#endregion

		#region -- IEnumerable --------------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<IDataRow> GetEnumerator()
			=> new XmlViewDataEnumerator(this);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		#region -- Create -------------------------------------------------------------

		#region -- class XmlViewFileReader --------------------------------------------

		private sealed class XmlViewFileReader : XmlViewDataReader
		{
			private readonly string fileName;

			public XmlViewFileReader(string fileName)
			{
				this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
			} // ctor

			protected override XmlReader CreateXmlReader(out bool disposeXml)
			{
				disposeXml = true;
				return XmlReader.Create(fileName, Procs.XmlReaderSettings);
			} // func CreateXmlReader
		} // class XmlViewFileReader

		#endregion

		#region -- class XmlViewTextReader --------------------------------------------

		private sealed class XmlViewTextReader : XmlViewDataReader
		{
			private readonly TextReader tr;
			private readonly XmlReaderSettings settings;

			public XmlViewTextReader(TextReader tr, bool disposeTextReader)
			{
				this.tr = tr ?? throw new ArgumentNullException(nameof(tr));
				settings = Procs.XmlReaderSettings.Clone();
				settings.CloseInput = disposeTextReader;
			} // ctor

			protected override XmlReader CreateXmlReader(out bool disposeXml)
			{
				disposeXml = true;
				return XmlReader.Create(tr, settings);
			} // func CreateXmlReader
		} // class XmlViewTextReader

		#endregion

		#region -- class XmlViewStreamReader ------------------------------------------

		private sealed class XmlViewStreamReader : XmlViewDataReader
		{
			private readonly Stream stream;
			private readonly XmlReaderSettings settings;

			public XmlViewStreamReader(Stream stream, bool disposeStream)
			{
				this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
				settings = Procs.XmlReaderSettings.Clone();
				settings.CloseInput = disposeStream;
			} // ctor

			protected override XmlReader CreateXmlReader(out bool disposeXml)
			{
				disposeXml = true;
				return XmlReader.Create(stream, settings);
			} // func CreateXmlReader
		} // class XmlViewStreamReader

		#endregion

		#region -- class XmlViewXmlReader ---------------------------------------------

		private sealed class XmlViewXmlReader : XmlViewDataReader
		{
			private readonly bool disposeXml;
			private readonly XmlReader xml;

			public XmlViewXmlReader(XmlReader xml, bool disposeXml)
			{
				this.xml = xml ?? throw new ArgumentNullException(nameof(xml));
				this.disposeXml = disposeXml;
			} // ctor

			protected override XmlReader CreateXmlReader(out bool doDispose)
			{
				doDispose = disposeXml;
				return xml;
			} // func CreateXmlReader
		} // class XmlViewXmlReader

		#endregion

		/// <summary></summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static XmlViewDataReader Create(string fileName)
			=> new XmlViewFileReader(fileName);

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="disposeTextReader"></param>
		/// <returns></returns>
		public static XmlViewDataReader Create(TextReader tr, bool disposeTextReader = true)
			=> new XmlViewTextReader(tr, disposeTextReader);

		/// <summary></summary>
		/// <param name="stream"></param>
		/// <param name="disposeStream"></param>
		/// <returns></returns>
		public static XmlViewDataReader Create(Stream stream, bool disposeStream = true)
			=> new XmlViewStreamReader(stream, disposeStream);

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="disposeXml"></param>
		/// <returns></returns>
		public static XmlViewDataReader Create(XmlReader xml, bool disposeXml = true)
			=> new XmlViewXmlReader(xml, disposeXml);

		#endregion
	} // class ViewDataReader

	#endregion
}
