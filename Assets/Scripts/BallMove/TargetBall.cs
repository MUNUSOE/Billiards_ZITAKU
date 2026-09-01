using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TargetBall : MonoBehaviour
{
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
}