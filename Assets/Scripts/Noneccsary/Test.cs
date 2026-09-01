using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Test : MonoBehaviour
{
    public float shotSpeed = 5f;
    public float shotDuration = 1.5f;
    public float shotDistance = 5f;

    public float[] distanceLevels = { 3f, 5f, 8f };
    public float[] speedLevels = { 4f, 6f, 9f };
    private int currentLevel = 1;

    public GameObject arrowWeakObj;
    public GameObject arrowMiddleObj;
    public GameObject arrowStrongObj;

    private Transform arrow;

    private bool isMoving = false;
    private Vector3 moveDir;

    private InputAction clickAction;

    void Awake()
    {
        clickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        clickAction.Enable();

        ApplyPowerLevel();
        UpdateArrowObject();
    }

    void OnDestroy()
    {
        clickAction.Disable();
    }

    void Update()
    {
        if (!isMoving)
        {
            UpdateArrowByMouse();
            HandlePowerChange();
        }

        if (!isMoving && clickAction.WasPressedThisFrame())
        {
            ShootFromMouse();
        }
    }

    void HandlePowerChange()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            currentLevel = Mathf.Max(0, currentLevel - 1);
            ApplyPowerLevel();
            UpdateArrowObject();
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            currentLevel = Mathf.Min(distanceLevels.Length - 1, currentLevel + 1);
            ApplyPowerLevel();
            UpdateArrowObject();
        }
    }

    void ApplyPowerLevel()
    {
        shotDistance = distanceLevels[currentLevel];
        shotSpeed = speedLevels[currentLevel];
    }

    void UpdateArrowObject()
    {
        if (arrowWeakObj != null) arrowWeakObj.SetActive(false);
        if (arrowMiddleObj != null) arrowMiddleObj.SetActive(false);
        if (arrowStrongObj != null) arrowStrongObj.SetActive(false);

        switch (currentLevel)
        {
            case 0:
                arrowWeakObj.SetActive(true);
                arrow = arrowWeakObj.transform;
                break;
            case 1:
                arrowMiddleObj.SetActive(true);
                arrow = arrowMiddleObj.transform;
                break;
            case 2:
                arrowStrongObj.SetActive(true);
                arrow = arrowStrongObj.transform;
                break;
        }
    }

    void UpdateArrowByMouse()
    {
        if (arrow == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        var cam = Camera.main;

        float depth = Vector3.Distance(cam.transform.position, transform.position);

        Vector3 worldMouse = cam.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth)
        );

        Vector3 rawDir = transform.position - worldMouse;
        rawDir.y = 0f;

        if (rawDir.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = Get8Direction(rawDir.normalized);

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

        float depth = Vector3.Distance(cam.transform.position, transform.position);

        Vector3 worldMouse = cam.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth)
        );

        Vector3 rawDir = transform.position - worldMouse;
        rawDir.y = 0f;

        moveDir = Get8Direction(rawDir.normalized);

        bool diagonal = (moveDir.x != 0f && moveDir.z != 0f);

        if (diagonal)
        {
            shotDistance = distanceLevels[currentLevel] * 1.41421356f;
            shotSpeed = speedLevels[currentLevel] * 1.3f;
        }
        else
        {
            shotDistance = distanceLevels[currentLevel];
            shotSpeed = speedLevels[currentLevel];
        }

        StartCoroutine(MoveBall());
    }

    Vector3 Get8Direction(Vector3 dir)
    {
        float x = dir.x;
        float z = dir.z;

        float sx = Mathf.Abs(x) < 0.3f ? 0f : (x > 0 ? 1f : -1f);
        float sz = Mathf.Abs(z) < 0.3f ? 0f : (z > 0 ? 1f : -1f);

        if (sx == 0f && sz == 0f)
        {
            if (Mathf.Abs(x) > Mathf.Abs(z))
                sx = x > 0 ? 1f : -1f;
            else
                sz = z > 0 ? 1f : -1f;
        }

        return new Vector3(sx, 0f, sz).normalized;
    }

    // ★ 修正: Rotate90を廃止し、OnCollisionEnter で壁の法線を基準に反射する
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Reflect"))
        {
            // 衝突した壁の向き（法線ベクトル）を取得
            Vector3 normal = collision.contacts[0].normal;

            // Y軸方向のブレを防ぐため、Y成分を0にして再正規化
            normal.y = 0f;
            normal.Normalize();

            // 入射角と法線を元に、正しい反射ベクトルを計算
            moveDir = Vector3.Reflect(moveDir, normal).normalized;
        }
    }

    IEnumerator MoveBall()
    {
        isMoving = true;

        if (arrow != null) arrow.gameObject.SetActive(false);

        float traveled = 0f;
        float t = 0f;

        while (traveled < shotDistance)
        {
            float ratio = Mathf.Clamp01(t / shotDuration);

            float factor = 1f - ratio;
            factor = factor * factor;

            float frameSpeed = shotSpeed * factor * Time.deltaTime;

            if (traveled + frameSpeed > shotDistance)
            {
                float remain = shotDistance - traveled;
                transform.position += moveDir * remain;
                break;
            }

            transform.position += moveDir * frameSpeed;
            traveled += frameSpeed;

            t += Time.deltaTime;
            yield return null;
        }

        // ★ 修正: 移動終了時に蓄積した誤差をリセットし、マス目（整数座標）にスナップさせる
        Vector3 finalPos = transform.position;
        finalPos.x = Mathf.Round(finalPos.x);
        finalPos.z = Mathf.Round(finalPos.z);
        transform.position = finalPos;

        isMoving = false;

        if (arrow != null) arrow.gameObject.SetActive(true);
        UpdateArrowByMouse();
    }
}