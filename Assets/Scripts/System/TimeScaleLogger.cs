using System.Collections;
using UnityEngine;

public class TimeScaleLogger : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(LogTimeScaleRoutine());
    }

    private IEnumerator LogTimeScaleRoutine()
    {
        while (true)
        {
            Debug.Log($"[TimeScale Check] Current TimeScale: {Time.timeScale}");

            // Time.timeScale ‚ª 0 ‚ÌŽž‚Å‚à‰e‹¿‚ðŽó‚¯‚¸‚É1•b‘Ò‚Â
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}