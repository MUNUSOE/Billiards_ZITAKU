using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UILeftRightAlphaGradient : BaseMeshEffect
{
    [Range(0f, 1f)]
    [SerializeField] private float edgeAlpha = 0.0f; // 左右端の透明度（0で透明）

    [Range(0f, 1f)]
    [SerializeField] private float centerAlpha = 1.0f; // 中央の透明度

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null) return;

        Rect rect = rectTransform.rect;
        vh.Clear();

        // メッシュを左右に分割して頂点を生成（左・中央・右）
        UIVertex v = UIVertex.simpleVert;

        // 左端
        v.position = new Vector3(rect.xMin, rect.yMin);
        v.color = GetColorWithAlpha(edgeAlpha);
        vh.AddVert(v);

        v.position = new Vector3(rect.xMin, rect.yMax);
        v.color = GetColorWithAlpha(edgeAlpha);
        vh.AddVert(v);

        // 中央
        v.position = new Vector3(0, rect.yMax);
        v.color = GetColorWithAlpha(centerAlpha);
        vh.AddVert(v);

        v.position = new Vector3(0, rect.yMin);
        v.color = GetColorWithAlpha(centerAlpha);
        vh.AddVert(v);

        // 右端
        v.position = new Vector3(rect.xMax, rect.yMax);
        v.color = GetColorWithAlpha(edgeAlpha);
        vh.AddVert(v);

        v.position = new Vector3(rect.xMax, rect.yMin);
        v.color = GetColorWithAlpha(edgeAlpha);
        vh.AddVert(v);

        // ポリゴン（三角形）の構成
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
        vh.AddTriangle(3, 2, 4);
        vh.AddTriangle(3, 4, 5);
    }

    private Color32 GetColorWithAlpha(float alphaFactor)
    {
        Graphic graphic = GetComponent<Graphic>();
        Color c = graphic != null ? graphic.color : Color.white;
        c.a *= alphaFactor;
        return c;
    }
}