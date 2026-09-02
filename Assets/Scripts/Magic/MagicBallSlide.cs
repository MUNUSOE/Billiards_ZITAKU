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
    /// <param name="snapToGrid">
    /// true ならグリッドへ吸着させる。炎への接触点など、マスの中間で止めたい場合は false を指定する。
    /// </param>
    public static IEnumerator SlideTo(GameObject ball, Vector3 destination, bool snapToGrid = true)
    {
        if (ball == null) yield break;

        BallPath.GetBallSettings(ball, out float panelSize, out _, out float shotSpeed, out _);
        Transform transform = ball.transform;
        Vector3 target = snapToGrid ? BallPath.SnapToGrid(destination, panelSize) : destination;
        float distance = Vector3.Distance(transform.position, target);

        if (distance < 0.0001f)
        {
            transform.position = target;
            yield break;
        }

        // 魔法移動は通常ショットと同じ速度感を保ちつつ、短距離でも見える速度にします。
        float speed = Mathf.Max(shotSpeed, panelSize * 4f);
        // [変更] 通常ショット(BallPath.PlayChain)と同じ等速移動にそろえる。
        // 以前は Lerp + イージング(始終端で減速)だったため、通常ショットと見た目が異なっていた。
        // 通常ショットは Vector3.MoveTowards による完全な等速移動なので、ここでも同じ方式にする。
        while (true)
        {
            if (ball == null) yield break;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) <= 0.0001f) break;

            yield return null;
        }

        if (ball != null)
        {
            transform.position = target;
            MagicPotion.TryCollectAtBall(ball);
        }
    }
}