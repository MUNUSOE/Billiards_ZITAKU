using System.Collections;
using UnityEngine;

/// <summary>
/// 水魔法で消火するまで危険な炎マスです。
/// BallPath が移動先セルでこのコンポーネントを確認し、水なしの到達をゲームオーバーとして扱います。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class FlameTile : MonoBehaviour
{
    [Header("Flame Tile Settings")]
    [Tooltip("水魔法で消火済みかどうかです。実行中に変更する必要はありません。")]
    [SerializeField] private bool extinguished = false;

    [Tooltip("消火時に非表示にする炎の見た目。未指定ならこのオブジェクトのRendererを無効化します。")]
    [SerializeField] private GameObject flameVisual;

    [Tooltip("消火時に無効化するCollider。未指定なら同じオブジェクトのColliderを無効化します。")]
    [SerializeField] private Collider flameCollider;

    [Header("Extinguish Effect Settings")]
    [Tooltip("消火時に生成・再生するエフェクトのPrefab（またはシーン内のParticleSystem/GameObject）。")]
    [SerializeField] private GameObject extinguishEffectPrefab;

    [Tooltip("エフェクトを生成する位置。未指定の場合はこのオブジェクトの位置を中心に生成します。")]
    [SerializeField] private Transform effectSpawnPoint;

    [Header("Delay Settings")]
    [Tooltip("消火SEを鳴らすまでのディレイ時間（秒）")]
    [Min(0f)][SerializeField] private float seDelay = 0.0f;

    [Tooltip("消火エフェクトを表示するまでのディレイ時間（秒）")]
    [Min(0f)][SerializeField] private float effectDelay = 0.0f;

    public bool IsActiveFlame => !extinguished;

    // アクティブな FlameTile の総数を保持する静的変数
    private static int activeFlameCount = 0;

    private void Awake()
    {
        if (flameCollider == null) flameCollider = GetComponent<Collider>();
        ApplyExtinguishedState();
    }

    private void OnEnable()
    {
        if (!extinguished)
        {
            RegisterFlame();
        }
    }

    private void OnDisable()
    {
        if (!extinguished)
        {
            UnregisterFlame();
        }
    }

    private void OnValidate()
    {
        if (flameCollider == null) flameCollider = GetComponent<Collider>();
        ApplyExtinguishedState();
    }

    /// <summary>
    /// 炎マスを消火し、同一ショット後の移動でも通過可能な状態にします。
    /// </summary>
    public void Extinguish()
    {
        if (extinguished) return;

        extinguished = true;
        UnregisterFlame();
        ApplyExtinguishedState();

        // ディレイ付きでSEを再生
        if (seDelay > 0f)
        {
            StartCoroutine(PlaySEDelayed(seDelay));
        }
        else
        {
            PlaySEDirect();
        }

        // ディレイ付きでエフェクトを再生
        if (effectDelay > 0f)
        {
            StartCoroutine(PlayEffectDelayed(effectDelay));
        }
        else
        {
            PlayExtinguishEffect();
        }
    }

    // --- SEの遅延処理 ---

    private IEnumerator PlaySEDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySEDirect();
    }

    private void PlaySEDirect()
    {
        if (SoundManager.Instance != null)
        {
            // ※プロジェクト内の該当する消火音Enum（例: SEType.WaterMagic や SEType.Extinguish）を指定してください
            SoundManager.Instance.PlaySE(SEType.WaterMagic);
        }
    }

    // --- エフェクトの遅延処理 ---

    private IEnumerator PlayEffectDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayExtinguishEffect();
    }

    /// <summary>
    /// 消火時のエフェクトを生成または再生します。
    /// </summary>
    private void PlayExtinguishEffect()
    {
        if (extinguishEffectPrefab == null) return;

        Vector3 spawnPosition = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;

        // effectSpawnPoint が指定されている場合はその回転、未指定なら Prefab 自体が持っている回転（-90,0,0など）を維持する
        Quaternion spawnRotation = effectSpawnPoint != null
            ? effectSpawnPoint.rotation
            : extinguishEffectPrefab.transform.rotation;

        // エフェクトがPrefabの場合は生成、シーン内のオブジェクトならアクティブ化して再生
        if (!extinguishEffectPrefab.scene.IsValid())
        {
            // Prefabからインスタンス化（回転を維持）
            GameObject effectInstance = Instantiate(extinguishEffectPrefab, spawnPosition, spawnRotation);

            // ParticleSystemがあれば自動破棄を設定（または一定時間後にDestroy）
            var ps = effectInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
            else
            {
                // ParticleSystemが付いていない演出用Prefabの場合は一定時間後(2秒後)に破棄
                Destroy(effectInstance, 2.0f);
            }
        }
        else
        {
            // シーン上に既にある子オブジェクトなどのエフェクトを有効化して再生する場合
            extinguishEffectPrefab.transform.position = spawnPosition;
            extinguishEffectPrefab.transform.rotation = spawnRotation;
            extinguishEffectPrefab.SetActive(true);

            var ps = extinguishEffectPrefab.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
        }
    }

    private static void RegisterFlame()
    {
        activeFlameCount++;
        if (activeFlameCount == 1)
        {
            // 画面上に最初の1個目の炎が現れたらループ再生を開始
            SoundManager.Instance?.PlayLoopSE(SEType.FrameTile);
        }
    }

    private static void UnregisterFlame()
    {
        activeFlameCount--;
        if (activeFlameCount <= 0)
        {
            activeFlameCount = 0;
            // 画面上の炎がすべて消火（または破棄）されたらループ停止
            SoundManager.Instance?.StopLoopSE();
        }
    }

    private void ApplyExtinguishedState()
    {
        if (flameVisual != null)
        {
            flameVisual.SetActive(!extinguished);
        }
        else
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = !extinguished;
        }

        if (flameCollider != null)
        {
            flameCollider.enabled = !extinguished;
        }
    }
}