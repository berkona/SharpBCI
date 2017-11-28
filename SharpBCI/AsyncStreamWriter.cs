using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace SharpBCI {
	
	/**
	 * Used internally to asynchronously write a string to a file stream.
	 */
	public class AsyncStreamWriter {

		StreamWriter writer;

		Thread writingThread;
		BlockingCollection<string> writeQueue = new BlockingCollection<string>();

		public AsyncStreamWriter(string fileName, bool append) {
			writer = new StreamWriter(fileName, append);

			writingThread = new Thread(new ThreadStart(DoWrite));
			writingThread.Start();

		}

		protected void DoWrite() {
			foreach (string line in writeQueue.GetConsumingEnumerable()) {
				writer.WriteLine(line);	
			}
		}

		public void Close() {
			writeQueue.CompleteAdding();
			writingThread.Join();
		}

		public void WriteLine(string line) {
			writeQueue.Add(line);	
		}
	}

}