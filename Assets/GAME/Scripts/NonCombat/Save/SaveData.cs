using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Save
{
    [Serializable]
    public sealed class SaveData
    {
        public string currentChapterId;
        public List<BoolEntry> flags = new();
        public List<PersonaStatEntry> personaStats = new();
        public int gold;
        public List<IntEntry> inventory = new();
        public List<string> completedObjectives = new();
        public Vector3 playerPosition;
    }

    [Serializable]
    public sealed class BoolEntry
    {
        public string id;
        public bool value;
    }

    [Serializable]
    public sealed class IntEntry
    {
        public string id;
        public int value;
    }

    [Serializable]
    public sealed class PersonaStatEntry
    {
        public string stat;
        public int level;
        public int xp;
    }
}
