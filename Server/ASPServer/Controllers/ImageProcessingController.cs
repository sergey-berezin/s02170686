using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DataBaseEntityFramework;
using ServerContracts;
using ServerContracts.DatabaseInterface;

namespace ASPServer.Controllers
{
    // how concurrency work here???
    // add return codes
    // does he respond when awaited ???
    // await the same thread continue ???
    [ApiController]
    [Route("image_processing_api")]
    public class ImageProcessingController : ControllerBase
    {
        IDatabaseEF _currDatabes;
        List<ProcessedImageContracts> _alreadyProcessedImages;
        int _sendBackProcessedImagesAmount;
        int _imagesNeededToBeProcessedAmount;

        public ImageProcessingController(IDatabaseEF db)
        {
            _currDatabes = db;
        }

        [HttpDelete, Route("clean_database")]
        public async Task DeleteDatabaseInfoRequest()
        {

            Console.WriteLine("Server got Clean Database request.");

            await _currDatabes.CleanDatabase();

            Console.WriteLine("Clean Database request: Done.");
            Console.WriteLine();
        }

        [HttpGet, Route("get_previously_processed_images")]
        public List<ProcessedImageContracts> ReturnPreviouslyProcessedImages()
        {
            Console.WriteLine("Server got Return Previously Processed Images request.");

            var res = _currDatabes.ReturnProcessedImages();

            Console.WriteLine($"Returned {res.Count} processed images.");

            return res;
        }

        [HttpGet, Route("get_all_from_class/{class_num:int}")]
        public async Task<List<ProcessedImageContracts>> GetAllImagesForGivenClass(int class_num)
        {
            Console.WriteLine($"Server got Return All Images For Class {class_num} request.");

            var res = await _currDatabes.GetAllImagesForGivenClassFromDB(class_num);

            Console.WriteLine($"Return All Images For Class {class_num} request: Done.");

            return res;
        }

        [HttpGet, Route("empty")]
        public void EmptyRequest()
        {
            Console.WriteLine("Empty request: Done.");
        }

        [HttpGet, Route("statistics_database")]
        public async Task<string> DatabaseStatisticsRequest()
        {
            Console.WriteLine("Server got Database Statistic request.");

            var res =  await _currDatabes.GetStatistics();

            Console.WriteLine("Database Statistics request: Done.");
            Console.WriteLine();

            return res;
        }

        [HttpGet, Route("is_currently_processing_images")]
        public bool IsCurrentlyProcessingImagesRequest()
        {
            return _currDatabes.IsCurrentlyProcessingImages();
        }

        [HttpGet, Route("terminate_current_processing")]
        public void TerminateCurrentImageProcessingRequest()
        {
            Console.WriteLine("Server got Terminate Current Image Processing request.");

            _currDatabes.InterruptCurrentImageProcessing();

            Console.WriteLine("Terminate Current Image Processing request: Done.");
        }

        [HttpGet, Route("processed_images")]
        public async Task<List<ProcessedImageContracts>> ReturnAlreadyProcessedImagesRequest()
        {
            Console.WriteLine("Server got Give Already Processed Images request.");
            
            var res = await _currDatabes.ReturnAlreadyProcessedImages();

            Console.WriteLine($"Give Already Processed Images request: {res.Count} were(was) returned.");
            Console.WriteLine();

            return res;
        }

        //change
        [HttpPost, Route("process_image_browser")]
        public void ProcessImage([FromBody] NewImageContracts new_image)
        {
            Console.WriteLine("Server got Process Image request.");

            List<NewImageContracts> new_img_as_list = new List<NewImageContracts>();
            new_img_as_list.Add(new_image);

            _currDatabes.ProcessOrRetrieveImagesFromDB(new_img_as_list);
            
            Console.WriteLine("Process Image request: done.");
        }

        [HttpPost, Route("start_image_processing")]
        public void StartProcessingRecievedImages(
            [FromBody] List<NewImageContracts> new_images)
        {
            Console.WriteLine("Server got Image Processing request.");

            _currDatabes.ProcessOrRetrieveImagesFromDB(new_images);

            Console.WriteLine();
        }
    }
}
