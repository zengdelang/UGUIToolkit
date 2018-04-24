using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 动态竖直布局(支持多种不同item以及高度不一的item), 支持ScrollView竖直滑动的时候使用有限的几个item不断复用进行布局
/// </summary>
public class DynamicVerticalLayout : DynamicFlexibleLayout
{
    [SerializeField] protected bool m_ExpandWidth; //强制Item的宽度自适应可视区域的宽,竖直滑动的时候有效

    [SerializeField] protected BoolUnityEvent m_ArrowUpEvent = new BoolUnityEvent(); //向上箭头指示
    [SerializeField] protected BoolUnityEvent m_ArrowDownEvent = new BoolUnityEvent(); //向下箭头指示

    protected float m_FisrtItemTopHeight;        //第一个可视item的上边缘距离content上边缘的高度
    protected float m_LastItemBottomHeight;      //最后一个可视item的下边缘距离content上边缘的高度

    public bool expandWidth
    {
        get { return m_ExpandWidth; }
        set
        {
            m_ExpandWidth = value;
            RefreshCurrentItem();
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

    /// <summary>
    /// 检查能否拖拽
    /// </summary>
    protected override void CheckAutoDrag()
    {
        if (m_AutoDrag)
        {
            if (m_ScrollView != null)
            {
                m_ScrollView.enabled = !(m_FirstPosition == 0 && m_FirstPosition + m_ItemViewChildren.Count == m_Adapter.GetCount() &&
                    m_ScrollView.content.rect.height - 0.2f < m_ScrollView.viewport.rect.height);//添加0.2f避免相等时候的浮点数误差
            }
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
            if (m_FirstPosition == 0 && m_FirstPosition + m_ItemViewChildren.Count == m_Adapter.GetCount() &&
                m_ScrollView.content.rect.height - 0.2f < m_ScrollView.viewport.rect.height)
            {
                return;
            }

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

    /// <summary>
    /// 刷新所有item，布局也从content上面开始重新布局
    /// </summary>
    public override void RefreshAllItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        m_FirstPosition = 0;
        m_FisrtItemTopHeight = m_Padding.top - divider;
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

        m_LastItemBottomHeight = m_FisrtItemTopHeight + divider;

        var itemCount = m_Adapter.GetCount();
        var preContentTopY = m_ScrollView.content.anchoredPosition.y;
        if (m_FirstPosition >= itemCount)
        {
            m_FirstPosition = itemCount;
            var bottomY = preContentTopY + m_ScrollView.viewport.rect.height;
            var contentHeight = m_ScrollView.content.rect.height;
            if (bottomY >= contentHeight - 0.2f) //增加一个0.2f误差，避免浮点数判断误差
            {
                bottomY = contentHeight - m_Padding.bottom - divider;
            }

            m_FisrtItemTopHeight = bottomY + divider;
            m_LastItemBottomHeight = m_FisrtItemTopHeight;
        }

        PerformLayout();

        var curContentTopY = m_ScrollView.content.anchoredPosition.y;
        if (!m_ScrollView.dragging && m_ScrollView.velocity == Vector2.zero || (preContentTopY > 0 && curContentTopY < 0))
        {
            var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
            var contentHeight = m_ScrollView.content.rect.height;
            if (bottomY > contentHeight || m_ScrollView.content.anchoredPosition.y < 0)
            {
                var oldPos = m_ScrollView.content.anchoredPosition;
                oldPos.y = Mathf.Max(0, contentHeight - m_ScrollView.viewport.rect.height);
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
                var preItemHeight = itemView.rectTransform.rect.height;
                m_Adapter.ProcessItemView(index, itemView, this);
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemView.rectTransform);
                if (!Mathf.Approximately(preItemHeight, itemView.rectTransform.rect.height))
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

        if (!m_ScrollView.vertical || m_ScrollView.horizontal)
        {
            Debug.LogError("DynamicVerticalLayout只支持ScrollView为竖直滑动的时候才生效");
            return;
        }

        PerformLayout();
        CheckLoadMore(newPos.y > oldPos.y);
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

        var contentTopY = m_ScrollView.content.anchoredPosition.y;
        while (m_ItemViewChildren.Count > 0)
        {
            //从队列头部开始检查哪些item需要被回收
            var itemTransform = m_ItemViewChildren.PeekFirst().rectTransform;
            var bottomY = itemTransform.anchoredPosition.y - itemTransform.rect.height + contentTopY;
            if (bottomY <= 0)
            {
                break;
            }

            var itemView = m_ItemViewChildren.DequeueFirst();
            m_RecycleBin.AddScrapView(itemView);
            ++m_FirstPosition;
            m_FisrtItemTopHeight = m_FisrtItemTopHeight + itemView.rectTransform.rect.height + divider;
        }

        var viewportBottomY = -m_ScrollView.viewport.rect.height;
        while (m_ItemViewChildren.Count > 0)
        {
            //从队列尾部开始检查哪些item需要被回收
            var itemTransform = m_ItemViewChildren.PeekLast().rectTransform;
            var topY = itemTransform.anchoredPosition.y + contentTopY;
            if (topY > viewportBottomY)
            {
                break;
            }

            var itemView = m_ItemViewChildren.DequeueLast();
            m_RecycleBin.AddScrapView(itemView);
            m_LastItemBottomHeight = m_LastItemBottomHeight - itemView.rectTransform.rect.height - divider;
        }
    }

    protected void GetFirstItemTopHeight()
    {
        // 更新第一个可视item的上边缘距离content上边缘的高度
        if (m_ItemViewChildren.Count > 0)
        {
            var itemTransform = m_ItemViewChildren.PeekFirst().rectTransform;
            m_FisrtItemTopHeight = -itemTransform.anchoredPosition.y - divider;
        }
    }

    protected void GetLastItemBottomHeight()
    {
        //更新最后一个可视item的上边缘坐标距离content上边缘的高度
        if (m_ItemViewChildren.Count > 0)
        {
            var itemTransform = m_ItemViewChildren.PeekLast().rectTransform;
            m_LastItemBottomHeight = -itemTransform.anchoredPosition.y + itemTransform.rect.height + divider;
        }
    }

    protected void FillItem()
    {
        var content = m_ScrollView.content;
        var contentTopY = content.anchoredPosition.y;
        var viewportBottomY = m_ScrollView.viewport.rect.height;

        GetLastItemBottomHeight();
        var itemCount = m_Adapter.GetCount();
        var lastItemIndex = m_FirstPosition + m_ItemViewChildren.Count;  //待添加的最后一个item的索引
        var bottomHeight = m_LastItemBottomHeight - contentTopY;
        while (lastItemIndex < itemCount && bottomHeight < viewportBottomY)     //向下填充
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
            var itemHeight = itemRect.rect.height;

            //更新m_LastItemBottomHeight
            m_LastItemBottomHeight += itemHeight;
            bottomHeight = m_LastItemBottomHeight - contentTopY;
            if (bottomHeight < 0) //如果生成的item的在viewport的上方则重新放回缓存
            {
                m_RecycleBin.AddScrapView(itemView);
                //一次滑动很大距离时候m_firstPosition需要更新    
                m_FirstPosition = lastItemIndex + 1;
            }
            else
            {
                m_ItemViewChildren.EnqueueLast(itemView);
                SetItemVisible(itemRect, true);
                SetItemPosition(itemRect, m_LastItemBottomHeight - itemHeight);
            }

            ++lastItemIndex;
            m_LastItemBottomHeight += divider;
            bottomHeight += divider;
        }

        AdjustContentHeight();

        GetFirstItemTopHeight();
        var topHeight = m_FisrtItemTopHeight - contentTopY;
        while (m_FirstPosition > 0 && topHeight > 0)          //向上填充
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
            var itemHeight = itemRect.rect.height;

            //更新m_FisrtItemTopHeight
            m_FisrtItemTopHeight -= itemHeight;
            topHeight = m_FisrtItemTopHeight - contentTopY;
            if (topHeight > viewportBottomY)   //如果生成的item的在viewport的下方则重新放回缓存
            {
                m_RecycleBin.AddScrapView(itemView);
            }
            else
            {
                m_ItemViewChildren.EnqueueFirst(itemView);
                SetItemVisible(itemRect, true);
                SetItemPosition(itemRect, m_FisrtItemTopHeight);
            }

            --m_FirstPosition;
            m_FisrtItemTopHeight -= divider;
            topHeight -= divider;        }

        AdjustContentPosition();
    }

    //调整content高度
    protected void AdjustContentHeight()
    {
        //最后一个可视item下边缘在content的下方，调整content的高度
        var itemCount = m_Adapter.GetCount();
        var lastItemIndex = m_FirstPosition + m_ItemViewChildren.Count;
        var bottomHeight = m_LastItemBottomHeight;
        var content = m_ScrollView.content;

        if (lastItemIndex + 1 >= itemCount)
        {
            bottomHeight = bottomHeight - divider + m_Padding.bottom;
        }

        //第二个条件，如果是最后一个item，并且这个item的下边缘(加m_Padding.bottom的下边缘)在content的下边缘上方，调整高度
        var contentHeight = content.rect.height;
        if (bottomHeight > contentHeight || (lastItemIndex + 1 >= itemCount && bottomHeight < contentHeight))
        {
            SetContentHeight(bottomHeight);
        }
    }

    protected void AdjustContentPosition()
    {
        //第一个可视item上边缘在content的上方，向上移动content的位置同时拉长content的高度
        var topHeight = m_FisrtItemTopHeight;
        if (m_FirstPosition == 0)
        {
            topHeight = topHeight + divider - m_Padding.top;
        }

        //第二个条件，如果是第一个item，并且这个item的上边缘(加m_Padding.top的上边缘)在content的上边缘下方，调整content的位置和高度
        if (topHeight < 0 || (m_FirstPosition == 0 && topHeight > 0))
        {
            //更新content的位置
            SetContentPosition(new Vector2(0, m_ScrollView.content.anchoredPosition.y - topHeight));
            //更新content的高度
            SetContentHeight(m_ScrollView.content.rect.height - topHeight);

            //重新调整item的位置
            var itemPos = m_FirstPosition == 0 ? m_Padding.top : divider;
            for (int i = 0, count = m_ItemViewChildren.Count; i < count; i++)
            {
                var itemView = m_ItemViewChildren.GetElement(i);
                SetChildAlongAxis(itemView.rectTransform, 1, itemPos);
                itemPos = itemPos + divider + itemView.rectTransform.rect.height;
            }

            //回收多余的item
            RecycleItem();
        }
    }

    protected void SetContentPosition(Vector2 newPos)
    {
        var content = m_ScrollView.content;
        var oldPos = content.anchoredPosition;
        var delta = content.anchoredPosition - newPos;
        content.anchoredPosition = newPos;

        //防止拖拽发生过大的偏移，由于修改了当前的content的位置和初始开始拖拽的位置会发生过大偏差，修正这个偏差
        var contentStartPosition = m_ScrollView.contentStartPosition;
        contentStartPosition -= delta;
        m_ScrollView.contentStartPosition = contentStartPosition;

        //修正上一次的prevPosition的位置，这个用于计算滑动的速度
        var prevPosition = m_ScrollView.prevPosition;
        prevPosition -= delta;
        m_ScrollView.prevPosition = prevPosition;

        if (oldPos.y < 0 && newPos.y > 0 && m_ScrollView.velocity.y > 0)
        {
            m_ScrollView.InverseVelocity();
        }
    }

    protected void SetContentHeight(float contentHeight)
    {
        var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
        var prevContentHeight = m_ScrollView.content.rect.height;
        if (bottomY > prevContentHeight && bottomY < contentHeight && m_ScrollView.velocity.y < 0)
        {
            m_ScrollView.InverseVelocity();
        }
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
    }

    protected void SetItemPosition(RectTransform rect, float pos)
    {
        if (rect == null)
            return;

        rect.pivot = new Vector2(0, 1);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        //水平布局
        if (m_ExpandWidth)
        {
            SetChildAlongAxis(rect, 0, m_Padding.left, m_ScrollView.viewport.rect.width - m_Padding.horizontal);
        }
        else
        {
            SetChildAlongAxis(rect, 0, m_Padding.left);
        }

        //竖直布局
        SetChildAlongAxis(rect, 1, pos);
    }
}