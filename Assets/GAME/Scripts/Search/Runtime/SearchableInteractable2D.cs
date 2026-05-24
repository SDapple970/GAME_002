using Game.Core;
using Game.Search.Data;
using Game.Story;
using Game.Story.Interaction;
using UnityEngine;

namespace Game.Search
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class SearchableInteractable2D : MonoBehaviour
    {
        [SerializeField] private SearchableObjectDefinitionSO definition;
        [SerializeField] private SearchResultRunner runner;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requireExplorationState = true;
        [SerializeField] private StoryInteractionPromptUI promptUI;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [SerializeField] private bool searchOnceOverride = false;
        [SerializeField] private bool disableAfterSearch = false;
        [SerializeField] private string usedFlagKey;

        private Collider2D _triggerCollider;
        private bool _playerInside;
        private bool _searchedInMemory;

        public SearchableObjectDefinitionSO Definition => definition;
        public bool IsPlayerInside => _playerInside;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider2D>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }

            ResolveRunner();
            ResolvePromptUI();
        }

        private void OnDisable()
        {
            _playerInside = false;
            promptUI?.Hide();
        }

        private void Update()
        {
            if (!_playerInside) return;

            if (CanShowPrompt())
            {
                promptUI?.Show(GetPromptText());
            }
            else
            {
                promptUI?.Hide();
            }

            if (Input.GetKeyDown(fallbackInteractKey))
            {
                Search();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = true;
            if (CanShowPrompt())
            {
                promptUI?.Show(GetPromptText());
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = false;
            promptUI?.Hide();
        }

        public void Search()
        {
            if (!CanSearch())
            {
                Debug.Log($"[SearchableInteractable2D] Search blocked. object='{definition?.ObjectId}' reason='{GetCannotSearchReason()}'.", this);
                return;
            }

            ResolveRunner();
            runner.Execute(definition);
            MarkSearchedIfNeeded();

            if (!disableAfterSearch) return;

            _playerInside = false;
            promptUI?.Hide();
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
            }
        }

        public bool CanSearch()
        {
            return GetCannotSearchReason() == "OK";
        }

        public string GetCannotSearchReason()
        {
            if (!_playerInside) return "Player is not inside trigger";
            if (definition == null) return "Definition is missing";

            ResolveRunner();
            if (runner == null) return "SearchResultRunner is missing";

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return $"GameState is not Exploration: {GameStateMachine.Instance.Current}";
            }

            if (IsAlreadySearched()) return $"Already searched: {ResolveUsedFlagKey()}";

            return "OK";
        }

        public bool CanShowPrompt()
        {
            if (!_playerInside) return false;
            if (definition == null) return false;

            ResolveRunner();
            if (runner == null) return false;

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            return !IsAlreadySearched();
        }

        private string GetPromptText()
        {
            if (definition != null && !string.IsNullOrEmpty(definition.PromptText))
            {
                return definition.PromptText;
            }

            return "E: 조사";
        }

        private void MarkSearchedIfNeeded()
        {
            if (!ShouldSearchOnce()) return;

            _searchedInMemory = true;

            string flagKey = ResolveUsedFlagKey();
            if (string.IsNullOrEmpty(flagKey))
            {
                Debug.LogWarning($"[SearchableInteractable2D] Search once is enabled but no used flag key could be resolved. object='{definition?.ObjectId}'.", this);
                return;
            }

            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[SearchableInteractable2D] StoryFlagManager missing. Used flag key='{flagKey}' was kept in memory only.", this);
                return;
            }

            StoryFlagManager.Instance.SetBool(flagKey, true);
        }

        private bool IsAlreadySearched()
        {
            if (!ShouldSearchOnce()) return false;
            if (_searchedInMemory) return true;

            string flagKey = ResolveUsedFlagKey();
            if (string.IsNullOrEmpty(flagKey) || StoryFlagManager.Instance == null) return false;

            return StoryFlagManager.Instance.GetBool(flagKey);
        }

        private bool ShouldSearchOnce()
        {
            return searchOnceOverride || (definition != null && definition.SearchOnce);
        }

        private string ResolveUsedFlagKey()
        {
            if (!string.IsNullOrEmpty(usedFlagKey)) return usedFlagKey;
            if (definition == null || string.IsNullOrEmpty(definition.ObjectId)) return string.Empty;

            return $"searched_{definition.ObjectId}";
        }

        private void ResolveRunner()
        {
            if (runner != null) return;

#if UNITY_2023_1_OR_NEWER
            runner = FindFirstObjectByType<SearchResultRunner>();
#else
            runner = FindObjectOfType<SearchResultRunner>();
#endif
        }

        private void ResolvePromptUI()
        {
            if (promptUI != null) return;

#if UNITY_2023_1_OR_NEWER
            promptUI = FindFirstObjectByType<StoryInteractionPromptUI>();
#else
            promptUI = FindObjectOfType<StoryInteractionPromptUI>();
#endif
        }
    }
}
