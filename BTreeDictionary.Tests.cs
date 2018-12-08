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

        [Test]
        public void BTree_Get()
        {
            var btree = new BTreeDictionary<string, int>();
            btree.Add("3", 3);
            Assert.AreEqual(3, btree["3"]);
        }

        [Test]
        public void BTree_EnumeratorThrowIfMutated()
        {
            var btree = new BTreeDictionary<string, int>();
            btree.Add("3", 3);
            var e = btree.GetEnumerator();
            Assert.True(e.MoveNext());
            btree.Add("2", 2);
            try
            {
                e.MoveNext();
                Assert.Fail("Should have thrown exception");
            } catch (InvalidOperationException)
            {
            }
        }

        [Test]
        public void BTree_SetNonExisting()
        {
            var btree = new BTreeDictionary<string, int>();
            btree["3"] = 3;
            Assert.AreEqual(3, btree["3"]);
        }

        [Test]
        public void BTree_SetExisting()
        {
            var btree = new BTreeDictionary<string, int>();
            btree["3"] = 3;
            btree["3"] = 2;
            Assert.AreEqual(2, btree["3"]);
        }
    }
}
