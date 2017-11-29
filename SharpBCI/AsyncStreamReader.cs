using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

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
            reader.Read();
            reader.Close();
        }

        public void Close() {
            readQueue.CompleteAdding();
            readingThread.Join();
        }

        public string ReadLine() {
            return readQueue.Take();
        }
    }

}