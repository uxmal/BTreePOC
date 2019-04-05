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
        private const int InternalNodeChildren = 4;
        private const int LeafNodeChildren = 3;

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

        public BTreeDictionary(IComparer<TKey> cmp)
        {
            this.cmp = cmp ?? throw new ArgumentNullException(nameof(cmp));
            this.count = 0;
            this.version = 0;
        }

        private abstract class Node
        {
            public int count;
            public InternalNode parent;
            public TKey[] keys;

            public abstract (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public abstract TValue Get(TKey key, BTreeDictionary<TKey, TValue> tree);

            public abstract (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree);

            public override string ToString()
            {
                return $"{GetType().Name}: {count} items";
            }
        }

        private class LeafNode : Node 
        {
            public LeafNode nextLeaf;
            public TValue[] values;

            public LeafNode(InternalNode parent)
            {
                this.parent = parent;
                this.keys = new TKey[LeafNodeChildren];
                this.values = new TValue[LeafNodeChildren];
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
                var right = new LeafNode(parent);
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

        private InternalNode NewInternalRoot(Node left, Node right)
        {
            var intern = new InternalNode(null);
            intern.count = 2;
            intern.keys[0] = left.keys[0];
            intern.keys[1] = right.keys[0];
            intern.nodes[0] = left;
            intern.nodes[1] = right;
            left.parent = intern;
            right.parent = intern;
            return intern;
        }

        private class InternalNode : Node
        {
            public Node[] nodes;
            public int totalCount;

            public InternalNode(InternalNode parent)
            {
                this.parent = parent;
                this.keys = new TKey[InternalNodeChildren];
                this.nodes = new Node[InternalNodeChildren];
            }

            public override (Node, Node) Add(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                int idx = Array.BinarySearch(keys, 0, count, key, tree.cmp);
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
                int idx = Array.BinarySearch(keys, 0, count - 1, key, tree.cmp);
                if (idx >= 0)
                    return nodes[idx].Get(key, tree);
                else
                    return nodes[~idx].Get(key, tree);
            }

            public override (Node, Node) Set(TKey key, TValue value, BTreeDictionary<TKey, TValue> tree)
            {
                throw new NotImplementedException();
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
                    Array.Copy(keys, idx, keys, idx, count - idx);
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
                var right = new InternalNode(parent);
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
                root.Set(key, value, this);
                ++version;
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
            var (left, right) = root.Add(key, value, this);
            if (right != null)
                root = NewInternalRoot(left, right);
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
            root = new LeafNode(null);
        }

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
    }
}
