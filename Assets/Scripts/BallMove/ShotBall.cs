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
    [SerializeField] private Color windColor = Color.green;   // 風（緑）

    private Transform arrow;
    private bool isMoving = false;
    private bool gameOverTriggered = false;
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
    }

    // 主ボールが実際にポケットへ入った瞬間にゲームオーバーを通知します。
    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.CompareTag("Pocket"))
        {
            TriggerGameOverFromPocket();
        }
    }

    // 経路予測ではなく実際のポケット接触を基準に、一度だけゲームオーバーを実行します。
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
        // 選択中の魔法に応じて主ボールの色を変更します。
        UpdateBallColor();

        // 移動中、または残り手数が0以下の場合は操作できません。
        bool canOperate = !isMoving && (GameManager.Instance == null || GameManager.Instance.CurrentMoves > 0);

        if (canOperate)
        {
            UpdateArrowByMouse();
            HandlePowerChange();

            if (clickAction.WasPressedThisFrame())
            {
                ShootFromMouse();
            }
        }
    }

    private void UpdateBallColor()
    {
        if (ballRenderer == null || MagicManager.Instance == null) return;

        MagicType active = MagicManager.Instance.ActiveMagic;

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

    void HandlePowerChange()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            if (currentLevel > 0)
            {
                currentLevel--;
                UpdateArrowObject();
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.WeakArrow);
            }
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame)
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
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        var cam = Camera.main;
        if (cam == null) return;

        float depth = Vector3.Distance(cam.transform.position, transform.position);
        Vector3 worldMouse = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth));
        Vector3 rawDir = transform.position - worldMouse;
        rawDir.y = 0f;

        if (rawDir.sqrMagnitude < 0.0001f) return;
        Vector3 dir = BallPath.Get8Direction(rawDir.normalized);

        float arrowDistance = 0.7f;
        Vector3 pos = transform.position + dir * arrowDistance;
        pos.y = transform.position.y;
        arrow.position = pos;

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        arrow.localRotation = Quaternion.Euler(90f, angle, 180f);
    }

    void ShootFromMouse()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        var cam = Camera.main;
        if (cam == null) return;

        float depth = Vector3.Distance(cam.transform.position, transform.position);
        Vector3 worldMouse = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth));
        Vector3 rawDir = transform.position - worldMouse;
        rawDir.y = 0f;

        moveDir = BallPath.Get8Direction(rawDir.normalized);
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
        if (arrow != null) arrow.gameObject.SetActive(false);

        // 移動アニメーションと連鎖が完了するまで待機します。
        yield return BallPath.PlayChain(steps);

        // ★ 移動完了時に対象オブジェクトが既に破棄されていれば処理中断
        if (this == null || gameObject == null) yield break;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

        // 主ボールの経路上にポケット到達が記録されていれば、反射経路でも確実にゲームオーバーにします。
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

        // 主ボールがポケットへ入った場合の処理です。
        if (fellIntoPocket)
        {
            TriggerGameOverFromPocket();
            yield break;
        }

        // 水・風魔法はショット球と連鎖が停止した後、一度だけ発動します。
        // 魔法で動くターゲット球は、瞬間移動ではなくスライド完了まで待機します。
        if (usedMagic == MagicType.Water)
        {
            yield return WaterMagic.ApplyPull(gameObject);
        }
        else if (usedMagic == MagicType.Wind)
        {
            yield return WindMagic.ApplyPush(gameObject);
        }

        // ★ 魔法処理後に対象オブジェクトが破棄された場合はここで中断（ゲームオーバー等で消滅した場合）
        if (this == null || gameObject == null) yield break;

        // 水・風魔法を含め、ショット後に未消費の魔法を消費します。
        if (MagicManager.Instance != null && usedMagic != MagicType.None)
        {
            MagicManager.Instance.ConsumeMagic(usedMagic);
        }

        isMoving = false;

        // 移動完了後に手数を消費します。
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