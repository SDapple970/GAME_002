using System;
using System.Collections;
using Game.Combat.Actions;
using UnityEngine;
using Game.Combat.Adapters;
using Game.Combat.Animation;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;

namespace Game.Combat.Effects
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatCameraController cameraController;

        [Header("Fallback Animation Settings")]
        [SerializeField] private float fallbackApproachDuration = 0.2f;
        [SerializeField] private float fallbackActionDelay = 0.15f;

        public void PlayResolution(CombatSession session, Action onComplete)
        {
            StartCoroutine(Co_PlayTurnAnimation(session, onComplete));
        }

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (cameraController == null)
                cameraController = FindFirstObjectByType<CombatCameraController>();
        }

        private IEnumerator Co_PlayTurnAnimation(CombatSession session, Action onComplete)
        {
            if (session == null || session.CurrentTurn == null || session.CurrentTurn.Playbook.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            for (int i = 0; i < session.CurrentTurn.Playbook.Count; i++)
            {
                PlaybookEvent playbookEvent = session.CurrentTurn.Playbook[i];

                if (playbookEvent is Event_Unopposed unopposed)
                    yield return PlayUnopposed(unopposed);
                else if (playbookEvent is Event_Clash clash)
                    yield return PlayClash(clash);
                else if (playbookEvent is Event_Utility utility)
                    yield return PlayUtility(utility);

                yield return new WaitForSeconds(0.2f);
            }

            onComplete?.Invoke();
        }

        private IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev == null)
                yield break;

            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject actorObj = GetFieldObject(ev.Actor);
            GameObject targetObj = GetFieldObject(ev.Target);

            if (actorObj == null)
                yield break;

            Debug.Log($"[CombatDirector] Actor={ev.Actor?.Id.Value} Skill={ev.Skill?.Name} Target={ev.Target?.Id.Value}", this);

            if (targetObj != null)
            {
                cameraController?.FocusAction(ev.Actor, ev.Target);
                yield return MoveActorForSkill(actorObj.transform, targetObj.transform, ev.Skill);
            }

            PlayCastPresentation(actorObj, ev.Skill);
            PlayAttackTrigger(actorObj, ev.Skill);
            yield return WaitAfterMove(ev.Skill);

            if (targetObj != null)
            {
                PlayImpactPresentation(targetObj, ev.Skill);
                PlayTargetReaction(ev.Target, targetObj, ev.DamageDealt, ev.StaggerDealt);
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));
            }

            yield return new WaitForSeconds(0.3f);

        }

        private IEnumerator PlayClash(Event_Clash ev)
        {
            if (ev == null)
                yield break;

            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA == null || objB == null)
                yield break;

            Debug.Log(
                $"[CombatDirector] Clash A={ev.ActorA?.Id.Value}:{ev.SkillA?.Name} B={ev.ActorB?.Id.Value}:{ev.SkillB?.Name}",
                this
            );

            cameraController?.FocusAction(ev.ActorA, ev.ActorB);

            yield return MoveActorForSkill(objA.transform, objB.transform, ev.SkillA);
            yield return MoveActorForSkill(objB.transform, objA.transform, ev.SkillB);

            PlayCastPresentation(objA, ev.SkillA);
            PlayAttackTrigger(objA, ev.SkillA);
            PlayCastPresentation(objB, ev.SkillB);
            PlayAttackTrigger(objB, ev.SkillB);

            float delay = Mathf.Max(GetActionDelay(ev.SkillA), GetActionDelay(ev.SkillB));
            yield return new WaitForSeconds(delay);

            if (ev.Loser != null)
            {
                GameObject loserObj = GetFieldObject(ev.Loser);
                if (loserObj != null)
                {
                    PlayImpactPresentation(loserObj, GetWinnerSkill(ev));
                    PlayTargetReaction(ev.Loser, loserObj, ev.DamageDealtToLoser, ev.StaggerDealtToLoser);
                    StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
                }
            }

            yield return new WaitForSeconds(0.4f);

        }

        private IEnumerator PlayUtility(Event_Utility ev)
        {
            if (ev == null || ev.IsCancelled)
                yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj == null)
                yield break;

            Debug.Log($"[CombatDirector] Utility Actor={ev.Actor?.Id.Value} Skill={ev.Skill?.Name}", this);

            cameraController?.FocusAction(ev.Actor, ev.Actor);
            PlayCastPresentation(actorObj, ev.Skill);
            PlayAttackTrigger(actorObj, ev.Skill);
            PlayImpactPresentation(actorObj, ev.Skill);
            StartCoroutine(FlashColor(actorObj, Color.yellow, 0.3f));
            yield return WaitAfterMove(ev.Skill);
        }

        private IEnumerator MoveActorForSkill(Transform actor, Transform target, ISkill skill)
        {
            if (actor == null || target == null || !ShouldApproach(skill))
                yield break;

            Vector3 attackPoint = CalculateAttackPoint(actor.position, target.position, skill);
            attackPoint.z = actor.position.z;

            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
            yield return MoveTransform(actor, actor.position, attackPoint, GetMoveDuration(actor.position, attackPoint, skill));
            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
        }

        private static bool ShouldApproach(ISkill skill)
        {
            if (skill == null)
                return false;

            return skill.MovementMode == SkillMovementMode.ApproachAndStay;
        }

        private static Vector3 CalculateAttackPoint(Vector3 actorPosition, Vector3 targetPosition, ISkill skill)
        {
            Vector3 direction = actorPosition - targetPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.left;
            else
                direction.Normalize();

            float distance = skill != null ? Mathf.Max(0f, skill.DesiredTargetDistance) : 1f;
            return targetPosition + direction * distance;
        }

        private float GetMoveDuration(Vector3 start, Vector3 end, ISkill skill)
        {
            float distance = Vector3.Distance(start, end);
            if (distance <= 0.001f)
                return 0f;

            float speed = skill != null ? skill.MoveSpeed : 0f;
            if (speed > 0.001f)
                return Mathf.Max(0.01f, distance / speed);

            return Mathf.Max(0.01f, fallbackApproachDuration);
        }

        private float GetActionDelay(ISkill skill)
        {
            return skill != null ? Mathf.Max(0f, skill.ActionDelayAfterMove) : fallbackActionDelay;
        }

        private IEnumerator WaitAfterMove(ISkill skill)
        {
            yield return new WaitForSeconds(GetActionDelay(skill));
        }

        private static void PlayAttackTrigger(GameObject actorObj, ISkill skill)
        {
            if (actorObj == null)
                return;

            CombatantAnimationDriver driver = actorObj.GetComponentInChildren<CombatantAnimationDriver>();
            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (driver != null)
            {
                driver.PlaySkill(skillDefinition != null ? skillDefinition.CombatAnimationTrigger : null);
                return;
            }

            Animator anim = actorObj.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger(Animator.StringToHash("Attack"));
        }

        private void PlayCastPresentation(GameObject actorObj, ISkill skill)
        {
            if (actorObj == null)
                return;

            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (skillDefinition == null)
                return;

            Vector3 position = actorObj.transform.position;
            SpawnPresentationVfx(skillDefinition.CastVfxPrefab, position);
            PlayPresentationSfx(skillDefinition.CastSfx, position);
        }

        private void PlayImpactPresentation(GameObject targetObj, ISkill skill)
        {
            if (targetObj == null)
                return;

            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (skillDefinition == null)
                return;

            Vector3 position = targetObj.transform.position;
            SpawnPresentationVfx(skillDefinition.ImpactVfxPrefab, position);
            PlayPresentationSfx(skillDefinition.ImpactSfx, position);
        }

        private static void SpawnPresentationVfx(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, GetPresentationVfxLifetime(instance));
        }

        private static void PlayPresentationSfx(AudioClip clip, Vector3 position)
        {
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position);
        }

        private static float GetPresentationVfxLifetime(GameObject instance)
        {
            const float fallbackLifetime = 2f;

            if (instance == null)
                return fallbackLifetime;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems == null || particleSystems.Length == 0)
                return fallbackLifetime;

            float lifetime = 0f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                float systemLifetime = main.duration + main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, systemLifetime);
            }

            return lifetime > 0f ? lifetime : fallbackLifetime;
        }

        private static void PlayTargetReaction(ICombatant target, GameObject targetObj, int damageDealt, int staggerDealt)
        {
            if (targetObj == null || target == null)
                return;

            if (damageDealt <= 0 && staggerDealt <= 0)
                return;

            CombatantAnimationDriver driver = targetObj.GetComponentInChildren<CombatantAnimationDriver>();
            if (driver == null)
                return;

            if (target.HP <= 0)
                driver.PlayDie();
            else if (staggerDealt > 0 && target.Stagger >= target.StaggerMax)
                driver.PlayStagger();
            else
                driver.PlayHit();
        }

        private static SkillDefinitionSO ResolveSkillDefinition(ISkill skill)
        {
            return skill is SoSkill soSkill ? soSkill.Definition : null;
        }

        private static ISkill GetWinnerSkill(Event_Clash ev)
        {
            if (ev == null || ev.Winner == null)
                return null;

            if (ev.Winner == ev.ActorA)
                return ev.SkillA;

            if (ev.Winner == ev.ActorB)
                return ev.SkillB;

            return null;
        }

        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter fieldCombatant)
                return fieldCombatant.FieldObject;

            return null;
        }

        private ICombatant GetCombatant(GameObject fieldObject)
        {
            if (fieldObject == null || entryPoint == null || entryPoint.ActiveSession == null)
                return null;

            CombatSession session = entryPoint.ActiveSession;

            for (int i = 0; i < session.Allies.Count; i++)
            {
                if (session.Allies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                if (session.Enemies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            return null;
        }

        private IEnumerator MoveTransform(Transform tf, Vector3 start, Vector3 end, float duration)
        {
            if (tf == null)
                yield break;

            if (duration <= 0.001f)
            {
                tf.position = end;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / duration);
                float eased = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);
                tf.position = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            tf.position = end;
        }

        private IEnumerator FlashColor(GameObject obj, Color flashColor, float duration)
        {
            if (obj == null)
                yield break;

            SpriteRenderer sprite = obj.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null)
                yield break;

            Color original = sprite.color;
            sprite.color = flashColor;
            yield return new WaitForSeconds(duration);
            sprite.color = original;
        }
    }
}
