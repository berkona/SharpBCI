using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharpBCI
{

    public class AsyncStreamReader
    {

        StreamReader reader;

        Thread readingThread;
        BlockingCollection<string> readQueue = new BlockingCollection<string>();

        public AsyncStreamReader(string fileName)
        {
            reader = new StreamReader(fileName);
            readingThread = new Thread(new ThreadStart(DoRead));
            readingThread.Start();
        }

        protected void DoRead() {
            string line;
            while ((line = reader.ReadLine()) != null) {
                readQueue.Add(line);
            }
            readQueue.CompleteAdding();
            reader.Read();
            reader.Close();
        }

        public IEnumerable<String> GetEnumerable() {
            return readQueue.GetConsumingEnumerable();
        }

        public void Close() {
            readingThread.Join();
        }

        public string ReadLine() {
            return readQueue.Take();
        }
    }

}