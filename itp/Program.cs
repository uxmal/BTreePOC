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
                RunTrial(SetItemsBackward, items, () => new BTreeDictionary<string, int>(), "btree");
                RunTrial(SetItemsBackward, items, () => new SortedList<string, int>(), "SortedList");
                RunTrial(SetItemsBackward, items, () => new SortedDictionary<string, int>(), "SortedDictionary");
                Console.WriteLine();
                items *= 2;
            }
        }

        private static void SetItemsBackward(IDictionary<string,int> dict, int items)
        {
            foreach (var i in Enumerable.Range(0, items))
            {
                var ii = items - (i + 1);
                dict[ii.ToString()] = ii;
            }
        }

        private static double RunTrial(Action<IDictionary<string,int>, int> kernel, int items, Func<IDictionary<string,int>> mkdict, string caption)
        {
            const int trials = 1;
            var totalmsec = 0.0;
            for (int j = 0; j < trials; ++j)
            {
                var uStart = DateTime.UtcNow;
                IDictionary<string, int> btree = mkdict();
                kernel(btree, items);
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
