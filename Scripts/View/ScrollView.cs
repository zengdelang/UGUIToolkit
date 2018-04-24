﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/Scroll View", 37)]
[SelectionBase]
[ExecuteInEditMode]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ScrollView : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler,
    IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
{
    public enum MovementType
    {
        Unrestricted, // Unrestricted movement -- can scroll forever
        Elastic, // Restricted but flexible -- can go past the edges, but springs back in place
        Clamped, // Restricted movement where it's not possible to go past the edges
    }

    public enum ScrollbarVisibility
    {
        Permanent,
        AutoHide,
        AutoHideAndExpandViewport,
    }

    [Serializable]
    public class ScrollRectEvent : UnityEvent<Vector2>
    {
    }

    [Serializable]
    public class ContentPosChangedEvent : UnityEvent<Vector2, Vector2>
    {
        
    }

    [SerializeField] private RectTransform m_Content;
 
    public RectTransform content
    {
        get { return m_Content; }
        set { m_Content = value; }
    }

    [SerializeField] private bool m_Horizontal = true;

    public bool horizontal
    {
        get { return m_Horizontal; }
        set { m_Horizontal = value; }
    }

    [SerializeField] private bool m_Vertical = true;

    public bool vertical
    {
        get { return m_Vertical; }
        set { m_Vertical = value; }
    }

    [SerializeField] private MovementType m_MovementType = MovementType.Elastic;

    public MovementType movementType
    {
        get { return m_MovementType; }
        set { m_MovementType = value; }
    }

    [SerializeField] private float m_Elasticity = 0.1f; // Only used for MovementType.Elastic

    public float elasticity
    {
        get { return m_Elasticity; }
        set { m_Elasticity = value; }
    }

    [SerializeField] private bool m_Inertia = true;

    public bool inertia
    {
        get { return m_Inertia; }
        set { m_Inertia = value; }
    }

    [SerializeField] private float m_DecelerationRate = 0.135f; // Only used when inertia is enabled

    public float decelerationRate
    {
        get { return m_DecelerationRate; }
        set { m_DecelerationRate = value; }
    }

    [SerializeField] private float m_ScrollSensitivity = 1.0f;

    public float scrollSensitivity
    {
        get { return m_ScrollSensitivity; }
        set { m_ScrollSensitivity = value; }
    }

    [SerializeField] private RectTransform m_Viewport;

    public RectTransform viewport
    {
        get { return m_Viewport; }
        set
        {
            m_Viewport = value;
            SetDirtyCaching();
        }
    }

    [SerializeField] private Scrollbar m_HorizontalScrollbar;

    public Scrollbar horizontalScrollbar
    {
        get { return m_HorizontalScrollbar; }
        set
        {
            if (m_HorizontalScrollbar)
                m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
            m_HorizontalScrollbar = value;
            if (m_HorizontalScrollbar)
                m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
            SetDirtyCaching();
        }
    }

    [SerializeField] private Scrollbar m_VerticalScrollbar;

    public Scrollbar verticalScrollbar
    {
        get { return m_VerticalScrollbar; }
        set
        {
            if (m_VerticalScrollbar)
                m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
            m_VerticalScrollbar = value;
            if (m_VerticalScrollbar)
                m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
            SetDirtyCaching();
        }
    }

    [SerializeField] private ScrollbarVisibility m_HorizontalScrollbarVisibility;

    public ScrollbarVisibility horizontalScrollbarVisibility
    {
        get { return m_HorizontalScrollbarVisibility; }
        set
        {
            m_HorizontalScrollbarVisibility = value;
            SetDirtyCaching();
        }
    }

    [SerializeField] private ScrollbarVisibility m_VerticalScrollbarVisibility;

    public ScrollbarVisibility verticalScrollbarVisibility
    {
        get { return m_VerticalScrollbarVisibility; }
        set
        {
            m_VerticalScrollbarVisibility = value;
            SetDirtyCaching();
        }
    }

    [SerializeField] private float m_HorizontalScrollbarSpacing;

    public float horizontalScrollbarSpacing
    {
        get { return m_HorizontalScrollbarSpacing; }
        set
        {
            m_HorizontalScrollbarSpacing = value;
            SetDirty();
        }
    }

    [SerializeField] private float m_VerticalScrollbarSpacing;

    public float verticalScrollbarSpacing
    {
        get { return m_VerticalScrollbarSpacing; }
        set
        {
            m_VerticalScrollbarSpacing = value;
            SetDirty();
        }
    }

    [SerializeField] private ScrollRectEvent m_OnValueChanged = new ScrollRectEvent();
    [SerializeField] private ContentPosChangedEvent m_OnContentPosChanged = new ContentPosChangedEvent();

    public ScrollRectEvent onValueChanged
    {
        get { return m_OnValueChanged; }
        set { m_OnValueChanged = value; }
    }

    public ContentPosChangedEvent onContentPosChanged
    {
        get { return m_OnContentPosChanged; }
        set { m_OnContentPosChanged = value; }
    }

    // The offset from handle position to mouse down position
    private Vector2 m_PointerStartLocalCursor = Vector2.zero;

    protected Vector2 m_ContentStartPosition = Vector2.zero;
    public Vector2 contentStartPosition
    {
        get { return m_ContentStartPosition; }
        set { m_ContentStartPosition = value; }
    }

    private RectTransform m_ViewRect;

    protected RectTransform viewRect
    {
        get
        {
            if (m_ViewRect == null)
                m_ViewRect = m_Viewport;
            if (m_ViewRect == null)
                m_ViewRect = (RectTransform) transform;
            return m_ViewRect;
        }
    }

    protected Bounds m_ContentBounds;
    private Bounds m_ViewBounds;

    public Bounds contentBounds
    {
        get { return m_ContentBounds;}
    }

    public Bounds viewBounds
    {
        get { return m_ViewBounds; }
    }

    private Vector2 m_Velocity;

    public Vector2 velocity
    {
        get { return m_Velocity; }
    }

    private bool m_Dragging;

    private Vector2 m_PrevPosition = Vector2.zero;
    public Vector2 prevPosition
    {
        get { return m_PrevPosition; }
        set { m_PrevPosition = value; }
    }

    private Bounds m_PrevContentBounds;
    private Bounds m_PrevViewBounds;
    [NonSerialized] private bool m_HasRebuiltLayout = false;

    private bool m_HSliderExpand;
    private bool m_VSliderExpand;
    private float m_HSliderHeight;
    private float m_VSliderWidth;

    [System.NonSerialized] private RectTransform m_Rect;

    private RectTransform rectTransform
    {
        get
        {
            if (m_Rect == null)
                m_Rect = GetComponent<RectTransform>();
            return m_Rect;
        }
    }

    private RectTransform m_HorizontalScrollbarRect;
    private RectTransform m_VerticalScrollbarRect;

    private DrivenRectTransformTracker m_Tracker;

    //用于控制多点触摸，只有最新的拖拽才响应，比如多根手指一起拖动的问题
    [SerializeField] protected bool m_SupportMultiTouch = true;
    [NonSerialized] protected PointerEventData m_CurrentDragEvent;

    public bool supportMultiTouch
    {
        get { return m_SupportMultiTouch; }
        set
        {
            m_SupportMultiTouch = value;
        }
    }

    [NonSerialized]
    protected Vector2 m_StartPos;
    [NonSerialized]
    protected Vector2 m_EndPos;
    [NonSerialized]
    protected float m_StartAnimationTime;
    [SerializeField]
    protected float m_AnimationTime = 0.5f;

    [NonSerialized]
    protected IInterpolator m_Interpolator = new AccelerateDecelerateInterpolator();

    public IInterpolator interpolator
    {
        get { return m_Interpolator; }
        set { m_Interpolator = value; }
    }
    protected bool isAnimating
    {
        get; set;
    }

    public bool dragging
    {
        get { return m_Dragging; }
    }

    protected ScrollView()
    {
    }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.Prelayout)
        {
            UpdateCachedData();
        }

        if (executing == CanvasUpdate.PostLayout)
        {
            UpdateBounds();
            UpdateScrollbars(Vector2.zero);
            UpdatePrevData();

            m_HasRebuiltLayout = true;
        }
    }

    public virtual void LayoutComplete()
    {
    }

    public virtual void GraphicUpdateComplete()
    {
    }

    void UpdateCachedData()
    {
        Transform transform = this.transform;
        m_HorizontalScrollbarRect = m_HorizontalScrollbar == null
            ? null
            : m_HorizontalScrollbar.transform as RectTransform;
        m_VerticalScrollbarRect = m_VerticalScrollbar == null ? null : m_VerticalScrollbar.transform as RectTransform;

        // These are true if either the elements are children, or they don't exist at all.
        bool viewIsChild = (viewRect.parent == transform);
        bool hScrollbarIsChild = (!m_HorizontalScrollbarRect || m_HorizontalScrollbarRect.parent == transform);
        bool vScrollbarIsChild = (!m_VerticalScrollbarRect || m_VerticalScrollbarRect.parent == transform);
        bool allAreChildren = (viewIsChild && hScrollbarIsChild && vScrollbarIsChild);

        m_HSliderExpand = allAreChildren && m_HorizontalScrollbarRect &&
                          horizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
        m_VSliderExpand = allAreChildren && m_VerticalScrollbarRect &&
                          verticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
        m_HSliderHeight = (m_HorizontalScrollbarRect == null ? 0 : m_HorizontalScrollbarRect.rect.height);
        m_VSliderWidth = (m_VerticalScrollbarRect == null ? 0 : m_VerticalScrollbarRect.rect.width);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (m_HorizontalScrollbar)
            m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
        if (m_VerticalScrollbar)
            m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);

        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

        if (m_HorizontalScrollbar)
            m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
        if (m_VerticalScrollbar)
            m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);

        StopAnimation(true);
        m_HasRebuiltLayout = false;
        m_Tracker.Clear();
        m_Velocity = Vector2.zero;
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        base.OnDisable();

        m_CurrentDragEvent = null;
    }

    public override bool IsActive()
    {
        return base.IsActive() && m_Content != null;
    }

    private void EnsureLayoutHasRebuilt()
    {
        if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
            Canvas.ForceUpdateCanvases();
    }

    public virtual void StopMovement()
    {
        m_Velocity = Vector2.zero;
    }

    public virtual void InverseVelocity()
    {
        m_Velocity = -m_Velocity;
    }

    public virtual void OnScroll(PointerEventData data)
    {
        if (!IsActive())
            return;

        EnsureLayoutHasRebuilt();
        UpdateBounds();

        Vector2 delta = data.scrollDelta;
        // Down is positive for scroll events, while in UI system up is positive.
        delta.y *= -1;
        if (vertical && !horizontal)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                delta.y = delta.x;
            delta.x = 0;
        }
        if (horizontal && !vertical)
        {
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                delta.x = delta.y;
            delta.y = 0;
        }

        Vector2 position = m_Content.anchoredPosition;
        position += delta * m_ScrollSensitivity;
        if (m_MovementType == MovementType.Clamped)
            position += CalculateOffset(position - m_Content.anchoredPosition);

        SetContentAnchoredPosition(position);
        UpdateBounds();
    }

    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        m_CurrentDragEvent = eventData;
        m_Velocity = Vector2.zero;
        StopAnimation(false);
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        if (m_SupportMultiTouch)
        {
            if (m_CurrentDragEvent == null || eventData != m_CurrentDragEvent)
                return;
        }

        UpdateBounds();

        m_PointerStartLocalCursor = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position,
            eventData.pressEventCamera, out m_PointerStartLocalCursor);
        m_ContentStartPosition = m_Content.anchoredPosition;
        m_Dragging = true;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (m_SupportMultiTouch)
        {
            if (m_CurrentDragEvent == null || eventData != m_CurrentDragEvent)
                return;
        }

        m_CurrentDragEvent = null;
        m_Dragging = false;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        if (m_SupportMultiTouch)
        {
            if (m_CurrentDragEvent == null || eventData != m_CurrentDragEvent)
                return;
        }

        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position,
            eventData.pressEventCamera, out localCursor))
            return;

        UpdateBounds();

        var pointerDelta = localCursor - m_PointerStartLocalCursor;
        Vector2 position = m_ContentStartPosition + pointerDelta;

        // Offset to get content into place in the view.
        Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
        position += offset;
        if (m_MovementType == MovementType.Elastic)
        {
            if (offset.x != 0)
                position.x = position.x - RubberDelta(offset.x, m_ViewBounds.size.x);
            if (offset.y != 0)
                position.y = position.y - RubberDelta(offset.y, m_ViewBounds.size.y);
        }

        SetContentAnchoredPosition(position);
    }

    protected virtual void SetContentAnchoredPosition(Vector2 position)
    {
        if (!m_Horizontal)
            position.x = m_Content.anchoredPosition.x;
        if (!m_Vertical)
            position.y = m_Content.anchoredPosition.y;

        if (position != m_Content.anchoredPosition)
        {
            var oldPos = m_Content.anchoredPosition;
            m_Content.anchoredPosition = position;
            UpdateBounds();

            onContentPosChanged.Invoke(oldPos, position);
        }
    }

    public float AnimationTime
    {
        get { return m_AnimationTime; }
        set { m_AnimationTime = value; }
    }

    public void StartAnimation(Vector2 targetPos, bool resetStartPos = true, bool useAnimation = false)
    {
        if (!useAnimation)
        {
            SetContentAnchoredPosition(targetPos);
            return;
        }

        if (resetStartPos)
        {
            SetContentAnchoredPosition(Vector2.zero);
        }

        isAnimating = true;
        m_StartPos = m_Content.anchoredPosition;
        m_EndPos = targetPos;
        m_Velocity = Vector2.zero;
        m_StartAnimationTime = 0;
    }

    protected void DoAnimation()
    {
        var deltaTime = Time.unscaledDeltaTime;
        m_StartAnimationTime += deltaTime;
        if (m_StartAnimationTime >= m_AnimationTime)
        {
            isAnimating = false;
            SetContentAnchoredPosition(ClampAnimationPos(m_EndPos));
            return;
        }

        if (m_Interpolator != null)
        {
            var factor = m_Interpolator.GetInterpolation(m_StartAnimationTime / AnimationTime);
            SetContentAnchoredPosition(ClampAnimationPos((m_EndPos - m_StartPos) * factor + m_StartPos));
        }
        else
        {
            Debug.LogError("动画插值器为空");
        }
    }

    public void StopAnimation(bool setEndPos)
    {
        if (isAnimating)
        {
            isAnimating = false;
            if (setEndPos)
            {
                SetContentAnchoredPosition(ClampAnimationPos(m_EndPos));
            }
        }
    }

    protected Vector2 ClampAnimationPos(Vector2 pos)
    {
        if (vertical && !horizontal)
        {
            var viewportHeight = viewport.rect.height;
            var contentHeight = content.rect.height;
            var deltaHeight = contentHeight - viewportHeight;
            if (deltaHeight < 0)
                deltaHeight = 0;

            if (pos.y < 0)
            {
                pos.y = 0;
                isAnimating = false;
            }

            if (pos.y > deltaHeight)
            {
                pos.y = deltaHeight;
                isAnimating = false;
            }
        }
        else if(horizontal && !vertical)
        {
            var viewportWidth = viewport.rect.width;
            var contentWidth = content.rect.width;
            var deltaWidth = viewportWidth - contentWidth;
            if (deltaWidth > 0)
                deltaWidth = 0;

            if (pos.x > 0)
            {
                pos.x = 0;
                isAnimating = false;
            }

            if (pos.x < deltaWidth)
            {
                pos.x = deltaWidth;
                isAnimating = false;
            }
        }
        return pos;
    }

    protected virtual void LateUpdate()
    {
        if (!m_Content)
            return;

        if (isAnimating)
        {
            DoAnimation();
        }

        EnsureLayoutHasRebuilt();
        UpdateScrollbarVisibility();
        UpdateBounds();
        float deltaTime = Time.unscaledDeltaTime;
        Vector2 offset = CalculateOffset(Vector2.zero);
        if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
        {
            Vector2 position = m_Content.anchoredPosition;
            for (int axis = 0; axis < 2; axis++)
            {
                // Apply spring physics if movement is elastic and content has an offset from the view.
                if (m_MovementType == MovementType.Elastic && offset[axis] != 0)
                {
                    float speed = m_Velocity[axis];
                    position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis],
                        m_Content.anchoredPosition[axis] + offset[axis], ref speed, m_Elasticity, Mathf.Infinity,
                        deltaTime);
                    if (Mathf.Abs(speed) < 1)
                        speed = 0;
                    m_Velocity[axis] = speed;
                }
                // Else move content according to velocity with deceleration applied.
                else if (m_Inertia)
                {
                    m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                    if (Mathf.Abs(m_Velocity[axis]) < 1)
                        m_Velocity[axis] = 0;
                    position[axis] += m_Velocity[axis] * deltaTime;
                }
                // If we have neither elaticity or friction, there shouldn't be any velocity.
                else
                {
                    m_Velocity[axis] = 0;
                }
            }

            if (m_Velocity != Vector2.zero)
            {
                if (m_MovementType == MovementType.Clamped)
                {
                    offset = CalculateOffset(position - m_Content.anchoredPosition);
                    position += offset;
                }

                SetContentAnchoredPosition(position);
            }
        }

        if (m_Dragging && m_Inertia)
        {
            Vector3 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
            m_Velocity = Vector3.Lerp(m_Velocity, newVelocity, deltaTime * 10);
        }

        if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds ||
            m_Content.anchoredPosition != m_PrevPosition)
        {
            UpdateScrollbars(offset);
            m_OnValueChanged.Invoke(normalizedPosition);
            UpdatePrevData();
        }
    }

    protected void UpdatePrevData()
    {
        if (m_Content == null)
            m_PrevPosition = Vector2.zero;
        else
            m_PrevPosition = m_Content.anchoredPosition;
        m_PrevViewBounds = m_ViewBounds;
        m_PrevContentBounds = m_ContentBounds;
    }

    private void UpdateScrollbars(Vector2 offset)
    {
        if (m_HorizontalScrollbar)
        {
            if (m_ContentBounds.size.x > 0)
                m_HorizontalScrollbar.size =
                    Mathf.Clamp01((m_ViewBounds.size.x - Mathf.Abs(offset.x)) / m_ContentBounds.size.x);
            else
                m_HorizontalScrollbar.size = 1;

            m_HorizontalScrollbar.value = horizontalNormalizedPosition;
        }

        if (m_VerticalScrollbar)
        {
            if (m_ContentBounds.size.y > 0)
                m_VerticalScrollbar.size =
                    Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / m_ContentBounds.size.y);
            else
                m_VerticalScrollbar.size = 1;

            m_VerticalScrollbar.value = verticalNormalizedPosition;
        }
    }

    public Vector2 normalizedPosition
    {
        get { return new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition); }
        set
        {
            SetNormalizedPosition(value.x, 0);
            SetNormalizedPosition(value.y, 1);
        }
    }

    public float horizontalNormalizedPosition
    {
        get
        {
            UpdateBounds();
            if (m_ContentBounds.size.x <= m_ViewBounds.size.x)
                return (m_ViewBounds.min.x > m_ContentBounds.min.x) ? 1 : 0;
            return (m_ViewBounds.min.x - m_ContentBounds.min.x) / (m_ContentBounds.size.x - m_ViewBounds.size.x);
        }
        set { SetNormalizedPosition(value, 0); }
    }

    public float verticalNormalizedPosition
    {
        get
        {
            UpdateBounds();
            if (m_ContentBounds.size.y <= m_ViewBounds.size.y)
                return (m_ViewBounds.min.y > m_ContentBounds.min.y) ? 1 : 0;
            ;
            return (m_ViewBounds.min.y - m_ContentBounds.min.y) / (m_ContentBounds.size.y - m_ViewBounds.size.y);
        }
        set { SetNormalizedPosition(value, 1); }
    }

    private void SetHorizontalNormalizedPosition(float value)
    {
        SetNormalizedPosition(value, 0);
    }

    private void SetVerticalNormalizedPosition(float value)
    {
        SetNormalizedPosition(value, 1);
    }

    protected virtual void SetNormalizedPosition(float value, int axis)
    {
        EnsureLayoutHasRebuilt();
        UpdateBounds();
        // How much the content is larger than the view.
        float hiddenLength = m_ContentBounds.size[axis] - m_ViewBounds.size[axis];
        // Where the position of the lower left corner of the content bounds should be, in the space of the view.
        float contentBoundsMinPosition = m_ViewBounds.min[axis] - value * hiddenLength;
        // The new content localPosition, in the space of the view.
        float newLocalPosition = m_Content.localPosition[axis] + contentBoundsMinPosition - m_ContentBounds.min[axis];

        Vector3 localPosition = m_Content.localPosition;
        if (Mathf.Abs(localPosition[axis] - newLocalPosition) > 0.01f)
        {
            localPosition[axis] = newLocalPosition;
            m_Content.localPosition = localPosition;
            m_Velocity[axis] = 0;
            UpdateBounds();
        }
    }

    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        SetDirty();
    }

    private bool hScrollingNeeded
    {
        get
        {
            if (Application.isPlaying)
                return m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
            return true;
        }
    }

    private bool vScrollingNeeded
    {
        get
        {
            if (Application.isPlaying)
                return m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f;
            return true;
        }
    }

    public virtual void CalculateLayoutInputHorizontal()
    {
    }

    public virtual void CalculateLayoutInputVertical()
    {
    }

    public virtual float minWidth
    {
        get { return -1; }
    }

    public virtual float preferredWidth
    {
        get { return -1; }
    }

    public virtual float flexibleWidth
    {
        get { return -1; }
    }

    public virtual float minHeight
    {
        get { return -1; }
    }

    public virtual float preferredHeight
    {
        get { return -1; }
    }

    public virtual float flexibleHeight
    {
        get { return -1; }
    }

    public virtual int layoutPriority
    {
        get { return -1; }
    }

    public virtual void SetLayoutHorizontal()
    {
        m_Tracker.Clear();

        if (m_HSliderExpand || m_VSliderExpand)
        {
            m_Tracker.Add(this, viewRect,
                DrivenTransformProperties.Anchors |
                DrivenTransformProperties.SizeDelta |
                DrivenTransformProperties.AnchoredPosition);

            // Make view full size to see if content fits.
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.sizeDelta = Vector2.zero;
            viewRect.anchoredPosition = Vector2.zero;

            // Recalculate content layout with this size to see if it fits when there are no scrollbars.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
        }

        // If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
        if (m_VSliderExpand && vScrollingNeeded)
        {
            viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);

            // Recalculate content layout with this size to see if it fits vertically
            // when there is a vertical scrollbar (which may reflowed the content to make it taller).
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
        }

        // If it doesn't fit horizontally, enable horizontal scrollbar and shrink view vertically to make room for it.
        if (m_HSliderExpand && hScrollingNeeded)
        {
            viewRect.sizeDelta = new Vector2(viewRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
        }

        // If the vertical slider didn't kick in the first time, and the horizontal one did,
        // we need to check again if the vertical slider now needs to kick in.
        // If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
        if (m_VSliderExpand && vScrollingNeeded && viewRect.sizeDelta.x == 0 && viewRect.sizeDelta.y < 0)
        {
            viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
        }
    }

    public virtual void SetLayoutVertical()
    {
        UpdateScrollbarLayout();
        m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        m_ContentBounds = GetBounds();
    }

    void UpdateScrollbarVisibility()
    {
        UpdateOneScrollbarVisibility(vScrollingNeeded, m_Vertical, m_VerticalScrollbarVisibility, m_VerticalScrollbar);
        UpdateOneScrollbarVisibility(hScrollingNeeded, m_Horizontal, m_HorizontalScrollbarVisibility,
            m_HorizontalScrollbar);
    }

    private static void UpdateOneScrollbarVisibility(bool xScrollingNeeded, bool xAxisEnabled,
        ScrollbarVisibility scrollbarVisibility, Scrollbar scrollbar)
    {
        if (scrollbar)
        {
            if (scrollbarVisibility == ScrollbarVisibility.Permanent)
            {
                if (scrollbar.gameObject.activeSelf != xAxisEnabled)
                    scrollbar.gameObject.SetActive(xAxisEnabled);
            }
            else
            {
                if (scrollbar.gameObject.activeSelf != xScrollingNeeded)
                    scrollbar.gameObject.SetActive(xScrollingNeeded);
            }
        }
    }

    void UpdateScrollbarLayout()
    {
        if (m_VSliderExpand && m_HorizontalScrollbar)
        {
            m_Tracker.Add(this, m_HorizontalScrollbarRect,
                DrivenTransformProperties.AnchorMinX |
                DrivenTransformProperties.AnchorMaxX |
                DrivenTransformProperties.SizeDeltaX |
                DrivenTransformProperties.AnchoredPositionX);
            m_HorizontalScrollbarRect.anchorMin = new Vector2(0, m_HorizontalScrollbarRect.anchorMin.y);
            m_HorizontalScrollbarRect.anchorMax = new Vector2(1, m_HorizontalScrollbarRect.anchorMax.y);
            m_HorizontalScrollbarRect.anchoredPosition = new Vector2(0, m_HorizontalScrollbarRect.anchoredPosition.y);
            if (vScrollingNeeded)
                m_HorizontalScrollbarRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing),
                    m_HorizontalScrollbarRect.sizeDelta.y);
            else
                m_HorizontalScrollbarRect.sizeDelta = new Vector2(0, m_HorizontalScrollbarRect.sizeDelta.y);
        }

        if (m_HSliderExpand && m_VerticalScrollbar)
        {
            m_Tracker.Add(this, m_VerticalScrollbarRect,
                DrivenTransformProperties.AnchorMinY |
                DrivenTransformProperties.AnchorMaxY |
                DrivenTransformProperties.SizeDeltaY |
                DrivenTransformProperties.AnchoredPositionY);
            m_VerticalScrollbarRect.anchorMin = new Vector2(m_VerticalScrollbarRect.anchorMin.x, 0);
            m_VerticalScrollbarRect.anchorMax = new Vector2(m_VerticalScrollbarRect.anchorMax.x, 1);
            m_VerticalScrollbarRect.anchoredPosition = new Vector2(m_VerticalScrollbarRect.anchoredPosition.x, 0);
            if (hScrollingNeeded)
                m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x,
                    -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
            else
                m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x, 0);
        }
    }

    public void UpdateBounds()
    {
        m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        m_ContentBounds = GetBounds();

        if (m_Content == null)
            return;

        Vector3 contentSize = m_ContentBounds.size;
        Vector3 contentPos = m_ContentBounds.center;
        var contentPivot = m_Content.pivot;
        AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
        m_ContentBounds.size = contentSize;
        m_ContentBounds.center = contentPos;

        if (movementType == MovementType.Clamped)
        {
            // Adjust content so that content bounds bottom (right side) is never higher (to the left) than the view bounds bottom (right side).
            //                                       top (left side) is never lower (to the right) than the view bounds top (left side).
            // All this can happen if content has shrunk.
            // This works because content size is at least as big as view size (because of the call to InternalUpdateBounds above).
            Vector3 delta = Vector3.zero;
            if (m_ViewBounds.max.x > m_ContentBounds.max.x)
            {
                delta.x = Math.Min(m_ViewBounds.min.x - m_ContentBounds.min.x,
                    m_ViewBounds.max.x - m_ContentBounds.max.x);
            }
            else if (m_ViewBounds.min.x < m_ContentBounds.min.x)
            {
                delta.x = Math.Max(m_ViewBounds.min.x - m_ContentBounds.min.x,
                    m_ViewBounds.max.x - m_ContentBounds.max.x);
            }

            if (m_ViewBounds.min.y < m_ContentBounds.min.y)
            {
                delta.y = Math.Max(m_ViewBounds.min.y - m_ContentBounds.min.y,
                    m_ViewBounds.max.y - m_ContentBounds.max.y);
            }
            else if (m_ViewBounds.max.y > m_ContentBounds.max.y)
            {
                delta.y = Math.Min(m_ViewBounds.min.y - m_ContentBounds.min.y,
                    m_ViewBounds.max.y - m_ContentBounds.max.y);
            }
            if (delta != Vector3.zero)
            {
                m_Content.Translate(delta);

                // Content position changed; recompute content bounds.
                m_ContentBounds = GetBounds();
                contentSize = m_ContentBounds.size;
                contentPos = m_ContentBounds.center;
                contentPivot = m_Content.pivot;
                AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
                m_ContentBounds.size = contentSize;
                m_ContentBounds.center = contentPos;
            }
        }
    }

    internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize,
        ref Vector3 contentPos)
    {
        // Make sure content bounds are at least as large as view by adding padding if not.
        // One might think at first that if the content is smaller than the view, scrolling should be allowed.
        // However, that's not how scroll views normally work.
        // Scrolling is *only* possible when content is *larger* than view.
        // We use the pivot of the content rect to decide in which directions the content bounds should be expanded.
        // E.g. if pivot is at top, bounds are expanded downwards.
        // This also works nicely when ContentSizeFitter is used on the content.
        Vector3 excess = viewBounds.size - contentSize;
        if (excess.x > 0)
        {
            contentPos.x -= excess.x * (contentPivot.x - 0.5f);
            contentSize.x = viewBounds.size.x;
        }
        if (excess.y > 0)
        {
            contentPos.y -= excess.y * (contentPivot.y - 0.5f);
            contentSize.y = viewBounds.size.y;
        }
    }

    private readonly Vector3[] m_Corners = new Vector3[4];

    private Bounds GetBounds()
    {
        if (m_Content == null)
            return new Bounds();
        m_Content.GetWorldCorners(m_Corners);
        var viewWorldToLocalMatrix = viewRect.worldToLocalMatrix;
        return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
    }

    internal static Bounds InternalGetBounds(Vector3[] corners, ref Matrix4x4 viewWorldToLocalMatrix)
    {
        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int j = 0; j < 4; j++)
        {
            Vector3 v = viewWorldToLocalMatrix.MultiplyPoint3x4(corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }

    private Vector2 CalculateOffset(Vector2 delta)
    {
        return InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, m_MovementType,
            ref delta);
    }

    internal static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal,
        bool vertical, MovementType movementType, ref Vector2 delta)
    {
        Vector2 offset = Vector2.zero;
        if (movementType == MovementType.Unrestricted)
            return offset;

        Vector2 min = contentBounds.min;
        Vector2 max = contentBounds.max;

        if (horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;
            if (min.x > viewBounds.min.x)
                offset.x = viewBounds.min.x - min.x;
            else if (max.x < viewBounds.max.x)
                offset.x = viewBounds.max.x - max.x;
        }

        if (vertical)
        {
            min.y += delta.y;
            max.y += delta.y;
            if (max.y < viewBounds.max.y)
                offset.y = viewBounds.max.y - max.y;
            else if (min.y > viewBounds.min.y)
                offset.y = viewBounds.min.y - min.y;
        }

        return offset;
    }

    protected void SetDirty()
    {
        if (!IsActive())
            return;

        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    protected void SetDirtyCaching()
    {
        if (!IsActive())
            return;

        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        SetDirtyCaching();
    }

#endif
}

