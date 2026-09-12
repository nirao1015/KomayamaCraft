using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー横進行の優勢を 20 秒窓で集計し、3 秒ごとにロックする（【11b】後方フォールオフ削除用）。
/// </summary>
public sealed class Game03ScrollProgressTracker
{
    public enum DominantAxis
    {
        None = 0,
        Right = 1,
        Left = -1
    }

    private struct Sample
    {
        public float Time;
        public float RightPx;
        public float LeftPx;
    }

    private readonly List<Sample> samples = new List<Sample>(128);
    private float evaluateTimer;
    private DominantAxis lockedAxis = DominantAxis.None;
    private float lockedRemaining;

    public DominantAxis LockedAxis => lockedAxis;

    public string LockedAxisDebugLabel => lockedAxis switch
    {
        DominantAxis.Right => "進行:右",
        DominantAxis.Left => "進行:左",
        _ => "進行:未定"
    };

    public void Reset()
    {
        samples.Clear();
        evaluateTimer = 0f;
        lockedAxis = DominantAxis.None;
        lockedRemaining = 0f;
    }

    public void Tick(float gameplayDeltaTime, float localDeltaX, float historyWindowSeconds, float evaluateIntervalSeconds, float dominanceThresholdPx)
    {
        if (gameplayDeltaTime <= 0f)
        {
            return;
        }

        float rightPx = Mathf.Max(0f, -localDeltaX);
        float leftPx = Mathf.Max(0f, localDeltaX);
        float now = Time.time;
        samples.Add(new Sample { Time = now, RightPx = rightPx, LeftPx = leftPx });
        PruneOlderThan(now - Mathf.Max(1f, historyWindowSeconds));

        if (lockedRemaining > 0f)
        {
            lockedRemaining -= gameplayDeltaTime;
            if (lockedRemaining <= 0f)
            {
                lockedAxis = DominantAxis.None;
            }
        }

        evaluateTimer += gameplayDeltaTime;
        if (evaluateTimer < Mathf.Max(0.25f, evaluateIntervalSeconds))
        {
            return;
        }

        evaluateTimer = 0f;
        float sumRight = 0f;
        float sumLeft = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            sumRight += samples[i].RightPx;
            sumLeft += samples[i].LeftPx;
        }

        float diff = sumRight - sumLeft;
        float threshold = Mathf.Max(1f, dominanceThresholdPx);
        if (Mathf.Abs(diff) < threshold)
        {
            lockedAxis = DominantAxis.None;
            lockedRemaining = 0f;
            return;
        }

        lockedAxis = diff > 0f ? DominantAxis.Right : DominantAxis.Left;
        lockedRemaining = Mathf.Max(0.25f, evaluateIntervalSeconds);
    }

    public void AccumulateMinuteScroll(float localDeltaX, ref float minuteRightPx, ref float minuteLeftPx)
    {
        minuteRightPx += Mathf.Max(0f, -localDeltaX);
        minuteLeftPx += Mathf.Max(0f, localDeltaX);
    }

    private void PruneOlderThan(float minTime)
    {
        int removeCount = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].Time >= minTime)
            {
                break;
            }

            removeCount++;
        }

        if (removeCount > 0)
        {
            samples.RemoveRange(0, removeCount);
        }
    }
}
