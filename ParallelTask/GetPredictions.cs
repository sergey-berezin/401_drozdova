using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq; //для получения файлов из выбранной папки


namespace ParallelTask
{
    public class GetPredictions
    {
        const string modelPath = @"E:\Programs\yolov4.onnx";


        static readonly string[] classesNames = { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        public static void Detections(string directory, CancellationToken token, ConcurrentQueue<Tuple<string, YoloV4Result>> ResultsQueue)
        {
            MLContext mlContext = new MLContext();

            var images = Directory.GetFiles(directory).Select(path => Path.GetFullPath(path)).ToArray();

            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));

            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));

            //создаём predictionEngine для каждого файла в директории
            var predictionEngines = ImmutableList.Create<PredictionEngine<YoloV4BitmapData, YoloV4Prediction>>();
            for (int i = 0; i < images.Length; ++i)
            {
                predictionEngines = predictionEngines.Add(mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model));
            }

            var sw = new Stopwatch();
            sw.Start();

            var tasks = new Task[images.Length];
            for (int i = 0; i < images.Length; ++i)
            {
                tasks[i] = Task.Factory.StartNew(pi =>
                {
                    int num = (int)pi;
                    var path = images[num];
                    var bitmap = new Bitmap(Image.FromFile(path));
                    var predict = predictionEngines[num].Predict(new YoloV4BitmapData() { Image = bitmap });
                    var results = predict.GetResults(classesNames, 0.3f, 0.7f);

                    foreach(var detection in results)
                    {
                        var TupleWithResult = Tuple.Create(images[num], detection);
                        ResultsQueue.Enqueue(TupleWithResult);
                    }
                }, i, token);
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (Exception)
            {

            }
            finally
            {
                sw.Stop();
            }
        }
    }
}