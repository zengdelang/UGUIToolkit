using UnityEngine;

[AddComponentMenu("Layout/Horizontal Layout Group Ex", 150)]
public class HorizontalLayoutGroupEx : VerticalLayoutGroupEx
{
    protected HorizontalLayoutGroupEx()
    { }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        CalcAlongAxisEx(0, false);
    }

    public override void CalculateLayoutInputVertical()
    {
        CalcAlongAxisEx(1, false);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenAlongAxis(0, false);
    }

    public override void SetLayoutVertical()
    {
        SetChildrenAlongAxis(1, false);
    }
}