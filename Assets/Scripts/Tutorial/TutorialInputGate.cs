using UnityEngine;

/// <summary>
/// チュートリアル中に、プレイヤーの操作をどこまで許可するかを保持する受け渡し用のクラスです。
/// 既存の ShotBall / MagicManager はこの値を見て入力を受け付けるかどうかを決めます。
///
/// チュートリアル以外のステージでは IsActive が false のままなので、
/// 通常のプレイには一切影響しません。
/// </summary>
public static class TutorialInputGate
{
    /// <summary>チュートリアルによる操作制限が有効か。通常ステージでは false。</summary>
    public static bool IsActive { get; private set; }

    /// <summary>方向の変更を許可するか。</summary>
    public static bool AllowDirection { get; private set; }

    /// <summary>威力の変更を許可するか。</summary>
    public static bool AllowPower { get; private set; }

    /// <summary>打ち出しを許可するか。</summary>
    public static bool AllowShot { get; private set; }

    /// <summary>魔法の選択を許可するか。</summary>
    public static bool AllowMagic { get; private set; }

    /// <summary>
    /// 特定の魔法だけを許可するか。true のとき AllowedMagicType 以外は選択できません。
    /// </summary>
    public static bool RestrictToSingleMagic { get; private set; }

    /// <summary>RestrictToSingleMagic が true のとき、選択を許可する魔法。</summary>
    public static MagicType AllowedMagicType { get; private set; } = MagicType.None;

    /// <summary>方向を固定するか。true のとき ForcedDirection が使われます。</summary>
    public static bool UseForcedAim { get; private set; }

    /// <summary>固定する方向（8方向のいずれか）。</summary>
    public static Vector3 ForcedDirection { get; private set; }

    /// <summary>固定する威力レベル（0〜2）。-1 なら固定しません。</summary>
    public static int ForcedPowerLevel { get; private set; } = -1;

    /// <summary>
    /// このフレームでチュートリアル中に打ち出しが行われたか。
    /// TutorialManager が読み取ったあと ConsumeShotFired() で消費します。
    /// </summary>
    public static bool ShotFired { get; private set; }

    /// <summary>チュートリアルの制限を有効にして、許可する操作を設定します。</summary>
    public static void Apply(bool allowDirection, bool allowPower, bool allowShot, bool allowMagic,
                             bool useForcedAim, Vector3 forcedDirection, int forcedPowerLevel,
                             bool restrictToSingleMagic = false, MagicType allowedMagicType = MagicType.None)
    {
        IsActive = true;
        AllowDirection = allowDirection;
        AllowPower = allowPower;
        AllowShot = allowShot;
        AllowMagic = allowMagic;
        UseForcedAim = useForcedAim;
        ForcedDirection = forcedDirection;
        ForcedPowerLevel = forcedPowerLevel;
        RestrictToSingleMagic = restrictToSingleMagic;
        AllowedMagicType = allowedMagicType;
    }

    /// <summary>
    /// その魔法を選択できるか。チュートリアル中でなければ常に true。
    /// </summary>
    public static bool CanSelectMagic(MagicType type)
    {
        if (!IsActive) return true;
        if (!AllowMagic) return false;
        if (!RestrictToSingleMagic) return true;

        return type == AllowedMagicType;
    }

    /// <summary>ShotBall から、打ち出しが行われたことを通知します。</summary>
    public static void NotifyShotFired()
    {
        if (!IsActive) return;
        ShotFired = true;
    }

    /// <summary>打ち出し通知を読み取って消費します。</summary>
    public static bool ConsumeShotFired()
    {
        if (!ShotFired) return false;
        ShotFired = false;
        return true;
    }

    /// <summary>
    /// 制限を解除して通常のプレイに戻します。
    /// チュートリアル終了時や、チュートリアル以外のシーンの開始時に呼びます。
    /// </summary>
    public static void Clear()
    {
        IsActive = false;
        AllowDirection = true;
        AllowPower = true;
        AllowShot = true;
        AllowMagic = true;
        UseForcedAim = false;
        ForcedDirection = Vector3.zero;
        ForcedPowerLevel = -1;
        ShotFired = false;
        RestrictToSingleMagic = false;
        AllowedMagicType = MagicType.None;
    }
}