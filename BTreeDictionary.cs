using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BTreePOC
{
    public class BTreeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private const int NodesPerLevel = 256;

        private Node root;
        private IComparer<TKey> cmp;
        private int count;

        public BTreeDictionary()
        {
            this.cmp = Comparer<TKey>.Default;
            this.count = 0;
        }

        private abstract class Node
        {
            public int index;
            public int count;
            public InternalNode parent;

            public abstract void Add(TKey key, TValue value, IComparer<TKey> cmp);
        }

        private class LeafNode : Node 
        {
            public TKey[] keys;
            public TValue[] values;

            public LeafNode(InternalNode parent)
            {
                this.parent = parent;
                this.keys = new TKey[NodesPerLevel];
                this.values = new TValue[NodesPerLevel];
            }

            public override void Add(TKey key, TValue value, IComparer<TKey> cmp)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, cmp);
                if (idx >= 0)
                    throw new ArgumentException("Duplicate key.");
                Insert(~idx, key, value);
            }

            private void Insert(int idx, TKey key, TValue value)
            {
                if (count == keys.Length)
                    throw new NotImplementedException("Split this node");
                if (idx < count)
                {
                    Array.Copy(keys, idx, keys, idx + 1, count - index);
                    Array.Copy(values, idx, values, idx + 1, count - index);
                }
                keys[idx] = key;
                values[idx] = value;

            }
        }

        private class InternalNode : Node
        {
            public TKey[] Keys;
            public Node[] nodes;

            public InternalNode(InternalNode parent)
            {
                this.parent = parent;
                this.Keys = new TKey[NodesPerLevel];
                this.nodes = new Node[NodesPerLevel];
            }

            public override void Add(TKey key, TValue value, IComparer<TKey> cmp)
            {
                throw new NotImplementedException();
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int Count => count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            EnsureRoot();
            root.Add(key, value, cmp);
            ++count;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(TKey key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(TKey key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;
            root = new LeafNode(null);
        }
    }
}
