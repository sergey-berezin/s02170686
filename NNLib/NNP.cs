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
        public LabeledImage(string name, int label, byte[] byte_image) 
        {
            Name = name;
            Label = label;
            ByteImage = byte_image;
        }

        //name including absolute path
        public string Name { get; set; }
        public int Label { get; set; }
        public byte[] ByteImage { get; set; }

        public override string ToString()
        {
            return Path.GetFileName(Name) + " " + Label.ToString();
        }
    }

    public class NewImage
    {
        public NewImage(string name, byte[] byte_image)
        {
            Name = name;
            ByteImage = byte_image;
        }

        public string Name { get; set; }
        public byte[] ByteImage { get; set; }

        public override string ToString()
        {
            return Path.GetFileName(Name);
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
        ByteImageSharpConverter _converter;


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
        public NNP(string model_name, ProcessResultDelegate processResult, int width=28, int height=28) 
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
            _converter = new ByteImageSharpConverter(width, height);
        }

        public int LoadAndPredict(Image<Rgb24> image)
        {
            // using var image =  Image.Load<Rgb24>(img_name);

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

        void ThreadWork(List<NewImage> new_images, CancellationToken ct)
        {
            foreach (NewImage new_image in new_images)
            {
                if (ct.IsCancellationRequested)
                    break;

                Image<Rgb24> image = _converter.ImageSharpFromByteImage(new_image.ByteImage);
                int label = this.LoadAndPredict(image);
                processResult(new LabeledImage(new_image.Name, label, new_image.ByteImage));
                
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
                // ProcessDirectory((string)dir_name, cts.Token);
                finishedProcessing = !wasTerminated;
                IsProcessing = false;
            });

            processing_thread.Start(dir_name);
        }

        // Changed them for compitability with the first task
        // public async Task ProcessDirectoryAsync(string dir_name)
        // {
        //     IsProcessing = true;
        //     cts = new CancellationTokenSource();
        //     Task processing_task = new Task((object dir_name) => 
        //         {
        //             finishedProcessing = false;
        //             wasTerminated = false;
        //             ProcessDirectory((string)dir_name, cts.Token);
        //             finishedProcessing = !wasTerminated;
        //         }, 
        //         dir_name);
        //     processing_task.Start();
        //     await processing_task;
        //     IsProcessing = false;
        // }

        // public void ProcessDirectory(string dir_name, CancellationToken ct)
        // {
        //     string[] file_names = Directory.GetFiles(dir_name, "*.png");
        //     ProcessImagesByNames(file_names, ct);
        // }

        public async Task ProcessImageListAsync(List<NewImage> new_images)
        {
            IsProcessing = true;
            cts = new CancellationTokenSource();
            Task processing_task = new Task((object new_images) => 
                {
                    finishedProcessing = false;
                    wasTerminated = false;
                    ProcessImageList((List<NewImage>) new_images, cts.Token);
                    finishedProcessing = !wasTerminated;
                }, 
                new_images);
            processing_task.Start();
            await processing_task;
            IsProcessing = false;
        }

        public void ProcessImageList(List<NewImage> new_images, CancellationToken ct) 
        {
            // string[] file_names = Directory.GetFiles(dir_name, "*.png");

            int amount_of_img_per_thread = new_images.Count / thread_arr.Length;
            int img_counter = 0;
            int residue = new_images.Count % thread_arr.Length;

            for (int i = 0; i < thread_arr.Length; i++)
            {
                thread_arr[i] = new Thread(thread_new_images => ThreadWork((List<NewImage>)thread_new_images, ct));
                thread_arr[i].Name = "Thread" + i.ToString();
                thread_arr[i].Start(this.CreateImagesForThread(new_images, ref residue,
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

        List<NewImage> CreateImagesForThread(List<NewImage> new_images, ref int residue,
                                       int amount_of_images, ref int img_counter)
        {
            if (residue > 0)
            {
                amount_of_images++;
                residue--;
            }
            
            List<NewImage> images_for_thread = new List<NewImage>();
            for (int j = 0; j < amount_of_images; j++)
                images_for_thread.Add(new_images[img_counter + j]);

            img_counter += amount_of_images;

            return images_for_thread;
        }

        static readonly string[] classLabels = new[] 
        {   
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        };

    }
}
