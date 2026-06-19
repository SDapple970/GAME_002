using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Game.Core;
using Game.DemoMission.Data;

namespace Game.DemoMission.Runtime
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RescueNpcActor : MonoBehaviour
    {
        [SerializeField] private RescueNpcDefinitionSO npcDefinition;
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private GameObject interactPromptRoot;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [FormerlySerializedAs("rescueOnlyAfterRequiredKills")]
        [SerializeField] private bool requireEnemyKillsBeforeRescue = true;
        [SerializeField] private bool markCompleteOnInteract = true;
        [SerializeField] private float interactionCooldown = 0.25f;

        private bool _playerInRange;
        private bool _rescueRegistered;
        private bool _dialogueRunning;
        private float _lastInteractionTime = -999f;
        private GameInputInstaller _input;
        private bool _inputSubscribed;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
                Debug.LogWarning("[RescueNpcActor] Collider2D should be set to Is Trigger.", this);

            ApplyFieldSprite();
            SetPromptVisible(false);
        }

        private void OnEnable()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            TrySubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        private void Update()
        {
            if (!_inputSubscribed)
                TrySubscribeInput();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other != null && other.CompareTag(playerTag))
            {
                _playerInRange = true;
                SetPromptVisible(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && other.CompareTag(playerTag))
            {
                _playerInRange = false;
                SetPromptVisible(false);
            }
        }

        public void TryInteract()
        {
            if (missionRuntime == null)
            {
                Debug.LogWarning("[RescueNpcActor] DemoMissionRuntime is missing.", this);
                return;
            }

            if (missionRuntime.CurrentMission == null)
            {
                Debug.LogWarning("[RescueNpcActor] Current mission is null.", this);
                return;
            }

            if (Time.unscaledTime - _lastInteractionTime < Mathf.Max(0f, interactionCooldown))
                return;

            _lastInteractionTime = Time.unscaledTime;

            if (requireEnemyKillsBeforeRescue && !missionRuntime.HasRequiredEnemyKills())
            {
                Debug.Log("[RescueNpcActor] Required enemy kills are not complete yet.", this);
                LogDialogue(GetDialogueLines(false));
                return;
            }

            if (_rescueRegistered || missionRuntime.IsNpcRescued)
            {
                LogDialogue(GetDialogueLines(true));
                return;
            }

            StartCoroutine(Co_RescueSequence());
        }

        private IEnumerator Co_RescueSequence()
        {
            _dialogueRunning = true;
            LogDialogue(GetDialogueLines(false));
            yield return null;

            _rescueRegistered = true;
            if (markCompleteOnInteract)
                missionRuntime.RegisterNpcRescued();

            LogDialogue(GetDialogueLines(true));

            _dialogueRunning = false;
        }

        private IEnumerable<string> GetDialogueLines(bool afterRescue)
        {
            if (npcDefinition == null)
                yield break;

            List<string> lines = afterRescue
                ? npcDefinition.afterRescueDialogue
                : npcDefinition.beforeRescueDialogue;

            if (lines == null || lines.Count == 0)
            {
                yield return afterRescue
                    ? $"{npcDefinition.displayName}: Thank you."
                    : $"{npcDefinition.displayName}: Help me.";
                yield break;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    yield return lines[i];
            }
        }

        private void LogDialogue(IEnumerable<string> lines)
        {
            foreach (string line in lines)
                Debug.Log($"[RescueNpcActor] {line}", this);
        }

        private void ApplyFieldSprite()
        {
            if (npcDefinition == null || npcDefinition.fieldSprite == null)
                return;

            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = npcDefinition.fieldSprite;
        }

        private void SetPromptVisible(bool visible)
        {
            if (interactPromptRoot != null)
                interactPromptRoot.SetActive(visible);
        }

        private void TrySubscribeInput()
        {
            if (_inputSubscribed)
                return;

            _input = global::GameInputInstaller.Instance;
            if (_input == null)
                return;

            _input.Interact += HandleInteractInput;
            _inputSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (!_inputSubscribed || _input == null)
            {
                _inputSubscribed = false;
                _input = null;
                return;
            }

            _input.Interact -= HandleInteractInput;
            _inputSubscribed = false;
            _input = null;
        }

        private void HandleInteractInput()
        {
            if (!_playerInRange || _dialogueRunning)
                return;

            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.AllowsExplorationInput())
                return;

            TryInteract();
        }
    }
}
