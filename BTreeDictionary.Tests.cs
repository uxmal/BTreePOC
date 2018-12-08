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

        [Test]
        public void BTree_AddItem()
        {
            var btree = new BTreeDictionary<string, int>();
            btree.Add("3", 3);
            Assert.AreEqual(1, btree.Count);
        }
    }
}
