using UnityEngine;

public class DirectionView01Controller : DirectionViewLoopPulseBase
{
    private void Reset()
    {
        effectStartSeconds = 0.2f;
        repeatIntervalSeconds = 2.6f;
        fadeInSeconds = 0.4f;
        holdSeconds = 1.2f;
        fadeOutSeconds = 0.4f;
        effectScaleMultiplier = 1.15f;
    }
}
