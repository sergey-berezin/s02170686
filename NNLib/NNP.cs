using System;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Concurrent;

//AutoResetEvent ???
namespace NNLib
{
    public delegate void ProcessResultDelegate(LabeledImage labeledImage);

    public class LabeledImage
    {
        public LabeledImage(string full_name, int label) 
        {
            FullName = full_name;
            Label = label;
        }

        //name including absolute path
        public string FullName { get; set; }
        public string Name 
        {
            get { return Path.GetFileName(FullName); } 
        }
        public int Label { get; set; }

        public override string ToString()
        {
            return Path.GetFileName(FullName) + " " + Label.ToString();
        }
    }

    //Neural Network Processing
    public class NNP : INotifyPropertyChanged
    {
        int logProcAmount;
        bool finishedProcessing, wasTerminated;
        Thread[] thread_arr;
        // Thread processing_thread;
        AutoResetEvent waiter;
        CancellationTokenSource cts;
        string model_name;
        ProcessResultDelegate processResult;

        public event PropertyChangedEventHandler PropertyChanged;
        public bool FinishedProcessing { get {return finishedProcessing; } }

        bool _isProcessing;
        public bool IsProcessing 
        { 
            get { return _isProcessing; } 
            set 
            {
                _isProcessing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsProcessing)));
            }
        }

        // public ConcurrentQueue<LabeledImage> QueueForLabeledImages;
        public NNP(string model_name, ProcessResultDelegate processResult) 
        {
            logProcAmount = Environment.ProcessorCount;
            thread_arr = new Thread[logProcAmount];
            waiter = new AutoResetEvent(true);
            cts = new CancellationTokenSource();
            this.processResult = processResult;
            finishedProcessing = false;
            IsProcessing = false;
            this.model_name = model_name;
            wasTerminated = false;
        }

        public int LoadAndPredict(string img_name)
        {
            using var image =  Image.Load<Rgb24>(img_name);

            const int TargetWidth = 28;
            const int TargetHeight = 28;

            image.Mutate(x =>
            {
                x.Resize(new ResizeOptions { Size = new Size(TargetWidth, TargetHeight),
                                             Mode = ResizeMode.Crop}).Grayscale();
            });

            var input = new DenseTensor<float>(new[] { 1, 1, TargetHeight, TargetWidth });
            for (int y = 0; y < TargetHeight; y++)
            {           
                Span<Rgb24> pixelSpan = image.GetPixelRowSpan(y);

                for (int x = 0; x < TargetWidth; x++)
                    input[0, 0, y, x] = pixelSpan[x].R / 255.0f;
            }

            using var session = new InferenceSession(model_name);
            string input_name = session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(input_name, input) };
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

            var output = results.First().AsEnumerable<float>().ToArray();
            var sum = output.Sum(x => (float)Math.Exp(x));
            var softmax = output.Select(x => (float)Math.Exp(x) / sum);
            var query = softmax.Select((x, i) => new { Label = classLabels[i], Confidence = x })
                .OrderByDescending(x => x.Confidence);

            return  Int32.Parse(query.First().Label);
        }

        void ThreadWork(string[] names, CancellationToken ct)
        {
            foreach (string name in names)
            {
                if (ct.IsCancellationRequested)
                    break;


                int label = this.LoadAndPredict(name);
                processResult(new LabeledImage(name, label));
                
                // waiter.WaitOne();
                // Console.WriteLine($"{Thread.CurrentThread.Name}: image \"{Path.GetFileName(name)}\" is {res}");
                // waiter.Set();
            }
        }

        //obsolete
        public void CreateThreadToProcessDirectory(string dir_name)
        {
            cts = new CancellationTokenSource();
            Thread processing_thread = new Thread(dir_name => 
            {
                finishedProcessing = false;
                wasTerminated = false;
                IsProcessing = true;
                ProcessDirectory((string)dir_name, cts.Token);
                finishedProcessing = !wasTerminated;
                IsProcessing = false;
            });

            processing_thread.Start(dir_name);
        }

        public async Task ProcessDirectoryAsync(string dir_name)
        {
            IsProcessing = true;
            cts = new CancellationTokenSource();
            Task processing_task = new Task((object dir_name) => 
                {
                    finishedProcessing = false;
                    wasTerminated = false;
                    ProcessDirectory((string)dir_name, cts.Token);
                    finishedProcessing = !wasTerminated;
                }, 
                dir_name);
            processing_task.Start();
            await processing_task;
            IsProcessing = false;
        }

        public void ProcessDirectory(string dir_name, CancellationToken ct)
        {
            string[] file_names = Directory.GetFiles(dir_name, "*.png");
            ProcessImagesByNames(file_names, ct);
        }

        public async Task ProcessImagesByNamesAsync(string[] file_names)
        {
            IsProcessing = true;
            cts = new CancellationTokenSource();
            Task processing_task = new Task((object file_names) => 
                {
                    finishedProcessing = false;
                    wasTerminated = false;
                    ProcessImagesByNames((string []) file_names, cts.Token);
                    finishedProcessing = !wasTerminated;
                }, 
                file_names);
            processing_task.Start();
            await processing_task;
            IsProcessing = false;
        }

        public void ProcessImagesByNames(string[] file_names, CancellationToken ct) 
        {
            // string[] file_names = Directory.GetFiles(dir_name, "*.png");

            int amount_of_img_per_thread = file_names.Length / thread_arr.Length;
            int img_counter = 0;
            int residue = file_names.Length % thread_arr.Length;

            for (int i = 0; i < thread_arr.Length; i++)
            {
                thread_arr[i] = new Thread(obj_names => ThreadWork((string[])obj_names, ct));
                thread_arr[i].Name = "Thread" + i.ToString();
                thread_arr[i].Start(this.CreateImagesNamesForThread(file_names, ref residue,
                                                                    amount_of_img_per_thread,
                                                                    ref img_counter));
            }

            for (int i = 0; i < thread_arr.Length; i++)
                if (!ct.IsCancellationRequested)
                    thread_arr[i].Join();
                else
                    break;

        }

        public void TerminateProcessing()
        {
            cts.Cancel();
            wasTerminated = true;
        }

        string[] CreateImagesNamesForThread(string[] file_names, ref int residue,
                                            int amount_of_images, ref int img_counter)
        {
            if (residue > 0)
            {
                amount_of_images++;
                residue--;
            }
            
            string[] images_for_thread = new string[amount_of_images];
            for (int j = 0; j < amount_of_images; j++)
                images_for_thread[j] = file_names[img_counter + j];

            img_counter += amount_of_images;

            return images_for_thread;
        }

        static readonly string[] classLabels = new[] 
        {   
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        };

    }
}
