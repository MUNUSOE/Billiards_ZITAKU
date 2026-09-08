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
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color fireColor = Color.red;
    [SerializeField] private Color waterColor = Color.blue;
    [SerializeField] private Color windColor = Color.green;

    [Header("Magic Select Effect Prefabs (選択中)")]
    [SerializeField] private GameObject fireEffectPrefab;
    [SerializeField] private float fireEffectOffsetY = 0f;

    [SerializeField] private GameObject waterEffectPrefab;
    [SerializeField] private float waterEffectOffsetY = 0f;

    [SerializeField] private GameObject windEffectPrefab;
    [SerializeField] private float windEffectOffsetY = 0f;

    [Header("Magic Action Effect Prefabs (発動時)")]
    [SerializeField] private GameObject waterCastEffectPrefab;
    [SerializeField] private float waterCastEffectOffsetY = 0f;

    [SerializeField] private GameObject windCastEffectPrefab;
    [SerializeField] private float windCastEffectOffsetY = 0f;

    private GameObject currentMagicEffectObj;
    private MagicType currentActiveMagic = MagicType.None;

    [Header("Keyboard Aim")]
    [SerializeField] private bool enableKeyboardAim = true;
    [SerializeField] private float mouseSwitchThreshold = 2f;

    private Vector3 keyboardAimDir = Vector3.zero;
    private bool usingKeyboardAim = false;
    private Vector2 lastMousePosition;

    [Header("Shot Preview (チュートリアル用)")]
    [SerializeField] private ShotPreview shotPreview;

    private Transform arrow;
    private bool isMoving = false;
    private bool gameOverTriggered = false;

    /// <summary>
    /// 矢印の操作やショットが可能な状態か。
    /// </summary>
    public bool IsOperable =>
        !isMoving
        && (OptionManager.Instance == null || !OptionManager.Instance.IsPaused)
        && (HelpManager.Instance == null || !HelpManager.Instance.IsHelpOpen)
        && (GameManager.Instance == null || GameManager.Instance.CurrentMoves > 0)
        && (GameClear.Instance == null || !GameClear.Instance.IsClearPendingOrTriggered)
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
            shotPreview.Hide();
        }
    }

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

    private void UpdateAimInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (enableKeyboardAim && keyboard != null)
        {
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

    private bool IsShootKeyPressed()
    {
        if (!enableKeyboardAim) return false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    private Vector3 GetAimDirection()
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
        bool decrease = false;
        bool increase = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) decrease = true;
            else if (Keyboard.current.dKey.wasPressedThisFrame) increase = true;
        }

        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (scrollY > 0f) increase = true;
            else if (scrollY < 0f) decrease = true;
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

    /// <summary>
    /// チュートリアル等から威力を強制的に設定します（0:弱, 1:中, 2:強）。
    /// </summary>
    public void SetPowerLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, distanceLevels.Length - 1);
        ApplyPowerLevel();
        UpdateArrowObject();
    }

    /// <summary>
    /// チュートリアル等から照準方向を強制的に設定します。
    /// </summary>
    public void SetAimDirection(Vector3 direction)
    {
        if (direction != Vector3.zero)
        {
            keyboardAimDir = BallPath.Get8Direction(direction.normalized);
            usingKeyboardAim = true;
            UpdateArrowByMouse(); // 矢印の向きを即座に反映
        }
        else
        {
            keyboardAimDir = Vector3.zero;
            usingKeyboardAim = false;
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
        Vector3 aimDir = GetAimDirection();
        if (aimDir == Vector3.zero) return;

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

            MagicManager.Instance.ApplyPendingPotionRestores();
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