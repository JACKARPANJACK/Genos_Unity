using UnityEngine;

namespace Climbing
{
    /// <summary>
    /// Configures a single dash move: how long it lasts, how hard it pushes,
    /// the force profile over time (curve), and the recovery window before the
    /// player can act again.
    /// </summary>
    [System.Serializable]
    public class DashSetting
    {
        [Tooltip("Total time in seconds the dash lasts.")]
        public float dashTime = 0.2f;

        [Tooltip("Peak force (velocity magnitude) applied at the start of the dash.")]
        public float dashForce = 15f;

        [Tooltip("Force profile over normalised dash time (0-1). " +
                 "A curve that starts at 1 and decays to 0 gives the standard snap-and-decelerate feel.")]
        public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Seconds after the dash ends before the same dash can be used again.")]
        public float recoverTime = 0.3f;
    }
}
