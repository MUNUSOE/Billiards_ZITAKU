using System;
using UnityEngine;

/// <summary>
/// MagicPotionの属性に合わせて、瓶の見た目と周囲の魔法エフェクトを切り替えます。
/// エフェクトはランタイムに生成するため、外部のVFXパッケージや既存UIには依存しません。
/// </summary>
[DisallowMultipleComponent]
public sealed class MagicPotionVisual : MonoBehaviour
{
    [Header("Visual Tuning")]
    [SerializeField, Min(0.05f)] private float visualScale = 1f;
    [SerializeField, Min(0.1f)] private float effectRadius = 0.65f;
    [SerializeField] private bool showEffect = true;

    private Transform visualRoot;
    private MagicPotion potion;
    private ParticleSystem particles;
    private LineRenderer[] windRibbons;
    private Material bottleMaterial;
    private Material capMaterial;
    private MagicType currentType = MagicType.Fire;
    private float animationTime;
    private bool built;

    private static readonly Color Fire = new Color(1f, 0.12f, 0.025f, 1f);
    private static readonly Color Water = new Color(0.04f, 0.45f, 1f, 1f);
    private static readonly Color Wind = new Color(0.12f, 0.95f, 0.38f, 1f);

    private void Awake()
    {
        potion = GetComponent<MagicPotion>();
        if (potion != null) currentType = potion.PotionType;
        EnsureBuilt();
        Apply(currentType);
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EnsureBuilt();
            Apply(currentType);
        }
    }

    private void Update()
    {
        if (!built) return;
        if (potion != null && potion.PotionType != currentType)
        {
            Apply(potion.PotionType);
        }
        animationTime += Time.deltaTime;

        if (currentType == MagicType.Wind && windRibbons != null)
        {
            AnimateWindRibbons();
        }
        else if (currentType == MagicType.Water && visualRoot != null)
        {
            visualRoot.localScale = Vector3.one * (1f + Mathf.Sin(animationTime * 2.1f) * 0.025f);
        }
    }

    /// <summary>炎・水・風の属性を切り替えます。</summary>
    public void Apply(MagicType type)
    {
        if (type == MagicType.None) type = MagicType.Fire;
        currentType = type;
        if (!Application.isPlaying) return;

        EnsureBuilt();
        Color primary = GetPrimaryColor(type);
        Color secondary = GetSecondaryColor(type);
        SetMaterialColor(bottleMaterial, primary, 1.6f);
        SetMaterialColor(capMaterial, Color.Lerp(primary, Color.white, 0.35f), 1.2f);
        ConfigureParticles(type, primary, secondary);
        ConfigureWindRibbons(type, primary);
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        visualRoot = new GameObject("PotionMagicVisual").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = Vector3.one * visualScale;

        bottleMaterial = CreateMaterial("PotionLiquidMaterial", new Color(1f, 0.15f, 0.05f, 1f), 1.4f, 0.72f);
        capMaterial = CreateMaterial("PotionCapMaterial", new Color(0.25f, 0.25f, 0.28f, 1f), 1.1f, 0f);
        CreateBottleDetails();
        particles = CreateParticleSystem();
        windRibbons = CreateWindRibbons();
    }

    private void CreateBottleDetails()
    {
        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        liquid.name = "PotionLiquid";
        liquid.transform.SetParent(visualRoot, false);
        liquid.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        liquid.transform.localScale = new Vector3(0.72f, 0.58f, 0.72f);
        RemoveCollider(liquid);
        liquid.GetComponent<Renderer>().sharedMaterial = bottleMaterial;

        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        neck.name = "PotionNeck";
        neck.transform.SetParent(visualRoot, false);
        neck.transform.localPosition = new Vector3(0f, 0.43f, 0f);
        neck.transform.localScale = new Vector3(0.22f, 0.24f, 0.22f);
        RemoveCollider(neck);
        neck.GetComponent<Renderer>().sharedMaterial = bottleMaterial;

        GameObject stopper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stopper.name = "PotionStopper";
        stopper.transform.SetParent(visualRoot, false);
        stopper.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        stopper.transform.localScale = new Vector3(0.28f, 0.12f, 0.28f);
        RemoveCollider(stopper);
        stopper.GetComponent<Renderer>().sharedMaterial = capMaterial;
    }

    private ParticleSystem CreateParticleSystem()
    {
        GameObject effectObject = new GameObject("PotionMagicParticles");
        effectObject.transform.SetParent(visualRoot, false);
        effectObject.transform.localPosition = Vector3.zero;
        ParticleSystem system = effectObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = 96;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 18f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = effectRadius;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.22f);
        velocity.radial = new ParticleSystem.MinMaxCurve(0.08f);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateParticleMaterial();
        return system;
    }

    private LineRenderer[] CreateWindRibbons()
    {
        LineRenderer[] ribbons = new LineRenderer[2];
        for (int ribbonIndex = 0; ribbonIndex < ribbons.Length; ribbonIndex++)
        {
            GameObject ribbonObject = new GameObject("WindRibbon" + ribbonIndex);
            ribbonObject.transform.SetParent(visualRoot, false);
            LineRenderer ribbon = ribbonObject.AddComponent<LineRenderer>();
            ribbon.useWorldSpace = false;
            ribbon.positionCount = 18;
            ribbon.loop = false;
            ribbon.widthMultiplier = 0.018f;
            ribbon.numCapVertices = 3;
            ribbon.numCornerVertices = 3;
            ribbon.material = CreateParticleMaterial();
            ribbons[ribbonIndex] = ribbon;
        }
        return ribbons;
    }

    private void ConfigureParticles(MagicType type, Color primary, Color secondary)
    {
        if (particles == null) return;
        ParticleSystem.MainModule main = particles.main;
        ParticleSystem.EmissionModule emission = particles.emission;
        ParticleSystem.ShapeModule shape = particles.shape;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;

        if (!showEffect)
        {
            emission.rateOverTime = 0f;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        particles.Play(true);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
        emission.rateOverTime = type == MagicType.Fire ? 22f : 16f;
        shape.radius = type == MagicType.Wind ? effectRadius * 1.15f : effectRadius * 0.75f;
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(type == MagicType.Wind ? 0.8f : 0.22f);
        velocity.radial = new ParticleSystem.MinMaxCurve(type == MagicType.Water ? 0.15f : 0.08f);

        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(secondary, 0f), new GradientColorKey(primary, 0.55f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.18f), new GradientAlphaKey(0.55f, 0.78f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void ConfigureWindRibbons(MagicType type, Color color)
    {
        if (windRibbons == null) return;
        bool visible = showEffect && type == MagicType.Wind;
        for (int ribbonIndex = 0; ribbonIndex < windRibbons.Length; ribbonIndex++)
        {
            windRibbons[ribbonIndex].gameObject.SetActive(visible);
            if (visible)
            {
                windRibbons[ribbonIndex].startColor = new Color(color.r, color.g, color.b, 0.72f);
                windRibbons[ribbonIndex].endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }
    }

    private void AnimateWindRibbons()
    {
        for (int ribbonIndex = 0; ribbonIndex < windRibbons.Length; ribbonIndex++)
        {
            LineRenderer ribbon = windRibbons[ribbonIndex];
            float phase = ribbonIndex * Mathf.PI + animationTime * (ribbonIndex == 0 ? 1.4f : -1.1f);
            for (int point = 0; point < ribbon.positionCount; point++)
            {
                float t = point / (float)(ribbon.positionCount - 1);
                float angle = phase + t * Mathf.PI * 2.2f;
                float radius = effectRadius * (0.55f + 0.35f * Mathf.Sin(t * Mathf.PI));
                float y = Mathf.Lerp(-0.55f, 0.72f, t);
                ribbon.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            }
        }
    }

    private static Color GetPrimaryColor(MagicType type)
    {
        return type switch
        {
            MagicType.Water => Water,
            MagicType.Wind => Wind,
            _ => Fire,
        };
    }

    private static Color GetSecondaryColor(MagicType type)
    {
        return type switch
        {
            MagicType.Water => new Color(0.45f, 0.95f, 1f, 1f),
            MagicType.Wind => new Color(0.75f, 1f, 0.55f, 1f),
            _ => new Color(1f, 0.7f, 0.1f, 1f),
        };
    }

    private static Material CreateMaterial(string name, Color color, float emissionStrength, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        SetMaterialColor(material, color, emissionStrength);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.78f);
        return material;
    }

    private static Material CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { name = "PotionMagicParticleMaterial" };
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color, float emissionStrength)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * emissionStrength);
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color * emissionStrength);
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }
}
