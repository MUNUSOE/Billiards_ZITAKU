using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TargetBall : MonoBehaviour
{

    [Header("Stage Start Effect")]
    [Tooltip("ステージ開始時に位置を示すエフェクトを表示します。")]
    [SerializeField] private bool showStartEffect = true;
    [Tooltip("Prefab/Effects/StartEffect_Sub を登録してください。")]
    [SerializeField] private GameObject startEffectPrefab;
    [Tooltip("表示する秒数（停止中も実時間で進みます）。")]
    [SerializeField, Min(0.1f)] private float startEffectDuration = 2f;
    [Tooltip("ボールの開始位置からのワールド座標オフセット。")]
    [SerializeField] private Vector3 startEffectOffset = Vector3.zero;
    private GameObject startEffectInstance;

    [Header("Puzzle Settings")]
    public float panelSize = 1f;

    [Header("Movement Settings")]
    public float shotSpeed = 5f;
    public float shotDuration = 1.5f;

    [Header("Collision Settings")]
    public float ballRadius = 0.25f;

    private bool isMoving = false;

    public Vector3 SnapToGrid(Vector3 pos)
    {
        return BallPath.SnapToGrid(pos, panelSize);
    }

    // ★外部(ギミック等)から直接押し出された場合の入口。
    // 通常のショットでは BallPath.SimulateChain が連鎖をまとめて処理するため、
    // このメソッドは呼ばれない。ここが呼ばれるのは外部から明示的に押し出された時だけ。
    // 自前で連鎖を組み立てず、必ず BallPath 側の窓口(PushBallRoutine)に委ねることで、
    // 移動済みボールの除外(SimState)・ポケット・エネルギー計算が
    // 通常のショットとまったく同じ扱いになる。
    public void BePushed(Vector3 pushDirection, int totalPanels)
    {
        if (isMoving) return;
        if (totalPanels <= 0) return;
        StartCoroutine(RunPush(pushDirection, totalPanels));
    }

    IEnumerator RunPush(Vector3 pushDirection, int totalPanels)
    {
        isMoving = true;

        yield return BallPath.PushBallRoutine(gameObject, pushDirection, totalPanels);

        // ★自分がポケットに落ちて Destroy された場合、この先の後片付けはできない
        if (this == null) yield break;

        isMoving = false;
    }

    // Startは、この球が初めて有効になったときに一度だけ実行される。
    private IEnumerator Start()
    {
        if (!showStartEffect) yield break;
        if (startEffectPrefab == null)
        {
            Debug.LogWarning("[TargetBall] Start Effect PrefabにStartEffect_Subを登録してください。", this);
            yield break;
        }

        // 球を親にせず、開始位置に固定。Prefabの回転・スケールは保持する。
        startEffectInstance = Instantiate(startEffectPrefab,
            transform.position + startEffectOffset, startEffectPrefab.transform.rotation);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            startEffectInstance, gameObject.scene);
        startEffectInstance.SetActive(true);

        // Play On AwakeがOFFのPrefabも再生する。
        foreach (var particles in startEffectInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.useUnscaledTime = true;
            if (particles.gameObject.activeInHierarchy) particles.Play(false);
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, startEffectDuration));
        ClearStartEffect();
    }

    private void OnDisable()
    {
        ClearStartEffect();
    }

    private void ClearStartEffect()
    {
        if (startEffectInstance == null) return;
        Destroy(startEffectInstance);
        startEffectInstance = null;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AssignDefaultStartEffect();
    }

    private void OnValidate()
    {
        AssignDefaultStartEffect();
    }

    // 通常フォルダのPrefabをEditorで参照に変換する。ビルドでは保存済み参照を使用。
    private void AssignDefaultStartEffect()
    {
        if (startEffectPrefab != null) return;
        startEffectPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/Effects/StartEffect_Sub.prefab");
    }
#endif

    private void OnDestroy()
    {
        ClearStartEffect();
    }

}