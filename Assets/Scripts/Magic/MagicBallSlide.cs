using System.Collections;
using UnityEngine;

/// <summary>
/// 水・風魔法で動かすターゲット球を、瞬間移動ではなくグリッド間を滑らかにスライドさせます。
/// </summary>
public static class MagicBallSlide
{
    /// <summary>
    /// 球を移動先まで一定速度でスライドさせ、完了時にグリッドへ正確に合わせます。
    /// </summary>
    public static IEnumerator SlideTo(GameObject ball, Vector3 destination)
    {
        if (ball == null) yield break;

        BallPath.GetBallSettings(ball, out float panelSize, out _, out float shotSpeed, out _);
        Transform transform = ball.transform;
        Vector3 start = transform.position;
        Vector3 target = BallPath.SnapToGrid(destination, panelSize);
        float distance = Vector3.Distance(start, target);

        if (distance < 0.0001f)
        {
            transform.position = target;
            yield break;
        }

        // 魔法移動は通常ショットと同じ速度感を保ちつつ、短距離でも見える速度にします。
        float speed = Mathf.Max(shotSpeed, panelSize * 4f);
        float elapsed = 0f;
        float duration = distance / speed;

        while (elapsed < duration)
        {
            if (ball == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 始終端を少し滑らかにする補間。移動経路は直線のままです。
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        if (ball != null)
        {
            transform.position = target;
            MagicPotion.TryCollectAtBall(ball);
        }
    }
}