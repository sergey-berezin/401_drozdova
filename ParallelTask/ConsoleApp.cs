using System;
using ParallelTask.RecognitionLibrary;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace ParallelTask
{
    class ConsoleApp
    {
        static void Main(string[] args)
        {
            string directory;
            if (args.Length >= 1)
            {
                directory = args[0];
            }
            else
            {
                Console.WriteLine("Please type path again");
                directory = Console.ReadLine();
            }

            var ResultsQueue = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            var source = new CancellationTokenSource();
            var token = source.Token;

            Console.WriteLine("To stop execution type any symbol");
            var StopTask = Task.Factory.StartNew(() =>
            {
                var StopSymb = Console.ReadLine();
                if (StopSymb.Length > 0)
                {
                    source.Cancel();
                }
                Console.WriteLine("Execution is stopped...");
            }, TaskCreationOptions.LongRunning);

            var detection_task = Task.Factory.StartNew(() => GetPredictions.Detections(directory, token, ResultsQueue), TaskCreationOptions.LongRunning);
            var write_task = Task.Factory.StartNew(() =>
            {
                while (detection_task.Status == TaskStatus.Running)
                {
                    while (ResultsQueue.TryDequeue(out Tuple<string, YoloV4Result> result))
                    {
                        string name = result.Item1;
                        var detected_object = result.Item2;
                        var x1 = detected_object.BBox[0];
                        var y1 = detected_object.BBox[1];
                        var x2 = detected_object.BBox[2];
                        var y2 = detected_object.BBox[3];
                        Console.WriteLine($"Image: {name}" + $" object: {detected_object.Label}" +
                            $" rectangle is placed between ({x1:0.0}, {y1:0.0}) and ({x2:0.0}, {y2:0.0})" +
                            $" probability: {detected_object.Confidence.ToString("0.00")}");
                    }
                }
            });
            Task.WaitAll(detection_task, write_task);

        }
    }
}
