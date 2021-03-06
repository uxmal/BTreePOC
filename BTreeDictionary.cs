﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BTreePOC
{
    /// <summary>
    /// Represents a collection of key/value pairs that are sorted by the keys
    /// and are accessible by key and by index. It's intended to be a drop-in
    /// replacement for <see cref="System.Collections.Generic.SortedList{TKey, TValue}"/>.
    /// </summary>
    /// <remarks>
    /// This class implemements most of the same API as <see cref="System.Collections.Generic.SortedList{TKey, TValue}"/>
    /// but with much better performance. Where n random insertions into a 
    /// SortedList have a complexity of O(n^2), the BtreeDictionary is 
    /// organized as a B+tree, and this has a complexity of O(n log n). 
    /// In addition, benchmark measurements show that BTreeDictionary is
    /// about twice as fast as SortedDictionary (which also is O(n log n))
    /// and in addition provides the IndexOf functionality from SortedList
    /// that SortedDictionary lacks.
    /// </remarks>
    public class BTreeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private Node root;
        private readonly IComparer<TKey> cmp;
        private int version;
        private int InternalNodeChildren;
        private int LeafNodeChildren;
        private readonly KeyCollection keyCollection;
        private readonly ValueCollection valueCollection;

        public BTreeDictionary() :
            this(Comparer<TKey>.Default) {
        }

        public BTreeDictionary(IComparer<TKey> cmp)
        {
            this.cmp = cmp ?? throw new ArgumentNullException(nameof(cmp));
            this.version = 0;
            this.InternalNodeChildren = 16;
            this.LeafNodeChildren = InternalNodeChildren - 1;
            this.keyCollection = new KeyCollection(this);
            this.valueCollection = new ValueCollection(this);
        }

        public BTreeDictionary(IDictionary<TKey,TValue> entries) :
            this()
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            Populate(entries);
        }

        public BTreeDictionary(IDictionary<TKey,TValue> entries, IComparer<TKey> comparer) :
            this(comparer)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            Populate(entries);
        }

        private abstract class Node
        {
            public int count;       // # of direct children
            public int totalCount;  // # of recursively reachable children.
            public TKey[] keys;

            public abstract (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public abstract (TValue, bool) Get(TKey key, BTreeDictionary<TKey, TValue> tree);

            public abstract (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public abstract bool Remove(TKey key, BTreeDictionary<TKey, TValue> tree);

            public override string ToString()
            {
                return $"{GetType().Name}: {count} items; keys: {string.Join(",",keys)}.";
            }
        }

        /// <summary>
        /// In a B+Tree, the values are held in the leaf nodes of the data structure.
        /// </summary>
        private class LeafNode : Node 
        {
            public LeafNode nextLeaf;   // leaves are threaded together for ease of enumeration.
            public TValue[] values;

            public LeafNode(int children)
            {
                this.keys = new TKey[children];
                this.values = new TValue[children];
            }

            public override (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
                if (idx >= 0)
                    throw new ArgumentException("Duplicate key.");
                return Insert(~idx, key, value, tree);
            }

            public override (TValue, bool) Get(TKey key, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
                if (idx < 0)
                    return (default(TValue), false);
                return (values[idx], true);
            }

            public override (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
                if (idx >= 0)
                {
                    values[idx] = value;
                    return (this, null);
                }
                return Insert(~idx, key, value, tree);
            }

            public override bool Remove(TKey key, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
                if (idx >= 0)
                {
                    --count;
                    --totalCount;
                    Array.Copy(keys, idx + 1, keys, idx, count - idx);
                    Array.Copy(values, idx + 1, values, idx, count - idx);
                    keys[count] = default(TKey);
                    values[count] = default(TValue);
                    return true;
                }
                return false;
            }

            private (Node, Node) Insert(int idx, TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                if (count == keys.Length)
                {
                    var newRight = SplitAndInsert(key, value, tree);
                    return (this, newRight);
                }
                else if (idx < count)
                {
                    // Make a hole
                    Array.Copy(keys, idx, keys, idx + 1, count - idx);
                    Array.Copy(values, idx, values, idx + 1, count - idx);
                }
                keys[idx] = key;
                values[idx] = value;
                ++this.count;
                ++this.totalCount;
                return (this, null);
            }

            /// <summary>
            /// Splits this node into subnodes by creating a new "right" node
            /// and adds the (key,value) to the appropriate subnode.
            /// </summary>
            private Node SplitAndInsert(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                var iSplit = (count + 1) / 2;
                var right = new LeafNode(tree.LeafNodeChildren);
                right.count = count - iSplit;
                this.count = iSplit;
                right.totalCount = right.count;
                this.totalCount = this.count;
                Array.Copy(this.keys, iSplit, right.keys, 0, right.count);
                Array.Clear(this.keys, iSplit, right.count);
                Array.Copy(this.values, iSplit, right.values, 0, right.count);
                Array.Clear(this.values, iSplit, right.count);
                right.nextLeaf = this.nextLeaf;
                this.nextLeaf = right;
                if (tree.cmp.Compare(right.keys[0], key) < 0)
                    right.Add(key, value, tree);
                else
                    this.Add(key, value, tree);
                return right;
            }
        }

        /// <summary>
        /// In a B+tree, the internals node only contain links to other nodes.
        /// </summary>
        private class InternalNode : Node
        {
            public Node[] nodes;

            public InternalNode(int children)
            {
                this.keys = new TKey[children];
                this.nodes = new Node[children];
            }

            public override (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count-1, key, tree.cmp);
                int iPos;
                if (idx >= 0)
                    iPos = idx - 1;
                else
                    iPos = (~idx) - 1;
                var subnode = nodes[iPos];
                var (leftNode, rightNode) = subnode.Add(key, value, tree);
                if (rightNode == null)
                {
                    this.totalCount = SumNodeCounts(this.nodes, this.count);
                    return (leftNode, null);
                }
                return Insert(iPos + 1, rightNode.keys[0], rightNode,  tree);
            }

            public Node Add(TKey key, Node node, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count-1, key, tree.cmp);
                if (idx >= 0)
                    throw new ArgumentException("Duplicate key.");
                var subnode = nodes[~idx];
                return Insert(~idx, node.keys[0], node, tree).Item1;
            }

            public override (TValue, bool) Get(TKey key, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count - 1, key, tree.cmp);
                if (idx >= 0)
                    return nodes[idx].Get(key, tree);
                else
                {
                    var iPos = (~idx) - 1;
                    return nodes[iPos].Get(key, tree);
                }
            }

            public override (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count - 1, key, tree.cmp);
                int iPos = (idx >= 0)
                    ? idx
                    : (~idx) - 1;
                var subnode = nodes[iPos];
                var (leftNode, rightNode) = subnode.Set(key, value, tree);
                if (rightNode == null)
                {
                    this.totalCount = SumNodeCounts(this.nodes, this.count);
                    tree.Validate(this);
                    return (leftNode, null);
                }
                else
                {
                    return Insert(iPos + 1, rightNode.keys[0], rightNode, tree);
                }
            }

            public override bool Remove(TKey key, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count - 1, key, tree.cmp);
                bool removed;
                if (idx >= 0)
                {
                    removed = nodes[idx].Remove(key, tree);
                }
                else
                {
                    var iPos = (~idx) - 1;
                    removed = nodes[iPos].Remove(key, tree);
                }
                --this.totalCount;
                return removed;
            }

            private (Node, Node) Insert(int idx, TKey key, Node node, BTreeDictionary<TKey, TValue> tree)
            {
                if (count == keys.Length)
                {
                    var newRight = SplitAndInsert(key, node, tree);
                    return (this, newRight);
                }
                if (idx < count)
                {
                    Array.Copy(keys, idx, keys, idx + 1, count - idx);
                    Array.Copy(nodes, idx, nodes, idx + 1, count - idx);
                }
                keys[idx] = key;
                nodes[idx] = node;
                ++this.count;
                this.totalCount = SumNodeCounts(this.nodes, this.count);
                return (this, null);
            }

            private Node SplitAndInsert(TKey key, Node node, BTreeDictionary<TKey, TValue> tree)
            {
                var iSplit = (count + 1) / 2;
                var right = new InternalNode(tree.InternalNodeChildren);
                right.count = count - iSplit;
                this.count = iSplit;
                Array.Copy(this.keys, iSplit, right.keys, 0, right.count);
                Array.Clear(this.keys, iSplit, right.count);
                Array.Copy(this.nodes, iSplit, right.nodes, 0, right.count);
                Array.Clear(this.nodes, iSplit, right.count);
                if (tree.cmp.Compare(right.keys[0], key) < 0)
                {
                    right.Add(key, node, tree);
                    this.totalCount = SumNodeCounts(this.nodes, this.count);
                }
                else
                {
                    this.Add(key, node, tree);
                    right.totalCount = SumNodeCounts(right.nodes, right.count);
                }
                return right;
            }

            private static int SumNodeCounts(Node[] nodes, int count)
            {
                int n = 0;
                for (int i = 0; i < count; ++i)
                    n += nodes[i].totalCount;
                return n;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                if (root == null)
                    throw new KeyNotFoundException();
                var (value, found) = root.Get(key, this);
                if (!found)
                    throw new KeyNotFoundException();
                return value;
            }

            set
            {
                EnsureRoot();
                var (left, right) = root.Set(key, value, this);
                if (right != null)
                    root = NewInternalRoot(left, right);
                ++version;
                // Validate(root);
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        public KeyCollection Keys => keyCollection;

        public ValueCollection Values => valueCollection;

        public int Count => root != null ? root.totalCount : 0;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            EnsureRoot();
            var (left, right) = root.Add(key, value, this);
            if (right != null)
                root = NewInternalRoot(left, right);
            ++version;
            // Validate(root);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            ++version;
            root = null;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (root == null)
                return false;
            var (value, found) = root.Get(item.Key, this);
            return found && object.Equals(item.Value, value);
        }

        public bool ContainsKey(TKey key)
        {
            if (root == null)
                return false;
            var (value, found) = root.Get(key, this);
            return found;
        }

        public bool ContainsValue(TValue value)
        {
            return this.Any(e => e.Value.Equals(value));
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (this.Count > array.Length - arrayIndex) throw new ArgumentException();
            int iDst = arrayIndex;
            foreach (var item in this)
            {
                array[iDst++] = item;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (root == null)
                yield break;
            // Get the leftmost leaf node.
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

        /// <summary>
        /// Determine the 0-based index of <paramref name="key"/>.
        /// </summary>
        /// <returns>
        /// A non-negative number if the key is found. If the key is 
        /// not found, returns the one's complement of the index the key would
        /// have had if it were present in the collection.
        /// </returns>
        public int IndexOfKey(TKey key)
        {
            if (root == null)
                return ~0;
            int totalBefore = 0;
            Node node = root;
            int i;
            while (node is InternalNode intern)
            {
                for (i = 1; i < intern.count; ++i)
                {
                    int c = cmp.Compare(intern.keys[i], key);
                    if (c <= 0)
                    {
                        totalBefore += intern.nodes[i - 1].totalCount;
                    }
                    else
                    {
                        node = intern.nodes[i - 1];
                        break;
                    }
                }
                if (i == intern.count)
                {
                    // Key was larger than all nodes.
                    node = intern.nodes[i - 1];
                }
            }
            // Should have reached a leaf node.
            var leaf = (LeafNode)node;
            for (i = 0; i < leaf.count; ++i)
            {
                var c = cmp.Compare(leaf.keys[i], key);
                if (c == 0)
                    return totalBefore + i;
                if (c > 0)
                    break;
            }
            return ~(totalBefore + i);
        }

        public bool Remove(TKey key)
        {
            if (root == null)
                return false;
            return root.Remove(key, this);
        }

        bool ICollection<KeyValuePair<TKey,TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (root == null)
            {
                value = default(TValue);
                return false;
            }
            bool found;
            (value, found) = root.Get(key, this);
            return found;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;
            root = new LeafNode(LeafNodeChildren);
        }

        private KeyValuePair<TKey,TValue> GetEntry(int index)
        {
            if (0 <= index && index < this.Count)
            {
                Node node = root;
                int itemsLeft = index;
                while (node is InternalNode intern)
                {
                    for (int i = 0; i < intern.count; ++i)
                    {
                        var subNode = intern.nodes[i];
                        if (itemsLeft < subNode.totalCount)
                        {
                            node = subNode;
                            break;
                        }
                        itemsLeft -= subNode.totalCount;
                    }
                }
                var leaf = (LeafNode)node;
                return new KeyValuePair<TKey, TValue>(leaf.keys[itemsLeft], leaf.values[itemsLeft]);
            }
            else
                throw new ArgumentOutOfRangeException("Index was out of range. Must be non-negative and less than the size of the collection.");

        }

        private InternalNode NewInternalRoot(Node left, Node right)
        {
            var intern = new InternalNode(InternalNodeChildren);
            intern.count = 2;
            intern.totalCount = left.totalCount + right.totalCount;
            intern.keys[0] = left.keys[0];
            intern.keys[1] = right.keys[0];
            intern.nodes[0] = left;
            intern.nodes[1] = right;
            return intern;
        }

        private void Populate(IDictionary<TKey, TValue> entries)
        {
            foreach (var entry in entries)
            {
                Add(entry.Key, entry.Value);
            }
        }

        #region Debugging code 

        [Conditional("DEBUG")]
        public void Dump()
        {
            if (root == null)
                Debug.Print("(empty)");
            Dump(root, 0);
        }

        [Conditional("DEBUG")]
        private void Dump(Node n, int depth)
        {
            var prefix = new string(' ', depth);
            switch (n)
            {
            case InternalNode inode:
                for (int i = 0; i < inode.count; ++i)
                {
                    Debug.Print("{0}{1}: total nodes: {2}", prefix, inode.keys[i], inode.nodes[i].totalCount);
                    Dump(inode.nodes[i], depth + 4);
                }
                break;
            case LeafNode leaf:
                for (int i = 0; i < leaf.count; ++i)
                {
                    Debug.Print("{0}{1}: {2}", prefix, leaf.keys[i], leaf.values[i]);
                }
                break;
            default:
                Debug.Print("{0}huh?", prefix);
                break;
            }
        }

        [Conditional("DEBUG")]
        private void Validate(Node node)
        {
            if (node is LeafNode leaf)
            {
                if (leaf.totalCount != leaf.count)
                    throw new InvalidOperationException($"Leaf node {leaf} has mismatched counts.");
            }
            else if (node is InternalNode intern)
            {
                int sum = 0;
                for (int i = 0; i < intern.count; ++i)
                {
                    Validate(intern.nodes[i]);
                    sum += intern.nodes[i].totalCount;
                }
                if (sum != intern.totalCount)
                {
                    Dump();
                    Console.WriteLine("# of nodes: {0}", this.Count);
                    throw new InvalidOperationException($"Internal node {intern} has mismatched counts; expected {sum} but had {intern.totalCount}.");
                }
            }
        }
        #endregion

        public abstract class Collection<T> : ICollection<T>
        {
            protected readonly BTreeDictionary<TKey, TValue> btree;

            protected Collection(BTreeDictionary<TKey, TValue> btree)
            {
                this.btree = btree;
            }

            public int Count => btree.Count;

            public bool IsReadOnly => true;

            public abstract T this[int index] { get; }
            
            public void Add(T item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public abstract bool Contains(T item);

            public abstract void CopyTo(T[] array, int arrayIndex);

            public abstract IEnumerator<T> GetEnumerator();

            public bool Remove(T item)
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public class KeyCollection : Collection<TKey>
        {
            internal KeyCollection(BTreeDictionary<TKey, TValue> btree) : 
                base(btree)
            {
            }

            public override TKey this[int index] => btree.GetEntry(index).Key;

            public override bool Contains(TKey item) => btree.ContainsKey(item);

            public override void CopyTo(TKey[] array, int arrayIndex)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                if (btree.Count > array.Length - arrayIndex) throw new ArgumentException();
                var iDst = arrayIndex;
                foreach (var item in btree)
                {
                    array[iDst++] = item.Key;
                }
            }

            public int IndexOf(TKey item) => btree.IndexOfKey(item);

            public override IEnumerator<TKey> GetEnumerator() => btree.Select(e => e.Key).GetEnumerator();
        }

        public class ValueCollection : Collection<TValue>
        {
            internal ValueCollection(BTreeDictionary<TKey, TValue> btree) :
                base(btree)
            {
            }

            public override TValue this[int index] => btree.GetEntry(index).Value;

            public override bool Contains(TValue item) => btree.ContainsValue(item);

            public override void CopyTo(TValue[] array, int arrayIndex)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                if (btree.Count > array.Length - arrayIndex) throw new ArgumentException();
                var iDst = arrayIndex;
                foreach (var item in btree)
                {
                    array[iDst] = item.Value;
                }
            }

            public override IEnumerator<TValue> GetEnumerator() => btree.Select(e => e.Value).GetEnumerator();
        }
    }
}
