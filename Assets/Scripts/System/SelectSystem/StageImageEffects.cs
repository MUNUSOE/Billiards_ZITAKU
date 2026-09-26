using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class StageImageEffects : MonoBehaviour
{
    private Image target;
    private Material originalMaterial;
    private Material grayscaleMaterial;
    private StageSparkleGraphic sparkles;
    private bool initialized;
    private bool missingShaderReported;

    public void Apply(bool cleared, bool fastest, Color sparkleColor, int sparkleCount)
    {
        if (!initialized)
        {
            target = GetComponent<Image>();
            originalMaterial = target.material;
            initialized = true;
        }
        bool hasImage = target.sprite != null;
        target.enabled = hasImage;
        if (!cleared && hasImage)
        {
            if (grayscaleMaterial == null)
            {
                // Resources内に配置してビルドにも確実に含める。
                var shader = Resources.Load<Shader>("StagePreviewGrayscale");
                if (shader != null && shader.isSupported)
                {
                    grayscaleMaterial = new Material(shader);
                    grayscaleMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                else if (!missingShaderReported)
                {
                    Debug.LogError("[StagePreview] Assets/Resources/StagePreviewGrayscale.shader を配置してください。Shaderのコンパイルエラーも確認してください。", this);
                    missingShaderReported = true;
                }
            }
            target.material = grayscaleMaterial != null ? grayscaleMaterial : originalMaterial;
        }
        else target.material = originalMaterial;

        bool showSparkles = hasImage && cleared && fastest;
        if (showSparkles && sparkles == null)
        {
            var go = new GameObject("FastestSparkles", typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            sparkles = go.AddComponent<StageSparkleGraphic>();
            sparkles.raycastTarget = false;
        }
        if (sparkles != null)
        {
            sparkles.Configure(target, sparkleColor, sparkleCount);
            sparkles.gameObject.SetActive(showSparkles);
        }
    }

    private void OnDestroy()
    {
        if (target != null && target.material == grayscaleMaterial)
            target.material = originalMaterial;
        if (grayscaleMaterial != null) Destroy(grayscaleMaterial);
        if (sparkles != null) Destroy(sparkles.gameObject);
    }
}
