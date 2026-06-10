using System;
using UnityEngine;

namespace Game.DemoMission.Data
{
    [Serializable]
    public sealed class MonsterBriefingEntry
    {
        public string monsterId;
        public string displayName;
        public Sprite portrait;
        [TextArea] public string description;
    }
}
