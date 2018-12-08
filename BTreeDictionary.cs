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
        private int version;

        public BTreeDictionary()
        {
            this.cmp = Comparer<TKey>.Default;
            this.count = 0;
            this.version = 0;
        }

        private abstract class Node
        {
            public int count;
            public InternalNode parent;

            public abstract void Add(TKey key, TValue value, IComparer<TKey> cmp);

            public abstract TValue Get(TKey key, IComparer<TKey> cmp);
        }

        private class LeafNode : Node 
        {
            public TKey[] keys;
            public TValue[] values;
            public LeafNode nextLeaf;

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

            public override TValue Get(TKey key, IComparer<TKey> cmp)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, cmp);
                if (idx < 0)
                    throw new KeyNotFoundException();
                return values[idx];
            }

            private void Insert(int idx, TKey key, TValue value)
            {
                if (count == keys.Length)
                    throw new NotImplementedException("Split this node");
                if (idx < count)
                {
                    Array.Copy(keys, idx, keys, idx + 1, count - idx);
                    Array.Copy(values, idx, values, idx + 1, count - idx);
                }
                keys[idx] = key;
                values[idx] = value;
                ++count;
            }
        }

        private class InternalNode : Node
        {
            public TKey[] Keys;
            public Node[] nodes;
            public int totalCount;

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

            public override TValue Get(TKey key, IComparer<TKey> cmp)
            {
                throw new NotImplementedException();
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                if (root == null)
                    throw new KeyNotFoundException();
                return root.Get(key, cmp);
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
            ++version;
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
            if (root == null)
                yield break;
            Node node;
            for (node = root; node is InternalNode intern; node = intern.nodes[0])
                ;
            var leaf = (LeafNode)node;
            int myVersion = this.version;
            while (leaf != null)
            {
                for (int i = 0; i < leaf.count; ++i)
                {
                    if (myVersion != this.version)
                        throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                    yield return new KeyValuePair<TKey, TValue>(leaf.keys[i], leaf.values[i]);
                }
                leaf = leaf.nextLeaf;
            }
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
            return GetEnumerator();
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;
            root = new LeafNode(null);
        }
    }
}
