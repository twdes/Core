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
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.DE.Networking
{
	#region -- class DEHttpSocketEventArgs --------------------------------------------

	/// <summary></summary>
	public class DEHttpSocketEventArgs : EventArgs
	{
		internal DEHttpSocketEventArgs(string id, XElement xEvent)
		{
			Id = id ?? throw new ArgumentNullException(nameof(id));

			Path = xEvent.GetAttribute("path", "/");
			Index = xEvent.Attribute("index")?.Value;
			Values = xEvent.Elements().FirstOrDefault();
		} // ctor

		/// <summary>Node path</summary>
		public string Path { get; }
		/// <summary>Event id</summary>
		public string Id { get; }
		/// <summary>Event index</summary>
		public string Index { get; }

		/// <summary>Extended values</summary>
		public XElement Values { get; }
	} // class DEHttpSocketEventArgs

	#endregion

	#region -- class DEHttpEventSocket ------------------------------------------------

	/// <summary>Log info connection to receive events and state of the server</summary>
	public abstract class DEHttpEventSocket : DEHttpSocketBase
	{
		/// <summary>Event notification.</summary>
		public event EventHandler<DEHttpSocketEventArgs> Notify;

		private readonly SynchronizationContext synchronizationContext;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="http"></param>
		protected DEHttpEventSocket(DEHttpClient http)
			: base(http.BaseAddress, http.Credentials)
		{
			synchronizationContext = SynchronizationContext.Current;
		} // ctor

		/// <summary></summary>
		/// <param name="serverUri"></param>
		/// <param name="credentials"></param>
		protected DEHttpEventSocket(Uri serverUri, ICredentials credentials)
			: base(serverUri, credentials)
		{
			synchronizationContext = SynchronizationContext.Current;
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			DisposePingServer();
			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		#region -- Protocol -----------------------------------------------------------

		private CancellationTokenSource pingTokenReset = null;
		
		private void ResetPingServer()
		{
			DisposePingServer();

			pingTokenReset = new CancellationTokenSource();
			SendPingAsync(pingTokenReset.Token);
		} // proc ResetPingServer

		private void DisposePingServer()
		{
			try
			{
				pingTokenReset?.Cancel();
				pingTokenReset?.Dispose();
				pingTokenReset = null;
			}
			catch
			{
			}
		} // proc DisposePingServer

		private async void SendPingAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					await Task.Delay(1000, cancellationToken);
					await SendAsync("/ping", cancellationToken);
				}
			}
			catch (TaskCanceledException)
			{
			}
		} // proc SendPingAsync

		/// <summary></summary>
		/// <param name="socket"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected sealed override Task OnConnectedAsync(ClientWebSocket socket, CancellationToken cancellationToken)
		{
			ResetPingServer();
			return base.OnConnectedAsync(socket, cancellationToken);
		} // proc OnConnectedAsync 

		/// <summary></summary>
		/// <returns></returns>
		protected override Task OnConnectionLostAsync()
		{
			DisposePingServer();
			return base.OnConnectionLostAsync();
		} // proc OnConnectionLostAsync

		/// <summary></summary>
		/// <param name="recvBuffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected sealed override Task OnProcessMessageAsync(byte[] recvBuffer, int offset, int count)
			=> OnNotifyAsnyc(XElement.Parse(Encoding.UTF8.GetString(recvBuffer, offset, count)));

		private Task OnNotifyAsnyc(XElement x)
		{
			ResetPingServer();

			if (x.Name.LocalName != "event")
				return Task.CompletedTask;

			var eventId = x.Attribute("event")?.Value;
			if (String.IsNullOrEmpty(eventId))
				return Task.CompletedTask;

			if (eventId == "pong")
				return Task.CompletedTask; // answer for ping

			if (eventId == "eventFilter") // new filter
				ReceiveFilter(x.Element("f")?.Value?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
			else
				PostEvent(new DEHttpSocketEventArgs(eventId, x));

			return Task.CompletedTask;
		} // func OnNotifyAsync

		private void PostEvent(DEHttpSocketEventArgs ev)
		{
			if (synchronizationContext != null)
				synchronizationContext.Post(OnNotifyIntern, ev);
			else
				Task.Run(() => OnNotify(ev)).ContinueWith(t => OnCommunicationExceptionAsync(t.Exception).Wait(), TaskContinuationOptions.OnlyOnFaulted);
		} // func PostEvent

		private void OnNotifyIntern(object state)
			=> OnNotify((DEHttpSocketEventArgs)state);

		/// <summary></summary>
		/// <param name="e"></param>
		protected virtual void OnNotify(DEHttpSocketEventArgs e)
			=> Notify?.Invoke(this, e);

		#endregion

		#region -- SendAsync ----------------------------------------------------------

		private async Task SendAsync(string message, CancellationToken cancellationToken)
		{
			var buf = Encoding.UTF8.GetBytes(message);
			if (buf.Length > 16 << 10)
				throw new ArgumentOutOfRangeException(nameof(message), message.Length, "Message to big.");

			using (var source = GetSendCancellationTokenSource(0, cancellationToken))
				await GetSocket().SendAsync(new ArraySegment<byte>(buf, 0, buf.Length), WebSocketMessageType.Text, true, source.Token);
		} // func SendAsync

		#endregion

		#region -- Event Filter -------------------------------------------------------

		private string[] currentFilter = Array.Empty<string>();

		private readonly object lockCurrentNotifyGetFilter = new object();
		private TaskCompletionSource<string[]> currentNotifyGetFilter = null;

		/// <summary>Set a new filter</summary>
		/// <param name="filter"></param>
		/// <returns></returns>
		public Task SetFilterAsync(params string[] filter)
			=> SetFilterAsync(CancellationToken.None, filter);

		/// <summary>Set a new filter</summary>
		/// <param name="cancellationToken"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		public Task SetFilterAsync(CancellationToken cancellationToken, params string[] filter)
		{
			var sb = new StringBuilder("/setFilter ");
			var first = true;
			foreach (var f in filter)
			{
				if (first)
					first = false;
				else
					sb.Append(';');
				sb.Append(f);
			}

			return SendAsync(sb.ToString(), cancellationToken);
		} // proc SetFilterAsync

		/// <summary>Get the current filter</summary>
		/// <returns></returns>
		public Task<string[]> GetFilterAsync()
			=> GetFilterAsync(CancellationToken.None);

		/// <summary>Get the current filter</summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<string[]> GetFilterAsync(CancellationToken cancellationToken)
		{
			var taskCompletion = RegisterGetFilter(cancellationToken);
			await SendAsync("/getFilter", cancellationToken);
			return await taskCompletion.Task;
		} // func GetFilterAsync

		private TaskCompletionSource<string[]> RegisterGetFilter(CancellationToken cancellationToken)
		{
			lock (lockCurrentNotifyGetFilter)
			{
				if (currentNotifyGetFilter != null)
					throw new InvalidOperationException();

				currentNotifyGetFilter = new TaskCompletionSource<string[]>();
				cancellationToken.Register(() => currentNotifyGetFilter.TrySetCanceled());
				return currentNotifyGetFilter;
			}
		} // func RegisterGetFilter

		private void ReceiveFilter(string[] newCurrentFilter)
		{
			currentFilter = newCurrentFilter;

			lock (lockCurrentNotifyGetFilter)
			{
				currentNotifyGetFilter?.TrySetResult(currentFilter);
				currentNotifyGetFilter = null;
			}
		} // proc ReceiveFilter

		/// <summary></summary>
		public string[] CurrentFilter => currentFilter;

		#endregion

		/// <summary>des_event</summary>
		protected sealed override string SubProtocol => "des_event";
	} // class DEHttpEventSocket

	#endregion
}
