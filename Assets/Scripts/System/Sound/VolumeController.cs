using UnityEngine;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
    [Header("BGM 用スライダー")]
    [SerializeField] private Slider bgmSlider;

    [Header("SE 用スライダー")]
    [SerializeField] private Slider seSlider;

    private void Start()
    {
        if (SoundManager.Instance == null) return;

        // 1. 現在の音量をスライダーの初期値にセット
        if (bgmSlider != null)
        {
            bgmSlider.value = SoundManager.Instance.BGMVolume;
            // スライダーの値が動いた時のイベントを登録
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (seSlider != null)
        {
            seSlider.value = SoundManager.Instance.SEVolume;
            seSlider.onValueChanged.AddListener(OnSEVolumeChanged);
        }
    }

    private void OnDestroy()
    {
        // メモリリーク防止のためイベント登録解除
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
        if (seSlider != null) seSlider.onValueChanged.RemoveListener(OnSEVolumeChanged);
    }

    private void OnBGMVolumeChanged(float value)
    {
        SoundManager.Instance.SetBGMVolume(value);
    }

    private void OnSEVolumeChanged(float value)
    {
        SoundManager.Instance.SetSEVolume(value);
    }
}