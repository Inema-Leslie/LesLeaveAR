using UnityEngine;

namespace LesLeave
{
    /// <summary>Strategy: scoring rules can be swapped without touching the code that asks for a score.</summary>
    public interface ScoringStrategy
    {
        string Name { get; }
        float Compute(Session s);
    }

    /// <summary>Shared maths. The three difficulty levels only differ in their weights.</summary>
    public abstract class WeightedScoring : ScoringStrategy
    {
        public abstract string Name { get; }
        protected abstract float NoAlarmPenalty { get; }
        protected abstract float NoDoorTestPenalty { get; }
        protected abstract float HotDoorPenalty { get; }
        protected abstract float ExposureWeight { get; }
        protected abstract float FailCap { get; }

        public float Compute(Session s)
        {
            if (s == null || s.outcome == "Aborted") return 0f;

            float score = 100f;
            if (!s.alarmActivated) score -= NoAlarmPenalty;
            if (!s.inspectedDoor) score -= NoDoorTestPenalty;
            if (s.enteredHotDoor) score -= HotDoorPenalty;
            if (s.usedElevator) score -= 40f;
            if (s.outcome == "Completed" && !s.usedStairs) score -= 10f;
            score -= s.peakExposure * ExposureWeight;
            score -= Mathf.Min(10f, s.speedWarnings * 2f);

            if (s.outcome == "Completed" && s.timeLimit > 0f)
            {
                score += 10f * Mathf.Clamp01(1f - s.duration / s.timeLimit);
            }
            if (s.outcome == "Failed") score = Mathf.Min(score, FailCap);

            return Mathf.Round(Mathf.Clamp(score, 0f, 100f) * 10f) / 10f;
        }
    }

    public class EasyScoring : WeightedScoring
    {
        public override string Name { get { return "Easy"; } }
        protected override float NoAlarmPenalty { get { return 10f; } }
        protected override float NoDoorTestPenalty { get { return 5f; } }
        protected override float HotDoorPenalty { get { return 10f; } }
        protected override float ExposureWeight { get { return 20f; } }
        protected override float FailCap { get { return 40f; } }
    }

    public class StandardScoring : WeightedScoring
    {
        public override string Name { get { return "Standard"; } }
        protected override float NoAlarmPenalty { get { return 15f; } }
        protected override float NoDoorTestPenalty { get { return 10f; } }
        protected override float HotDoorPenalty { get { return 20f; } }
        protected override float ExposureWeight { get { return 30f; } }
        protected override float FailCap { get { return 30f; } }
    }

    public class StrictScoring : WeightedScoring
    {
        public override string Name { get { return "Strict"; } }
        protected override float NoAlarmPenalty { get { return 25f; } }
        protected override float NoDoorTestPenalty { get { return 15f; } }
        protected override float HotDoorPenalty { get { return 30f; } }
        protected override float ExposureWeight { get { return 40f; } }
        protected override float FailCap { get { return 20f; } }
    }
}
