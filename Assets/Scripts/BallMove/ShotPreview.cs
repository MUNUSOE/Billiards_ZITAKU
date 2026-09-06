using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ショットの予測表示です。チュートリアル向けの補助表示で、
/// GameManager の「Show Shot Preview」がオンのときだけ動きます。
///
/// 表示する内容:
///   1. ショット球が通過するマス
///   2. ショット球が停止するマス
///   3. 最初に当たるターゲット球が受け取る運動量（マス数）
///
/// 経路は実際のショットと同じ BallPath.SimulateChain で求めるため、
/// 予測と結果がずれることはありません。
/// </summary>
public class ShotPreview : MonoBehaviour
{
    [Header("Cell Effects")]
    [Tooltip("ショット球が通過するマスに表示するエフェクト。")]
    [SerializeField] private GameObject passCellEffectPrefab;

    [Tooltip("ショット球が停止するマスに表示するエフェクト。")]
    [SerializeField] private GameObject stopCellEffectPrefab;

    [Tooltip("マス上のエフェクトのY軸オフセット。盤面に埋まる場合は少し上げてください。")]
    [SerializeField] private float cellEffectOffsetY = 0.02f;

    [Header("Momentum Label")]
    [Tooltip("最初に当たるターゲット球の上に出す数値表示。子に TMP_Text を持つプレハブを指定します。")]
    [SerializeField] private GameObject momentumLabelPrefab;

    [Tooltip("数値表示の位置オフセット（対象球からの相対位置）。")]
    [SerializeField] private Vector3 momentumLabelOffset = new Vector3(0f, 0.6f, 0f);

    [Tooltip("数値表示を常にカメラへ向けるか。")]
    [SerializeField] private bool momentumLabelFaceCamera = true;

    // 生成済みのエフェクトを使い回すためのプール。
    private readonly List<GameObject> passEffects = new List<GameObject>();
    private GameObject stopEffect;
    private GameObject momentumLabel;
    private TMP_Text momentumText;

    // 直前の入力内容。同じ内容なら作り直さず、毎フレームの生成を避けます。
    private Vector3 lastDirection;
    private int lastPanels = -1;
    private bool lastFireActive;
    private bool hasPreview;

    private void LateUpdate()
    {
        if (momentumLabelFaceCamera && momentumLabel != null && momentumLabel.activeSelf)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                momentumLabel.transform.rotation = cam.transform.rotation;
            }
        }
    }

    /// <summary>
    /// 予測表示を更新します。狙いの方向・威力が前回と同じなら何もしません。
    /// </summary>
    public void Show(GameObject shotBall, Vector3 direction, int panels, bool isFireActive)
    {
        if (shotBall == null)
        {
            Hide();
            return;
        }

        // 同じ狙いなら作り直さない。
        if (hasPreview && lastPanels == panels && lastFireActive == isFireActive
            && Vector3.Distance(lastDirection, direction) < 0.01f)
        {
            return;
        }

        lastDirection = direction;
        lastPanels = panels;
        lastFireActive = isFireActive;
        hasPreview = true;

        Rebuild(shotBall, direction, panels, isFireActive);
    }

    /// <summary>予測表示をすべて隠します。</summary>
    public void Hide()
    {
        foreach (GameObject effect in passEffects)
        {
            if (effect != null) effect.SetActive(false);
        }

        if (stopEffect != null) stopEffect.SetActive(false);
        if (momentumLabel != null) momentumLabel.SetActive(false);

        hasPreview = false;
        lastPanels = -1;
    }

    private void Rebuild(GameObject shotBall, Vector3 direction, int panels, bool isFireActive)
    {
        BallPath.GetBallSettings(shotBall, out float panelSize, out _, out _, out _);

        // 実際のショットと同じ計算を使う。SimulateChain は盤面を変更しないため、
        // 予測用に呼んでも副作用はない。
        List<BallPath.ChainStep> steps = BallPath.SimulateChain(shotBall, direction, panels, isFireActive);

        Hide();

        if (steps == null || steps.Count == 0) return;

        BallPath.ChainStep shotStep = steps[0];
        if (shotStep == null || shotStep.path == null || shotStep.path.Count == 0) return;

        Vector3 startCell = BallPath.SnapToGrid(shotBall.transform.position, panelSize);

        // 経路点をマス単位にまとめる。反射点はマスの途中にあるため、
        // グリッドへ丸めたうえで重複を除く。
        // あわせて、いったん開始マスを離れたあとに再び戻ってくるか（＝開始マスも経路の一部か）を調べる。
        List<Vector3> cells = new List<Vector3>();
        bool leftStartCell = false;
        bool revisitsStartCell = false;

        foreach (BallPath.PathPoint point in shotStep.path)
        {
            if (point == null) continue;

            Vector3 cell = BallPath.SnapToGrid(point.position, panelSize);

            if (Vector3.Distance(cell, startCell) < 0.01f)
            {
                // 一度離れてから戻ってきた場合は、開始マスも通過マスとして扱う。
                if (leftStartCell) revisitsStartCell = true;
            }
            else
            {
                leftStartCell = true;
            }

            if (cells.Count > 0 && Vector3.Distance(cells[cells.Count - 1], cell) < 0.01f) continue;
            if (cells.Contains(cell)) continue;

            cells.Add(cell);
        }

        if (cells.Count == 0) return;

        // 停止マスは経路の最後の点から直接求める。
        // cells は重複を除いたリストなので、一度通ったマスに戻って停止する場合、
        // そのマスは末尾に追加されず、cells の最後＝停止マスにはならない。
        BallPath.PathPoint lastPoint = shotStep.path[shotStep.path.Count - 1];
        Vector3 stopCell = BallPath.SnapToGrid(lastPoint.position, panelSize);

        // 通過マス（停止マスを除く）。
        // 開始マスは、反射などでもう一度そこを通る場合のみ表示する。
        int passIndex = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            if (Vector3.Distance(cells[i], stopCell) < 0.01f) continue;
            if (!revisitsStartCell && Vector3.Distance(cells[i], startCell) < 0.01f) continue;

            ShowPassEffect(passIndex, cells[i]);
            passIndex++;
        }

        // 停止マス
        ShowStopEffect(stopCell);

        // 最初に当たるターゲット球の運動量
        if (steps.Count > 1)
        {
            BallPath.ChainStep firstTarget = steps[1];
            if (firstTarget != null && firstTarget.ball != null)
            {
                ShowMomentumLabel(firstTarget.ball, firstTarget.panels);
            }
        }
    }

    private void ShowPassEffect(int index, Vector3 cell)
    {
        if (passCellEffectPrefab == null) return;

        while (passEffects.Count <= index)
        {
            GameObject created = Instantiate(passCellEffectPrefab, transform);
            created.SetActive(false);
            passEffects.Add(created);
        }

        GameObject effect = passEffects[index];
        if (effect == null) return;

        effect.transform.position = cell + new Vector3(0f, cellEffectOffsetY, 0f);
        effect.SetActive(true);
    }

    private void ShowStopEffect(Vector3 cell)
    {
        if (stopCellEffectPrefab == null) return;

        if (stopEffect == null)
        {
            stopEffect = Instantiate(stopCellEffectPrefab, transform);
        }

        stopEffect.transform.position = cell + new Vector3(0f, cellEffectOffsetY, 0f);
        stopEffect.SetActive(true);
    }

    private void ShowMomentumLabel(GameObject target, int momentum)
    {
        if (momentumLabelPrefab == null) return;

        if (momentumLabel == null)
        {
            momentumLabel = Instantiate(momentumLabelPrefab, transform);
            momentumText = momentumLabel.GetComponentInChildren<TMP_Text>();
        }

        momentumLabel.transform.position = target.transform.position + momentumLabelOffset;

        if (momentumText != null)
        {
            momentumText.text = momentum.ToString();
        }

        momentumLabel.SetActive(true);
    }
}