using System;
using System.Collections;
using System.Collections.Generic;


//LinkedDictionary是参考.net的Dictionary源码结合Java的LinkedMap思想设计的一个数据结构 
//LinkedDictionary内部实现结合循环双向链表和Hash形成一个支持有序的hash数据结构，结构的
//内部除了支持Hash的访问方式，内部的数据存储也支持FIFO和LRU存储方式,结合这两种访问方式
//可以将LinkedDictionary用于缓存结构的设计
//LinkedDictionary与Dictionary的比较
//1，LinkedDictionary支持Dictionary相同的接口，支持数据移除的接口，因此不支持序列化
//2. LinkedDictionary内存存储使用双向链表，保存的单个数据会比Dicitonary空间大一点
public class LinkedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
{
    private struct Entry
    {
        public int hashCode;  // 低31个比特位用于hash code, 如果没有被使用则为-1
        public int next;      // 拉链法中用于指引下一个Entry在entries数组中的索引, 如果是链表的最后一个则为-1
        public TKey key;      // 保存的key
        public TValue value;  // 保存的value
        public int nextEntry; // Entry双链表的下一个Entry在entries数组中的索引，如果指向链表头，则为-1
        public int prevEntry; // Entry双链表的上一个Entry在entries数组中的索引，如果指向链表头，则为-1
    }

    private int[] buckets;
    private Entry[] entries;
    private int count;
    private int version;
    private int freeList;
    private int freeCount;
    private bool reverseEnumerate;
    private IEqualityComparer<TKey> comparer;
    private KeyCollection keys;
    private ValueCollection values;
    [NonSerialized]
    private Object _syncRoot;

    private readonly bool accessRecord;
    private Entry headEntry;
    private ILinkedDictionaryCache<TKey, TValue> cacheHandler;


    public LinkedDictionary() : this(0, null, false, null)
    {
    }

    public LinkedDictionary(bool accessRecord) : this(0, null, accessRecord, null)
    {
    }

    public LinkedDictionary(int capacity) : this(capacity, null, false, null)
    {
    }

    public LinkedDictionary(IEqualityComparer<TKey> comparer) : this(0, comparer, false, null)
    {
    }

    /// <summary>
    /// removeEldestEntry是用于自定义是否删除最旧的数据，可用于缓存的设计
    /// entryRemoved只有在removeEldestEntry有定义并且有返回true的时候才调用
    /// </summary>
    /// <param name="removeEldestEntry"></param>
    /// <param name="entryRemoved"></param>
    public LinkedDictionary(ILinkedDictionaryCache<TKey, TValue> cacheHandler)
        : this(0, null, false, cacheHandler)
    {
    }

    public LinkedDictionary(int capacity, IEqualityComparer<TKey> comparer, bool accessRecord,
        ILinkedDictionaryCache<TKey, TValue> cacheHandler)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.capacity);
        if (capacity > 0) Initialize(capacity);
        if (comparer == null) comparer = EqualityComparer<TKey>.Default;
        this.comparer = comparer;
        this.accessRecord = accessRecord;
        this.cacheHandler = cacheHandler;

        headEntry = new Entry()
        {
            nextEntry = -1,
            prevEntry = -1,
        };
    }

    public LinkedDictionary(IDictionary<TKey, TValue> dictionary) : this(dictionary, null, false, null)
    {
    }

    public LinkedDictionary(IDictionary<TKey, TValue> dictionary, bool accessRecord)
        : this(dictionary, null, accessRecord, null)
    {
    }

    public LinkedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        : this(dictionary, null, false, null)
    {
    }

    public LinkedDictionary(IDictionary<TKey, TValue> dictionary, ILinkedDictionaryCache<TKey, TValue> cacheHandler)
        : this(dictionary, null, false, cacheHandler)
    {
    }

    public LinkedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer,
        bool accessRecord, ILinkedDictionaryCache<TKey, TValue> cacheHandler) :
            this(dictionary != null ? dictionary.Count : 0, comparer, accessRecord, cacheHandler)
    {
        if (dictionary == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.dictionary);
        }

        foreach (var keyValuePair in dictionary)
            Add(keyValuePair.Key, keyValuePair.Value);
    }

    public bool ReverseEnumerate
    {
        get { return reverseEnumerate; }
        set
        {
            if (reverseEnumerate != value)
            {
                reverseEnumerate = value;
                ++version;
            }
        }
    }

    public IEqualityComparer<TKey> Comparer
    {
        get { return comparer; }
    }

    public int Count
    {
        get { return count - freeCount; }
    }

    public KeyCollection Keys
    {
        get
        {
            if (keys == null) keys = new KeyCollection(this);
            return keys;
        }
    }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys
    {
        get
        {
            if (keys == null) keys = new KeyCollection(this);
            return keys;
        }
    }

    public ValueCollection Values
    {
        get
        {
            if (values == null) values = new ValueCollection(this);
            return values;
        }
    }

    ICollection<TValue> IDictionary<TKey, TValue>.Values
    {
        get
        {
            if (values == null) values = new ValueCollection(this);
            return values;
        }
    }

    public TValue this[TKey key]
    {
        get
        {
            int i = FindEntry(key);
            if (i >= 0)
            {
                if (accessRecord)
                {
                    RemoveLinkedEntry(i);
                    AddLinkedEntry(i);
                    version++;
                }
                return entries[i].value;
            }
            ThrowHelper.ThrowKeyNotFoundException();
            return default(TValue);
        }
        set { Insert(key, value, false); }
    }

    public void Add(TKey key, TValue value)
    {
        Insert(key, value, true);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
    {
        Add(keyValuePair.Key, keyValuePair.Value);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
    {
        int i = FindEntry(keyValuePair.Key);
        if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value))
        {
            return true;
        }
        return false;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
    {
        int i = FindEntry(keyValuePair.Key);
        if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value))
        {
            Remove(keyValuePair.Key);
            return true;
        }
        return false;
    }

    public bool AddFirst(TKey key, TValue value)
    {
        if (accessRecord)
        {
            throw new Exception("accessRecord is true,AddFirst dont be supported");
        }

        if (ContainsKey(key))
            return false;

        Add(key, value);
        var index = FindEntry(key);
        RemoveLinkedEntry(index);

        entries[index].prevEntry = headEntry.prevEntry;
        entries[index].nextEntry = -1;
        if (headEntry.prevEntry < 0)
        {
            headEntry.prevEntry = index;
            headEntry.nextEntry = index;
        }
        else
        {
            entries[headEntry.prevEntry].nextEntry = index;
            headEntry.prevEntry = index;
        }
        return true;
    }

    public KeyValuePair<TKey, TValue> Peek()
    {
        if (accessRecord)
        {
            throw new Exception("accessRecord is true,RemoveFirst dont be supported");
        }

        if (Count > 0)
        {
            var entry = entries[headEntry.prevEntry];
            return new KeyValuePair<TKey, TValue>(entry.key, entry.value);
        }
        return new KeyValuePair<TKey, TValue>();
    }

    public KeyValuePair<TKey, TValue> RemoveFirst()
    {
        if (accessRecord)
        {
            throw new Exception("accessRecord is true,RemoveFirst dont be supported");
        }

        if (Count > 0)
        {
            var entry = entries[headEntry.prevEntry];
            var key = entry.key;
            var value = entry.value;
            Remove(key);
            return new KeyValuePair<TKey, TValue>(key, value);
        }
        return new KeyValuePair<TKey, TValue>();
    }

    public bool AddLast(TKey key, TValue value)
    {
        if (accessRecord)
        {
            throw new Exception("accessRecord is true,AddLast dont be supported");
        }

        if (ContainsKey(key))
            return false;

        Add(key, value);
        return true;
    }

    public KeyValuePair<TKey, TValue> RemoveLast()
    {
        if (accessRecord)
        {
            throw new Exception("accessRecord is true,RemoveLast dont be supported");
        }

        if (Count > 0)
        {
            var entry = entries[headEntry.nextEntry];
            var key = entry.key;
            var value = entry.value;
            Remove(key);
            return new KeyValuePair<TKey, TValue>(key, value);
        }
        return new KeyValuePair<TKey, TValue>();
    }


    public void Clear()
    {
        if (count > 0)
        {
            for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
            Array.Clear(entries, 0, count);
            freeList = -1;
            count = 0;
            freeCount = 0;
            headEntry.nextEntry = -1;
            headEntry.prevEntry = -1;
            version++;
        }
    }

    public bool ContainsKey(TKey key)
    {
        return FindEntry(key) >= 0;
    }

    public bool ContainsValue(TValue value)
    {
        if (value == null)
        {
            for (int i = headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
            {
                if (entries[i].hashCode >= 0 && entries[i].value == null) return true;
            }
        }
        else
        {
            EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
            for (int i = headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
            {
                if (entries[i].hashCode >= 0 && c.Equals(entries[i].value, value)) return true;
            }
        }
        return false;
    }

    private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
    {
        if (array == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
        }

        if (index < 0 || index > array.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
        }

        if (array.Length - index < Count)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);
        }

        Entry[] entries = this.entries;
        for (int i = headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
        {
            array[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
        }
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this, Enumerator.KeyValuePair);
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        return new Enumerator(this, Enumerator.KeyValuePair);
    }

    private int FindEntry(TKey key)
    {
        if (key == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.key);
        }

        if (buckets != null)
        {
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            for (int i = buckets[hashCode%buckets.Length]; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
                    return i;
            }
        }
        return -1;
    }

    private void Initialize(int capacity)
    {
        int size = HashHelpers.GetPrime(capacity);
        buckets = new int[size];
        for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
        entries = new Entry[size];
        freeList = -1;
    }

    private void Insert(TKey key, TValue value, bool add)
    {
        if (key == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.key);
        }

        if (buckets == null) Initialize(0);
        int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
        int targetBucket = hashCode%buckets.Length;
        for (int i = buckets[targetBucket]; i >= 0; i = entries[i].next)
        {
            if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
            {
                if (add)
                {
                    ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_AddingDuplicate);
                }

                if (accessRecord)
                {
                    RemoveLinkedEntry(i);
                    AddLinkedEntry(i);
                }
                entries[i].value = value;
                version++;
                return;
            }
        }
        int index;
        if (freeCount > 0)
        {
            index = freeList;
            freeList = entries[index].next;
            freeCount--;
        }
        else
        {
            if (count == entries.Length)
            {
                Resize();
                targetBucket = hashCode%buckets.Length;
            }
            index = count;
            count++;
        }

        entries[index].hashCode = hashCode;
        entries[index].next = buckets[targetBucket];
        entries[index].key = key;
        entries[index].value = value;
        buckets[targetBucket] = index;

        //增加Entry到双链表中
        AddLinkedEntry(index);
        var eldestEntryKey = entries[headEntry.prevEntry].key;
        var eldestEntryValue = entries[headEntry.prevEntry].value;
        if (cacheHandler != null && !cacheHandler.DisableTriggerCacheCheck &&
            cacheHandler.RemoveEldestEntry(this, eldestEntryKey, eldestEntryValue))
        {
            Remove(eldestEntryKey);
            cacheHandler.EntryRemoved(eldestEntryKey, eldestEntryValue);
        }

        version++;
    }

    private void AddLinkedEntry(int index)
    {
        entries[index].nextEntry = headEntry.nextEntry;
        entries[index].prevEntry = -1;
        if (headEntry.nextEntry < 0)
        {
            headEntry.nextEntry = index;
            headEntry.prevEntry = index;
        }
        else
        {
            if (entries[headEntry.nextEntry].prevEntry < 0)
            {
                entries[headEntry.nextEntry].prevEntry = index;
                headEntry.nextEntry = index;
            }
            else
            {
                entries[entries[headEntry.nextEntry].prevEntry].nextEntry = index;
                entries[headEntry.nextEntry].prevEntry = index;
            }
        }
    }

    private void Resize()
    {
        int newSize = HashHelpers.GetPrime(count << 1);
        int[] newBuckets = new int[newSize];
        for (int i = 0; i < newBuckets.Length; i++) newBuckets[i] = -1;
        Entry[] newEntries = new Entry[newSize];
        Array.Copy(entries, 0, newEntries, 0, count);
        for (int i = 0; i < count; i++)
        {
            int bucket = newEntries[i].hashCode%newSize;
            newEntries[i].next = newBuckets[bucket];
            newBuckets[bucket] = i;
        }
        buckets = newBuckets;
        entries = newEntries;
    }

    public bool Remove(TKey key)
    {
        if (key == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.key);
        }

        if (buckets != null)
        {
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int bucket = hashCode%buckets.Length;
            int last = -1;
            for (int i = buckets[bucket]; i >= 0; last = i, i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
                {
                    if (last < 0)
                    {
                        buckets[bucket] = entries[i].next;
                    }
                    else
                    {
                        entries[last].next = entries[i].next;
                    }
                    entries[i].hashCode = -1;
                    entries[i].next = freeList;
                    entries[i].key = default(TKey);
                    entries[i].value = default(TValue);
                    freeList = i;
                    freeCount++;
                    //删除双链表中对应的Entry
                    RemoveLinkedEntry(i);
                    version++;
                    return true;
                }
            }
        }
        return false;
    }

    private void RemoveLinkedEntry(int index)
    {
        if (entries[index].prevEntry < 0)
        {
            if (entries[index].nextEntry < 0)
            {
                headEntry.nextEntry = -1;
                headEntry.prevEntry = -1;
            }
            else
            {
                headEntry.nextEntry = entries[index].nextEntry;
                entries[entries[index].nextEntry].prevEntry = -1;
            }
        }
        else
        {
            if (entries[index].nextEntry < 0)
            {
                entries[entries[index].prevEntry].nextEntry = -1;
                headEntry.prevEntry = entries[index].prevEntry;
            }
            else
            {
                entries[entries[index].prevEntry].nextEntry = entries[index].nextEntry;
                entries[entries[index].nextEntry].prevEntry = entries[index].prevEntry;
            }
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        int i = FindEntry(key);
        if (i >= 0)
        {
            if (accessRecord)
            {
                RemoveLinkedEntry(i);
                AddLinkedEntry(i);
                version++;
            }
            value = entries[i].value;
            return true;
        }
        value = default(TValue);
        return false;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
    {
        get { return false; }
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
    {
        CopyTo(array, index);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        if (array == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
        }

        if (array.Rank != 1)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_RankMultiDimNotSupported);
        }

        if (array.GetLowerBound(0) != 0)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_NonZeroLowerBound);
        }

        if (index < 0 || index > array.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
        }

        if (array.Length - index < Count)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);
        }

        KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
        if (pairs != null)
        {
            CopyTo(pairs, index);
        }
        else if (array is DictionaryEntry[])
        {
            DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
            Entry[] entries = this.entries;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].hashCode >= 0)
                {
                    dictEntryArray[index++] = new DictionaryEntry(entries[i].key, entries[i].value);
                }
            }
        }
        else
        {
            object[] objects = array as object[];
            if (objects == null)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
            }

            try
            {
                int count = this.count;
                Entry[] entries = this.entries;
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0)
                    {
                        objects[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
                    }
                }
            }
            catch (ArrayTypeMismatchException)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this, Enumerator.KeyValuePair);
    }

    bool ICollection.IsSynchronized
    {
        get { return false; }
    }

    object ICollection.SyncRoot
    {
        get
        {
            if (_syncRoot == null)
            {
                System.Threading.Interlocked.CompareExchange(ref _syncRoot, new Object(), null);
            }
            return _syncRoot;
        }
    }

    bool IDictionary.IsFixedSize
    {
        get { return false; }
    }

    bool IDictionary.IsReadOnly
    {
        get { return false; }
    }

    ICollection IDictionary.Keys
    {
        get { return Keys; }
    }

    ICollection IDictionary.Values
    {
        get { return Values; }
    }

    object IDictionary.this[object key]
    {
        get
        {
            if (IsCompatibleKey(key))
            {
                int i = FindEntry((TKey) key);
                if (i >= 0)
                {
                    if (accessRecord)
                    {
                        RemoveLinkedEntry(i);
                        AddLinkedEntry(i);
                        version++;
                    }
                    return entries[i].value;
                }
            }
            return null;
        }
        set
        {
            VerifyKey(key);
            VerifyValueType(value);
            this[(TKey) key] = (TValue) value;
        }
    }

    private static void VerifyKey(object key)
    {
        if (key == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.key);
        }

        if (!(key is TKey))
        {
            ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof (TKey));
        }
    }

    private static bool IsCompatibleKey(object key)
    {
        if (key == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.key);
        }

        return (key is TKey);
    }

    private static void VerifyValueType(object value)
    {
        if ((value is TValue) || (value == null && !typeof (TValue).IsValueType))
        {
            return;
        }
        ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof (TValue));
    }

    void IDictionary.Add(object key, object value)
    {
        VerifyKey(key);
        VerifyValueType(value);
        Add((TKey) key, (TValue) value);
    }

    bool IDictionary.Contains(object key)
    {
        if (IsCompatibleKey(key))
        {
            return ContainsKey((TKey) key);
        }
        return false;
    }

    IDictionaryEnumerator IDictionary.GetEnumerator()
    {
        return new Enumerator(this, Enumerator.DictEntry);
    }

    void IDictionary.Remove(object key)
    {
        if (IsCompatibleKey(key))
        {
            Remove((TKey) key);
        }
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>,
        IDictionaryEnumerator
    {
        private LinkedDictionary<TKey, TValue> dictionary;
        private int version;
        private int index; //-2 代表还没开始MoveNext;
        private KeyValuePair<TKey, TValue> current;
        private int getEnumeratorRetType; // What should Enumerator.Current return?

        internal const int DictEntry = 1;
        internal const int KeyValuePair = 2;

        internal Enumerator(LinkedDictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
        {
            this.dictionary = dictionary;
            version = dictionary.version;
            index = -2;
            this.getEnumeratorRetType = getEnumeratorRetType;
            current = new KeyValuePair<TKey, TValue>();
        }

        public bool MoveNext()
        {
            if (version != dictionary.version)
            {
                ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
            }

            if (index == -2 || index >= 0)
            {
                index = dictionary.reverseEnumerate
                    ? (index == -2 ? dictionary.headEntry.prevEntry : dictionary.entries[index].prevEntry)
                    : (index == -2 ? dictionary.headEntry.nextEntry : dictionary.entries[index].nextEntry);
                if (index != -1)
                {
                    current = new KeyValuePair<TKey, TValue>(dictionary.entries[index].key,
                        dictionary.entries[index].value);
                    return true;
                }
            }

            index = -1;
            current = new KeyValuePair<TKey, TValue>();
            return false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            get { return current; }
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get
            {
                if (index == -2 || index == -1)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                }

                if (getEnumeratorRetType == DictEntry)
                {
                    return new DictionaryEntry(current.Key, current.Value);
                }
                else
                {
                    return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                }
            }
        }

        void IEnumerator.Reset()
        {
            if (version != dictionary.version)
            {
                ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
            }

            index = -2;
            current = new KeyValuePair<TKey, TValue>();
        }

        DictionaryEntry IDictionaryEnumerator.Entry
        {
            get
            {
                if (index == -2 || index == -1)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                }

                return new DictionaryEntry(current.Key, current.Value);
            }
        }

        object IDictionaryEnumerator.Key
        {
            get
            {
                if (index == -2 || index == -1)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                }

                return current.Key;
            }
        }

        object IDictionaryEnumerator.Value
        {
            get
            {
                if (index == -2 || index == -1)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                }

                return current.Value;
            }
        }
    }

    public sealed class KeyCollection : ICollection<TKey>, ICollection
    {
        private LinkedDictionary<TKey, TValue> dictionary;

        public KeyCollection(LinkedDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.dictionary);
            }
            this.dictionary = dictionary;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        public void CopyTo(TKey[] array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                    ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < dictionary.Count)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);
            }

            Entry[] entries = dictionary.entries;
            for (int i = dictionary.headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
            {
                array[index++] = entries[i].key;
            }
        }

        public int Count
        {
            get { return dictionary.Count; }
        }

        bool ICollection<TKey>.IsReadOnly
        {
            get { return true; }
        }

        void ICollection<TKey>.Add(TKey item)
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_KeyCollectionSet);
        }

        void ICollection<TKey>.Clear()
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_KeyCollectionSet);
        }

        bool ICollection<TKey>.Contains(TKey item)
        {
            return dictionary.ContainsKey(item);
        }

        bool ICollection<TKey>.Remove(TKey item)
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_KeyCollectionSet);
            return false;
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
            }

            if (array.Rank != 1)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_RankMultiDimNotSupported);
            }

            if (array.GetLowerBound(0) != 0)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_NonZeroLowerBound);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                    ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < dictionary.Count)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);
            }

            TKey[] keys = array as TKey[];
            if (keys != null)
            {
                CopyTo(keys, index);
            }
            else
            {
                object[] objects = array as object[];
                if (objects == null)
                {
                    ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
                }

                Entry[] entries = dictionary.entries;
                try
                {
                    for (int i = dictionary.headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
                    {
                        objects[index++] = entries[i].key;
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
                }
            }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        Object ICollection.SyncRoot
        {
            get { return ((ICollection) dictionary).SyncRoot; }
        }

        public struct Enumerator : IEnumerator<TKey>
        {
            private LinkedDictionary<TKey, TValue> dictionary;
            private int index;
            private int version;
            private TKey currentKey;

            internal Enumerator(LinkedDictionary<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
                version = dictionary.version;
                index = -2;
                currentKey = default(TKey);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
                }

                if (index == -2 || index >= 0)
                {
                    index = dictionary.reverseEnumerate
                        ? (index == -2 ? dictionary.headEntry.prevEntry : dictionary.entries[index].prevEntry)
                        : (index == -2 ? dictionary.headEntry.nextEntry : dictionary.entries[index].nextEntry);
                    if (index != -1)
                    {
                        currentKey = dictionary.entries[index].key;
                        return true;
                    }
                }

                index = -1;
                currentKey = default(TKey);
                return false;
            }

            public TKey Current
            {
                get { return currentKey; }
            }

            Object System.Collections.IEnumerator.Current
            {
                get
                {
                    if (index == -2 || index == -1)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                    }

                    return currentKey;
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
                }

                index = -2;
                currentKey = default(TKey);
            }
        }
    }

    public sealed class ValueCollection : ICollection<TValue>, ICollection
    {
        private LinkedDictionary<TKey, TValue> dictionary;

        public ValueCollection(LinkedDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.dictionary);
            }
            this.dictionary = dictionary;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        public void CopyTo(TValue[] array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                    ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < dictionary.Count)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);
            }

            Entry[] entries = dictionary.entries;
            for (int i = dictionary.headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
            {
                array[index++] = entries[i].value;
            }
        }

        public int Count
        {
            get { return dictionary.Count; }
        }

        bool ICollection<TValue>.IsReadOnly
        {
            get { return true; }
        }

        void ICollection<TValue>.Add(TValue item)
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_ValueCollectionSet);
        }

        bool ICollection<TValue>.Remove(TValue item)
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_ValueCollectionSet);
            return false;
        }

        void ICollection<TValue>.Clear()
        {
            ThrowHelper.ThrowNotSupportedException(ThrowHelper.NotSupported_ValueCollectionSet);
        }

        bool ICollection<TValue>.Contains(TValue item)
        {
            return dictionary.ContainsValue(item);
        }

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(dictionary);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
            }

            if (array.Rank != 1)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_RankMultiDimNotSupported);
            }

            if (array.GetLowerBound(0) != 0)
            {
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_NonZeroLowerBound);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index,
                    ThrowHelper.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < dictionary.Count)
                ThrowHelper.ThrowArgumentException(ThrowHelper.Arg_ArrayPlusOffTooSmall);

            TValue[] values = array as TValue[];
            if (values != null)
            {
                CopyTo(values, index);
            }
            else
            {
                object[] objects = array as object[];
                if (objects == null)
                {
                    ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
                }

                Entry[] entries = dictionary.entries;
                try
                {
                    for (int i = dictionary.headEntry.nextEntry; i >= 0; i = entries[i].nextEntry)
                    {
                        objects[index++] = entries[i].value;
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
                }
            }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        Object ICollection.SyncRoot
        {
            get { return ((ICollection) dictionary).SyncRoot; }
        }

        public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator
        {
            private LinkedDictionary<TKey, TValue> dictionary;
            private int index;
            private int version;
            private TValue currentValue;

            internal Enumerator(LinkedDictionary<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
                version = dictionary.version;
                index = -2;
                currentValue = default(TValue);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
                }

                if (index == -2 || index >= 0)
                {
                    index = dictionary.reverseEnumerate
                        ? (index == -2 ? dictionary.headEntry.prevEntry : dictionary.entries[index].prevEntry)
                        : (index == -2 ? dictionary.headEntry.nextEntry : dictionary.entries[index].nextEntry);
                    if (index != -1)
                    {
                        currentValue = dictionary.entries[index].value;
                        return true;
                    }
                }
                index = -1;
                currentValue = default(TValue);
                return false;
            }

            public TValue Current
            {
                get { return currentValue; }
            }

            Object System.Collections.IEnumerator.Current
            {
                get
                {
                    if (index == -2 || index == -1)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumOpCantHappen);
                    }

                    return currentValue;
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
                }
                index = -2;
                currentValue = default(TValue);
            }
        }
    }
}
