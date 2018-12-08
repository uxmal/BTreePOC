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

        [Test]
        public void BTree_AddTwoItems()
        {
            var btree = new BTreeDictionary<string, int>();
            btree.Add("3", 3);
            btree.Add("2", 2);
            Assert.AreEqual(2, btree.Count);
        }

        [Test]
        public void BTree_Enumerate()
        {
            var btree = new BTreeDictionary<string, int>();
            btree.Add("3", 3);
            btree.Add("2", 2);
            var e = btree.GetEnumerator();
            Assert.IsTrue(e.MoveNext());
            Assert.AreEqual("2", e.Current.Key);
            Assert.AreEqual(2, e.Current.Value);
            Assert.IsTrue(e.MoveNext());
            Assert.AreEqual("3", e.Current.Key);
            Assert.AreEqual(3, e.Current.Value);
            Assert.IsFalse(e.MoveNext());
        }
    }
}
