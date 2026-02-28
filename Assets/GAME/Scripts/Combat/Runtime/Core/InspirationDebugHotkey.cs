using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class InspirationDebugHotkey : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;

        private void Update()
        {
            var s = entryPoint != null ? entryPoint.ActiveSession : null;
            if (s == null) return;

            // G: 영감 +1
            if (Input.GetKeyDown(KeyCode.G))
                s.Inspiration.Gain(1);

            // H: 영감 -1 (있을 때만)
            if (Input.GetKeyDown(KeyCode.H))
                s.Inspiration.TrySpend(1);
        }
    }
}