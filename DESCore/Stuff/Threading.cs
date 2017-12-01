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
using System.Threading;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class ManualResetEventAsync --------------------------------------------

	/// <summary>Async ManualResetEvent implementation</summary>
	public sealed class ManualResetEventAsync
	{
		private volatile TaskCompletionSource<bool> flag = new TaskCompletionSource<bool>();
		private volatile bool isDisposed = false;

		public ManualResetEventAsync(bool isSet = true)
		{
			if (!isSet)
				flag.TrySetResult(true);
		} // ctor

		~ManualResetEventAsync()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;

				// cancel all pending tasks
				while (true)
				{
					var tmp = flag;
					if (!tmp.Task.IsCompleted)
						tmp.TrySetCanceled();
					else
						break;
				}
			}
		} // proc Dispose

		private void ThrowIfDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(ManualResetEventAsync));
		} // proc ThrowIfDisposed

		public Task WaitAsync()
			=> flag.Task;

		public async Task<bool> WaitAsync(int millisecondsDelay)
		{
			if (millisecondsDelay < 0)
			{
				await WaitAsync();
				return true;
			}
			else
			{
				using (var cts = new CancellationTokenSource(millisecondsDelay))
					return await WaitAsync(cts.Token);
			}
		} // func WaitAsync

		public async Task<bool> WaitAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken == CancellationToken.None)
			{
				await WaitAsync();
				return true;
			}
			else
			{
				ThrowIfDisposed();

				var isWaitActive = true;
				cancellationToken.Register(() =>
				{
					isWaitActive = false;
					Pulse();
				});

				while (isWaitActive)
				{
					if (await flag.Task)
						break;
				}

				return isWaitActive;
			}
		} // func WaitAsync

		private void Pulse()
		{
			ThrowIfDisposed();

			var tmp = flag;
			if (!tmp.Task.IsCompleted)
			{
				// pulse old tasks
				Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(false), tmp, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
				tmp.Task.Wait();

				// create new task, if not done
				Interlocked.CompareExchange(ref flag, new TaskCompletionSource<bool>(), tmp);
			}
		} // proc Set

		public void Set()
		{
			var tcs = flag;
			Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
			tcs.Task.Wait();
		} // proc Set

		public void Reset()
		{
			ThrowIfDisposed();
			while (true)
			{
				var tmp = flag;
				if (!tmp.Task.IsCompleted || // if not set any result to task, create a new one
					Interlocked.CompareExchange(ref flag, new TaskCompletionSource<bool>(), tmp) == tmp)
					return;
			}
		} // proc Reset
	} // class ManualResetEventAsync

	#endregion
}
