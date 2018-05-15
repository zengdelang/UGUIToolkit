using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 水平分页网格布局, 支持ScrollView水平滑动的时候使用有限的几个item不断复用进行网格布局
/// </summary>
public class HorizontalPageLayout : DynamicLayout
{
    protected class ColumnLayoutParam : LayoutParam
    {
        public int column;         //item所属的列
        public int index;          //item的索引
    }

    [Serializable]
    public class IntUnityEvent : UnityEvent<int>
    {

    }

    [Serializable]
    public class Int2UnityEvent : UnityEvent<int,int>
    {

    }

    [SerializeField] protected int  m_Row;              //布局信息,Item显示的行数
    [SerializeField] protected bool m_ExpandHeight;     //强制Item的高度自适应可视区域的高

    [SerializeField] protected BoolUnityEvent m_ArrowLeftEvent = new BoolUnityEvent(); //向左箭头指示
    [SerializeField] protected BoolUnityEvent m_ArrowRightEvent = new BoolUnityEvent(); //向右箭头指示

    [SerializeField] protected bool m_ExpandWidth;      //强制Item的高度自适应可视区域的宽
    [SerializeField] protected int  m_PageRow;          //布局信息,每一页Item显示的行数              
    [SerializeField] protected int  m_PageColumn;       //布局信息,每一页Item显示的列数
    [SerializeField] protected Vector2 m_PagePadding;   //x代表页的水平padding, y代表是竖直padding

    protected int m_FirstVisibleColumn = 0;  //当前区域第一个可见的列, 从0开始
    protected int m_LastVisibleColumn = -1;  //当前区域最后一个可见的列
    protected int m_TotalColumn = 0;         //可显示的总的列数

    protected int m_PageItemCount;           //一页所能容纳的item数目

    [SerializeField] protected IntUnityEvent m_PageCountChangedEvent = new IntUnityEvent(); //总的页数变换事件
    protected int m_PageCount;               //总的页数

    [SerializeField] protected Int2UnityEvent m_PageIndexChangedEvent = new Int2UnityEvent(); //当前可视页的索引变化事件
    protected int m_FirstPageIndex = -1;     //第一个可视的页  

    public int row
    {
        get
        {
            return m_Row <= 0 ? 1 : m_Row;
        }
        set
        {
            m_Row = Mathf.Clamp(m_Row, 1, int.MaxValue);
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

    public BoolUnityEvent arrowLeftEvent
    {
        get { return m_ArrowLeftEvent; }
        set { m_ArrowLeftEvent = value; }
    }

    public BoolUnityEvent arrowRightEvent
    {
        get { return m_ArrowRightEvent; }
        set { m_ArrowRightEvent = value; }
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

        m_FirstVisibleColumn = 0;
        m_LastVisibleColumn = -1;
        CalculateWidth();
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
            var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
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
    /// <param name="factor">[0, 1] 0显示的viewport的最左面， 1显示的viewport的最右面， 0-1之间按比例显示在viewport中间</param>
    public void ScrollToItem(int itemIndex, bool resetStartPos = false, bool useAnimation = true, float factor = 0.5f)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (itemIndex < 0 || itemIndex >= m_Adapter.GetCount())
            return;

        var viewportWidth = m_ScrollView.viewport.rect.width;
        var contentMinPos = viewportWidth - m_ScrollView.content.rect.width;
        if (contentMinPos > 0)
            contentMinPos = 0;
        var contentMaxPos = 0;

        var pageColumnCount = pageColumn;
        var pageIndex = itemIndex / m_PageItemCount;
        var pageItemIndex = itemIndex % m_PageItemCount;
        var columnIndex = pageItemIndex % pageColumnCount;
        var itemWidth = (viewportWidth + spacing.x - m_PagePadding.x) / pageColumnCount;
        var targetPosX = -pageIndex * viewportWidth - m_PagePadding.x * 0.5f - itemWidth * columnIndex;
        var deltaWidth = viewportWidth - itemWidth;
        targetPosX += Mathf.Clamp01(factor) * deltaWidth;
        targetPosX = Mathf.Clamp(targetPosX, contentMinPos, contentMaxPos);

        m_ScrollView.StartAnimation(new Vector2(targetPosX, 0), resetStartPos, useAnimation);
    }

    /// <summary>
    /// 定位到某一页
    /// </summary>
    /// <param name="pageIndex"></param>
    public void ScrollToPage(int pageIndex, bool resetStartPos = false, bool useAnimation = true)
    {
        if (pageIndex < 0 || pageIndex >= m_PageCount)
            return;

        m_ScrollView.StartAnimation(new Vector2(-pageIndex * m_ScrollView.viewport.rect.width, 0), resetStartPos, useAnimation);
    }

    /// <summary>
    /// 检查能否拖拽
    /// </summary>
    protected override void CheckAutoDrag()
    {
        if (m_AutoDrag)
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = m_ScrollView.content.rect.width > (m_ScrollView.viewport.rect.width + 0.2f); //添加0.2f避免相等时候的浮点数误差
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
            //anchoredPosition.x停留在左方可能存在一些误差,不完全等于0，设置当小于等于-0.13有向左箭头指示
            var contentLeftX = m_ScrollView.content.anchoredPosition.x;
            m_ArrowLeftEvent.Invoke(contentLeftX <= -0.13f);

            //计算当前在viewport右边缘处content的X坐标
            var viewportRightX = m_ScrollView.viewport.rect.width;
            var contentRightX = m_ScrollView.content.rect.width + contentLeftX;
            m_ArrowRightEvent.Invoke((contentRightX) > (viewportRightX + 0.13f));
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
            if (m_ScrollView.content.rect.width > (m_ScrollView.viewport.rect.width + 0.2f))
            {
                var contentX = m_ScrollView.content.anchoredPosition.x;
                if (m_IsLock && !m_ScrollView.dragging && contentX >= 0)
                {
                    isLock = false;
                    return;
                }

                if (contentX < 0)
                {
                    isLock = true;
                }
            }
        }
    }

    /// <summary>
    /// 检查加载更多处理
    /// </summary>
    protected override void CheckLoadMore(bool moveLeft)
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableLoadMoreEvent)
        {
            if (moveLeft)
            {
                //计算当前在viewport右边缘处content的X坐标
                var contentRightX = m_ScrollView.content.rect.width + m_ScrollView.content.anchoredPosition.x;
                if (contentRightX < m_ScrollView.viewport.rect.width)
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

        if (!m_ScrollView.horizontal || m_ScrollView.vertical)
        {
            Debug.LogError("HorizontalPageLayout只支持ScrollView为水平滑动的时候才生效");
            return;
        }

        PerformLayout();
        CheckArrowHint();
        CheckCanLockState();
        CheckLoadMore(newPos.x < oldPos.x);
    }

    protected void CalculatePageIndex()
    {
        var deltaWidthToLeft = -m_ScrollView.content.anchoredPosition.x;
        deltaWidthToLeft = Mathf.Clamp(deltaWidthToLeft, 0, m_ScrollView.content.rect.width);

        var pageIndex = Mathf.FloorToInt((deltaWidthToLeft + 0.2f) / m_ScrollView.viewport.rect.width);
        pageIndex = Mathf.Clamp(pageIndex, 0, m_PageCount - 1);
        firstPageIndex = pageIndex;
    }

    /// <summary>
    /// 计算ScrollView的Content的宽度
    /// </summary>
    protected void CalculateWidth()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        var itemCount = m_Adapter.GetCount();
        m_PageItemCount = pageColumn * pageRow;
        var totalPage = itemCount / m_PageItemCount;
        totalPage += itemCount % m_PageItemCount > 0 ? 1 : 0;
        pageCount = totalPage;

        var viewportWidth = m_ScrollView.viewport.rect.width;
        var contentWidth = viewportWidth * m_PageCount;
        SetContentWidth(contentWidth);
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, m_ScrollView.viewport.rect.height);

        if (!m_ScrollView.dragging && m_ScrollView.velocity == Vector2.zero)
        {
            var contentLeftX = m_ScrollView.content.anchoredPosition.x;
            var viewportRightX = viewportWidth - contentLeftX;
            if (viewportRightX > contentWidth)
            {
                var oldPos = m_ScrollView.content.anchoredPosition;
                oldPos.x = Mathf.Min(0, viewportWidth - contentWidth);
                SetContentPosition(oldPos);
            }
            else if (contentLeftX > 0.2)
            {
                SetContentPosition(Vector2.zero);
            }
        }

        CheckAutoDrag();
        CheckArrowHint();
        CheckLoadMore(true);
    }

    protected void SetContentWidth(float contentWidth)
    {
        var rightX = m_ScrollView.viewport.rect.width - m_ScrollView.content.anchoredPosition.x;
        var preContentWidth = m_ScrollView.content.rect.width;

        if (rightX > preContentWidth && rightX < contentWidth && m_ScrollView.velocity.x > 0)
        {
            m_ScrollView.InverseVelocity();
        }
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
    }

    /// <summary>
    /// 执行布局处理,回收不显示的item, 添加需要显示的item到可视列表中
    /// </summary>
    protected void PerformLayout()
    {
        if (m_ScrollView == null)
            return;

        int newFirstVisibleColumn = 0;
        int newLastVisibleColumn = 0;
        GetVisibleColumnRange(ref newFirstVisibleColumn, ref newLastVisibleColumn);
        //新的可视列区域和旧的不同时,重新计算需要显示的item
        if (newFirstVisibleColumn != m_FirstVisibleColumn || newLastVisibleColumn != m_LastVisibleColumn)
        {
            //如果新的第一个可视列大于旧的,从队列头部移除需要隐藏的item
            if (m_FirstVisibleColumn < newFirstVisibleColumn)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekFirst();
                    var layoutParam = itemView.layoutParam as ColumnLayoutParam;
                    if (layoutParam.column >= newFirstVisibleColumn)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueFirst());
                }
            }

            //如果新的最后一个可视列小于旧的,从队列尾部移除需要隐藏的item
            if (m_LastVisibleColumn > newLastVisibleColumn)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekLast();
                    var layoutParam = itemView.layoutParam as ColumnLayoutParam;
                    if (layoutParam.column <= newLastVisibleColumn)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueLast());
                }
            }

            var pageRowCount = pageRow;             //页的行数,pageRow是个属性提前在这算下,避免循环中多次计算
            var pageColumnCount = pageColumn;       //页的列数,pageColumn是个属性提前在这算下,避免循环中多次计算
            var itemCount = m_Adapter.GetCount();   //item的总数,提前在这算下,避免循环中多次计算
            
            var widthDelta = (m_ScrollView.viewport.rect.width + spacing.x - m_PagePadding.x) / pageColumnCount;
            var heightDelta = (m_ScrollView.viewport.rect.height + spacing.y - m_PagePadding.y) / pageRowCount;

            var forceItemHeight = heightDelta - spacing.y;
            var forceItemWidth = widthDelta - spacing.x;

            //如果新的第一个可视列小于旧的,从队列头部添加新的item
            if (m_FirstVisibleColumn > newFirstVisibleColumn)
            {
                var lastAddItemColumn = m_FirstVisibleColumn - 1;
                if (newLastVisibleColumn < lastAddItemColumn)
                {
                    lastAddItemColumn = newLastVisibleColumn;
                }

                //添加新的item
                for (int i = lastAddItemColumn; i >= newFirstVisibleColumn; --i)
                {
                    for (int j = 0; j < pageRowCount; ++j)
                    {
                        var itemIndex = GetItemIndex(j, i, pageColumnCount);
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content);
                            layoutInfo.itemView.rectTransform.SetParent(m_ScrollView.content);
                            layoutInfo.layoutParam = new ColumnLayoutParam();
                        }

                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if(layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.column = i;    //把列信息存进去
                        layoutParam.index = itemIndex;
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueFirst(layoutInfo);
                        SetItemPosition(j, i, pageColumnCount, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight, forceItemWidth);
                    }
                }
            }

            //如果新的最后一个可视列大于旧的,从队列尾部添加新的item
            if (m_LastVisibleColumn < newLastVisibleColumn)
            {
                var firstAddItemColumn = m_LastVisibleColumn + 1;
                if (newFirstVisibleColumn > firstAddItemColumn)
                {
                    firstAddItemColumn = newFirstVisibleColumn;
                }

                //添加新的item
                for (int i = firstAddItemColumn; i <= newLastVisibleColumn; ++i)
                {
                    for (int j = 0; j < pageRowCount; ++j)
                    {
                        var itemIndex = GetItemIndex(j, i, pageColumnCount);
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content);
                            layoutInfo.itemView.rectTransform.SetParent(m_ScrollView.content);
                            layoutInfo.layoutParam = new ColumnLayoutParam();
                        }

                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if (layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.column = i;    //把列信息存进去
                        layoutParam.index = itemIndex;   
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueLast(layoutInfo);
                        SetItemPosition(j, i, pageColumnCount, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight, forceItemWidth);
                    }
                }
            }

            m_FirstVisibleColumn = newFirstVisibleColumn;
            m_LastVisibleColumn = newLastVisibleColumn;
        }

        CalculatePageIndex();
    }

    protected int GetItemIndex(int rowIndex, int columnIndex, int pageColumnCount)
    {
        var pageIndex = columnIndex / pageColumnCount;
        columnIndex = columnIndex % pageColumnCount;
        return pageIndex * m_PageItemCount + rowIndex * pageColumnCount + columnIndex;
    }

    /// <summary>
    /// 获取当前可视的列范围
    /// </summary>
    protected void GetVisibleColumnRange(ref int firstVisibleColumn, ref int lastVisibleColumn)
    {
        //content的左边距离viewport左边的宽度
        var deltaWidthToLeft = -m_ScrollView.content.anchoredPosition.x;
        var contentWidth = m_ScrollView.content.rect.width;
        var viewportWidth = m_ScrollView.viewport.rect.width;
        var maxDeltaWidth = contentWidth - viewportWidth;         //达到最右端的时候再向右,拖动不改变firstVisibleRow的值
        if (deltaWidthToLeft > maxDeltaWidth)
            deltaWidthToLeft = maxDeltaWidth;

        var pageWidth = viewportWidth;
        var firstVisiblePage = Mathf.FloorToInt(deltaWidthToLeft / pageWidth);

        var halfPagePadding = m_PagePadding.x * 0.5f;
        var extraWidthToLeft = deltaWidthToLeft - firstVisiblePage * viewportWidth - halfPagePadding;
        if (firstVisiblePage < 0)
        {
            firstVisiblePage = 0;
            extraWidthToLeft = halfPagePadding;
        }

        var pageColumnCount = pageColumn;
        var itemWidth = (viewportWidth - m_PagePadding.x + spacing.x) / pageColumnCount;
        firstVisibleColumn = firstVisiblePage * pageColumnCount + Mathf.FloorToInt(extraWidthToLeft / itemWidth);
        if (firstVisibleColumn < 0)
        {
            firstVisibleColumn = 0;
        }

        //content的左边距离viewport右边的宽度
        var deltaWidthToRight = viewportWidth + (deltaWidthToLeft < 0 ? 0 : deltaWidthToLeft);  //如果小于viewport的宽度,则设置为viewport的宽度
        var lastVisiblePage = Mathf.FloorToInt(deltaWidthToRight / pageWidth);
        var extraWidthToRight = deltaWidthToRight - lastVisiblePage * viewportWidth - halfPagePadding;
        lastVisibleColumn = lastVisiblePage * pageColumnCount + Mathf.CeilToInt(extraWidthToRight / itemWidth);
        lastVisibleColumn = Mathf.Clamp(lastVisibleColumn, 0, pageColumnCount * m_PageCount - 1);
    }

    /// <summary>
    /// 设置Item的位置
    /// </summary>
    protected void SetItemPosition(int rowIndex, int columnIndex, int pageColumnCount, RectTransform rect, float heightDelta, float widthDelta, float forceItemHeight, float forceItemWidth)
    {
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        //竖直布局
        if (m_ExpandHeight)
        {
            SetChildAlongAxis(rect, 1, m_PagePadding.y * 0.5f + rowIndex * heightDelta, forceItemHeight);
        }
        else
        {
            SetChildAlongAxis(rect, 1, m_PagePadding.y * 0.5f + rowIndex * heightDelta);
        }

        //水平布局
        var pageIndex = columnIndex / pageColumnCount;
        var pageWidth = m_ScrollView.viewport.rect.width;
        columnIndex = columnIndex % pageColumnCount;
        if (m_ExpandWidth)
        {
            SetChildAlongAxis(rect, 0, pageWidth * pageIndex + columnIndex * widthDelta + m_PagePadding.x * 0.5f, forceItemWidth);
        }
        else
        {
            SetChildAlongAxis(rect, 0, pageWidth * pageIndex + columnIndex * widthDelta + m_PagePadding.x * 0.5f);
        }
    }

    #endregion
}