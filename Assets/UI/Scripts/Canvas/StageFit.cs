using UnityEngine;

// Keeps a 1920x1080 layout area fully visible at any window shape: scales it to fit inside its parent,
// centred. Children are placed in 1920x1080 pixels from the top-left corner.
[ExecuteAlways]
public class StageFit : MonoBehaviour
{
    public Vector2 designSize = new Vector2(1920f, 1080f);

    void LateUpdate()
    {
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null) return;
        Rect r = parent.rect;
        float s = Mathf.Min(r.width / designSize.x, r.height / designSize.y);
        if (s <= 0f) return;
        transform.localScale = new Vector3(s, s, 1f);
    }
}
