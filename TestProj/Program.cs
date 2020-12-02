using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestProj
{
    class Program
    {
        public async Task M1()
        {
            await Task.Delay(10000);
            Console.WriteLine(Thread.CurrentThread);
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine(Thread.CurrentThread);
            await new Program().M1();
            Console.WriteLine("here");
            Console.ReadLine();
        }
    }
}
