using UnityEngine;

/// <summary>
/// TriangleWall / InterCellWall の判定処理を変更せず、見た目だけを魔法壁へ差し替えます。
/// PrefabのRendererとColliderは既存のまま利用し、周囲に控えめなきらめきを生成します。
/// </summary>
[DisallowMultipleComponent]
public sealed class MagicWallVisual : MonoBehaviour
{
    [Header("魔法壁の見た目")]
    [SerializeField] private Color wallColor = new Color(0.16f, 0.72f, 1f, 0.42f);
    [SerializeField, Range(0.05f, 1f)] private float wallAlpha = 0.42f;
    [SerializeField] private Color sparkleColor = new Color(0.55f, 0.9f, 1f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float sparkleAmount = 0.45f;
    [SerializeField] private float sparkleSize = 0.035f;
    [SerializeField] private float sparkleLifetime = 1.8f;
    [SerializeField] private int sparkleMaxParticles = 18;

    private ParticleSystem sparkles;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        ApplyWallMaterial();
        CreateSparkles();
    }

    private void OnValidate()
    {
        ApplyWallMaterial();
        if (sparkles != null) ConfigureSparkles();
    }

    private void ApplyWallMaterial()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;

        propertyBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        Color color = wallColor;
        color.a = wallAlpha;
        propertyBlock.SetColor("_Color", color);
        propertyBlock.SetColor("_EmissionColor", new Color(color.r * 0.8f, color.g * 0.9f, color.b, 1f));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void CreateSparkles()
    {
        if (sparkleAmount <= 0f || sparkleMaxParticles <= 0) return;
        GameObject child = new GameObject("MagicWall_Sparkles");
        child.transform.SetParent(transform, false);
        sparkles = child.AddComponent<ParticleSystem>();
        ConfigureSparkles();
    }

    private void ConfigureSparkles()
    {
        if (sparkles == null) return;
        var main = sparkles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = sparkleLifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(sparkleSize * 0.55f, sparkleSize);
        main.startColor = sparkleColor;
        main.maxParticles = sparkleMaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = sparkles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Lerp(2f, 8f, sparkleAmount);

        var shape = sparkles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.34f;
        shape.radiusThickness = 0.05f;
        shape.arc = 360f;

        var renderer = sparkles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateSparkleMaterial();
    }

    private static Material CreateSparkleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        Material material = new Material(shader) { name = "MagicWallSparkle_Runtime" };
        material.SetColor("_Color", Color.white);
        return material;
    }
}