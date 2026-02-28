using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Integration
{
    public sealed class CombatEncounterGroup : MonoBehaviour
    {
        [SerializeField] private bool autoCollectChildren = true;
        [SerializeField] private List<GameObject> enemies = new();

        public List<GameObject> GetActiveEnemies()
        {
            if (autoCollectChildren)
            {
                enemies.Clear();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child == null) continue;

                    var go = child.gameObject;
                    if (go != null && go.activeInHierarchy)
                        enemies.Add(go);
                }
            }

            // 결과는 "활성"만 반환
            var result = new List<GameObject>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
            {
                var go = enemies[i];
                if (go != null && go.activeInHierarchy)
                    result.Add(go);
            }
            return result;
        }
    }
}