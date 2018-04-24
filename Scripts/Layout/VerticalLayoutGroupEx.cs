using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Vertical Layout Group Ex", 151)]
public class VerticalLayoutGroupEx : HorizontalOrVerticalLayoutGroup
{
    [SerializeField] protected Vector2 m_MaxSize = new Vector2(-1, -1);
    public Vector2 maxSize { get { return m_MaxSize; } set { SetProperty(ref m_MaxSize, value); } }

    protected VerticalLayoutGroupEx()
    {
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        CalcAlongAxisEx(0, true);
    }

    public override void CalculateLayoutInputVertical()
    {
        CalcAlongAxisEx(1, true);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenAlongAxis(0, true);
    }

    public override void SetLayoutVertical()
    {
        SetChildrenAlongAxis(1, true);
    }

    protected void CalcAlongAxisEx(int axis, bool isVertical)
    {
        float combinedPadding = (axis == 0 ? padding.horizontal : padding.vertical);
        bool controlSize = (axis == 0 ? m_ChildControlWidth : m_ChildControlHeight);
        bool childForceExpandSize = (axis == 0 ? childForceExpandWidth : childForceExpandHeight);

        float totalMin = combinedPadding;
        float totalPreferred = combinedPadding;
        float totalFlexible = 0;

        bool alongOtherAxis = (isVertical ^ (axis == 1));
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float min, preferred, flexible;
            GetChildSizes(child, axis, controlSize, childForceExpandSize, out min, out preferred, out flexible);

            if (alongOtherAxis)
            {
                totalMin = Mathf.Max(min + combinedPadding, totalMin);
                totalPreferred = Mathf.Max(preferred + combinedPadding, totalPreferred);
                totalFlexible = Mathf.Max(flexible, totalFlexible);
            }
            else
            {
                totalMin += min + spacing;
                totalPreferred += preferred + spacing;

                // Increment flexible size with element's flexible size.
                totalFlexible += flexible;
            }
        }

        if (!alongOtherAxis && rectChildren.Count > 0)
        {
            totalMin -= spacing;
            totalPreferred -= spacing;
        }
        totalPreferred = Mathf.Max(totalMin, totalPreferred);
        var totalMax = maxSize[axis];
        if (totalMax >= 0)
        {
            totalPreferred = Mathf.Min(totalPreferred, totalMax);
        }

        SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, axis);
    }

    private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand,
        out float min, out float preferred, out float flexible)
    {
        if (!controlSize)
        {
            min = child.sizeDelta[axis];
            preferred = min;
            flexible = 0;
        }
        else
        {
            min = LayoutUtility.GetMinSize(child, axis);
            preferred = LayoutUtility.GetPreferredSize(child, axis);
            flexible = LayoutUtility.GetFlexibleSize(child, axis);
        }

        if (childForceExpand)
            flexible = Mathf.Max(flexible, 1);
    }
}