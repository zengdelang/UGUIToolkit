using System;

internal static class ThrowHelper
{
    public static readonly string Arg_ArrayPlusOffTooSmall = "Destination array is not long enough to copy all the items in the collection. Check array index and length.";
    public static readonly string ArgumentOutOfRange_NeedNonNegNum = "Non-negative number required.";
    public static readonly string Argument_AddingDuplicate = "An item with the same key has already been added.";
    public static readonly string Arg_RankMultiDimNotSupported = "Only single dimensional arrays are supported for the requested action.";
    public static readonly string Arg_NonZeroLowerBound = "The lower bound of target array must be zero.";
    public static readonly string Argument_InvalidArrayType = "Target array type is not compatible with the type of items in the collection.";
    public static readonly string InvalidOperation_EnumFailedVersion = "Collection was modified; enumeration operation may not execute.";
    public static readonly string InvalidOperation_EnumOpCantHappen = "Enumeration has either not started or has already finished.";
    public static readonly string NotSupported_KeyCollectionSet = "Mutating a key collection derived from a dictionary is not allowed.";
    public static readonly string NotSupported_ValueCollectionSet = "Mutating a value collection derived from a dictionary is not allowed.";
    public static readonly string InvalidOperation_EnumNotStarted = "Enumeration has not started. Call MoveNext.";
    public static readonly string InvalidOperation_EnumEnded = "Enumeration already finished.";
    public static readonly string InvalidOperation_EmptyDeque = "Deque empty.";
    public static readonly string InvalidOperation_EmptyPriorityQueue = "PriorityQueue empty";
    public static readonly string Argument_InvalidOffLen = "Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.";
    public static readonly string ArgumentOutOfRange_Index = "Index was out of range. Must be non-negative and less than the size of the collection.";
    public static readonly string ArgumentOutOfRange_NeedNonNegNumRequired = "Non-negative number required.";

    public static readonly string index = "index";
    public static readonly string array = "array";
    public static readonly string dictionary = "dictionary";
    public static readonly string key = "key";
    public static readonly string capacity = "capacity";
    public static readonly string collection = "collection";
    public static readonly string arrayIndex = "arrayIndex";

    internal static void ThrowArgumentOutOfRangeException()
    {
        throw new ArgumentOutOfRangeException(
            "index",
            "Index was out of range. Must be non-negative and less than the size of the collection.");
    }

    internal static void ThrowWrongKeyTypeArgumentException(object key, Type targetType)
    {
        throw new ArgumentException(
            string.Format("The value \"{0}\" is not of type \"{1}\" and cannot be used in this generic collection.", key,
                targetType), "key");
    }

    internal static void ThrowWrongValueTypeArgumentException(object value, Type targetType)
    {
        throw new ArgumentException(
            string.Format("The value \"{0}\" is not of type \"{1}\" and cannot be used in this generic collection.",
                value, targetType), "value");
    }

    internal static void ThrowKeyNotFoundException()
    {
        throw new System.Collections.Generic.KeyNotFoundException();
    }

    internal static void ThrowArgumentException(string decs)
    {
        throw new ArgumentException(decs);
    }

    internal static void ThrowArgumentNullException(string argument)
    {
        throw new ArgumentNullException(argument);
    }

    internal static void ThrowArgumentOutOfRangeException(string argument)
    {
        throw new ArgumentOutOfRangeException(argument);
    }

    internal static void ThrowArgumentOutOfRangeException(string argument, string desc)
    {
        throw new ArgumentOutOfRangeException(argument, desc);
    }

    internal static void ThrowInvalidOperationException(string desc)
    {
        throw new InvalidOperationException(desc);
    }

    internal static void ThrowNotSupportedException(string desc)
    {
        throw new NotSupportedException(desc);
    }
}
