using System;
using System.IO;

namespace playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            Console.WriteLine($"working in {currentDirectory}");
        }
    }
}
