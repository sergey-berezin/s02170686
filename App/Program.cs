using System;
using System.IO;
using System.Threading;
using NNLib;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            NNP nnp = new NNP();
            nnp.CreateThreadToProcessDirectory(args[0]);
            
            //if u want to terminate processing thread delete comment below
            //nnp.TerminateProcessing();
        }
    }
}
