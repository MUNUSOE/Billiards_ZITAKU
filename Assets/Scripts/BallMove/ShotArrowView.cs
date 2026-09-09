using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ショットの方向を示す矢印の表示を担当します。
/// 威力ごとに別の3Dオブジェクトを Inspector で登録し、選択中の威力に応じて切り替えます。
///
/// ショット球とは別のオブジェクトに置いても、子オブジェクトとして置いても動きます。
/// 位置と向きは毎フレーム ShotBall から更新されます。
/// </summary>
public class ShotArrowView : MonoBehaviour
{
    [System.Serializable]
    public class ArrowEntry
    {
        [Tooltip("Inspector で見分けるための名前。動作には影響しません。")]
        public string label;

        [Tooltip("この威力のときに表示する矢印の3Dオブジェクト。")]
        public GameObject arrowObject;

        [Tooltip("この矢印だけ距離を個別に指定する。オフなら共通の Distance From Ball を使います。")]
        public bool overrideDistance = false;

        [Tooltip("この矢印を球からどれだけ離すか。Override Distance がオンのときだけ使われます。")]
        public float distanceFromBall = 0.7f;

        [Tooltip("この矢印だけの位置の微調整。\nX=狙う方向に対して横、Y=高さ、Z=狙う方向に対して前後。\nモデルの原点が中心からずれている場合の補正に使います。")]
        public Vector3 localOffset = Vector3.zero;
    }

    [Header("Arrows")]
    [Tooltip("威力ごとの矢印。並び順が威力レベルに対応します（0=弱、1=中、2=強）。")]
    [SerializeField] private List<ArrowEntry> arrows = new List<ArrowEntry>();

    [Header("Placement")]
    [Tooltip("ショット球から矢印までの距離。威力ごとに Override Distance を指定した場合はそちらが優先されます。")]
    [SerializeField] private float distanceFromBall = 0.7f;

    [Tooltip("矢印の高さ。ショット球の高さからのオフセットです。全威力に効きます。")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("全威力に共通で効く位置の調整。\nX=狙う方向に対して横、Y=高さ、Z=狙う方向に対して前後。\n威力ごとの Local Offset とは加算されます。")]
    [SerializeField] private Vector3 commonLocalOffset = Vector3.zero;

    [Tooltip("矢印モデルの向きを補正する回転（度）。\nモデルが+Z方向を向いて作られていれば 0 のままで合います。")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Header("Options")]
    [Tooltip("矢印をショット球の位置に置きます。オフにすると、向きだけ更新して位置は動かしません。")]
    [SerializeField] private bool followBallPosition = true;

    private int currentLevel = -1;
    private bool visible = true;

    // 実際に表示に使うオブジェクト。
    // プレハブが指定された場合は、ここに生成した実体が入ります。
    private readonly List<GameObject> runtimeArrows = new List<GameObject>();

    // このコンポーネントが生成したオブジェクト。作り直すときに破棄するため控えておきます。
    private readonly List<GameObject> spawnedArrows = new List<GameObject>();

    // 用意が済んでいるか。二重に生成しないためのフラグです。
    private bool built;

    private void Awake()
    {
        BuildRuntimeArrows();
    }

    /// <summary>
    /// 表示に使うオブジェクトを用意します。
    /// Project のプレハブが指定された場合は、そのままでは画面に出せないため、
    /// このオブジェクトの子として実体を生成して使います。
    /// シーン上のオブジェクトが指定されている場合はそれをそのまま使います。
    /// </summary>
    private void BuildRuntimeArrows()
    {
        // 既に用意済みなら何もしない。
        // ShotBall の Awake が先に走って SetPowerLevel から呼ばれた場合、
        // そのあと自分の Awake でも呼ばれるため、ここで二重生成を防ぎます。
        if (built) return;
        built = true;

        // 作り直しに備えて、以前生成したものがあれば破棄する。
        foreach (GameObject spawned in spawnedArrows)
        {
            if (spawned == null) continue;

            if (Application.isPlaying) Destroy(spawned);
            else DestroyImmediate(spawned);
        }
        spawnedArrows.Clear();

        runtimeArrows.Clear();

        foreach (ArrowEntry entry in arrows)
        {
            if (entry == null || entry.arrowObject == null)
            {
                runtimeArrows.Add(null);
                continue;
            }

            // シーンに存在しないオブジェクト（＝プレハブアセット）なら実体化する。
            bool isPrefabAsset = !entry.arrowObject.scene.IsValid();

            if (isPrefabAsset)
            {
                GameObject instance = Instantiate(entry.arrowObject, transform);
                instance.name = entry.arrowObject.name;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.SetActive(false);
                runtimeArrows.Add(instance);
                spawnedArrows.Add(instance);
            }
            else
            {
                runtimeArrows.Add(entry.arrowObject);
            }
        }

        RefreshActiveArrow();
    }

    /// <summary>現在表示中の矢印。未設定なら null。</summary>
    public Transform CurrentArrow
    {
        get
        {
            if (currentLevel < 0 || currentLevel >= runtimeArrows.Count) return null;
            if (runtimeArrows[currentLevel] == null) return null;
            return runtimeArrows[currentLevel].transform;
        }
    }

    /// <summary>
    /// 表示する矢印を威力レベルで切り替えます。
    /// </summary>
    public void SetPowerLevel(int level)
    {
        // ShotBall の Awake から先に呼ばれる場合に備えて、未準備なら用意する。
        if (runtimeArrows.Count == 0) BuildRuntimeArrows();

        currentLevel = level;
        RefreshActiveArrow();
    }

    /// <summary>矢印の表示・非表示を切り替えます。</summary>
    public void SetVisible(bool value)
    {
        visible = value;
        RefreshActiveArrow();
    }

    /// <summary>
    /// 矢印の位置と向きを更新します。
    /// </summary>
    /// <param name="ballPosition">ショット球の位置。</param>
    /// <param name="direction">狙っている方向（8方向のいずれか）。</param>
    public void UpdateTransform(Vector3 ballPosition, Vector3 direction)
    {
        Transform arrow = CurrentArrow;
        if (arrow == null) return;
        if (direction == Vector3.zero) return;

        ArrowEntry entry = GetCurrentEntry();

        // 狙う方向を向く回転。位置のオフセットもこの向きを基準に適用します。
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Quaternion aimRotation = Quaternion.Euler(0f, angle, 0f);

        if (followBallPosition)
        {
            // 距離は威力ごとの指定があればそちらを使う。
            float distance = (entry != null && entry.overrideDistance)
                ? entry.distanceFromBall
                : distanceFromBall;

            Vector3 position = ballPosition + direction.normalized * distance;
            position.y = ballPosition.y + heightOffset;

            // 位置の微調整。狙う方向を基準にした相対位置で足します。
            // 全威力共通のぶんと、威力ごとのぶんを両方足します。
            Vector3 offset = commonLocalOffset;
            if (entry != null) offset += entry.localOffset;

            if (offset != Vector3.zero)
            {
                position += aimRotation * offset;
            }

            arrow.position = position;
        }

        // 進行方向へ向ける。モデルの向きのずれは rotationOffset で補正します。
        arrow.rotation = aimRotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>現在表示中の矢印の設定。未設定なら null。</summary>
    private ArrowEntry GetCurrentEntry()
    {
        if (currentLevel < 0 || currentLevel >= arrows.Count) return null;
        return arrows[currentLevel];
    }

    /// <summary>選択中の矢印だけを表示し、それ以外を隠します。</summary>
    private void RefreshActiveArrow()
    {
        for (int i = 0; i < runtimeArrows.Count; i++)
        {
            if (runtimeArrows[i] == null) continue;

            bool shouldShow = visible && i == currentLevel;
            runtimeArrows[i].SetActive(shouldShow);
        }
    }
}