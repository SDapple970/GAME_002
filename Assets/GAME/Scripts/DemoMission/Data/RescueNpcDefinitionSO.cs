using System.Collections.Generic;
using UnityEngine;

namespace Game.DemoMission.Data
{
    [CreateAssetMenu(menuName = "GAME_002/Demo Mission/Rescue NPC Definition")]
    public sealed class RescueNpcDefinitionSO : ScriptableObject
    {
        public string npcId;
        public string displayName;
        public Sprite portrait;
        public Sprite fieldSprite;
        [TextArea] public string briefDescription;
        public string lastKnownLocation;
        [TextArea] public List<string> beforeRescueDialogue = new();
        [TextArea] public List<string> afterRescueDialogue = new();
    }
}
