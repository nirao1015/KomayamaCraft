using UnityEngine;

public class DirectionView03Controller : DirectionViewLoopPulseBase
{
    private void Reset()
    {
        effectStartSeconds = 1.4f;
        repeatIntervalSeconds = 3.4f;
        fadeInSeconds = 0.4f;
        holdSeconds = 1.2f;
        fadeOutSeconds = 0.4f;
        effectScaleMultiplier = 1.15f;
    }
}
