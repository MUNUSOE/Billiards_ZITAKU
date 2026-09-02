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

        if (MagicManager.Instance != null && usedMagic != MagicType.None)
        {
            MagicManager.Instance.ConsumeMagic(usedMagic);
        }

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