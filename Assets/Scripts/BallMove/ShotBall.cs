using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class ShotBall : MonoBehaviour
{
    [Header("Puzzle Settings")]
    public float panelSize = 1f;

    [Header("Movement Settings")]
    public float shotSpeed = 5f;
    public float shotDuration = 1.5f;
    public float[] distanceLevels = { 1f, 2f, 3f };
    public float[] speedLevels = { 4f, 6f, 9f };
    private int currentLevel = 1;

    [Header("Collision Settings")]
    public float ballRadius = 0.25f;

    [Header("Arrow Settings")]
    public GameObject arrowWeakObj;
    public GameObject arrowMiddleObj;
    public GameObject arrowStrongObj;

    [Header("Magic Visual Settings")]
    [SerializeField] private Color normalColor = Color.white; // 通常（白）
    [SerializeField] private Color fireColor = Color.red;     // 炎（赤）
    [SerializeField] private Color waterColor = Color.blue;   // 水（青）
    [SerializeField] private Color windColor = Color.green;  // 風（緑）

    [Header("Magic Select Effect Prefabs (選択中)")]
    [Tooltip("炎魔法選択時にボールに付与するエフェクト")]
    [SerializeField] private GameObject fireEffectPrefab;
    [SerializeField] private float fireEffectOffsetY = 0f;

    [Tooltip("水魔法選択時にボールに付与するエフェクト")]
    [SerializeField] private GameObject waterEffectPrefab;
    [SerializeField] private float waterEffectOffsetY = 0f;

    [Tooltip("風魔法選択時にボールに付与するエフェクト")]
    [SerializeField] private GameObject windEffectPrefab;
    [SerializeField] private float windEffectOffsetY = 0f;

    [Header("Magic Action Effect Prefabs (発動時)")]
    [Tooltip("水魔法発動（引き寄せ）時に発生させる専用エフェクト")]
    [SerializeField] private GameObject waterCastEffectPrefab;
    [Tooltip("水魔法発動時エフェクトのY軸オフセット調整")]
    [SerializeField] private float waterCastEffectOffsetY = 0f;

    [Tooltip("風魔法発動（押し出し）時に発生させる専用エフェクト")]
    [SerializeField] private GameObject windCastEffectPrefab;
    [Tooltip("風魔法発動時エフェクトのY軸オフセット調整")]
    [SerializeField] private float windCastEffectOffsetY = 0f;

    // 現在ボールに追従・表示している選択中エフェクトのインスタンス
    private GameObject currentMagicEffectObj;
    private MagicType currentActiveMagic = MagicType.None;

    [Header("Keyboard Aim")]
    [Tooltip("矢印キーでの照準とEnterキーでのショットを有効にします。")]
    [SerializeField] private bool enableKeyboardAim = true;

    [Tooltip("マウスをこのピクセル数以上動かすと、キーボード照準からマウス照準へ戻ります。")]
    [SerializeField] private float mouseSwitchThreshold = 2f;

    // キーボードで指定中の方向。Vector3.zero なら未指定。
    private Vector3 keyboardAimDir = Vector3.zero;
    // 直近に確定した狙いの方向。方向変更が禁止されている間はこれを保持し続ける。
    private Vector3 lastAimDirection = Vector3.zero;
    // 現在キーボードで狙っているか（false ならマウス）。
    private bool usingKeyboardAim = false;
    private Vector2 lastMousePosition;

    [Header("Shot Preview (チュートリアル用)")]
    [Tooltip("ショットの予測表示。GameManager の Show Shot Preview がオンのときだけ動きます。")]
    [SerializeField] private ShotPreview shotPreview;

    private Transform arrow;
    private bool isMoving = false;
    private bool gameOverTriggered = false;

    /// <summary>
    /// 矢印の操作やショットが可能な状態か。
    /// Update 内の canOperate と同じ条件で、魔法ボタンの押下可否の判定にも使います。
    /// </summary>
    public bool IsOperable =>
        !isMoving
        // オプション画面を開いている間は操作を受け付けない。
        && (OptionManager.Instance == null || !OptionManager.Instance.IsPaused)
        // ヘルプ画面を開いている間は操作を受け付けない。
        && (HelpManager.Instance == null || !HelpManager.Instance.IsHelpOpen)
        // ヒント画面を開いている間は操作を受け付けない。
        && (HintManager.Instance == null || !HintManager.Instance.IsHintOpen)
        && (GameManager.Instance == null || GameManager.Instance.CurrentMoves > 0)
        // 最後のターゲットが落ちてクリア確定を待っている間に撃たれると、
        // 手数が減ってゲームオーバーになってしまうため操作を止める。
        && (GameClear.Instance == null || !GameClear.Instance.IsClearPendingOrTriggered)
        // ポケットへの吸い込み演出中は、まだ球が消えておらずクリア判定が成立しないため、
        // その隙に撃たれて手数が減らないよう操作を止める。
        && !Pocket.IsAnyBallBeingPocketed;

    private Vector3 moveDir;
    private InputAction clickAction;
    private Renderer ballRenderer;

    void Awake()
    {
        clickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");
        clickAction.Enable();

        ballRenderer = GetComponent<Renderer>();
        if (ballRenderer != null)
        {
            normalColor = ballRenderer.material.color;
        }

        ApplyPowerLevel();
        UpdateArrowObject();
    }

    void OnDestroy()
    {
        clickAction.Disable();

        if (currentMagicEffectObj != null)
        {
            Destroy(currentMagicEffectObj);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.CompareTag("Pocket"))
        {
            TriggerGameOverFromPocket();
        }
    }

    public void TriggerGameOverFromPocket()
    {
        if (gameOverTriggered) return;

        gameOverTriggered = true;
        isMoving = true;

        if (arrow != null) arrow.gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.Pocket);
        }

        Destroy(gameObject);
    }

    void Update()
    {
        UpdateBallColorAndEffect();

        // チュートリアルで威力が固定されている場合は、その値に合わせる。
        if (TutorialInputGate.IsActive && TutorialInputGate.ForcedPowerLevel >= 0
            && currentLevel != TutorialInputGate.ForcedPowerLevel)
        {
            currentLevel = Mathf.Clamp(TutorialInputGate.ForcedPowerLevel, 0, distanceLevels.Length - 1);
            UpdateArrowObject();
        }

        bool canOperate = IsOperable;

        if (canOperate)
        {
            UpdateAimInput();
            UpdateArrowByMouse();
            HandlePowerChange();
            UpdateShotPreview();

            if (clickAction.WasPressedThisFrame() || IsShootKeyPressed())
            {
                ShootFromMouse();
            }
        }
        else if (shotPreview != null)
        {
            // 移動中やクリア待ちの間は予測を消す。
            shotPreview.Hide();
        }
    }

    /// <summary>
    /// 現在の狙いにあわせてショット予測を更新します。
    /// GameManager の Show Shot Preview がオフのときは何も表示しません。
    /// </summary>
    private void UpdateShotPreview()
    {
        if (shotPreview == null) return;

        bool enabledByStage = GameManager.Instance != null && GameManager.Instance.ShowShotPreview;
        if (!enabledByStage)
        {
            shotPreview.Hide();
            return;
        }

        Vector3 dir = GetAimDirection();
        if (dir == Vector3.zero)
        {
            shotPreview.Hide();
            return;
        }

        int targetPanels = Mathf.RoundToInt(distanceLevels[currentLevel]);
        bool isFireActive = MagicManager.Instance != null
            && MagicManager.Instance.ActiveMagic == MagicType.Fire;

        shotPreview.Show(gameObject, dir, targetPanels, isFireActive);
    }

    /// <summary>
    /// 矢印キーとマウスの入力を見て、どちらで狙っているかと方向を更新します。
    /// 矢印キーを押すとキーボード照準に切り替わり、マウスを動かすとマウス照準に戻ります。
    /// 斜め方向は2つの矢印キーを同時に押して指定します。
    /// </summary>
    private void UpdateAimInput()
    {
        // チュートリアルで方向変更が禁止されている間は受け付けない。
        if (TutorialInputGate.IsActive && !TutorialInputGate.AllowDirection) return;

        Keyboard keyboard = Keyboard.current;

        if (enableKeyboardAim && keyboard != null)
        {
            // いずれかの矢印キーが「押された瞬間」だけ方向を確定する。
            // 押されている間ずっと更新すると、斜め（2キー同時押し）から片方を離したとき、
            // 残った1キーの方向に上書きされてしまい、斜めのまま狙えない。
            bool anyArrowPressedThisFrame =
                keyboard.leftArrowKey.wasPressedThisFrame ||
                keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.downArrowKey.wasPressedThisFrame;

            if (anyArrowPressedThisFrame)
            {
                int x = 0;
                int z = 0;

                if (keyboard.leftArrowKey.isPressed) x -= 1;
                if (keyboard.rightArrowKey.isPressed) x += 1;
                if (keyboard.upArrowKey.isPressed) z += 1;
                if (keyboard.downArrowKey.isPressed) z -= 1;

                if (x != 0 || z != 0)
                {
                    keyboardAimDir = BallPath.Get8Direction(new Vector3(x, 0f, z).normalized);
                    usingKeyboardAim = true;
                }
            }
        }

        // マウスが動いたらマウス照準へ戻す。
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if ((position - lastMousePosition).sqrMagnitude > mouseSwitchThreshold * mouseSwitchThreshold)
            {
                usingKeyboardAim = false;
            }
            lastMousePosition = position;
        }
    }

    /// <summary>ショット実行のキーが押されたか。</summary>
    private bool IsShootKeyPressed()
    {
        if (!enableKeyboardAim) return false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    /// <summary>
    /// 現在の狙いの方向を8方向で返します。求められない場合は zero。
    /// キーボード照準中はその方向を、そうでなければマウス位置から求めます。
    /// </summary>
    private Vector3 GetAimDirection()
    {
        // チュートリアルで方向が固定されている場合はそれを返す。
        if (TutorialInputGate.IsActive && TutorialInputGate.UseForcedAim)
        {
            lastAimDirection = BallPath.Get8Direction(TutorialInputGate.ForcedDirection);
            return lastAimDirection;
        }

        // チュートリアルで方向変更が禁止されている間は、直近の方向を保持し続ける。
        // ここでマウス位置から計算し直すと、入力を無視していても矢印がカーソルに追従してしまう。
        if (TutorialInputGate.IsActive && !TutorialInputGate.AllowDirection)
        {
            if (lastAimDirection != Vector3.zero) return lastAimDirection;
        }

        Vector3 dir = ComputeAimDirection();
        if (dir != Vector3.zero) lastAimDirection = dir;

        return dir;
    }

    /// <summary>
    /// 狙いの方向を外部から設定します。チュートリアルで初期値を与えるために使います。
    /// </summary>
    public void SetAimDirection(Vector3 direction)
    {
        if (direction == Vector3.zero) return;

        Vector3 dir = BallPath.Get8Direction(direction.normalized);
        keyboardAimDir = dir;
        lastAimDirection = dir;
        usingKeyboardAim = true;

        UpdateArrowByMouse();
    }

    /// <summary>
    /// 威力レベルを外部から設定します。チュートリアルで初期値を与えるために使います。
    /// </summary>
    public void SetPowerLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, distanceLevels.Length - 1);
        ApplyPowerLevel();
        UpdateArrowObject();
    }

    /// <summary>キーボードまたはマウスの現在の入力から、狙いの方向を計算します。</summary>
    private Vector3 ComputeAimDirection()
    {
        if (usingKeyboardAim && keyboardAimDir != Vector3.zero)
        {
            return keyboardAimDir;
        }

        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        float depth = Vector3.Distance(cam.transform.position, transform.position);
        Vector3 worldMouse = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth));
        Vector3 rawDir = transform.position - worldMouse;
        rawDir.y = 0f;

        if (rawDir.sqrMagnitude < 0.0001f) return Vector3.zero;

        return BallPath.Get8Direction(rawDir.normalized);
    }

    private void UpdateBallColorAndEffect()
    {
        if (MagicManager.Instance == null) return;

        MagicType active = MagicManager.Instance.ActiveMagic;

        if (currentActiveMagic != active)
        {
            currentActiveMagic = active;
            ChangeMagicEffect(active);
        }

        if (ballRenderer != null)
        {
            switch (active)
            {
                case MagicType.Fire:
                    ballRenderer.material.color = fireColor;
                    break;
                case MagicType.Water:
                    ballRenderer.material.color = waterColor;
                    break;
                case MagicType.Wind:
                    ballRenderer.material.color = windColor;
                    break;
                default:
                    ballRenderer.material.color = normalColor;
                    break;
            }
        }
    }

    private void ChangeMagicEffect(MagicType active)
    {
        if (currentMagicEffectObj != null)
        {
            Destroy(currentMagicEffectObj);
            currentMagicEffectObj = null;
        }

        GameObject prefabToInstantiate = null;
        float offsetY = 0f;

        switch (active)
        {
            case MagicType.Fire:
                prefabToInstantiate = fireEffectPrefab;
                offsetY = fireEffectOffsetY;
                break;
            case MagicType.Water:
                prefabToInstantiate = waterEffectPrefab;
                offsetY = waterEffectOffsetY;
                break;
            case MagicType.Wind:
                prefabToInstantiate = windEffectPrefab;
                offsetY = windEffectOffsetY;
                break;
        }

        if (prefabToInstantiate != null)
        {
            currentMagicEffectObj = Instantiate(prefabToInstantiate, transform.position, Quaternion.identity, transform);
            currentMagicEffectObj.transform.localPosition = new Vector3(0f, offsetY, 0f);
        }
    }

    void HandlePowerChange()
    {
        // チュートリアルで威力変更が禁止されている間は受け付けない。
        if (TutorialInputGate.IsActive && !TutorialInputGate.AllowPower) return;

        bool decrease = false;
        bool increase = false;

        // キーボード（A/Dキー）での判定
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) decrease = true;
            else if (Keyboard.current.dKey.wasPressedThisFrame) increase = true;
        }

        // マウスホイールでの判定
        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (scrollY > 0f) increase = true;       // 上スクロールで増加
            else if (scrollY < 0f) decrease = true;  // 下スクロールで減少
        }

        if (decrease)
        {
            if (currentLevel > 0)
            {
                currentLevel--;
                UpdateArrowObject();
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.WeakArrow);
            }
        }
        else if (increase)
        {
            if (currentLevel < distanceLevels.Length - 1)
            {
                currentLevel++;
                UpdateArrowObject();
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.StrongArrow);
            }
        }
    }

    void ApplyPowerLevel()
    {
        shotSpeed = speedLevels[currentLevel];
    }

    void UpdateArrowObject()
    {
        if (arrowWeakObj != null) arrowWeakObj.SetActive(false);
        if (arrowMiddleObj != null) arrowMiddleObj.SetActive(false);
        if (arrowStrongObj != null) arrowStrongObj.SetActive(false);

        switch (currentLevel)
        {
            case 0: if (arrowWeakObj != null) { arrowWeakObj.SetActive(true); arrow = arrowWeakObj.transform; } break;
            case 1: if (arrowMiddleObj != null) { arrowMiddleObj.SetActive(true); arrow = arrowMiddleObj.transform; } break;
            case 2: if (arrowStrongObj != null) { arrowStrongObj.SetActive(true); arrow = arrowStrongObj.transform; } break;
        }
    }

    void UpdateArrowByMouse()
    {
        if (arrow == null) return;

        // マウス・キーボードのどちらの照準にも対応するため、共通の方向取得を使う。
        Vector3 dir = GetAimDirection();
        if (dir == Vector3.zero) return;

        float arrowDistance = 0.7f;
        Vector3 pos = transform.position + dir * arrowDistance;
        pos.y = transform.position.y;
        arrow.position = pos;

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        arrow.localRotation = Quaternion.Euler(90f, angle, 180f);
    }

    void ShootFromMouse()
    {
        // チュートリアルで打ち出しが禁止されている間は撃てない。
        if (TutorialInputGate.IsActive && !TutorialInputGate.AllowShot) return;

        Vector3 aimDir = GetAimDirection();
        if (aimDir == Vector3.zero) return;

        // チュートリアル側へ「打った」ことを知らせる。
        TutorialInputGate.NotifyShotFired();

        // ボールを打ち出したら、画面上部のヒントテキストを消去する
        if (HintManager.Instance != null)
        {
            HintManager.Instance.HideHintText();
        }

        moveDir = aimDir;
        ApplyPowerLevel();

        int targetPanels = Mathf.RoundToInt(distanceLevels[currentLevel]);

        MagicType activeMagicAtShot = MagicManager.Instance != null ? MagicManager.Instance.ActiveMagic : MagicType.None;
        bool isFireActive = (activeMagicAtShot == MagicType.Fire);

        List<BallPath.ChainStep> steps = BallPath.SimulateChain(gameObject, moveDir, targetPanels, isFireActive);
        StartCoroutine(RunChain(steps, activeMagicAtShot));
    }

    IEnumerator RunChain(List<BallPath.ChainStep> steps, MagicType usedMagic)
    {
        isMoving = true;
        if (shotPreview != null) shotPreview.Hide();
        if (arrow != null) arrow.gameObject.SetActive(false);

        yield return BallPath.PlayChain(steps);

        if (this == null || gameObject == null) yield break;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

        bool fellIntoPocket = false;
        if (steps != null && steps.Count > 0)
        {
            var firstStep = steps[0];
            if (firstStep.path != null)
            {
                foreach (var point in firstStep.path)
                {
                    if (point != null && point.isPocket)
                    {
                        fellIntoPocket = true;
                        break;
                    }
                }
            }
        }

        if (fellIntoPocket)
        {
            TriggerGameOverFromPocket();
            yield break;
        }

        // 水・風魔法ともに専用の発動時エフェクトとY座標オフセットを渡して呼び出し
        if (usedMagic == MagicType.Water)
        {
            yield return WaterMagic.ApplyPull(gameObject, waterCastEffectPrefab, waterCastEffectOffsetY);
        }
        else if (usedMagic == MagicType.Wind)
        {
            yield return WindMagic.ApplyPush(gameObject, windCastEffectPrefab, windCastEffectOffsetY);
        }

        if (this == null || gameObject == null) yield break;

        if (MagicManager.Instance != null)
        {
            if (usedMagic != MagicType.None)
            {
                MagicManager.Instance.ConsumeMagic(usedMagic);
            }

            // 消費が終わったあとに、このショットで取得したポーションの回復を適用する。
            // 順序を逆にすると、使った魔法のポーションを取っても回復しない。
            MagicManager.Instance.ApplyPendingPotionRestores();
        }

        // ポケットへの吸い込み演出が終わるまで待つ。
        // 演出中はまだ球が Destroy されておらずクリア判定が成立しないため、
        // 先に手数を消費すると「最後の球を落として手数0」のときに
        // クリアより先にゲームオーバーが確定してしまう。
        while (Pocket.IsAnyBallBeingPocketed)
        {
            yield return null;
        }

        // 消滅の反映を1フレーム待ってから手数を消費する。
        yield return null;

        isMoving = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ConsumeMove();
        }

        if (GameManager.Instance == null || GameManager.Instance.CurrentMoves > 0)
        {
            if (arrow != null) arrow.gameObject.SetActive(true);
            UpdateArrowByMouse();
        }
    }

    public void BePushed(Vector3 pushDirection, int totalPanels)
    {
        if (isMoving) return;
        if (totalPanels <= 0) return;
        StartCoroutine(RunPush(pushDirection, totalPanels));
    }

    IEnumerator RunPush(Vector3 pushDirection, int totalPanels)
    {
        isMoving = true;
        if (arrow != null) arrow.gameObject.SetActive(false);

        yield return BallPath.PushBallRoutine(gameObject, pushDirection, totalPanels);

        if (this == null || gameObject == null) yield break;

        isMoving = false;

        if (GameManager.Instance == null || GameManager.Instance.CurrentMoves > 0)
        {
            if (arrow != null) arrow.gameObject.SetActive(true);
            UpdateArrowByMouse();
        }
    }

    public Vector3 SnapToGrid(Vector3 pos)
    {
        return BallPath.SnapToGrid(pos, panelSize);
    }
}