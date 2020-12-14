using System;
using DataBaseEntityFramework;
using System.Threading.Tasks;
using System.Threading;
using NNLib;
using System.Threading.Tasks.Dataflow;
using ServerContracts;
using ServerContracts.DatabaseInterface;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ServerInteractions.ServerDatabase
{

    public class NewImageServer
    {
        public NewImageServer(string name, byte[] byte_img) 
		{
			Name = name;
			ByteImage = byte_img;
		}

		public string Name { get; set; }
		public byte[] ByteImage { get; set; }

		public override string ToString()
		{
			return Name;
		}
    }

    public class ProcessedImageServer
    {

		// id to be self defined ???
        public ProcessedImageServer(string name, int label, byte[] byte_img) 
		{
			Name = name;
			Label = label;
			ByteImage = byte_img;
		}

		public string Name { get; set; }
		public int Label { get; set; }
		public byte[] ByteImage { get; set; }

		public override string ToString()
		{
			return Name + " " + Label.ToString();
		}
    }

    // Class purpose is storing in database new iamges or retrieving from database processed images 
    // ASPServer --> {NewImageConstacts} --> ServerDatabase --> NNLib --> {ProcessedImageServer} --> Database
    // ASPServer <-- {ProcessedImageConstracts} <-- ServerDatabase <-- {ProcessedImageServer} <-- Database
    public class ServerDatabase : IDatabaseEF
    {
        MyContext _currDatabaseContext;
        NNP _nnpModel;
        ActionBlock<ProcessedImageServer> _databaseMailbox;
        ActionBlock<Func<Task>> _dispatcher;
        AutoResetEvent _dispatcherCompletionEvent;
        AutoResetEvent _addedNewProcessedImage;
        int _newImagesAmount;
        int _processedImagesAmount;
        int _alreadyTakenAmountOfProcessedImages;
        delegate void DispatcherDelegate(Func<Task> func);

        public List<ProcessedImageServer> ProcessedImageCollection { get; set; }
        public List<NewImageServer> NewImageCollection { get; set; }
        public bool IsDatabaseBusy { get; set; }

        public ServerDatabase(string model_path)
        {
            _currDatabaseContext = new MyContext();
            _dispatcher = CreateLocalDispatcher();
            _dispatcherCompletionEvent = new AutoResetEvent(false);
            _addedNewProcessedImage = new AutoResetEvent(false);
            _databaseMailbox = CreateDatabaseMailbox();
            _nnpModel = new NNP(model_path, new ProcessResultDelegate(ProcessLabeledImage));
        }

        void ProcessLabeledImage(LabeledImage labeledImage)
        {
            ProcessedImageServer labeledAvImage = new ProcessedImageServer(labeledImage.Name,
                                                                           labeledImage.Label,
                                                                           labeledImage.ByteImage);
            _dispatcher.Post(() => 
            {
                Task t = new Task(() => 
                {
                    ProcessedImageCollection.Add(labeledAvImage);
                    _databaseMailbox.Post(labeledAvImage);
                    _processedImagesAmount++;
                    _addedNewProcessedImage.Set();

                    Console.Write($"[Ready {_processedImagesAmount}/{_newImagesAmount}]");
                    Console.WriteLine($" Image <{labeledAvImage.Name}> added into Database.");
                    
                    if (_processedImagesAmount == _newImagesAmount)
                        _dispatcherCompletionEvent.Set();
                });
                t.Start();

                return t;
            });
        }

        public bool IsCurrentlyProcessingImages()
        {
            return _nnpModel.IsProcessing;
        }

        public void InterruptCurrentImageProcessing()
        {
            if (_nnpModel.IsProcessing)
                _nnpModel.TerminateProcessing();
        }

        public async Task<List<ProcessedImageContracts>> ReturnAlreadyProcessedImages()
        {
            List<ProcessedImageContracts> already_processed_images = new List<ProcessedImageContracts>();

            Task wait_when_added = new Task(() =>
            {
                _addedNewProcessedImage.WaitOne();
            }
            );
            wait_when_added.Start();

            await wait_when_added;
            if (_alreadyTakenAmountOfProcessedImages != _newImagesAmount)
                _addedNewProcessedImage.Reset();

            int currently_processed_amount = ProcessedImageCollection.Count;
            
            for (int i = _alreadyTakenAmountOfProcessedImages; i < currently_processed_amount; i++)
            {
                // converting ProcessedImageServer to ProcessedImageContracts to send it back to client
                string img_base64 = Convert.ToBase64String(ProcessedImageCollection[i].ByteImage);
                string name = ProcessedImageCollection[i].Name;
                int label = ProcessedImageCollection[i].Label;
                already_processed_images.Add(new ProcessedImageContracts(name, label, img_base64));
            }

            int returned_amount = currently_processed_amount - _alreadyTakenAmountOfProcessedImages;
            _alreadyTakenAmountOfProcessedImages += returned_amount;

            return already_processed_images;
        }

        void RemoveProcessedImageTable()
        {
            foreach(var db_elem in _currDatabaseContext.ProcessedImages)
                _currDatabaseContext.ProcessedImages.Remove(db_elem);
        }

        void RemoveImageDetailsTable()
        {
            foreach(var db_elem in _currDatabaseContext.ImageDetails)
                _currDatabaseContext.ImageDetails.Remove(db_elem);
        }

        public async Task CleanDatabase()
        {
            Task delete_tables = new Task(() =>
            {
                RemoveProcessedImageTable();
                RemoveImageDetailsTable();

            });
            delete_tables.Start();

            await delete_tables;
            _currDatabaseContext.SaveChanges();
        }

        // add note about still processing or not 
        public Task<string> GetStatistics()
        {
            string database_info = "";
            Func<ProcessedImageDB, int> func = a => a.ImageLabel;

            Task<string> get_statistics = new Task<string>(() => 
            {
                foreach(var label_group in _currDatabaseContext.ProcessedImages.GroupBy(func))
                    if (label_group.Count() > 0)
                        database_info += $"Class {label_group.Key}: Database has {label_group.Count()} elements.\n";
                
                if (database_info == "")
                    database_info = "Database is empty.";

                return database_info;
            }
            );
            get_statistics.Start();

            return get_statistics;
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

        void AddProcessedImagesFromDataBase(int[] processed_image_id, MyContext db)
        {
            Func<ProcessedImageDB, bool> func = a => IsIdInArray(a.ImageId, processed_image_id);
            var query = db.ProcessedImages.Include(a => a.AdditionalInfo).Where(func);
            foreach(var db_elem in query)
            {
                byte[] byte_image = db_elem.AdditionalInfo.First().ByteImage;
                ProcessedImageServer labeled_image = new ProcessedImageServer(db_elem.ImageName,
                                                                              db_elem.ImageLabel,
                                                                              byte_image);

                _dispatcher.Post(() =>
                {
                    Task t = new Task(() => 
                    {
                        ProcessedImageCollection.Add(labeled_image);
                        _processedImagesAmount++;
                        _addedNewProcessedImage.Set();

                        Console.Write($"[Ready {_processedImagesAmount}/{_newImagesAmount}]");
                        Console.WriteLine($" Image <{labeled_image.Name}> was inside Database.");
                        if (_processedImagesAmount == _newImagesAmount)
                            _dispatcherCompletionEvent.Set();
                    });
                    t.Start();

                    return t;
                });
            }

            IsDatabaseBusy = false;
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

        string[] GetFileNames(List<NewImageServer> images)
        {
            string[] names = new string[images.Count];
            for (int i = 0; i < images.Count; i++)
                names[i] = images[i].Name;

            return names;
        }

        List<NewImageServer> CreateUnprocessedImages(MyContext db, out int[] processed_images_id)
        {
            List<NewImageServer> images_to_process;
            List<int> processed_image_id_list;
            string[] file_names = GetFileNames(NewImageCollection);

            string[] found_names;
            List<(string, List<(int, byte[])>)> processed_images_groups = CreateProcessedImageGroups(db, file_names,
                                                                                                     out found_names);

            images_to_process = FormImagesToProcess(found_names, processed_images_groups,
                                                    out processed_image_id_list);

            processed_images_id = processed_image_id_list.ToArray<int>();

            Console.WriteLine($"Need to process: {images_to_process.Count}/{NewImageCollection.Count}");
            Console.WriteLine($"Were In Database: {processed_image_id_list.Count}/{NewImageCollection.Count}");

            return images_to_process;
        }

        List<NewImageServer> FormImagesToProcess(string[] found_names, 
                                                    List<(string, List<(int, byte[])>)> processed_images_groups,
                                                    out List<int> processed_image_id_list)
        {
            // Console.WriteLine("form");
            List<NewImageServer> new_images_to_process = new List<NewImageServer>();
            processed_image_id_list = new List<int>();
            int group_counter = 0;
            bool already_in_database;
            foreach(var img in NewImageCollection)
            {
                // Console.WriteLine("outer");

                if (IsNameInArray(img.Name, found_names))
                {
                    // Console.WriteLine("inner");

                    byte[] new_byte_image = img.ByteImage;
                    already_in_database = false;
                    int matched_image_id = -1;
                    group_counter = FindProperGroup(img.Name, processed_images_groups);
                    // Console.WriteLine("found");

                    for (int in_group_counter = 0; in_group_counter < processed_images_groups.Count(); in_group_counter++)
                    {
                        // Console.WriteLine($"in {in_group_counter}");
                        // try{
                        if (AreByteArraysEqual(new_byte_image, 
                                               processed_images_groups[group_counter].Item2[in_group_counter].Item2))
                        {
                            // Console.WriteLine("equal");
                            already_in_database = true;
                            matched_image_id = processed_images_groups[group_counter].Item2[in_group_counter].Item1;
                            break;
                        }
                        // }
                        // catch(Exception e)
                        // {
                        //     Console.WriteLine($"exception : {e}");
                        // }

                        // Console.WriteLine(in_group_counter);
                    }
                    // Console.WriteLine("decide");
                    
                    if (already_in_database)
                        processed_image_id_list.Add(matched_image_id);
                    else
                        new_images_to_process.Add(img);
                }
                else
                    new_images_to_process.Add(img);
            }

            // Console.WriteLine("form");
            return new_images_to_process;
        }

        int FindProperGroup(string name, List<(string, List<(int, byte[])>)> groups)
        {
            int index = -1;
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

        async Task ProcessNewImagesWithDataBaseUsage()
        {
            Func<object, List<NewImageServer>> func = (object db) => 
            {
                int[] image_id_were_processed;
                List<NewImageServer> images_to_process = CreateUnprocessedImages((MyContext)db, 
                                                                            out image_id_were_processed);

                // Console.WriteLine("not added");
                AddProcessedImagesFromDataBase(image_id_were_processed, (MyContext)db);
                // Console.WriteLine("added");
                return images_to_process;
            };

            Task<List<NewImageServer>> unprocessed_image_task = 
                new Task<List<NewImageServer>>(func, _currDatabaseContext);

            unprocessed_image_task.Start();
            List<NewImageServer> images_to_process = await unprocessed_image_task;

            // converting types to be able to use neural network class
            List<NewImage> need_to_classify_images = new List<NewImage>();
            foreach(var el in images_to_process)
                need_to_classify_images.Add(new NewImage(el.Name, el.ByteImage));

            await _nnpModel.ProcessImageListAsync(need_to_classify_images);
        }

        Task DispatcherCompleted()
        {
            Task completion = new Task(() => 
            {
                _dispatcherCompletionEvent.WaitOne();
            });
            completion.Start();

            return completion;
        }

        public List<ProcessedImageContracts> ReturnProcessedImages()
        {
            List<ProcessedImageContracts> processed_images = new List<ProcessedImageContracts>();

            // converting ProcessedImageServer to ProcessedImageContracts to send it back to client
            if (!(ProcessedImageCollection is null))
                foreach(var pr_img in ProcessedImageCollection)
                {
                    string img_base64 = Convert.ToBase64String(pr_img.ByteImage);
                    processed_images.Add(new ProcessedImageContracts(pr_img.Name, pr_img.Label, img_base64));
                }    

            return processed_images;
        }

        public async Task<List<ProcessedImageContracts>> GetAllImagesForGivenClassFromDB(int class_num)
        {
            AutoResetEvent ev = new AutoResetEvent(false);
            List<ProcessedImageContracts> res = new List<ProcessedImageContracts>();

            _dispatcher.Post(() =>
            {
                Task get_class_images_task = new Task(() =>
                {
                    Func<ProcessedImageDB, bool> func = a => (a.ImageLabel == class_num);
                    var query = _currDatabaseContext.ProcessedImages.Include(a => a.AdditionalInfo).Where(func);

                    foreach(var el in query)
                    {
                        string img_base64 = Convert.ToBase64String(el.AdditionalInfo.First().ByteImage);
                        ProcessedImageContracts processed_img = new ProcessedImageContracts(el.ImageName,
                                                                                            el.ImageLabel,
                                                                                            img_base64);
                        res.Add(processed_img);
                    }
                    ev.Set();
                });
                get_class_images_task.Start();

                return get_class_images_task;
            });

            Task wait_completion = new Task(() => ev.WaitOne());
            wait_completion.Start();

            await wait_completion;

            return res;
        }

        public void ProcessOrRetrieveImagesFromDB(List<NewImageContracts> new_images)
        {
            ProcessedImageCollection = new List<ProcessedImageServer>();
            IsDatabaseBusy = true;
            _newImagesAmount = new_images.Count;
            _processedImagesAmount = 0;
            _alreadyTakenAmountOfProcessedImages = 0;

            // covnerting NewImageContracts to NewImageServer to use in Server <-> Database communication
            NewImageCollection = new List<NewImageServer>();
            foreach (var new_img in new_images)
                NewImageCollection.Add(new NewImageServer(new_img.Name, 
                                                          Convert.FromBase64String(new_img.IncodedImageBase64)));

            _ = ProcessNewImagesWithDataBaseUsage();

            // wait when all images will be added to ProcessedImageCollection
            // await DispatcherCompleted();
            _dispatcherCompletionEvent.Reset();
        }

        ActionBlock<Func<Task>> CreateLocalDispatcher() 
        {
            ActionBlock<Func<Task>> dispatcher_block = new ActionBlock<Func<Task>>(
                async (Func<Task> method) => await method()
            );

            return dispatcher_block;
        }

        ActionBlock<ProcessedImageServer> CreateDatabaseMailbox()
        {
            ActionBlock<ProcessedImageServer> add_block = new ActionBlock<ProcessedImageServer>(
            (ProcessedImageServer labeledImage) => 
            {
                //creating ProcessedImage object to store in Data Base
                ProcessedImageDB processed_image = new ProcessedImageDB();
                processed_image.ImageLabel = labeledImage.Label;
                processed_image.ImageName = labeledImage.Name;
                processed_image.AdditionalInfo = new List<ImageDetailDB>();

                //creating ImageDetails object to store in Data Base
                ImageDetailDB details = new ImageDetailDB();
                details.ByteImage = labeledImage.ByteImage;
                details.PrimaryInfo = new List<ProcessedImageDB>();

                //adding links to each other
                details.PrimaryInfo.Add(processed_image);
                processed_image.AdditionalInfo.Add(details);

                _currDatabaseContext.Add(processed_image);
                _currDatabaseContext.Add(details);
                _currDatabaseContext.SaveChanges();

                // if (ProcessedImagesAmount == _totalAmountOfImagesInDirectory)
                //     IsDatabaseBusy = false;
            });

            return add_block;
        }
    }
}