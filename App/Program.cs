using System;
using System.IO;
using System.Threading;
using NNLib;

namespace App
{
    class Program
    {
        public void ProcessResults(LabeledImage image)
        {
            Console.WriteLine(image.ToString());
        }

        static void Main(string[] args)
        {
            NNP nnp = new NNP("/Users/macbookpro/autumn_prac/s02170686/mnist-8.onnx",
                              (new Program()).ProcessResults);
            nnp.CreateThreadToProcessDirectory(args[0]);
            
            //if u want to terminate processing thread delete comment below
            //nnp.TerminateProcessing();
        }
    }
}
