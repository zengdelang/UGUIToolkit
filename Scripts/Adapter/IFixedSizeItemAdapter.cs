using UnityEngine;

public interface IFixedSizeItemAdapter
{
    /// <summary>
    /// 需要展示的数据的总数目
    /// </summary>
    /// <returns></returns>
    int GetCount();

    /// <summary>
    /// 适配器数据数目是否为0
    /// </summary>
    /// <returns></returns>
    bool IsEmpty();

    /// <summary>
    /// 得到Item的UI大小, 对于分页布局(HorizontalPageLayout，VerticalPageLayout)该函数未使用
    /// </summary>
    /// <returns></returns>
    Vector2 GetItemSize();

    /// <summary>
    /// 得到ItemView的信息,ItemView用于保存UI控件数据,用于ProcessItemView中直接得到对应ItemView的UI控件
    /// </summary>
    /// <param name="itemParent">新生成的Item要挂载的父节点</param>
    /// <returns></returns>
    IItemView GetItemView(RectTransform itemParent);

    /// <summary>
    /// 处理Item的ui逻辑
    /// </summary>
    /// <param name="position">当前需要处理的数据在总数据中的索引,默认从0开始</param>
    /// <param name="itemView">当前item对应的itemView, 保存每一个item的信息, 一般保存item对应的ui控件, 由GetItemView设置信息</param>
    /// <param name="parent">当前使用该适配器的布局</param>
    void ProcessItemView(int position, IItemView itemView, DynamicLayout parent);

    /// <summary>
    /// 当布局组件被禁用或者删除以及手动调用布局的RecycleAllItem的时候时候调用，可用于一些通用ui控件的回收，比如有一个通用的ui用于显示道具
    /// 这个通用的ui有一个自己的缓存池，当RecycleItemView的时候可以考虑把通用ui相关的ui控件回收到它自己的缓冲池
    /// </summary>
    /// <param name="itemView"></param>
    /// <param name="parent"></param>
    /// <returns>
    /// 如果返回true代表adapter自行处理了itemView, 底层会将itemView从缓存池中移除但不做其他操作，如删除等，返回false，itemView的处理交由底层处理
    /// 如果是回收itemView上面的某些UI组件应该返回false,否则内部缓存池会失去对itemView的引用
    /// 如果是要自行删除itemView或者需要将itemView移到别的缓存池需要返回true，以便底层将itemView从缓存池中移除
    /// </returns>
    bool RecycleItemView(IItemView itemView, DynamicLayout parent);

    /// <summary>
    /// 当全部的RecycleItemView执行完毕的时候调用，可用于清理布局内置的缓存等
    /// 如RecycleItemView把每一个itemView移动到一个通用的缓存池中方便多个UI共享，然后在该函数中清理掉使用该适配器的布局的ItemView缓存
    /// </summary>
    void RecycleItemViewDone(DynamicLayout parent);
}
