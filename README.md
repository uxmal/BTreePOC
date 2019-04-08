# BTreePOC
This is a C# sorted dictionary, using a B+tree implementation.

## Motivation
The [Reko decompiler](https://github.com/uxmal/reko) uses `SortedList<K,V>` in many places to maintain collections in sorted order. Especially useful is `SortedList`'s `IndexOfKey` method, which is used to compute lower bounds. Lower bounds cannot be computed cheaply using `SortedDictionary<K,V>`. 

However, the implementation of `SortedList` is simply an array; it's basically the embodiment of the [insertion sort](https://en.wikipedia.org/wiki/Insertion_sort) algorithm. If `n` items are added to the end of the `SortedList`, the complexity is `O(n)` only if the items are added in sorted order. If they are added in random order, or even worse, in reverse-sorted order, the complexity explodes to `O(n^2)`.

`SortedDictionary` is implemented as a balanced binary tree. That implementation results in `O(n log n)` complexity. However, `SortedDictionary` lacks a good way to find a lower bound to a given key, i.e. to find the largest item in the collection that is smaller than or equal to the given key.

Spurred by this, I wrote the `BTreeDictionary<K,V>` class, which implements `IDictionary<K,V>`, and additionally an `IndexOfKey` method which either returns the index `i` of an item equal to the key, or the index of the location the key would have been in. In the latter case, the index of the lower bound is `i-1`.

Benchmark measurements of `BTreeDictionary` compared to `SortedList` and `SortedDictionary` show the following:
* The `O(n^2)` behaviour of `SortedList` is confirmed.
* The `O(n log n)` behaviour of `BTreeDictionary` and `SortedDictionary` are confirmed.
* `BTreeDictionary` consistently outperforms `SortedDictionary` by a factor of 2. On my development machine, adding an item to an `BTreeDictionary` takes ~30 us, while adding an item to `SortedDictionary` takes ~60 us.
