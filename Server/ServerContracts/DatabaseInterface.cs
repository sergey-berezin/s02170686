using System;
using System.Collections.Generic;
using ServerContracts;
using System.Threading.Tasks;

namespace ServerContracts.DatabaseInterface
{
    public interface IDatabaseEF
    {
        Task<string> GetStatistics();
        void ProcessOrRetrieveImagesFromDB(List<NewImageContracts> new_images);
        Task CleanDatabase();
        Task<List<ProcessedImageContracts>> ReturnAlreadyProcessedImages();
        void InterruptCurrentImageProcessing();
        bool IsCurrentlyProcessingImages();
        List<ProcessedImageContracts> ReturnProcessedImages();
    }
}