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
using System.Threading.Tasks.Dataflow;
using Avalonia.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using ModelView.DataBaseClasses;

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

        bool _isDatabaseBusy;
        public bool IsDatabaseBusy
        {
            get { return _isDatabaseBusy; }
            set
            {
                _isDatabaseBusy = value;
                Dispatcher.UIThread.InvokeAsync(() =>
                    ((RelayCommand)CleanDataBaseCommand).RaiseCanExecuteChanged(this, new EventArgs())
                );
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

        ObservableCollection<ClassCounter> _classCounterForDataBaseCollection;
        public ObservableCollection<ClassCounter> ClassCounterForDataBaseCollection
        {
            get { return _classCounterForDataBaseCollection; }
            set 
            {
                _classCounterForDataBaseCollection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClassCounterForDataBaseCollection)));
            }
        }

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

        DataBaseClasses.MyContext _currDataBaseContext;

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
        ActionBlock<AvaloniaUILabeledImage> _databaseMailbox;
        string ImagePattern { get; set; }

        public ModelView(UIServices uiser, string image_pattern="*.png")
        {
            Init();

            _uiser = uiser;
            ImagePattern = image_pattern;
            _currDataBaseContext = new MyContext();
            FillClassCounterWithDataBase();
            UseDataBase = false;
            _databaseMailbox = CreateDatabaseMailbox();
            _converter = new UIDatabaseTypeConverters();
            PropertyChanged += ReactToSelectedIndexComboBox;
            ProcessedImageCollection = null;
            _processedImagesAmount = 0;
            IsDatabaseBusy = false;
            _processResult = new ProcessResultDelegate(ProcessLabeledImage);
            _nnpModel = new NNP("../../mnist-8.onnx", _processResult);
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
                FilteredImageCollection = query.ToList<AvaloniaUILabeledImage>();
            }
        }

        void RefreshCollection(object obj, NotifyCollectionChangedEventArgs e)
        {
            var query = ProcessedImageCollection.Where(x => x.Label == SelectedIndexComboBoxProperty);
            FilteredImageCollection = query.ToList<AvaloniaUILabeledImage>();
        }

        ActionBlock<AvaloniaUILabeledImage> CreateDatabaseMailbox()
        {
            ActionBlock<AvaloniaUILabeledImage> add_block = new ActionBlock<AvaloniaUILabeledImage>(
            (AvaloniaUILabeledImage labeledAvImage) => 
            {
                //creating ProcessedImage object to store in Data Base
                ProcessedImageDB processed_image = new ProcessedImageDB();
                processed_image.ImageLabel = labeledAvImage.Label;
                processed_image.ImageName = labeledAvImage.Name;
                processed_image.AdditionalInfo = new List<ImageDetailDB>();

                //creating ImageDetails object to store in Data Base
                ImageDetailDB details = new ImageDetailDB();
                details.ByteImage = _converter.ByteImageFromFile(labeledAvImage.FullName);
                details.PrimaryInfo = new List<ProcessedImageDB>();

                //adding links to each other
                details.PrimaryInfo.Add(processed_image);
                processed_image.AdditionalInfo.Add(details);

                _currDataBaseContext.Add(processed_image);
                _currDataBaseContext.Add(details);
                _currDataBaseContext.SaveChanges();

                if (ProcessedImagesAmount == _totalAmountOfImagesInDirectory)
                    IsDatabaseBusy = false;
            });

            return add_block;
        }

        //await?
        void ProcessLabeledImage(LabeledImage labeledImage)
        {
            Bitmap bitmap = new Bitmap(labeledImage.FullName);
            AvaloniaUILabeledImage labeledAvImage = new AvaloniaUILabeledImage(labeledImage.FullName,
                                                                                labeledImage.Label,
                                                                                bitmap);
            Dispatcher.UIThread.InvokeAsync(() => 
            {
                ProcessedImageCollection.Add(labeledAvImage);
                ProcessedImagesAmount++;

                if (UseDataBase)
                {
                    _databaseMailbox.Post(labeledAvImage);
                    ClassCounterForDataBaseCollection[labeledAvImage.Label].Amount += 1;

                    FillDataBaseInfo();
                    
                    Console.WriteLine($"Data Base added: {labeledAvImage.Name}");
                }
            });
        }

        void Init() 
        {
            InitDigits();
            InitCommands();
            InitClassCounterForDataBase();
        }

        void InitClassCounterForDataBase()
        {
            for(int i = 0; i < 10; i++)
                ClassCounterForDataBaseCollection.Add(new ClassCounter(i, 0));
        }

        void ChangeCounter(int class_id, int amount)
        {
            foreach(var el in ClassCounterForDataBaseCollection)
                if (class_id == el.ClassLabel)
                {
                    el.Amount = amount;
                    break;
                }
        }

        void FillDataBaseInfo()
        {
            bool have_info_to_display = false;
            foreach(var el in ClassCounterForDataBaseCollection)
                if (el.Amount != 0)
                {
                    have_info_to_display = true;
                    break;
                }

            string text_info = "";
            if (!have_info_to_display)
                text_info = "Database is empty.";
            else
            {
                text_info = "Database info of processed image classes:\n";
                foreach(var el in ClassCounterForDataBaseCollection)
                    if (el.Amount > 0)
                        text_info += "\n" + $"   -Class {el.ClassLabel}: Database has {el.Amount} element(s)";
            }

            TextForDataBaseInfo = text_info;
            Dispatcher.UIThread.InvokeAsync(() =>
                ((RelayCommand)CleanDataBaseCommand).RaiseCanExecuteChanged(this, new EventArgs())
            );
        }

        void FillClassCounterWithDataBase()
        {
            Func<ProcessedImageDB, int> func = a => a.ImageLabel;
            foreach(var found_class in _currDataBaseContext.ProcessedImages.GroupBy(func))
                ChangeCounter(found_class.Key, found_class.Count());
            
            FillDataBaseInfo();
            Dispatcher.UIThread.InvokeAsync(() =>
                ((RelayCommand)CleanDataBaseCommand).RaiseCanExecuteChanged(this, new EventArgs())
            );
        }

        void InitCommands()
        {
            ChooseDirCommand = new RelayCommand(async (object o) => await TryChooseDirectory());
            InterruptProcessingCommand = new RelayCommand((object o) => TryToInterrupt(),
                                                          (object o) => CanExecuteInterruptCommand());
            CleanDataBaseCommand = new RelayCommand(async (object o) => await TryToCleanDataBase(),
                                                    (object o) => CanExecuteCleanDataBaseCommand());
        }

        void RemoveProcessedImageTable()
        {
            foreach(var db_elem in _currDataBaseContext.ProcessedImages)
                _currDataBaseContext.ProcessedImages.Remove(db_elem);
        }

        void RemoveImageDetailsTable()
        {
            foreach(var db_elem in _currDataBaseContext.ImageDetails)
                _currDataBaseContext.ImageDetails.Remove(db_elem);
        }

        async Task TryToCleanDataBase()
        {
            Task delete_tables = new Task(() =>
            {
                RemoveProcessedImageTable();
                RemoveImageDetailsTable();

                ClassCounterForDataBaseCollection = new ObservableCollection<ClassCounter>();
                InitClassCounterForDataBase();
                FillDataBaseInfo();
            });
            delete_tables.Start();

            await delete_tables;
            _currDataBaseContext.SaveChanges();
        }

        bool CanExecuteCleanDataBaseCommand()
        {
            bool can_press = false;
            foreach(var el in ClassCounterForDataBaseCollection)
                if (el.Amount > 0)
                {
                    can_press = true;
                    break;
                }

            return can_press && !_nnpModel.IsProcessing && !_isDatabaseBusy;
        }

        bool CanExecuteInterruptCommand()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
                ((RelayCommand)CleanDataBaseCommand).RaiseCanExecuteChanged(this, new EventArgs())
            );
            
            return _nnpModel.IsProcessing;
        }

        void TryToInterrupt()
        {
            _nnpModel.TerminateProcessing();
        }

        int CountAmountOfImagesInDirectory() 
        {
            int counter = 0;

            foreach (string item in Directory.GetFiles(_workingDir, ImagePattern))
                counter++;

            return counter;
        }

        string[] GetFileNames(string[] full_path_names)
        {
            string[] names = new string[full_path_names.Length];

            int counter = 0;
            foreach(string full_name in full_path_names)
            {
                names[counter] = Path.GetFileName(full_name);
                counter++;
            }

            return names;
        }

        bool IsNameInArray(string name, string[] full_image_names)
        {
            foreach(string full_name in full_image_names)
                if (full_name == name)
                    return true;

            return false;
        }

        bool IsIdInArray(int id, int[] ids)
        {
            foreach(int curr_id in ids)
                if (id == curr_id)
                    return true;

            return false;
        }

        //(group_name, [(id, image itself)])
        List<(string, List<(int, byte[])>)> CreateProcessedImageGroups(MyContext db, string[] file_names,
                                                                       out string[] found_names)
        {
            Func<ProcessedImageDB, bool> func = a => IsNameInArray(a.ImageName, file_names);
            var query = db.ProcessedImages.Include(a => a.AdditionalInfo).Where(func);

            List<(string, List<(int, byte[])>)> processed_images_groups = new List<(string, List<(int, byte[])>)>();
            List<(int, byte[])> images_with_one_name;
            found_names = new string[query.GroupBy(a => a.ImageName).Count()];
            int image_counter = 0;
            foreach(var db_group in query.GroupBy(a => a.ImageName))
            {
                images_with_one_name = new List<(int, byte[])>();

                foreach(var db_elem in db_group)
                    images_with_one_name.Add((db_elem.ImageId, db_elem.AdditionalInfo.First().ByteImage));

                found_names[image_counter] = db_group.Key;
                image_counter++;
                processed_images_groups.Add((db_group.Key, images_with_one_name));
            }

            return processed_images_groups;
        }

        string[] CreateUnprocessedImageNames(MyContext db, out int[] processed_images_id)
        {
            string[] full_file_names = Directory.GetFiles(_workingDir, "*.png");
            List<string> full_new_names_list;
            List<int> processed_image_id_list;
            string[] file_names = GetFileNames(full_file_names);

            string[] found_names;
            List<(string, List<(int, byte[])>)> processed_images_groups = CreateProcessedImageGroups(db, file_names,
                                                                                                     out found_names);

            full_new_names_list = FormNamesToProcess(full_file_names, found_names, 
                                                     processed_images_groups,
                                                     out processed_image_id_list);

            processed_images_id = processed_image_id_list.ToArray<int>();

            Console.WriteLine($"Need to process: {full_new_names_list.Count()}/{full_file_names.Length}");
            Console.WriteLine($"Were In Database: {processed_image_id_list.Count}/{full_file_names.Length}");

            return full_new_names_list.ToArray<string>();
        }

        List<string> FormNamesToProcess(string[] full_file_names, string[] found_names,
                                        List<(string, List<(int, byte[])>)> processed_images_groups,
                                        out List<int> processed_image_id_list)
        {
            List<string> full_new_names_list = new List<string>();
            processed_image_id_list = new List<int>();
            int group_counter = 0;
            bool already_in_database;
            foreach(string full_name in full_file_names)
            {
                if (IsNameInArray(Path.GetFileName(full_name), found_names))
                {
                    byte[] new_byte_image = _converter.ByteImageFromFile(full_name);
                    already_in_database = false;
                    int matched_image_id = -1;
                    group_counter = FindProperGroup(full_name, processed_images_groups);

                    for (int in_group_counter = 0; in_group_counter < processed_images_groups.Count(); in_group_counter++)
                        if (AreByteArraysEqual(new_byte_image, 
                                               processed_images_groups[group_counter].Item2[in_group_counter].Item2))
                        {
                            already_in_database = true;
                            matched_image_id = processed_images_groups[group_counter].Item2[in_group_counter].Item1;
                            break;
                        }
                    
                    if (already_in_database)
                        processed_image_id_list.Add(matched_image_id);
                    else
                        full_new_names_list.Add(full_name);
                }
                else
                    full_new_names_list.Add(full_name);
            }

            return full_new_names_list;
        }

        int FindProperGroup(string full_name, List<(string, List<(int, byte[])>)> groups)
        {
            int index = -1;
            string name = Path.GetFileName(full_name);
            for (int i = 0; i < groups.Count(); i++)
                if (name == groups[i].Item1)
                {
                    index = i;
                    break;
                }

            return index;
        }

        bool AreByteArraysEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for(int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return false;

            return true;
        }

        void AddProcessedImagesFromDataBase(int[] processed_image_id, DataBaseClasses.MyContext db)
        {
            Func<ProcessedImageDB, bool> func = a => IsIdInArray(a.ImageId, processed_image_id);
            var query = db.ProcessedImages.Include(a => a.AdditionalInfo).Where(func);
            foreach(var db_elem in query)
            {
                byte[] byte_image = db_elem.AdditionalInfo.First().ByteImage;
                Bitmap bitmap = _converter.AvaloniaBitmapFromByteImage(byte_image);
                AvaloniaUILabeledImage labeled_image = new AvaloniaUILabeledImage(db_elem.ImageName,
                                                                                  db_elem.ImageLabel,
                                                                                  bitmap);
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ProcessedImagesAmount++;
                    ProcessedImageCollection.Add(labeled_image);
                });
            }

            IsDatabaseBusy= false;
        }

        async Task ProcessDirectoryWithoutDataBaseUsage()
        {
            await _nnpModel.ProcessDirectoryAsync(_workingDir);
        }

        async Task ProcessDirectoryWithDataBaseUsage()
        {
            Func<object, string[]> func = (object db) => 
            {
                int[] image_id_were_processed;
                string[] full_names_to_process = CreateUnprocessedImageNames((DataBaseClasses.MyContext)db, 
                                                                              out image_id_were_processed);

                //debug
                // Console.WriteLine("Names wer Processed");
                // foreach(string s in names_were_processed)
                //     Console.WriteLine(s);

                // Console.WriteLine();
                // Console.WriteLine("Names to Process");
                // foreach(string s in full_names_to_process)
                //     Console.WriteLine(s);

                AddProcessedImagesFromDataBase(image_id_were_processed, (DataBaseClasses.MyContext)db);

                return full_names_to_process;
            };

            Task<string[]> unprocessed_image_task = new Task<string[]>(func, _currDataBaseContext);

            unprocessed_image_task.Start();
            string[] full_file_names = await unprocessed_image_task;
            await _nnpModel.ProcessImagesByNamesAsync(full_file_names);
        }

        async Task TryChooseDirectory()
        {
            _uiser.IsVisibleProcessedImageViewer(true);
            _uiser.IsVisibleClassFilter(true);
            ProcessedImageCollection = new ObservableCollection<AvaloniaUILabeledImage>();
            ProcessedImageCollection.CollectionChanged += RefreshCollection;
            _workingDir = null;
            _workingDir = await _uiser.ShowOpenDialogAsync();
            _uiser.IsVisibleProgressBar(true);

            if (_workingDir != null) 
            {
                _totalAmountOfImagesInDirectory = CountAmountOfImagesInDirectory();
                ProcessedImagesAmount = 0;

                if (!UseDataBase)
                    await ProcessDirectoryWithoutDataBaseUsage();
                else
                {
                    IsDatabaseBusy = true;
                    await ProcessDirectoryWithDataBaseUsage();
                }
            }
            else
                _uiser.IsVisibleProgressBar(false);
        }

        void InitDigits()
        {
            DigitsListComboBox = new List<string>();
            ClassCounterForDataBaseCollection = new ObservableCollection<ClassCounter>();

            for (int i = 0; i < 10; i++)
                DigitsListComboBox.Add(i.ToString());
        }
    }
}
