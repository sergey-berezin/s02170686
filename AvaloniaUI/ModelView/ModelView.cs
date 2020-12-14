using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
// using NNLib;
using System.IO;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using System.Threading.Tasks.Dataflow;
using Avalonia.Media.Imaging;
using System.Net.Http;
using ServerContracts;
using Newtonsoft.Json;
// using DataBaseEntityFramework;

namespace ModelView
{
    public class ClassCounter 
    {
        public int ClassLabel;
        public int Amount;

        public ClassCounter(int class_label, int amount)
        {
            ClassLabel = class_label;
            Amount = amount;
        }
    }

    public interface UIServices
    {
        Task<string> ShowOpenDialogAsync();
        void IsVisibleProgressBar(bool value);
        void IsVisibleProcessedImageViewer(bool value);
        void IsVisibleFilteredImageViewer(bool value);
        void IsVisibleClassFilter(bool value);
        void GraphicalReactionToServerCondition(string condition);
    }

    public class ModelView : INotifyPropertyChanged
    {
        UIServices _uiser;
        string _workingDir;
        HttpClient _httpClient;
        bool _wasImageProcessingTerminated;

        int _processedImagesAmount;
        int ProcessedImagesAmount 
        {
            get { return _processedImagesAmount; }
            set 
            {
                _processedImagesAmount = value;
                ProgressBarValue = (int)((double)_processedImagesAmount / _totalAmountOfImagesInDirectory * 100);
            }
        }

        //who is subscriber?
        public event PropertyChangedEventHandler PropertyChanged;
        public List<string> DigitsListComboBox { get; set; }
        int _totalAmountOfImagesInDirectory;
        int _progressBarValue;
        public int ProgressBarValue
        {
            get { return _progressBarValue; }
            set 
            {
                _progressBarValue = value;

                if (_totalAmountOfImagesInDirectory == ProcessedImagesAmount)
                    _uiser.IsVisibleProgressBar(false);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressBarValue)));
            }
        }
        public ICommand ChooseDirCommand { get; set; }
        public ICommand InterruptProcessingCommand { get; set; }
        public ICommand CleanDataBaseCommand { get; set; }
        public ICommand UpgradeDatabaseStatistics { get; set; }
        
        int _selectedIndexComboBox;
        public int SelectedIndexComboBoxProperty
        {
             get { return _selectedIndexComboBox; } 
             set 
             {
                 _selectedIndexComboBox = value;
                 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndexComboBoxProperty)));
                 if (value != -1)
                    _uiser.IsVisibleFilteredImageViewer(true);
             }
        }
   
        ObservableCollection<AvaloniaUILabeledImage> _processedImageCollection;
        public ObservableCollection<AvaloniaUILabeledImage> ProcessedImageCollection 
        {
            get { return _processedImageCollection; }
            set
            {
                _processedImageCollection = value;

                //why need if have collection changed?
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessedImageCollection)));
            } 
        }

        // ObservableCollection<ClassCounter> _classCounterForDataBaseCollection;
        // public ObservableCollection<ClassCounter> ClassCounterForDataBaseCollection
        // {
        //     get { return _classCounterForDataBaseCollection; }
        //     set 
        //     {
        //         _classCounterForDataBaseCollection = value;
        //         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClassCounterForDataBaseCollection)));
        //     }
        // }

        string _textForDataBaseInfo;
        public string TextForDataBaseInfo 
        {
            get { return _textForDataBaseInfo; }
            set 
            {
                _textForDataBaseInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextForDataBaseInfo )));
            }
        }

        List<AvaloniaUILabeledImage> _filteredImageCollection;
        public List<AvaloniaUILabeledImage> FilteredImageCollection 
        { 
            get { return _filteredImageCollection; } 
            set 
            {
                _filteredImageCollection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredImageCollection)));
            } 
        }

        string _serverConnectionInfo;
        public string ServerConnectionInfo
        {
            get { return _serverConnectionInfo; }
            set
            {
                _serverConnectionInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServerConnectionInfo)));
            }
        }

        bool _useDataBase;
        public bool UseDataBase 
        { 
            get { return _useDataBase; }
            set 
            {
                _useDataBase = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseDataBase)));
            }
        }

        UIDatabaseTypeConverters _converter;
        string ImagePattern { get; set; }

        public ModelView(UIServices uiser, string image_pattern="*.png")
        {
            Init();

            _uiser = uiser;
            ImagePattern = image_pattern;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:5000/image_processing_api/");

            //to check connection with server
            MakeSafeHttpRequestAsync(() => _httpClient.GetAsync("empty"));

            // get previously processed images
            ProcessedImageCollection = new ObservableCollection<AvaloniaUILabeledImage>();
            ProcessedImageCollection.CollectionChanged += RefreshCollection;
            _ = TakePreviouslyProcessedImagesRequest();

            _converter = new UIDatabaseTypeConverters();
            PropertyChanged += ReactToSelectedIndexComboBox;
            _processedImagesAmount = 0;
        }

        async Task TakePreviouslyProcessedImagesRequest()
        {
            var result = await MakeSafeHttpRequestAsync(() => _httpClient.GetAsync("get_previously_processed_images"));
            var http_response_message = (HttpResponseMessage) result;

            if (http_response_message == null)
                return;

            string request_body = await http_response_message.Content.ReadAsStringAsync();

            List<ProcessedImageContracts> processed_images = 
                JsonConvert.DeserializeObject<List<ProcessedImageContracts>>(request_body);
            
            foreach (var img in processed_images)
            {
                // why do we need incoding or not just pass raw data ???
                byte[] byte_img = Convert.FromBase64String(img.IncodedImageBase64);
                Bitmap bitmap = _converter.AvaloniaBitmapFromByteImage(byte_img);
                AvaloniaUILabeledImage labeledAvImage = new AvaloniaUILabeledImage(img.Name, img.Label, bitmap);

                _processedImagesAmount++;
                _ = Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ProcessedImageCollection.Add(labeledAvImage);
                    ProcessedImagesAmount += 0;
                    
                });
            }

            Console.WriteLine($"Got previously processed images: {_processedImagesAmount}");
        }

        void ReactToSelectedIndexComboBox(object obj, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(SelectedIndexComboBoxProperty)) && (ProcessedImageCollection != null))
            {
                var query = ProcessedImageCollection.Where(x => x.Label == SelectedIndexComboBoxProperty);
                FilteredImageCollection = query.ToList<AvaloniaUILabeledImage>();
            }
        }

        void RefreshCollection(object obj, NotifyCollectionChangedEventArgs e)
        {
            var query = ProcessedImageCollection.Where(x => x.Label == SelectedIndexComboBoxProperty);
            FilteredImageCollection = query.ToList<AvaloniaUILabeledImage>();
        }

        void Init() 
        {
            InitDigits();
            InitCommands();
            // InitClassCounterForDataBase();
        }

        // void InitClassCounterForDataBase()
        // {
        //     for(int i = 0; i < 10; i++)
        //         ClassCounterForDataBaseCollection.Add(new ClassCounter(i, 0));
        // }

        void InitCommands()
        {
            ChooseDirCommand = new RelayCommand(async (object o) => await TryChooseDirectory());
            InterruptProcessingCommand = new RelayCommand((object o) => TryToInterrupt());
            CleanDataBaseCommand = new RelayCommand((object o) => TryToCleanDataBase());
            UpgradeDatabaseStatistics = new RelayCommand(async (object o) => await TryToGetDatabaseStatistics());
        }

        async Task TryToGetDatabaseStatistics()
        {
            var result = await MakeSafeHttpRequestAsync(() => _httpClient.GetAsync("statistics_database"));

            if (result == null)
                return;

            var http_response_message = (HttpResponseMessage)result;
            TextForDataBaseInfo = await http_response_message.Content.ReadAsStringAsync();
        }

        void TryToCleanDataBase()
        {
            _ = MakeSafeHttpRequestAsync(() => _httpClient.DeleteAsync("clean_database"));
        }

        void TryToInterrupt()
        {
            _ = MakeSafeHttpRequestAsync(() => _httpClient.GetAsync("terminate_current_processing"));
            _wasImageProcessingTerminated = true;
            _uiser.IsVisibleProgressBar(false);
        }

        Task<HttpResponseMessage> MakeSafeHttpRequestAsync(Func<Task<HttpResponseMessage>> http_request)
        {
            Func<Task<HttpResponseMessage>> func = new Func<Task<HttpResponseMessage>>(async () =>
            {
                HttpResponseMessage task_res = null;

                try
                {
                    HttpResponseMessage res = await http_request();
                    task_res = res;

                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // ServerConnectionInfo = "Server: Connected";
                        _uiser.GraphicalReactionToServerCondition("Connected");
                    }
                    );
                }
                catch(Exception)
                {
                    _ = Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        // ServerConnectionInfo = "Server: Disconnected";
                        _uiser.GraphicalReactionToServerCondition("Disconnected");
                        _uiser.IsVisibleProgressBar(false);
                    });
                }

                return task_res;
            });
            Task<HttpResponseMessage> http_request_task = func();

            return http_request_task;
        }

        int CountAmountOfImagesInDirectory() 
        {
            int counter = 0;

            foreach (string item in Directory.GetFiles(_workingDir, ImagePattern))
                counter++;

            return counter;
        }

        async Task TryToGetAndSaveProcessedImagesHttpRequest()
        {
            var result = await MakeSafeHttpRequestAsync(() => _httpClient.GetAsync("processed_images"));
            var http_response_message = (HttpResponseMessage) result;

            if (http_response_message == null)
                return;

            string request_body = await http_response_message.Content.ReadAsStringAsync();

            List<ProcessedImageContracts> processed_images = 
                JsonConvert.DeserializeObject<List<ProcessedImageContracts>>(request_body);
            
            foreach (var img in processed_images)
            {
                // why do we need incoding or not just pass raw data ???
                byte[] byte_img = Convert.FromBase64String(img.IncodedImageBase64);
                Bitmap bitmap = _converter.AvaloniaBitmapFromByteImage(byte_img);
                AvaloniaUILabeledImage labeledAvImage = new AvaloniaUILabeledImage(img.Name, img.Label, bitmap);

                _processedImagesAmount++;
                _ = Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ProcessedImageCollection.Add(labeledAvImage);
                    ProcessedImagesAmount += 0;
                });
            }

            Console.WriteLine($"Got images: {_processedImagesAmount}/{_totalAmountOfImagesInDirectory}");
        }

        async Task StartProcessImagesHttpRequest()
        {
            string[] full_file_names = Directory.GetFiles(_workingDir, "*.png");
            List<NewImageContracts> new_images = new List<NewImageContracts>();

            foreach (string full_name in full_file_names)
            {
                string img_base64 = Convert.ToBase64String(_converter.ByteImageFromFile(full_name));
                string name = Path.GetFileName(full_name);
                new_images.Add(new NewImageContracts(name , img_base64));
            }

            var content = JsonConvert.SerializeObject(new_images);

            var http_content = new StringContent(content);
            http_content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var res = await MakeSafeHttpRequestAsync(() => _httpClient.PostAsync("start_image_processing", http_content));
            
            Task try_to_get_processed_images_task = new Task(async () => 
            {
                while((ProcessedImagesAmount != _totalAmountOfImagesInDirectory) &&
                       !_wasImageProcessingTerminated && 
                       (res != null))
                    await TryToGetAndSaveProcessedImagesHttpRequest();
            }
            );
            try_to_get_processed_images_task.Start();

            await try_to_get_processed_images_task; 
        }

        async Task TryChooseDirectory()
        {
            _uiser.IsVisibleProcessedImageViewer(true);
            _uiser.IsVisibleClassFilter(true);
            // ProcessedImageCollection = new ObservableCollection<AvaloniaUILabeledImage>();
            // ProcessedImageCollection.CollectionChanged += RefreshCollection;
            _workingDir = null;
            _wasImageProcessingTerminated = false;
            _workingDir = await _uiser.ShowOpenDialogAsync();
            _uiser.IsVisibleProgressBar(true);

            if (_workingDir != null) 
            {
                ProcessedImageCollection = new ObservableCollection<AvaloniaUILabeledImage>();
                ProcessedImageCollection.CollectionChanged += RefreshCollection;
                _totalAmountOfImagesInDirectory = CountAmountOfImagesInDirectory();
                ProcessedImagesAmount = 0;

                await StartProcessImagesHttpRequest();
            }
            else
                _uiser.IsVisibleProgressBar(false);
        }

        void InitDigits()
        {
            DigitsListComboBox = new List<string>();
            // ClassCounterForDataBaseCollection = new ObservableCollection<ClassCounter>();

            for (int i = 0; i < 10; i++)
                DigitsListComboBox.Add(i.ToString());
        }
    }
}
