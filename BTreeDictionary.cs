using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BTreePOC
{
    public class BTreeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private Node root;
        private IComparer<TKey> cmp;
        private int version;
        private int InternalNodeChildren;
        private int LeafNodeChildren;
        private readonly BTreeKeyCollection keyCollection;

        public BTreeDictionary()
        {
            this.cmp = Comparer<TKey>.Default;
            this.Count = 0;
            this.version = 0;
            this.InternalNodeChildren = 16;
            this.LeafNodeChildren = InternalNodeChildren - 1;
            this.keyCollection = new BTreeKeyCollection(this);
        }

        public BTreeDictionary(IComparer<TKey> cmp)
        {
            this.cmp = cmp ?? throw new ArgumentNullException(nameof(cmp));
            this.Count = 0;
            this.version = 0;
        }

        private abstract class Node
        {
            public int count;       // # of direct children
            public TKey[] keys;

            public abstract (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public abstract TValue Get(TKey key, BTreeDictionary<TKey, TValue> tree);

            public abstract (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public override string ToString()
            {
                return $"{GetType().Name}: {count} items; keys: {string.Join(",",keys)}.";
            }
        }

        private class LeafNode : Node 
        {
            public LeafNode nextLeaf;
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

            public override TValue Get(TKey key, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
                if (idx < 0)
                    throw new KeyNotFoundException();
                return values[idx];
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

            private (Node, Node) Insert(int idx, TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                ++tree.Count;
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
                return (this, null);
            }

            /// <summary>
            /// Split this node by creating a new "right" node and push
            /// </summary>
            /// <param name="key"></param>
            /// <param name="value"></param>
            /// <param name="tree"></param>
            /// <returns></returns>
            private Node SplitAndInsert(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                var iSplit = (count + 1) / 2;
                var right = new LeafNode(tree.LeafNodeChildren);
                right.count = count - iSplit;
                this.count = iSplit;

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

        private class InternalNode : Node
        {
            public Node[] nodes;
            public int totalCount;

            public InternalNode(int children)
            {
                this.keys = new TKey[children];
                this.nodes = new Node[children];
            }

            public override (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 1, count-1, key, tree.cmp);
                if (idx >= 0)
                    throw new ArgumentException("Duplicate key.");
                int iPos = (~idx) - 1;
                var subnode = nodes[iPos];
                var (leftNode, rightNode) = subnode.Add(key, value, tree);
                if (rightNode == null)
                    return (leftNode, null);
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

            public override TValue Get(TKey key, BTreeDictionary<TKey, TValue> tree)
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
                    return (leftNode, null);
                return Insert(iPos + 1, rightNode.keys[0], rightNode, tree);
            }

            internal (Node, Node) Insert(int idx, TKey key, Node node, BTreeDictionary<TKey, TValue> tree)
            {
                if (count == keys.Length)
                {
                    var newRight = SplitAndInsert(key, node, tree);
                    return (this, newRight);
                }
                else if (idx < count)
                {
                    Array.Copy(keys, idx, keys, idx + 1, count - idx);
                    Array.Copy(nodes, idx, nodes, idx + 1, count - idx);
                }
                keys[idx] = key;
                nodes[idx] = node;
                ++count;
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
                    right.Add(key, node, tree);
                else
                    this.Add(key, node, tree);
                return right;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                if (root == null)
                    throw new KeyNotFoundException();
                return root.Get(key, this);
            }

            set
            {
                EnsureRoot();
                var (left, right) = root.Set(key, value, this);
                if (right != null)
                    root = NewInternalRoot(left, right);
                ++version;

                ++version;
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

        public BTreeKeyCollection Keys => keyCollection;

        public ICollection<TValue> Values => throw new NotImplementedException();

        public int Count { get; private set; }

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            EnsureRoot();
            var (left, right) = root.Add(key, value, this);
            if (right != null)
                root = NewInternalRoot(left, right);
            ++version;
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
            root = new LeafNode(LeafNodeChildren);
        }

        [Conditional("DEBUG")]
        public void Dump()
        {
            if (root == null)
                Debug.Print("(empty)");
            Dump(root, 0);
        }

        private TKey GetKey(int index)
        {
            throw new NotImplementedException();
        }

        private InternalNode NewInternalRoot(Node left, Node right)
        {
            var intern = new InternalNode(InternalNodeChildren);
            intern.count = 2;
            intern.keys[0] = left.keys[0];
            intern.keys[1] = right.keys[0];
            intern.nodes[0] = left;
            intern.nodes[1] = right;
            return intern;
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
                        Debug.Print("{0}{1}:", prefix, inode.keys[i]);
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

        public class BTreeKeyCollection : ICollection<TKey>
        {
            private BTreeDictionary<TKey, TValue> btree;

            public BTreeKeyCollection(BTreeDictionary<TKey, TValue> btree)
            {
                this.btree = btree;
            }

            public int Count => btree.Count;

            public bool IsReadOnly => true;

            public TKey this[int index] => btree.GetKey(index);
            
            public void Add(TKey item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(TKey item) => btree.ContainsKey(item);

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                throw new NotSupportedException();
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                return btree.Select(kv => kv.Key).GetEnumerator();
            }

            public bool Remove(TKey item)
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
