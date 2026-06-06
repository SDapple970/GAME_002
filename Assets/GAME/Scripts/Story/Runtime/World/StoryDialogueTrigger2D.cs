// Assets/GAME/Scripts/Story/Runtime/World/StoryDialogueTrigger2D.cs
using Game.Core;
using Game.Story.Core;
using Game.Story.Data;
using UnityEngine;

namespace Game.Story.World
{
    public sealed class StoryDialogueTrigger2D : MonoBehaviour
    {
        [SerializeField] private DialogueDefinitionSO dialogue;
        [SerializeField] private DialogueRunner runner;
        [SerializeField] private bool triggerOnce = false;
        [SerializeField] private bool useDebugEKey = true;

        [Header("Optional Required Flag")]
        [SerializeField] private string requiredBoolFlagKey;
        [SerializeField] private bool requiredBoolFlagValue = true;

        [Header("Optional Result Flag")]
        [SerializeField] private string setBoolFlagOnTriggered;
        [SerializeField] private bool setBoolFlagValue = true;

        private bool _playerInside;
        private bool _hasTriggered;

        private void Update()
        {
            if (useDebugEKey && _playerInside && Input.GetKeyDown(KeyCode.E))
            {
                TryStartEvent();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInside = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInside = false;
            }
        }

        public void TryStartEvent()
        {
            if (triggerOnce && _hasTriggered) return;

            if (dialogue == null)
            {
                Debug.LogWarning("[StoryDialogueTrigger2D] Dialogue is not assigned.");
                return;
            }

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return;
            }

            if (!string.IsNullOrEmpty(requiredBoolFlagKey))
            {
                if (StoryFlagManager.Instance == null)
                {
                    Debug.LogWarning("[StoryDialogueTrigger2D] StoryFlagManager is missing.");
                    return;
                }

                if (StoryFlagManager.Instance.GetBool(requiredBoolFlagKey) != requiredBoolFlagValue)
                {
                    return;
                }
            }

            if (runner == null)
            {
#if UNITY_2023_1_OR_NEWER
                runner = FindFirstObjectByType<DialogueRunner>();
#else
                runner = FindObjectOfType<DialogueRunner>();
#endif
            }

            if (runner == null)
            {
                Debug.LogWarning("[StoryDialogueTrigger2D] DialogueRunner could not be found.");
                return;
            }

            if (!string.IsNullOrEmpty(setBoolFlagOnTriggered))
            {
                if (StoryFlagManager.Instance != null)
                {
                    StoryFlagManager.Instance.SetBool(setBoolFlagOnTriggered, setBoolFlagValue);
                }
                else
                {
                    Debug.LogWarning("[StoryDialogueTrigger2D] StoryFlagManager is missing. Result flag was not set.");
                }
            }

            runner.StartDialogue(dialogue);
            _hasTriggered = true;
        }
    }
}
