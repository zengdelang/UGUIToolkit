using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public abstract class DynamicFlexibleLayout : MonoBehaviour
{
    [Serializable]
    public class BoolUnityEvent : UnityEvent<bool>
    {

    }

    [SerializeField] protected RectOffset m_Padding = new RectOffset();
    [SerializeField] protected float m_Divider;                       //item之间的间隔
    [SerializeField] protected ScrollView m_ScrollView;               //布局监听ScrollView的onContentPosChanged来动态布局

    [SerializeField] protected bool m_AutoDrag; //当content的显示超过viewport的显示区域的时候自动可拖拽，否则不支持拖拽，如果竖直布局的时候高度大于viewport的高度则可拖拽，否则不可拖拽

    [SerializeField] protected bool m_EnableCanLockEvent; //是否开启锁定状态通知，可锁定状态下有新数据来可以提示而不直接刷新ScrollView
    [SerializeField] protected BoolUnityEvent m_CanLockEvent = new BoolUnityEvent(); //可锁定状态指示事件
    [NonSerialized] protected bool m_IsLock; //是否是锁定状态，锁定状态下外部处理不应该在有新数据的时候立即刷新UI，可能会导致频繁的UI刷新，影响UI查看

    [SerializeField] protected bool m_EnableLoadMoreEvent; //是否开启加载更多事件，加载更多可用于分页请求数据
    [SerializeField] protected UnityEvent m_LoadMoreEvent = new UnityEvent(); //加载更多事件
    [SerializeField] protected float m_LoadMoreCD = 0.1f;
    [NonSerialized] protected float m_CurrentTime = -1;

    [SerializeField] protected bool m_EnableArrowHintEvent; //是否开启箭头指示事件, 如可选用指示当前viewport向上或向下拖拽是否有内容可拖拽

    protected DrivenRectTransformTracker m_Tracker;
    protected RecycleBin m_RecycleBin;                                //ItemView的缓存池
    protected Deque<IFlexibleItemView> m_ItemViewChildren = new Deque<IFlexibleItemView>(); //保存可视的itemView的双端队列
    protected IFlexibleAdapter m_Adapter;                             //布局所使用的适配器
    protected int m_FirstPosition;             //第一个可视item在数据中的索引位置

    /// <summary>
    ///   <para>The padding to add around the child layout elements.</para>
    /// </summary>
    public RectOffset padding
    {
        get
        {
            return m_Padding;
        }
        set
        {
            m_Padding = value;
            RefreshAllItem();
        }
    }

    public float divider
    {
        get { return m_Divider; }
        set
        {
            m_Divider = value;
            RefreshCurrentItem();
        }
    }

    public bool autoDrag
    {
        get { return m_AutoDrag; }
        set
        {
            m_AutoDrag = value;
            CheckAutoDrag();
        }
    }

    public bool enableCanLockEvent
    {
        get { return m_EnableCanLockEvent; }
        set { m_EnableCanLockEvent = value; }
    }

    public BoolUnityEvent canLockEvent
    {
        get { return m_CanLockEvent; }
        set { m_CanLockEvent = value; }
    }

    public bool isLock
    {
        get { return m_IsLock; }
        set
        {
            if (m_EnableCanLockEvent)
            {
                if (m_IsLock != value)
                {
                    m_IsLock = value;
                    m_CanLockEvent.Invoke(value);
                    if (!value)
                        RefreshAllItem();
                }
            }
        }
    }

    public bool enableLoadMoreEvent
    {
        get { return m_EnableLoadMoreEvent; }
        set { m_EnableLoadMoreEvent = value; }
    }

    public UnityEvent loadMoreEvent
    {
        get { return m_LoadMoreEvent; }
        set { m_LoadMoreEvent = value; }
    }

    public float loadMoreCD
    {
        get { return m_LoadMoreCD; }
        set { m_LoadMoreCD = value; }
    }

    public bool enableArrowHintEvent
    {
        get { return m_EnableArrowHintEvent; }
        set { m_EnableArrowHintEvent = value; }
    }

    protected virtual void Awake()
    {
        m_RecycleBin = new RecycleBin(this);
    }

    protected virtual void OnEnable()
    {
        SetupPivotAndAnchor();
        RefreshCurrentItem();
    }

    protected virtual void OnDisable()
    {
        m_Tracker.Clear();
        RecycleAllItem();
    }

    /// <summary>
    /// 回收当前所有正在显示的item到缓存池，同时调用adapter的RecycleItemView和RecycleItemViewDone
    /// 此函数默认只在OnDisable中调用，调用这个函数需要谨慎，因为它会把所有正在显示的item也回收
    /// </summary>
    public virtual void RecycleAllItem()
    {
        if (m_Adapter == null)
            return;
        //在组件被禁用或者Destroy的时候会调用OnDisable(),此时把所有的item进行回收
        while (m_ItemViewChildren.Count > 0)
        {
            m_RecycleBin.AddScrapView(m_ItemViewChildren.Dequeue());
        }

        m_RecycleBin.RecycleAllItemView(m_Adapter.RecycleItemView);
        m_Adapter.RecycleItemViewDone(this);
    }

    /// <summary>
    /// 设置要监听的ScrollView
    /// </summary>
    /// <param name="scrollView"></param>
    public void SetScrollView(ScrollView scrollView)
    {
        m_ScrollView = scrollView;
        SetupPivotAndAnchor();
        RefreshAllItem();
    }

    /// <summary>
    /// 设置数据适配器
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="clearBin">是否清空缓存池，如果多个适配器共用相同的item则不需要清除，如果使用不同item在一个布局上则需要清除，否则会复用错误的item甚至逻辑代码报错</param>
    public void SetAdapter(IFlexibleAdapter adapter, bool clearBin = true)
    {
        if (m_Adapter != adapter)
        {
            RecycleAllItem();
            ClearRecycleBin(true);
            m_Adapter = adapter;
            if (m_Adapter != null)
            {
                m_RecycleBin.SetViewTypeCount(m_Adapter.GetViewTypeCount());
            }
            RefreshAllItem();
        }
    }

    /// <summary>
    /// 检查能否拖拽
    /// </summary>
    protected abstract void CheckAutoDrag();

    /// <summary>
    /// 检查箭头指示
    /// </summary>
    protected abstract void CheckArrowHint();

    /// <summary>
    /// 检查可锁定状态
    /// </summary>
    protected abstract void CheckCanLockState();

    /// <summary>
    /// 检查加载更多处理
    /// </summary>
    protected abstract void CheckLoadMore(bool moveUpOrLeft);

    /// <summary>
    /// 刷新所有item，布局第一个item开始重新布局
    /// </summary>
    public abstract void RefreshAllItem();

    /// <summary>
    /// 刷新当前所有可见item,通常用于item对应数据被修改，或者有数据被删除时通知刷新UI
    /// </summary>
    public abstract void RefreshCurrentItem();

    /// <summary>
    /// 刷新index对应item的UI，当item是可视的时候才会刷新对应UI，否则不做任何处理
    /// </summary>
    /// <param name="index"></param>
    public abstract void RefreshItem(int index);

    public virtual void ClearRecycleBin(bool destoryItem)
    {
        m_RecycleBin.Clear(destoryItem);
    }

    /// <summary>
    /// 设置content的Pivor和Anchor方便布局计算
    /// </summary>
    protected void SetupPivotAndAnchor()
    {
        if (m_ScrollView != null)
        {
            var content = m_ScrollView.content;
            m_Tracker.Add(this, content,
                DrivenTransformProperties.Pivot | DrivenTransformProperties.AnchorMin |
                DrivenTransformProperties.AnchorMax);
            content.pivot = new Vector2(0, 1);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
        }
    }

    /// <summary>
    /// 设置item的可视性,这里不直接使用GameObject的SetActive(), 因为UGUI的ui控件在OnEnable的时候会有GC产生
    /// 因此使用设置z深度改变item的可视性
    /// </summary>
    /// <param name="itemTransform"></param>
    /// <param name="isVisible">是否可视</param>
    protected static void SetItemVisible(RectTransform itemTransform, bool isVisible)
    {
        var pos = itemTransform.localPosition;
        pos.z = isVisible ? 0 : -100000;
        itemTransform.localPosition = pos;
    }

    #region 布局

    /// <summary>
    /// 监听ScrollView的Content位置更改事件, 根据位置重新计算布局
    /// </summary>
    /// <param name="oldPos"></param>
    /// <param name="newPos"></param>
    public abstract void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos);

    protected void SetChildAlongAxis(RectTransform rect, int axis, float pos)
    {
        if (rect == null)
            return;
        m_Tracker.Add(this, rect, (DrivenTransformProperties)(3840 | (axis != 0 ? 4 : 2)));
        rect.SetInsetAndSizeFromParentEdge(axis != 0 ? RectTransform.Edge.Top : RectTransform.Edge.Left, pos, rect.sizeDelta[axis]);
    }

    protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
    {
        if (rect == null)
            return;
        m_Tracker.Add(this, rect, (DrivenTransformProperties)(3840 | (axis != 0 ? 8196 : 4098)));
        rect.SetInsetAndSizeFromParentEdge(axis != 0 ? RectTransform.Edge.Top : RectTransform.Edge.Left, pos, size);
    }

    #endregion

    #region Item缓存管理

    protected class RecycleBin
    {
        public delegate bool RecycleItemViewDelegate(IFlexibleItemView itemView, DynamicFlexibleLayout parent);

        private List<IFlexibleItemView>[] m_ScrapViews;
        private List<IFlexibleItemView> m_CurrentScrap;
        private DynamicFlexibleLayout m_Owner;
        private int m_ViewTypeCount;

        public RecycleBin(DynamicFlexibleLayout layout)
        {
            m_Owner = layout;
        }

        public void SetViewTypeCount(int viewTypeCount)
        {
            if (viewTypeCount < 1)
            {
                throw new ArgumentOutOfRangeException("viewTypeCount");
            }

            List<IFlexibleItemView>[] scrapViews = new List<IFlexibleItemView>[viewTypeCount];
            for (int i = 0; i < viewTypeCount; i++)
            {
                scrapViews[i] = new List<IFlexibleItemView>();
            }

            m_ViewTypeCount = viewTypeCount;
            m_CurrentScrap = scrapViews[0];
            m_ScrapViews = scrapViews;
        }

        public void RecycleAllItemView(RecycleItemViewDelegate recycleDelegate)
        {
            if (recycleDelegate == null)
                return;

            for (int i = 0, typeCount = m_ViewTypeCount; i < typeCount; ++i)
            {
                List<IFlexibleItemView> scrap = m_ScrapViews[i];
                for (int j = scrap.Count - 1; j >= 0; --j)
                {
                    if (recycleDelegate(scrap[j], m_Owner))
                    {
                        scrap.RemoveAt(j);
                    }
                }
            }
        }

        public void Clear(bool destoryItem)
        {
            for (int i = 0, typeCount = m_ViewTypeCount; i < typeCount; ++i)
            {
                List<IFlexibleItemView> scrap = m_ScrapViews[i];
                if (destoryItem)
                {
                    for (int j = scrap.Count - 1; j >= 0; --j)
                    {
                        Destroy(scrap[j].rectTransform.gameObject);
                        scrap.RemoveAt(j);
                    }
                }
                else
                {
                    scrap.Clear();
                }
            }
        }

        public IFlexibleItemView GetScrapView(int position)
        {
            List<IFlexibleItemView> scrapViews;
            if (m_ViewTypeCount == 1)
            {
                scrapViews = m_CurrentScrap;
                int size = scrapViews.Count;
                if (size > 0)
                {
                    var item = scrapViews[size - 1];
                    scrapViews.RemoveAt(size - 1);
                    return item;
                }
                return null;
            }

            int whichScrap = m_Owner.m_Adapter.GetItemViewType(position);
            if (whichScrap >= 0 && whichScrap < m_ScrapViews.Length)
            {
                scrapViews = m_ScrapViews[whichScrap];
                int size = scrapViews.Count;
                if (size > 0)
                {
                    var item = scrapViews[size - 1];
                    scrapViews.RemoveAt(size - 1);
                    return item;
                }
            }

            return null;
        }

        public void AddScrapView(IFlexibleItemView scrap)
        {
            SetItemVisible(scrap.rectTransform, false);
            int viewType = scrap.ViewType;
            if (m_ViewTypeCount == 1)
            {
                m_CurrentScrap.Add(scrap);
            }
            else
            {
                m_ScrapViews[viewType].Add(scrap);
            }
        }
    }

    #endregion
}
