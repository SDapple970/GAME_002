using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat.Adapters;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CombatFieldCallDebug : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private OpeningEffectSO openingEffect;

        [Header("Field References (drag real objects from Hierarchy)")]
        [SerializeField] private List<GameObject> allies = new();
        [SerializeField] private List<GameObject> enemies = new();

        [Header("Debug Input (Input System)")]
        [SerializeField] private InputActionReference f1StartPlayerFirstHit;
        [SerializeField] private InputActionReference f2StartPlayerGotHit;
        [SerializeField] private InputActionReference f3StartSpecialSkill;

        private void OnEnable()
        {
            EnableAndBind(f1StartPlayerFirstHit, OnF1);
            EnableAndBind(f2StartPlayerGotHit, OnF2);
            EnableAndBind(f3StartSpecialSkill, OnF3);
        }

        private void OnDisable()
        {
            DisableAndUnbind(f1StartPlayerFirstHit, OnF1);
            DisableAndUnbind(f2StartPlayerGotHit, OnF2);
            DisableAndUnbind(f3StartSpecialSkill, OnF3);
        }

        private static void EnableAndBind(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
        {
            if (r == null || r.action == null) return;
            r.action.performed += cb;
            r.action.Enable();
        }

        private static void DisableAndUnbind(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
        {
            if (r == null || r.action == null) return;
            r.action.performed -= cb;
            r.action.Disable();
        }

        private void OnF1(InputAction.CallbackContext _)
        {
            if (entryPoint == null) return;

            entryPoint.StartCombatFromField(
                allyFieldObjects: allies,
                enemyFieldObjects: enemies,
                reason: StartReason.PlayerFirstHit,
                initiativeSide: Side.Allies,
                openingEffectOrNull: null
            );
        }

        private void OnF2(InputAction.CallbackContext _)
        {
            if (entryPoint == null) return;

            entryPoint.StartCombatFromField(
                allyFieldObjects: allies,
                enemyFieldObjects: enemies,
                reason: StartReason.PlayerGotHit,
                initiativeSide: Side.Enemies,
                openingEffectOrNull: null
            );
        }

        private void OnF3(InputAction.CallbackContext _)
        {
            if (entryPoint == null) return;

            entryPoint.StartCombatFromField(
                allyFieldObjects: allies,
                enemyFieldObjects: enemies,
                reason: StartReason.SpecialSkill,
                initiativeSide: Side.Allies,
                openingEffectOrNull: openingEffect
            );
        }
    }
}