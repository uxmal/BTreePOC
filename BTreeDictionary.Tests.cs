using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BTreePOC
{
    public class BTreeDictionaryTests
    {
        [Test]
        public void BTree_Create()
        {
            var btree = new BTreeDictionary<string, int>();
        }
    }
}
