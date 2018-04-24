/// <summary>
/// RemoveEldestEntry是用于自定义是否删除最旧的数据，可用于缓存的设计
/// EntryRemoved只有在removeEldestEntry有定义并且有返回true的时候才调用
/// </summary>
public interface ILinkedDictionaryCache<TKey, TValue>
{
    /// <summary>
    /// LinkedDictionary用作缓存设计的时候被删除的条目处理
    /// </summary>
    void EntryRemoved(TKey key, TValue value);

    /// <summary>
    /// 是否移除LinkedDictionary中最旧的条目
    /// 实现此函数的时候，不应该操作Dictionary会更改内部数据的操作
    /// 否则可能会继续触发RemoveEldestEntry导致无限循环
    /// </summary>
    bool RemoveEldestEntry(LinkedDictionary<TKey, TValue> dictionary, TKey key, TValue value);


    bool DisableTriggerCacheCheck { get; set; }
}

