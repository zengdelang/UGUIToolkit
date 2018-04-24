using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 动态水平布局(支持多种不同item以及高度不一的item), 支持ScrollView水平滑动的时候使用有限的几个item不断复用进行布局
/// </summary>
public class DynamicHorizontalLayout : DynamicFlexibleLayout
{
    [SerializeField] protected bool m_ExpandHeight;     //强制Item的高度自适应可视区域的高

    protected float m_FisrtItemLeftWidth;       //第一个可视item的左边缘距离content左边缘的宽度
    protected float m_LastItemRightWidth;       //最后一个可视item的右边缘距离content左边缘的宽度

    [SerializeField] protected BoolUnityEvent m_ArrowLeftEvent = new BoolUnityEvent(); //向左箭头指示
    [SerializeField] protected BoolUnityEvent m_ArrowRightEvent = new BoolUnityEvent(); //向右箭头指示

    public bool expandHeight
    {
        get { return m_ExpandHeight; }
        set
        {
            m_ExpandHeight = value;
            RefreshCurrentItem();
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

    protected override void CheckAutoDrag()
    {
        if (m_AutoDrag)
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = !(m_FirstPosition == 0 && m_FirstPosition + m_ItemViewChildren.Count == m_Adapter.GetCount() &&
                    m_ScrollView.content.rect.width - 0.2f < m_ScrollView.viewport.rect.width);//添加0.2f避免相等时候的浮点数误差
        }
        else
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = true;
        }
    }

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

    protected override void CheckCanLockState()
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableCanLockEvent)
        {
            if (m_FirstPosition == 0 && m_FirstPosition + m_ItemViewChildren.Count == m_Adapter.GetCount() &&
                m_ScrollView.content.rect.width - 0.2f < m_ScrollView.viewport.rect.width)
                return;

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

    /// <summary>
    /// 刷新所有item，布局也从content上面开始重新布局
    /// </summary>
    public override void RefreshAllItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        m_FirstPosition = 0;
        m_FisrtItemLeftWidth = m_Padding.left - divider;
        SetContentPosition(Vector2.zero);
        m_ScrollView.StopMovement();

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
            m_RecycleBin.AddScrapView(m_ItemViewChildren.Dequeue());
        }

        m_LastItemRightWidth = m_FisrtItemLeftWidth + divider;

        var itemCount = m_Adapter.GetCount();
        var preContentLeftX = -m_ScrollView.content.anchoredPosition.x;
        if (m_FirstPosition >= itemCount)
        {
            m_FirstPosition = itemCount;
            var rightY = m_ScrollView.viewport.rect.width + preContentLeftX;
            var contentWidth = m_ScrollView.content.rect.width;
            if (rightY >= contentWidth - 0.2f) //增加一个0.2f误差，避免浮点数判断误差
            {
                rightY = contentWidth - m_Padding.right - divider;
            }

            m_FisrtItemLeftWidth = rightY + divider;
            m_LastItemRightWidth = m_FisrtItemLeftWidth;
        }

        PerformLayout();

        var curContentLeftX = -m_ScrollView.content.anchoredPosition.x;
        if (!m_ScrollView.dragging && m_ScrollView.velocity == Vector2.zero || (preContentLeftX > 0 && curContentLeftX < 0))
        {
            //计算当前在viewport右边缘处content的X坐标
            var viewportWidth = m_ScrollView.viewport.rect.width;
            var viewportRightX = viewportWidth + curContentLeftX;
            var contentWidth = m_ScrollView.content.rect.width;
            if (viewportRightX > contentWidth || curContentLeftX < 0)
            {
                var oldPos = m_ScrollView.content.anchoredPosition;
                oldPos.x = Mathf.Min(0, viewportWidth - contentWidth);
                SetContentPosition(oldPos);
                PerformLayout();
            }
        }

        CheckLoadMore(true);
    }

    /// <summary>
    /// 刷新index对应item的UI，item必须是可视的item，否则不进行处理
    /// </summary>
    /// <param name="index"></param>
    public override void RefreshItem(int index)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (index >= m_FirstPosition && index < m_FirstPosition + m_ItemViewChildren.Count)
        {
            var itemIndex = index - m_FirstPosition;
            var itemView = m_ItemViewChildren.GetElement(index - m_FirstPosition);
            var itemViewType = m_Adapter.GetItemViewType(index);
            var needLayout = true;

            if (itemView.ViewType == itemViewType)
            {
                var preItemWidth = itemView.rectTransform.rect.width;
                m_Adapter.ProcessItemView(index, itemView, this);
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemView.rectTransform);
                if (!Mathf.Approximately(preItemWidth, itemView.rectTransform.rect.width))
                {
                    itemIndex++;
                }
                else
                {
                    needLayout = false;
                }
            }

            if (needLayout)
            {
                for (int i = m_ItemViewChildren.Count - 1; i >= itemIndex; --i)
                {
                    m_RecycleBin.AddScrapView(m_ItemViewChildren.DequeueLast());
                }
                PerformLayout();
            }

            CheckLoadMore(true);
        }
    }

    public override void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        if (m_ScrollView == null || m_Adapter == null || m_Adapter.IsEmpty())
            return;

        if (!m_ScrollView.horizontal || m_ScrollView.vertical)
        {
            Debug.LogError("DynamicHorizontalLayout只支持ScrollView为水平滑动的时候才生效");
            return;
        }

        PerformLayout();
        CheckLoadMore(newPos.x < oldPos.x);
    }

    protected void PerformLayout()
    {
        if (m_Adapter.GetCount() == 0)
            return;
        RecycleItem();
        FillItem();

        CheckAutoDrag();
        CheckArrowHint();
        CheckCanLockState();
    }

    protected void RecycleItem()
    {
        if (m_ItemViewChildren.Count == 0)
            return;

        var contentLeftX = -m_ScrollView.content.anchoredPosition.x;
        while (m_ItemViewChildren.Count > 0)
        {
            //从队列头部开始检查哪些item需要被回收
            var itemTransform = m_ItemViewChildren.PeekFirst().rectTransform;
            var rightY = itemTransform.anchoredPosition.x + itemTransform.rect.width;
            if (rightY >= contentLeftX)
            {
                break;
            }

            var itemView = m_ItemViewChildren.DequeueFirst();
            m_RecycleBin.AddScrapView(itemView);
            ++m_FirstPosition;
            m_FisrtItemLeftWidth = m_FisrtItemLeftWidth + itemView.rectTransform.rect.width + divider;
        }

        var viewportRightY = contentLeftX + m_ScrollView.viewport.rect.width;
        while (m_ItemViewChildren.Count > 0)
        {
            //从队列尾部开始检查哪些item需要被回收
            var itemTransform = m_ItemViewChildren.PeekLast().rectTransform;
            var leftY = itemTransform.anchoredPosition.x;
            if (leftY < viewportRightY)
            {
                break;
            }

            var itemView = m_ItemViewChildren.DequeueLast();
            m_RecycleBin.AddScrapView(itemView);
            m_LastItemRightWidth = m_LastItemRightWidth - itemView.rectTransform.rect.width - divider;
        }
    }

    protected void GetFirstItemLeftWidth()
    {
        // 更新第一个可视item的左边缘距离content左边缘的宽度
        if (m_ItemViewChildren.Count > 0)
        {
            var itemTransform = m_ItemViewChildren.PeekFirst().rectTransform;
            m_FisrtItemLeftWidth = itemTransform.anchoredPosition.x - divider;
        }
    }

    protected void GetLastItemRightWidth()
    {
        //更新最后一个可视item的左边缘坐标距离content左边缘的宽度
        if (m_ItemViewChildren.Count > 0)
        {
            var itemTransform = m_ItemViewChildren.PeekLast().rectTransform;
            m_LastItemRightWidth = itemTransform.anchoredPosition.x + itemTransform.rect.width + divider;
        }
    }

    protected void FillItem()
    {
        var content = m_ScrollView.content;
        var contentLeftX = -content.anchoredPosition.x;
        var viewportRightX = contentLeftX + m_ScrollView.viewport.rect.width;

        GetLastItemRightWidth();
        var itemCount = m_Adapter.GetCount();
        var lastItemIndex = m_FirstPosition + m_ItemViewChildren.Count;  //待添加的最后一个item的索引

        var rightWidth = m_LastItemRightWidth;
        while (lastItemIndex < itemCount && rightWidth < viewportRightX)     //向右填充
        {
            //得到item并刷新item的内容
            var itemView = m_RecycleBin.GetScrapView(lastItemIndex);
            RectTransform itemRect = itemView != null ? itemView.rectTransform : null;
            if (itemView == null)
            {
                itemView = m_Adapter.GetItemView(lastItemIndex, m_ScrollView.content, this);
                itemRect = itemView.rectTransform;
                itemRect.SetParent(content);
            }

            m_Adapter.ProcessItemView(lastItemIndex, itemView, this);

            //强制立即布局计算
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
            var itemWidth = itemRect.rect.width;

            //更新m_LastItemRightWidth
            m_LastItemRightWidth += itemWidth;
            rightWidth = m_LastItemRightWidth;

            if (rightWidth <= contentLeftX) //如果生成的item的在viewport的左边则重新放回缓存
            {
                m_RecycleBin.AddScrapView(itemView);
                //一次滑动很大距离时候m_firstPosition需要更新    
                m_FirstPosition = lastItemIndex + 1;
            }
            else
            {
                m_ItemViewChildren.EnqueueLast(itemView);
                SetItemVisible(itemRect, true);
                SetItemPosition(itemRect, rightWidth - itemWidth);
            }

            ++lastItemIndex;
            m_LastItemRightWidth += divider;
            rightWidth = m_LastItemRightWidth;
        }

        AdjustContentWidth();

        GetFirstItemLeftWidth();
        var leftWidth = m_FisrtItemLeftWidth;
        while (m_FirstPosition > 0 && leftWidth > contentLeftX)          //向左填充
        {
            var firstItemIndex = m_FirstPosition - 1;

            //得到item并刷新item的内容
            var itemView = m_RecycleBin.GetScrapView(firstItemIndex);
            RectTransform itemRect = itemView != null ? itemView.rectTransform : null;
            if (itemView == null)
            {
                itemView = m_Adapter.GetItemView(firstItemIndex, m_ScrollView.content, this);
                itemRect = itemView.rectTransform;
                itemRect.SetParent(content);
            }

            m_Adapter.ProcessItemView(firstItemIndex, itemView, this);

            //强制立即布局计算
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
            var itemWidth = itemRect.rect.width;

            //更新m_FisrtItemLeftWidth
            m_FisrtItemLeftWidth -= itemWidth;
            leftWidth = m_FisrtItemLeftWidth;
            if (leftWidth >= viewportRightX)   //如果生成的item的在viewport的右边则重新放回缓存
            {
                m_RecycleBin.AddScrapView(itemView);
            }
            else
            {
                m_ItemViewChildren.EnqueueFirst(itemView);
                SetItemVisible(itemRect, true);
                SetItemPosition(itemRect, m_FisrtItemLeftWidth);
            }

            --m_FirstPosition;
            m_FisrtItemLeftWidth -= divider;
            leftWidth = m_FisrtItemLeftWidth;
        }

        AdjustContentPosition();
    }

    //调整content宽度
    protected void AdjustContentWidth()
    {
        //最后一个可视item右边缘在content的右方，调整content的宽度
        var itemCount = m_Adapter.GetCount();
        var lastItemIndex = m_FirstPosition + m_ItemViewChildren.Count;
        var rightWidth = m_LastItemRightWidth;
        var content = m_ScrollView.content;

        if (lastItemIndex + 1 >= itemCount)
        {
            rightWidth = rightWidth - divider + m_Padding.right;
        }

        //第二个条件，如果是最后一个item，并且这个item的右边缘(加m_Padding.right的右边缘)在content右边缘的左边，调整宽度
        var contentWidth = content.rect.width;
        if (rightWidth > contentWidth || (lastItemIndex + 1 >= itemCount && rightWidth < contentWidth))
        {
            SetContentWidth(rightWidth);
        }
    }

    protected void AdjustContentPosition()
    {
        //第一个可视item左边缘在content的左边，向左移动content的位置同时拉长content的宽度
        var leftWidth = m_FisrtItemLeftWidth;
        if (m_FirstPosition == 0)
        {
            leftWidth = leftWidth + divider - m_Padding.left;
        }

        //第二个条件，如果是第一个item，并且这个item的左边缘(加m_Padding.left的左边缘)在content的左边缘右边，调整content的位置和宽度
        if (leftWidth < 0 || (m_FirstPosition == 0 && leftWidth > 0))
        {
            //更新content的位置
            SetContentPosition(new Vector2(m_ScrollView.content.anchoredPosition.x + leftWidth, 0));
            //更新content的宽度
            SetContentWidth(m_ScrollView.content.rect.width - leftWidth);

            //重新调整item的位置
            var itemPos = m_FirstPosition == 0 ? m_Padding.left : divider;
            for (int i = 0, count = m_ItemViewChildren.Count; i < count; i++)
            {
                var itemView = m_ItemViewChildren.GetElement(i);
                SetChildAlongAxis(itemView.rectTransform, 0, itemPos);
                itemPos = itemPos + divider + itemView.rectTransform.rect.width;
            }

            //回收多余的item
            RecycleItem();
        }
    }

    protected void SetContentPosition(Vector2 newPos)
    {
        var content = m_ScrollView.content;
        var oldPos = content.anchoredPosition;
        var deltaWidth = oldPos.x - newPos.x;
        content.anchoredPosition = newPos;

        //防止拖拽发生过大的偏移，由于修改了当前的content的位置和初始开始拖拽的位置会发生过大偏差，修正这个偏差
        var contentStartPosition = m_ScrollView.contentStartPosition;
        contentStartPosition.x -= deltaWidth;
        m_ScrollView.contentStartPosition = contentStartPosition;

        //修正上一次的prevPosition的位置，这个值用于计算滑动的速度
        var prevPosition = m_ScrollView.prevPosition;
        prevPosition.x -= deltaWidth;
        m_ScrollView.prevPosition = prevPosition;

        if (oldPos.x > 0 && newPos.x < 0 && m_ScrollView.velocity.x < 0)
        {
            m_ScrollView.InverseVelocity();
        }
    }

    protected void SetContentWidth(float contentWidth)
    {
        var rightX = m_ScrollView.viewport.rect.width - m_ScrollView.content.anchoredPosition.x;
        var prevContentWidth = m_ScrollView.content.rect.width;
        if (rightX > prevContentWidth && rightX < contentWidth && m_ScrollView.velocity.x > 0)
        {
            m_ScrollView.InverseVelocity();
        }
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
    }

    protected void SetItemPosition(RectTransform rect, float pos)
    {
        if (rect == null)
            return;

        rect.pivot = new Vector2(0, 1);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        //水平布局
        SetChildAlongAxis(rect, 0, pos);

        //竖直布局
        if (m_ExpandHeight)
        {
            SetChildAlongAxis(rect, 1, m_Padding.top, m_ScrollView.viewport.rect.height - m_Padding.vertical);
        }
        else
        {
            SetChildAlongAxis(rect, 1, m_Padding.top);
        }
    }

}