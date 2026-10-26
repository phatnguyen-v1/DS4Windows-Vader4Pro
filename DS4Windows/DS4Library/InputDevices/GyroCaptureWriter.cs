using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace DS4Windows.InputDevices
{
    internal interface IGyroCaptureSample
    {
        // CSV fields after the sequence number
        string ToCsv();
    }

    // Writes motion samples to CSV for measuring gyro quantization and update
    // rate. The input thread only enqueues; a background thread formats and writes.
    internal sealed class GyroCaptureWriter<TSample> : IDisposable
        where TSample : struct, IGyroCaptureSample
    {
        private readonly ConcurrentQueue<TSample> queue = new ConcurrentQueue<TSample>();
        private readonly StreamWriter writer;
        private readonly Thread writerThread;
        private readonly string displayName;
        private volatile bool stopping;
        private volatile bool failed;
        private ulong sequence;

        public string FilePath { get; }

        // header includes the leading "seq" column
        public GyroCaptureWriter(string directory, string filePrefix, string deviceId,
            string header, string displayName)
        {
            this.displayName = displayName;
            Directory.CreateDirectory(directory);
            string safeId = string.Concat((deviceId ?? "device").Split(Path.GetInvalidFileNameChars()));
            FilePath = Path.Combine(directory, $"{filePrefix}-{safeId}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false), 1 << 16);
            writer.WriteLine(header);
            writerThread = new Thread(WriteLoop)
            {
                Name = displayName + " gyro capture",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,
            };
            writerThread.Start();
        }

        public void Record(in TSample sample)
        {
            if (!failed)
                queue.Enqueue(sample);
        }

        private void WriteLoop()
        {
            try
            {
                while (true)
                {
                    bool finalPass = stopping;
                    while (queue.TryDequeue(out TSample sample))
                    {
                        writer.Write(sequence++);
                        writer.Write(',');
                        writer.WriteLine(sample.ToCsv());
                    }
                    if (finalPass)
                        break;
                    writer.Flush();
                    Thread.Sleep(50);
                }
            }
            catch (IOException e)
            {
                failed = true;
                queue.Clear();
                AppLogger.LogToGui($"{displayName} gyro capture stopped: {e.Message}", true);
            }
            finally
            {
                try { writer.Dispose(); }
                catch (IOException) { }
            }
        }

        public void Dispose()
        {
            stopping = true;
            writerThread.Join();
        }
    }
}
