using UnityEngine;

[ExecuteAlways]
public class AspectKeeper : MonoBehaviour
{
    [SerializeField] private Vector2 targetAspect = new Vector2(16f, 9f);

    private void Update()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        float targetRatio = targetAspect.x / targetAspect.y;
        float currentRatio = (float)Screen.width / Screen.height;
        float scale = currentRatio / targetRatio;

        Rect rect = cam.rect;

        if (scale < 1.0f)
        {
            rect.width = 1.0f;
            rect.height = scale;
            rect.x = 0;
            rect.y = (1.0f - scale) / 2.0f;
        }
        else
        {
            rect.width = 1.0f / scale;
            rect.height = 1.0f;
            rect.x = (1.0f - (1.0f / scale)) / 2.0f;
            rect.y = 0;
        }

        cam.rect = rect;
    }
}