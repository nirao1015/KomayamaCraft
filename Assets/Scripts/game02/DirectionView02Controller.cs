using UnityEngine;

public class DirectionView02Controller : DirectionViewLoopPulseBase
{
    private void Reset()
    {
        effectStartSeconds = 0.8f;
        repeatIntervalSeconds = 3.0f;
        fadeInSeconds = 0.4f;
        holdSeconds = 1.2f;
        fadeOutSeconds = 0.4f;
        effectScaleMultiplier = 1.15f;
    }
}
