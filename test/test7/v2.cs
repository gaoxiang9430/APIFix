namespace Test{

    public class Utils{
        public static void LogTest(string message){
            using (var writer = new System.IO.StreamWriter(System.Console.OpenStandardOutput()))
                writer.WriteLine(message);
        }

        public static double ContentDistance(string content1, string content2) {
            diff_match_patch dmp = new diff_match_patch();
            List<Diff> diff = dmp.diff_main(content1, content2);
            
            return (double)dmp.diff_levenshtein(diff) / System.Math.Max(content1.Count(), content2.Count());
        }
        public class Test{

        }
    }
}
