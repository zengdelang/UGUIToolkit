using UnityEngine;

//https://images0.cnblogs.com/blog/587773/201504/111856556022155.png
public class AccelerateDecelerateInterpolator : IInterpolator
{
    public float GetInterpolation(float input)
    {
        return (Mathf.Cos((input + 1) * Mathf.PI) / 2.0f) + 0.5f;
    }
}
