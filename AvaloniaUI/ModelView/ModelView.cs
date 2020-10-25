using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NNLib;
using System.IO;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;

namespace ModelView
{

    public interface UIServices
    {
        Task<string> ShowOpenDialogAsync();
        void IsVisibleProgressBar(bool value);
        void IsVisibleProcessedImageViewer(bool value);
        void IsVisibleFilteredImageViewer(bool value);
        void IsVisibleClassFilter(bool value);
    }

    public class ModelView : INotifyPropertyChanged
    {
        UIServices _uiser;
        NNP _nnpModel;
        string _workingDir;
        ProcessResultDelegate _processResult;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressBarValue)));
            }
        }
        public ICommand ChooseDirCommand { get; set; }
        public ICommand InterruptProcessingCommand { get; set; }
        
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
   
        ObservableCollection<LabeledImage> _processedImageCollection;
        public ObservableCollection<LabeledImage> ProcessedImageCollection 
        {
            get { return _processedImageCollection; }
            set
            {
                _processedImageCollection = value;

                //why need if have collection changed?
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessedImageCollection)));
            } 
        }

        List<LabeledImage> _filteredImageCollection;
        public List<LabeledImage> FilteredImageCollection 
        { 
            get { return _filteredImageCollection; } 
            set 
            {
                _filteredImageCollection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredImageCollection)));
            } 
        }

        public ModelView(UIServices uiser)
        {
            Init();

            _uiser = uiser;
            PropertyChanged += ReactToSelectedIndexComboBox;
            ProcessedImageCollection = null;
            _processedImagesAmount = 0;
            _processResult = new ProcessResultDelegate(processLabeledImage);
            _nnpModel = new NNP("/Users/macbookpro/autumn_prac/s02170686/mnist-8.onnx", _processResult);
            _nnpModel.PropertyChanged += CheckExecuteCondition;
        }

        void CheckExecuteCondition(object obj, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_nnpModel.IsProcessing))
                ((RelayCommand)InterruptProcessingCommand).RaiseCanExecuteChanged(this, e);
        }

        void ReactToSelectedIndexComboBox(object obj, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(SelectedIndexComboBoxProperty)) && (ProcessedImageCollection != null))
            {
                var query = ProcessedImageCollection.Where(x => x.Label == SelectedIndexComboBoxProperty);
                FilteredImageCollection = query.ToList<LabeledImage>();
            }
        }

        void RefreshCollection(object obj, NotifyCollectionChangedEventArgs e)
        {
            var query = ProcessedImageCollection.Where(x => x.Label == SelectedIndexComboBoxProperty);
            FilteredImageCollection = query.ToList<LabeledImage>();
        }

        //await?
        void processLabeledImage(LabeledImage labeledImage)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProcessedImagesAmount++;
                ProcessedImageCollection.Add(labeledImage);
            });
        }

        void Init() 
        {
            InitDigits();
            InitCommands();
        }

        void InitCommands()
        {
            ChooseDirCommand = new RelayCommand(async (object o) => await TryChooseDirectory());
            InterruptProcessingCommand = new RelayCommand((object o) => TryToInterrupt(), 
                                                          (object o) => CanExecuteInterruptCommand());
        }

        bool CanExecuteInterruptCommand()
        {
            return _nnpModel.IsProcessing;
        }

        void TryToInterrupt()
        {
            _nnpModel.TerminateProcessing();
        }

        int CountAmountOfImagesInDirectory() 
        {
            int counter = 0;

            foreach (string item in Directory.GetFiles(_workingDir))
                counter++;

            return counter;
        }

        async Task TryChooseDirectory()
        {
            _uiser.IsVisibleProcessedImageViewer(true);
            _uiser.IsVisibleClassFilter(true);
            _processedImagesAmount = 0;
            ProgressBarValue = 0;
            ProcessedImageCollection = new ObservableCollection<LabeledImage>();
            ProcessedImageCollection.CollectionChanged += RefreshCollection;
            _workingDir = null;
            _workingDir = await _uiser.ShowOpenDialogAsync();
            _uiser.IsVisibleProgressBar(true);

            if (_workingDir != null) 
            {
                _totalAmountOfImagesInDirectory = CountAmountOfImagesInDirectory();
                await _nnpModel.ProcessDirectoryAsync(_workingDir);
            }
            _uiser.IsVisibleProgressBar(false);
        }

        void InitDigits()
        {
            DigitsListComboBox = new List<string>();

            for (int i = 0; i < 10; i++)
                DigitsListComboBox.Add(i.ToString());
        }
    }
}
