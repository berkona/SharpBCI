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

        /**
         * Main function that writes lines queued in the writeQueue blcoking collection to the file indicated
         */
		protected void DoWrite() {
			foreach (string line in writeQueue.GetConsumingEnumerable()) {
				writer.WriteLine(line);	
			}
            writer.Flush();
            writer.Close();
		}

        /**
         * Finish writing items in queue and close out the thread
         */ 
		public void Close() {
			writeQueue.CompleteAdding();
            writingThread.Join();
		}

        /**
         * Queue a line to be written
         */
		public void WriteLine(string line) {
			writeQueue.Add(line);	
		}
	}

}