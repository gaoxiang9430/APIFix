using Microsoft.ProgramSynthesis.Wrangling.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Synthesizer
{
    class Global
    {
        public static string benchmarkPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "benchmark");

        public static bool verbose = true;

        public static void Log(string log) {
            if (verbose)
                Console.WriteLine("Log: " + log);
        }

        public static int NumOldUsage = 0;
        public static int NumNewUsage = 0;
    }

    class Config {
        public static bool UseAdditionalOutput = false;
        public static bool UseAdditionalInput = false;
        public static bool OnlyNewUsage = false;
        public static int GivenExample = 1;

        public static bool UseTypedUsage = false;

        public static String NewKeyWords = null;
        public static String OldKeyWords = null;

        public static double OldUsageThreashold = 0.05; //0.15;
        public static double NewUsageThreashold = 0.15; //0.25;

        public static bool Validate = true;

        public static void PrintConfig(){
            Console.WriteLine("Configuration: ");
            Console.WriteLine("---- UseAdditionalOutput: " + UseAdditionalOutput);
            Console.WriteLine("---- UseAdditionalInput : " + UseAdditionalInput);
            Console.WriteLine("---- OnlyNewUsage       : " + OnlyNewUsage);
            Console.WriteLine("---- GivenExample       : " + GivenExample);
            Console.WriteLine("---- UseTypedUsage      : " + UseTypedUsage);
            Console.WriteLine("---- NewKeyWords        : " + NewKeyWords);
            Console.WriteLine("---- OldKeyWords        : " + OldKeyWords);
            Console.WriteLine("---- OldUsageThreashold : " + OldUsageThreashold);
            Console.WriteLine("---- NewUsageThreashold : " + NewUsageThreashold);
            Console.WriteLine("---- Validate           : " + Validate);
        }
    }

    public class Record<T1, T2>
    {

        public T1 Item1;
        public T2 Item2;
        public Record(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }

    public class Utils {
        public static bool IsGenericType(Node node) {
            foreach (var child in node.Children) {
                if (child.Label.Equals("TypeArgumentList") && child.Children.Length!=0)
                    return true;
            }
            return false;
        }
    }
}
