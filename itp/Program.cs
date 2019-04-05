using BTreePOC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace itp
{
    public class Program
    {
        static void Main(string[] args)
        {
            int items = 1_000;
            for (; ; )
            {
                Console.WriteLine("Running tests with {0} items.", items);
                RunTrial(items, () => new BTreeDictionary<string, int>(), "btree");
                RunTrial(items, () => new SortedList<string, int>(), "SortedList");
                RunTrial(items, () => new SortedDictionary<string, int>(), "SortedDictionary");
                Console.WriteLine();
                items *= 2;
            }
        }

        private static double RunTrial(int items, Func<IDictionary<string,int>> mkdict, string caption)
        {
            const int trials = 1;
            var totalmsec = 0.0;
            for (int j = 0; j < trials; ++j)
            {
                var uStart = DateTime.UtcNow;
                IDictionary<string, int> btree = mkdict();
                foreach (var i in Enumerable.Range(0, items))
                {
                    var ii = items - (i + 1);
                    btree.Add(ii.ToString(), ii);
                }
                var msec = (DateTime.UtcNow - uStart).TotalMilliseconds;
                totalmsec += msec;
            }
            var average = totalmsec / trials;
            Console.WriteLine("{0,-16}: time / item: {1:###0.0000} n log n: {2:###0.00000}",
                caption,
                average / items,
                average / (items * Math.Log(items)));
            return totalmsec;
        }
    }
}
