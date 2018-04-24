using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 优先级队列的C#实现，注意使用迭代器返回的数据不是按照优先级有序的
/// 而是二叉堆的数组存储结构顺序返回的
/// </summary>
public class PriorityQueue<T> : IEnumerable<T>, ICollection
{
    private T[] _array;
    private int _size;          // Number of elements.
    private int _version;

    private Comparer<T> _comparer;
    private IEqualityComparer<T> _equalityComparer;

    [NonSerialized] private object _syncRoot;
    static T[] _emptyArray = new T[0];

    public PriorityQueue() : this(0, null, null)
    {
   
    }

    public PriorityQueue(int capacity) : this(capacity, null, null)
    {

    }

    public PriorityQueue(Comparer<T> comparer) : this(0, comparer, null)
    {
       
    }

    public PriorityQueue(IEqualityComparer<T> equalityComparer) : this(0, null, equalityComparer)
    {

    }

    public PriorityQueue(int capacity, Comparer<T> comparer, IEqualityComparer<T> equalityComparer)
    {
        if (capacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.capacity, ThrowHelper.ArgumentOutOfRange_NeedNonNegNumRequired);

        if (comparer == null)
            comparer = Comparer<T>.Default;
        _comparer = comparer;

        if (equalityComparer == null)
            equalityComparer = EqualityComparer<T>.Default;
        _equalityComparer = equalityComparer;

        _size = 0;
        _array = _emptyArray;
        if(capacity > 0)
            _array = new T[capacity];
    }

    public PriorityQueue(IEnumerable<T> collection) : this(0, null, null)
    {
        if (collection == null)
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.collection);

        _array = new T[4];
        _size = 0;
        _version = 0;

        foreach (var value in collection)
        {
            Push(value);
        }
    }

    public int Count
    {
        get { return _size; }
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
                System.Threading.Interlocked.CompareExchange(ref _syncRoot, new object(), null);
            }
            return _syncRoot;
        }
    }

    private void Resize()
    {
        if (_size == _array.Length)
        {
            int oldcapacity = _array.Length;
            if(oldcapacity == 0)
            {
                oldcapacity = 2;
            }

            //当容量小于64时候，成倍增长，否则增长50%
            int newcapacity = (oldcapacity < 64) ?
                                  (oldcapacity << 1) :
                                  (oldcapacity + oldcapacity >> 1);
            SetCapacity(newcapacity);
        }
    }

    private void SetCapacity(int capacity)
    {
        T[] newarray = new T[capacity];
        if (_size > 0)
        {
            Array.Copy(_array, 0, newarray, 0, _size);
        }

        _array = newarray;
        _version++;
    }

    public bool Push(T value)
    {
        if (value == null)
        {
            throw new NullReferenceException("value");
        }

        Resize(); 
        if (_size == 0)
            _array[0] = value;
        else
            SiftUp(_size, value);
        _size++;
        _version++;
        return true;
    }

    private void SiftUp(int index, T value)
    {
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            T parentValue = _array[parent];
            if (_comparer.Compare(value, parentValue) >= 0)
                break;
            _array[index] = parentValue;
            index = parent;
        }
        _array[index] = value;
    }

    public T Peek()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyPriorityQueue);
        return _array[0];
    }

    private int IndexOf(T value)
    {
        if (value != null)
        {
            for (int i = 0; i < _size; i++)
                if (_equalityComparer.Equals(value,_array[i]))
                    return i;
        }
        return -1;
    }

    public bool Remove(T value)
    {
        int i = IndexOf(value);
        if (i == -1)
            return false;
        RemoveAt(i);
        return true;
    }

    public bool Contains(T value)
    {
        return IndexOf(value) != -1;
    }

    public void Clear()
    {
        if (_size > 0)
        {
            Array.Clear(_array, 0, _size);
            _size = 0;
            _version++;
        }
    }

    public T Pop()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyPriorityQueue);
        int s = --_size;
        _version++;
        T result = _array[0];
        T value  = _array[s];
        _array[s] = default(T);
        if (s != 0)
            SiftDown(0, value);
        return result;
    }

    private void RemoveAt(int i)
    {
        _version++;
        int s = --_size;
        if (s == i)    //直接移除最后一个元素
            _array[i] = default(T);
        else
        {
            T moved = _array[s];
            _array[s] = default(T);
            SiftDown(i, moved);
            if (_equalityComparer.Equals(_array[i],moved))
            {
                SiftUp(i, moved);
            }
        }
    }

    private void SiftDown(int index, T value)
    {
        int half = _size >> 1;
        while (index < half)
        {
            int child = (index << 1) + 1;
            T c = _array[child];
            int right = child + 1;
            if (right < _size && _comparer.Compare(c, _array[right]) > 0)
                c = _array[child = right];
            if (_comparer.Compare(value, c) <= 0)
                break;
            _array[index] = c;
            index = child;
        }
        _array[index] = value;
    }

    public void TrimExcess()
    {
        int threshold = (int) (_array.Length * 0.9);
        if (_size < threshold)
        {
            SetCapacity(_size);
        }
    }

    public void CopyTo(Array array, int arrayIndex)
    {
        if (array == null)
        {
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.array);
        }

        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.arrayIndex, ThrowHelper.ArgumentOutOfRange_Index);
        }

        int arrayLen = array.Length;
        if (arrayLen - arrayIndex < _size)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidOffLen);
        }

        if (_size == 0)
            return;

        Array.Copy(_array, 0, array, arrayIndex, _size);
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

        int arrayLen = array.Length;
        if (index < 0 || index > arrayLen)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.index, ThrowHelper.ArgumentOutOfRange_Index);
        }

        if (arrayLen - index < _size)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidOffLen);
        }

        if (_size == 0)
            return;
        
        try
        {
            Array.Copy(_array, 0, array, index, _size);
        }
        catch (ArrayTypeMismatchException)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
        }
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator : IEnumerator<T>
    {
        private PriorityQueue<T> _q;
        private int _index;   // -1 = not started, -2 = ended/disposed 
        private int _version;
        private T _currentElement;

        internal Enumerator(PriorityQueue<T> q)
        {
            _q = q;
            _version = _q._version;
            _index = -1;
            _currentElement = default(T);
        }

        public void Dispose()
        {
            _index = -2;
            _currentElement = default(T);
        }

        public bool MoveNext()
        {
            if (_version != _q._version) ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);

            if (_index == -2)
                return false;

            _index++;

            if (_index == _q._size)
            {
                _index = -2;
                _currentElement = default(T);
                return false;
            }

            _currentElement = _q._array[_index];
            return true;
        }

        public T Current
        {
            get
            {
                if (_index < 0)
                {
                    if (_index == -1)
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumNotStarted);
                    else
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumEnded);
                }
                return _currentElement;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (_index < 0)
                {
                    if (_index == -1)
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumNotStarted);
                    else
                        ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumEnded);
                }
                return _currentElement;
            }
        }

        void IEnumerator.Reset()
        {
            if (_version != _q._version) ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EnumFailedVersion);
            _index = -1;
            _currentElement = default(T);
        }
    }
}