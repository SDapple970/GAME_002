// GAME/Scripts/Combat/Effects/CombatDirector.cs
using System.Collections;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Enemy.Overworld;

namespace Game.Combat.Effects
{
    public sealed partial class CombatDirector : MonoBehaviour
    {
        private void TriggerAttackAnimation(GameObject actorObj)
        {
            if (actorObj == null) return;

            var driver = actorObj.GetComponentInChildren<EnemyAnimator2D>();
            if (driver != null)
            {
                driver.PlayAttack();
                return;
            }

            var anim = actorObj.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger("Attack");
        }
    }
}