using UnityEngine;
using UnityEngine.UI;

// 追加テクスチャ不要。UIメッシュで四芒星を描くのでCanvas上で表示できる。
public class StageSparkleGraphic : MaskableGraphic
{
    private Image target;
    private int sparkleCount = 12;

    public void Configure(Image image, Color tint, int count)
    {
        target = image;
        color = tint;
        sparkleCount = Mathf.Clamp(count, 1, 40);
        raycastTarget = false;
        SetVerticesDirty();
    }

    private void Update() { SetVerticesDirty(); }

    private static float Noise(int index, float seed)
    {
        return Mathf.Repeat(Mathf.Sin(index * 127.1f + seed * 311.7f) * 43758.5453f, 1f);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect area = rectTransform.rect;
        // Preserve Aspect時は画像が描画される矩形に合わせる。
        if (target != null && target.preserveAspect && target.sprite != null)
        {
            var size = target.sprite.rect.size;
            if (size.x > 0f && size.y > 0f && area.width > 0f && area.height > 0f)
            {
                float aspect = size.x / size.y;
                if (aspect > area.width / area.height)
                {
                    float height = area.width / aspect;
                    area.y += (area.height - height) * target.rectTransform.pivot.y;
                    area.height = height;
                }
                else
                {
                    float width = area.height * aspect;
                    area.x += (area.width - width) * target.rectTransform.pivot.x;
                    area.width = width;
                }
            }
        }
        float minSize = Mathf.Min(area.width, area.height);
        if (minSize <= 0f) return;
        float time = Time.unscaledTime;
        for (int i = 0; i < sparkleCount; i++)
        {
            float phase = Mathf.Repeat(time * (0.33f + Noise(i, 1f) * 0.22f) + Noise(i, 2f), 1f);
            float glow = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 3f);
            float radius = minSize * (0.018f + Noise(i, 3f) * 0.015f) * glow;
            Vector2 center = new Vector2(
                Mathf.Lerp(area.xMin, area.xMax, 0.08f + Noise(i, 4f) * 0.84f),
                Mathf.Lerp(area.yMin, area.yMax, 0.08f + Noise(i, 5f) * 0.80f + phase * 0.035f));
            Color tint = color;
            tint.a *= glow;
            AddStar(vh, center, radius, tint);
        }
    }

    private static void AddStar(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            float r = (i % 2 == 0) ? radius : radius * 0.22f;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            vh.AddVert(point, tint, Vector2.zero);
        }
        for (int i = 0; i < 8; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
    }
}
