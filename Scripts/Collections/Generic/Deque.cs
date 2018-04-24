using System;
using System.Collections;
using System.Collections.Generic;

public class Deque<T> : IEnumerable<T>, ICollection
{
    private T[] _array;
    private int _head;       // First valid element in the queue
    private int _tail;       // Last valid element in the queue
    private int _size;       // Number of elements.
    private int _version;

    [NonSerialized]
    private object _syncRoot;

    static T[] _emptyArray = new T[0];

    public Deque()
    {
        _array = _emptyArray;
    }

    public Deque(int capacity)
    {
        if (capacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ThrowHelper.capacity, ThrowHelper.ArgumentOutOfRange_NeedNonNegNumRequired);

        _array = new T[capacity];
        _head = 0;
        _tail = 0;
        _size = 0;
    }

    public Deque(IEnumerable<T> collection)
    {
        if (collection == null)
            ThrowHelper.ThrowArgumentNullException(ThrowHelper.collection);

        _array = new T[4];
        _size = 0;
        _version = 0;

        foreach (var value in collection)
        {
            Enqueue(value);
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

    public void Clear()
    {
        if (_head < _tail)
            Array.Clear(_array, _head, _size);
        else
        {
            Array.Clear(_array, _head, _array.Length - _head);
            Array.Clear(_array, 0, _tail);
        }

        _head = 0;
        _tail = 0;
        _size = 0;
        _version++;
    }

    public void CopyTo(T[] array, int arrayIndex)
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

        int numToCopy = (arrayLen - arrayIndex < _size) ? (arrayLen - arrayIndex) : _size;
        if (numToCopy == 0) return;

        int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
        Array.Copy(_array, _head, array, arrayIndex, firstPart);
        numToCopy -= firstPart;
        if (numToCopy > 0)
        {
            Array.Copy(_array, 0, array, arrayIndex + _array.Length - _head, numToCopy);
        }
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

        int numToCopy = (arrayLen - index < _size) ? arrayLen - index : _size;
        if (numToCopy == 0) return;

        try
        {
            int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
            Array.Copy(_array, _head, array, index, firstPart);
            numToCopy -= firstPart;

            if (numToCopy > 0)
            {
                Array.Copy(_array, 0, array, index + _array.Length - _head, numToCopy);
            }
        }
        catch (ArrayTypeMismatchException)
        {
            ThrowHelper.ThrowArgumentException(ThrowHelper.Argument_InvalidArrayType);
        }
    }

    private void Resize()
    { 
        if (_size == _array.Length)
        {
            int oldcapacity = _array.Length;
            if (oldcapacity == 0)
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

    public void EnqueueFirst(T item)
    {
        Resize();

        _head = (_head - 1 + _array.Length) % _array.Length;
        _array[_head] = item;
        _size++;
        _version++;
    }

    public void EnqueueLast(T item)
    {
        Enqueue(item);
    }

    public void Enqueue(T item)
    {
        Resize();

        _array[_tail] = item;
        _tail = (_tail + 1) % _array.Length;
        _size++;
        _version++;
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

    public T DequeueFirst()
    {
        return Dequeue();
    }

    public T DequeueLast()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyDeque);

        _tail = (_tail - 1 + _array.Length) % _array.Length;
        T removed = _array[_tail];
        _array[_tail] = default(T);
        _size--;
        _version++;
        return removed;
    }

    public T Dequeue()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyDeque);

        T removed = _array[_head];
        _array[_head] = default(T);
        _head = (_head + 1) % _array.Length;
        _size--;
        _version++;
        return removed;
    }

    public T PeekFirst()
    {
        return Peek();
    }

    public T PeekLast()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyDeque);

        return _array[(_tail - 1 + _array.Length) % _array.Length];
    }

    public T Peek()
    {
        if (_size == 0)
            ThrowHelper.ThrowInvalidOperationException(ThrowHelper.InvalidOperation_EmptyDeque);

        return _array[_head];
    }

    public bool Contains(T item)
    {
        int index = _head;
        int count = _size;

        EqualityComparer<T> c = EqualityComparer<T>.Default;
        while (count-- > 0)
        {
            if (item == null)
            {
                if (_array[index] == null)
                    return true;
            }
            else if (_array[index] != null && c.Equals(_array[index], item))
            {
                return true;
            }
            index = (index + 1) % _array.Length;
        }

        return false;
    }

    public T GetElement(int i)
    {
        return _array[(_head + i) % _array.Length];
    }

    public T[] ToArray()
    {
        T[] arr = new T[_size];
        if (_size == 0)
            return arr;

        if (_head < _tail)
        {
            Array.Copy(_array, _head, arr, 0, _size);
        }
        else {
            Array.Copy(_array, _head, arr, 0, _array.Length - _head);
            Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
        }

        return arr;
    }

    private void SetCapacity(int capacity)
    {
        T[] newarray = new T[capacity];
        if (_size > 0)
        {
            if (_head < _tail)
            {
                Array.Copy(_array, _head, newarray, 0, _size);
            }
            else {
                Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
            }
        }

        _array = newarray;
        _head = 0;
        _tail = (_size == capacity) ? 0 : _size;
        _version++;
    }

    public void TrimExcess()
    {
        int threshold = (int)(_array.Length * 0.9);
        if (_size < threshold)
        {
            SetCapacity(_size);
        }
    }

    public struct Enumerator : IEnumerator<T>
    {
        private Deque<T> _q;
        private int      _index;   // -1 = not started, -2 = ended/disposed 
        private int      _version;
        private T        _currentElement;

        internal Enumerator(Deque<T> q)
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

            _currentElement = _q.GetElement(_index);
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
