using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharpBCI
{

    /*
     * Used internall to asynchronously read csv data from a file to reemit/replay the data
     */

    public class AsyncStreamReader
    {

        StreamReader reader;
        /**
         * New thread is created to read each line of the CSV file because frequency of I/O operations is too high for original thread to handle
         */
        Thread readingThread;
        BlockingCollection<string> readQueue = new BlockingCollection<string>();

        public AsyncStreamReader(string fileName)
        {
            reader = new StreamReader(fileName);
            readingThread = new Thread(new ThreadStart(DoRead));
            readingThread.Start();
        }

        /*
         * Actual function used to perform reading from CSV in a loop.
         * Function terminates when all lines have been read and shuts down its thread.
         */ 
        protected void DoRead() {
            string line;
            while ((line = reader.ReadLine()) != null) {
                readQueue.Add(line);
            }
            readQueue.CompleteAdding();
            reader.Read();
            reader.Close();
        }

        /*
         * Helper function that retrieves an IEnumerable that allows iteration over the readQueue blocking collection
         */
        public IEnumerable<String> GetEnumerable() {
            return readQueue.GetConsumingEnumerable();
        }

        public void Close() {
            readingThread.Join();
        }

        /*
         * Manually retrieve one string from the readQueue blocking collection
         */
        public string ReadLine() {
            return readQueue.Take();
        }
    }

}