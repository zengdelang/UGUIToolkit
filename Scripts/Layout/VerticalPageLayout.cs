using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 竖直分页网格布局, 支持ScrollView水平滑动的时候使用有限的几个item不断复用进行网格布局
/// </summary>
public class VerticalPageLayout : DynamicLayout
{
    protected class RowLayoutParam : LayoutParam
    {
        public int row;         //item所属的行
        public int index;       //item的索引
    }

    [Serializable]
    public class IntUnityEvent : UnityEvent<int>
    {

    }

    [Serializable]
    public class Int2UnityEvent : UnityEvent<int,int>
    {

    }

    [SerializeField] protected int  m_Column = 1;       //布局信息,Item显示的列数
    [SerializeField] protected bool m_ExpandWidth;      //强制Item的宽度自适应可视区域的宽,竖直滑动的时候有效
    [SerializeField] protected bool m_ExpandHeight;     //强制Item的高度自适应可视区域的高

    [SerializeField] protected BoolUnityEvent m_ArrowUpEvent = new BoolUnityEvent(); //向上箭头指示
    [SerializeField] protected BoolUnityEvent m_ArrowDownEvent = new BoolUnityEvent(); //向下箭头指示

    [SerializeField] protected int     m_PageRow;       //布局信息,每一页Item显示的行数              
    [SerializeField] protected int     m_PageColumn;    //布局信息,每一页Item显示的列数
    [SerializeField] protected Vector2 m_PagePadding;   //x代表页的水平padding, y代表是竖直padding

    protected int m_FirstVisibleRow = 0;     //当前区域第一个可见的行, 从0开始
    protected int m_LastVisibleRow = -1;     //当前区域最后一个可见的行
    protected int m_TotalRow = 0;            //可显示的总的行数

    protected int m_PageItemCount;           //一页所能容纳的item数目

    [SerializeField] protected IntUnityEvent m_PageCountChangedEvent = new IntUnityEvent(); //总的页数变换事件
    protected int m_PageCount;               //总的页数

    [SerializeField] protected Int2UnityEvent m_PageIndexChangedEvent = new Int2UnityEvent(); //当前可视页的索引变化事件
    protected int m_FirstPageIndex = -1;     //第一个可视的页  

    public int column
    {
        get
        {
            return m_Column <= 0 ? 1 : m_Column;
        }
        set
        {
            m_Column = Mathf.Clamp(m_Column, 1, int.MaxValue);
            RefreshAllItem();
        }
    }

    public bool expandHeight
    {
        get { return m_ExpandHeight; }
        set
        {
            m_ExpandHeight = value;
            RefreshCurrentItem();
        }
    }

    public bool expandWidth
    {
        get { return m_ExpandWidth; }
        set
        {
            m_ExpandWidth = value;
            RefreshCurrentItem();
        }
    }

    public int pageRow
    {
        get { return m_PageRow <= 0 ? 1 : m_PageRow; }
        set
        {
            m_PageRow = value;
            RefreshAllItem();
        }
    }

    public int pageColumn
    {
        get { return m_PageColumn <= 0 ? 1 : m_PageColumn; }
        set
        {
            m_PageColumn = value;
            RefreshAllItem();
        }
    }

    public IntUnityEvent pageCountChangedEvent
    {
        get { return m_PageCountChangedEvent; }
        set { m_PageCountChangedEvent = value; }
    }

    public int pageCount
    {
        get { return m_PageCount; }
        protected set
        {
            if (m_PageCount != value)
            {
                m_PageCount = value;
                if(m_PageCountChangedEvent != null)
                    m_PageCountChangedEvent.Invoke(value);

                var oldFirstPageIndex = m_FirstPageIndex;
                m_FirstPageIndex = -1;
                firstPageIndex = oldFirstPageIndex;
            }
        }
    }

    public Int2UnityEvent pageIndexChangedEvent
    {
        get { return m_PageIndexChangedEvent; }
        set { m_PageIndexChangedEvent = value; }
    }

    public int firstPageIndex
    {
        get { return m_FirstPageIndex; }
        protected set
        {
            if (m_FirstPageIndex != value)
            {
                var oldPage = m_FirstPageIndex;
                m_FirstPageIndex = value;
                if (m_PageIndexChangedEvent != null)
                    m_PageIndexChangedEvent.Invoke(oldPage, m_FirstPageIndex);
            }
        }
    }

    public BoolUnityEvent arrowUpEvent
    {
        get { return m_ArrowUpEvent; }
        set { m_ArrowUpEvent = value; }
    }

    public BoolUnityEvent arrowDownEvent
    {
        get { return m_ArrowDownEvent; }
        set { m_ArrowDownEvent = value; }
    }

    #region Item操作

    /// <summary>
    /// 刷新所有item，布局也从第一个开始重新布局
    /// </summary>
    public override void RefreshAllItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        SetContentPosition(Vector2.zero);
        m_ScrollView.StopMovement();
        m_ScrollView.StopAnimation(false);

        isLock = false;
        RefreshCurrentItem();
    }

    /// <summary>
    /// 刷新当前可见item,通常用于item对应数据被修改，或者有数据被删除时通知刷新UI
    /// </summary>
    public override void RefreshCurrentItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        //最好把已有的item全部回收,特别是如果支持多种不同item的不回收，容易复用错误的item
        while (m_ItemViewChildren.Count > 0)
        {
            m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.Dequeue());
        }

        m_FirstVisibleRow = 0;
        m_LastVisibleRow = -1;
        CalculateHeight();
        PerformLayout();
    }

    /// <summary>
    /// 刷新index对应item的UI，item必须是可视的item，否则不进行处理
    /// </summary>
    /// <param name="index"></param>
    public override void RefreshItem(int index)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (index < 0 || index >= m_Adapter.GetCount())
            return;

        for (int i = 0, count = m_ItemViewChildren.Count; i < count; ++i)
        {
            var layoutInfo = m_ItemViewChildren.GetElement(i);
            var layoutParam = layoutInfo.layoutParam as RowLayoutParam;
            if (layoutParam.index == index)
            {
                m_Adapter.ProcessItemView(index, layoutInfo.itemView, this);
                break;
            }
        }
    }

    /// <summary>
    /// 定位到某一个item
    /// </summary>
    /// <param name="itemIndex">item在数据中的索引</param>
    /// <param name="resetStartPos">是否从content的开头开始动画滚动</param>
    /// <param name="useAnimation">是否使用动画滚动, false则为瞬间定位到item</param>
    /// <param name="factor">[0, 1] 0显示的viewport的最上面， 1显示的viewport的最下面， 0-1之间按比例显示在viewport中间</param>
    public void ScrollToItem(int itemIndex, bool resetStartPos = false, bool useAnimation = true, float factor = 0.5f)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (itemIndex < 0 || itemIndex >= m_Adapter.GetCount())
            return;

        var viewportHeight = m_ScrollView.viewport.rect.height;
        var contentMinPos = 0;
        var contentMaxPos = m_ScrollView.content.rect.height - viewportHeight;
        if (contentMaxPos < 0)
            contentMaxPos = 0;

        var pageRowCount = pageRow;
        var pageIndex = itemIndex / m_PageItemCount;
        var pageItemIndex = itemIndex % m_PageItemCount;
        var rowIndex = pageItemIndex / pageRowCount;

        var itemHeight = (viewportHeight + spacing.y - m_PagePadding.y) / pageRowCount;
        var targetPosY = pageIndex * viewportHeight + m_PagePadding.y * 0.5f + itemHeight * rowIndex;
        var deltaHeight = viewportHeight - itemHeight;
        targetPosY -= Mathf.Clamp01(factor) * deltaHeight;
        targetPosY = Mathf.Clamp(targetPosY, contentMinPos, contentMaxPos);

        m_ScrollView.StartAnimation(new Vector2(0, targetPosY), resetStartPos, useAnimation);
    }

    /// <summary>
    /// 定位到某一页
    /// </summary>
    /// <param name="pageIndex"></param>y
    public void ScrollToPage(int pageIndex, bool resetStartPos = false, bool useAnimation = true)
    {
        if (pageIndex < 0 || pageIndex >= m_PageCount)
            return;

        m_ScrollView.StartAnimation(new Vector2(0, pageIndex * m_ScrollView.viewport.rect.height), resetStartPos, useAnimation);
    }

    /// <summary>
    /// 检查能否拖拽
    /// </summary>
    protected override void CheckAutoDrag()
    {
        if (m_AutoDrag)
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = m_ScrollView.content.rect.height > (m_ScrollView.viewport.rect.height + 0.2f); //添加0.2f避免相等时候的浮点数误差
        }
        else
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = true;
        }
    }

    /// <summary>
    /// 检查箭头指示
    /// </summary>
    protected override void CheckArrowHint()
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableArrowHintEvent)
        {
            //anchoredPosition.y停留在上方可能存在一些误差,不完全等于0，设置当大于等于0.13有向上箭头指示
            var contentTopY = m_ScrollView.content.anchoredPosition.y;
            m_ArrowUpEvent.Invoke(contentTopY >= 0.13f);

            //计算当前在viewport下边缘处content的Y坐标
            var viewportHeight = m_ScrollView.viewport.rect.height;
            var bottomY = contentTopY + viewportHeight;
            m_ArrowDownEvent.Invoke((m_ScrollView.content.rect.height - 0.13f) > bottomY);
        }
    }

    /// <summary>
    /// 检查可锁定状态
    /// </summary>
    protected override void CheckCanLockState()
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableCanLockEvent)
        {
            if (m_ScrollView.content.rect.height > (m_ScrollView.viewport.rect.height + 0.2f))
            {
                var contentY = m_ScrollView.content.anchoredPosition.y;
                if (m_IsLock && !m_ScrollView.dragging && contentY <= 0.12f)
                {
                    isLock = false;
                    return;
                }

                if (contentY > 0.12f)
                {
                    isLock = true;
                }
            }
        }
    }

    /// <summary>
    /// 检查加载更多处理
    /// </summary>
    protected override void CheckLoadMore(bool moveUp)
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableLoadMoreEvent)
        {
            if (moveUp)
            {
                //计算当前在viewport下边缘处content的Y坐标
                var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
                if (m_ScrollView.content.rect.height < bottomY)
                {
                    if (Time.unscaledTime - m_CurrentTime < m_LoadMoreCD)
                    {
                        return;
                    }
                    m_CurrentTime = Time.unscaledTime;
                    m_LoadMoreEvent.Invoke();
                }
            }
        }
    }

    #endregion

    #region 布局处理

    /// <summary>
    /// 监听ScrollView的Content位置更改事件, 根据位置重新计算布局
    /// </summary>
    /// <param name="oldPos"></param>
    /// <param name="newPos"></param>
    public override void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        if (m_ScrollView == null || m_Adapter == null || m_Adapter.IsEmpty())
            return;

        if (!m_ScrollView.vertical || m_ScrollView.horizontal)
        {
            Debug.LogError("VerticalPageLayout只支持ScrollView为竖直滑动的时候才生效");
            return;
        }

        PerformLayout();
        CheckArrowHint();
        CheckCanLockState();
        CheckLoadMore(newPos.y > oldPos.y);
    }

    protected void CalculatePageIndex()
    {
        var deltaHeightToTop = m_ScrollView.content.anchoredPosition.y;
        deltaHeightToTop = Mathf.Clamp(deltaHeightToTop, 0, m_ScrollView.content.rect.height);

        var pageIndex = Mathf.FloorToInt((deltaHeightToTop + 0.2f) / m_ScrollView.viewport.rect.height);
        pageIndex = Mathf.Clamp(pageIndex, 0, m_PageCount - 1);
        firstPageIndex = pageIndex;
    }

    /// <summary>
    /// 计算ScrollView的Content的高度
    /// </summary>
    protected void CalculateHeight()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        var itemCount = m_Adapter.GetCount();
        m_PageItemCount = pageColumn * pageRow;
        var totalPage = itemCount / m_PageItemCount;
        totalPage += itemCount % m_PageItemCount > 0 ? 1 : 0;
        pageCount = totalPage;

        var viewportHeight = m_ScrollView.viewport.rect.height;
        var contentHeight = viewportHeight * m_PageCount;
        SetContentHeight(contentHeight);
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_ScrollView.viewport.rect.width);

        if (!m_ScrollView.dragging && m_ScrollView.velocity == Vector2.zero)
        {
            //计算当前在viewport下边缘处content的Y坐标
            var contentTopY = m_ScrollView.content.anchoredPosition.y;
            var viewportBottomY = contentTopY + viewportHeight;
            if (viewportBottomY > contentHeight || contentTopY < 0)
            {
                var oldPos = m_ScrollView.content.anchoredPosition;
                oldPos.y = Mathf.Max(0, contentHeight - viewportHeight);
                SetContentPosition(oldPos);
            }
        }

        CheckAutoDrag();
        CheckArrowHint();
        CheckLoadMore(true);
    }

    protected void SetContentHeight(float contentHeight)
    {
        var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
        var preContentHeight = m_ScrollView.content.rect.height;

        if (bottomY > preContentHeight && bottomY < contentHeight && m_ScrollView.velocity.y < 0)
        {
            m_ScrollView.InverseVelocity();
        }
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
    }

    /// <summary>
    /// 执行布局处理,回收不显示的item, 添加需要显示的item到可视列表中
    /// </summary>
    protected void PerformLayout()
    {
        if (m_ScrollView == null)
            return;

        int newFirstVisibleRow = 0;
        int newLastVisibleRow = 0;
        GetVisibleRowRange(ref newFirstVisibleRow, ref newLastVisibleRow);
        //新的可视行区域和旧的不同时,重新计算需要显示的item
        if (newFirstVisibleRow != m_FirstVisibleRow || newLastVisibleRow != m_FirstVisibleRow)
        {
            //如果新的第一个可视行大于旧的,从队列头部移除需要隐藏的item
            if (m_FirstVisibleRow < newFirstVisibleRow)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekFirst();
                    var layoutParam = itemView.layoutParam as RowLayoutParam;
                    if (layoutParam.row >= newFirstVisibleRow)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueFirst());
                }
            }

            //如果新的最后一个可视行小于旧的,从队列尾部移除需要隐藏的item
            if (m_LastVisibleRow > newLastVisibleRow)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekLast();
                    var layoutParam = itemView.layoutParam as RowLayoutParam;
                    if (layoutParam.row <= newLastVisibleRow)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueLast());
                }
            }

            var pageRowCount    = pageRow;          //页的行数,pageRow是个属性提前在这算下,避免循环中多次计算
            var pageColumnCount = pageColumn;       //页的列数,pageColumn是个属性提前在这算下,避免循环中多次计算
            var itemCount = m_Adapter.GetCount();   //item的总数,提前在这算下,避免循环中多次计算
            
            var widthDelta = (m_ScrollView.viewport.rect.width + spacing.x - m_PagePadding.x) / pageColumnCount;
            var heightDelta = (m_ScrollView.viewport.rect.height + spacing.y - m_PagePadding.y) / pageRowCount;

            var forceItemHeight = heightDelta - spacing.y;
            var forceItemWidth = widthDelta - spacing.x;

            //如果新的第一个可视行小于旧的,从队列头部添加新的item
            if (m_FirstVisibleRow > newFirstVisibleRow)
            {
                var lastAddItemRow = m_FirstVisibleRow - 1;
                if (newLastVisibleRow < lastAddItemRow)
                {
                    lastAddItemRow = newLastVisibleRow;
                }

                //添加新的item
                for (int i = lastAddItemRow; i >= newFirstVisibleRow; --i)
                {
                    for (int j = 0; j < pageColumnCount; ++j)
                    {
                        var itemIndex = GetItemIndex(i, j, pageRowCount, pageColumnCount);
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content);
                            layoutInfo.itemView.rectTransform.SetParent(m_ScrollView.content);
                            layoutInfo.layoutParam = new RowLayoutParam();
                        }

                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as RowLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if(layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.row = i;    //把行信息存进去
                        layoutParam.index = itemIndex;
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueFirst(layoutInfo);
                        SetItemPosition(i, j, pageRowCount, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight, forceItemWidth);
                    }
                }
            }

            //如果新的最后一个可视行大于旧的,从队列尾部添加新的item
            if (m_LastVisibleRow < newLastVisibleRow)
            {
                var firstAddItemRow = m_LastVisibleRow + 1;
                if (newFirstVisibleRow > firstAddItemRow)
                {
                    firstAddItemRow = newFirstVisibleRow;
                }

                //添加新的item
                for (int i = firstAddItemRow; i <= newLastVisibleRow; ++i)
                {
                    for (int j = 0; j < pageColumnCount; ++j)
                    {
                        var itemIndex = GetItemIndex(i, j, pageRowCount, pageColumnCount);
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content);
                            layoutInfo.itemView.rectTransform.SetParent(m_ScrollView.content);
                            layoutInfo.layoutParam = new RowLayoutParam();
                        }
                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as RowLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if (layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.row = i;    //把行信息存进去
                        layoutParam.index = itemIndex;   
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueLast(layoutInfo);
                        SetItemPosition(i, j, pageRowCount, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight, forceItemWidth);
                    }
                }
            }

            m_FirstVisibleRow = newFirstVisibleRow;
            m_LastVisibleRow = newLastVisibleRow;
        }

        CalculatePageIndex();
    }

    protected int GetItemIndex(int rowIndex, int columnIndex, int pageRowCount, int pageColumnCount)
    {
        var pageIndex = rowIndex / pageRowCount;
        rowIndex = rowIndex % pageRowCount;
        return pageIndex * m_PageItemCount + rowIndex * pageColumnCount + columnIndex;
    }

    /// <summary>
    /// 获取当前可视的行范围
    /// </summary>
    protected void GetVisibleRowRange(ref int firstVisibleRow, ref int lastVisibleRow)
    {
        //content的上边距离viewport上边的高度
        var deltaHeightToTop = m_ScrollView.content.anchoredPosition.y;
        var contentHeight = m_ScrollView.content.rect.height;
        var viewportHeight = m_ScrollView.viewport.rect.height;
        var maxDeltaHeight = contentHeight - viewportHeight;         //达到最下面的时候再向下,拖动不改变firstVisibleRow的值
        if (deltaHeightToTop > maxDeltaHeight)
            deltaHeightToTop = maxDeltaHeight;

        var pageHeight = viewportHeight;
        var firstVisiblePage = Mathf.FloorToInt(deltaHeightToTop / pageHeight);

        var halfPagePadding = m_PagePadding.y * 0.5f;
        var extraHeightToTop = deltaHeightToTop - firstVisiblePage * viewportHeight - halfPagePadding;
        if (firstVisiblePage < 0)
        {
            firstVisiblePage = 0;
            extraHeightToTop = halfPagePadding;
        }

        var pageRowCount = pageRow;
        var itemHeight = (viewportHeight - m_PagePadding.y + spacing.y) / pageRowCount;
        firstVisibleRow = firstVisiblePage * pageRowCount + Mathf.FloorToInt(extraHeightToTop / itemHeight);
        if (firstVisibleRow < 0)
        {
            firstVisibleRow = 0;
        }

        //content的上边距离viewport下边的高度
        var deltaHeightToBottom = viewportHeight + (deltaHeightToTop < 0 ? 0 : deltaHeightToTop);  //如果小于viewport的高度,则设置为viewport的高度
        var lastVisiblePage = Mathf.FloorToInt(deltaHeightToBottom / pageHeight);
        var extraHeightToBottom = deltaHeightToBottom - lastVisiblePage * viewportHeight - halfPagePadding;   
        lastVisibleRow = lastVisiblePage * pageRowCount + Mathf.CeilToInt(extraHeightToBottom / itemHeight);
        lastVisibleRow = Mathf.Clamp(lastVisibleRow, 0, pageRowCount * m_PageCount - 1);
    }

    /// <summary>
    /// 设置Item的位置
    /// </summary>
    protected void SetItemPosition(int rowIndex, int columnIndex, int pageRowCount, RectTransform rect, float heightDelta, float widthDelta, float forceItemHeight, float forceItemWidth)
    {
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        //竖直布局
        var pageIndex = rowIndex / pageRowCount;
        var pageHeight = m_ScrollView.viewport.rect.height;
        rowIndex = rowIndex % pageRowCount;
        if (m_ExpandHeight)
        {
            SetChildAlongAxis(rect, 1, pageHeight * pageIndex + m_PagePadding.y * 0.5f + rowIndex * heightDelta, forceItemHeight);
        }
        else
        {
            SetChildAlongAxis(rect, 1, pageHeight * pageIndex + m_PagePadding.y * 0.5f + rowIndex * heightDelta);
        }

        //水平布局   
        if (m_ExpandWidth)
        {
            SetChildAlongAxis(rect, 0, columnIndex * widthDelta + m_PagePadding.x * 0.5f, forceItemWidth);
        }
        else
        {
            SetChildAlongAxis(rect, 0, columnIndex * widthDelta + m_PagePadding.x * 0.5f);
        }
    }

    #endregion
}