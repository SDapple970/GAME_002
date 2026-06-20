using Game.Core;
using Game.Search.Data;
using Game.Search.UI;
using Game.Story;
using Game.Story.Core;
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
        [SerializeField] private SearchObjectAnchor objectAnchor;
        [SerializeField] private SearchDecisionHUD decisionHUD;
        [SerializeField] private SearchObjectVisualState2D visualState;

        private Collider2D _triggerCollider;
        private bool _playerInside;
        private bool _searchedInMemory;
        private bool _questionVisible;
        private bool _decisionOpen;

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
            ResolveObjectAnchor();
            ResolveDecisionHUD();
            ResolveVisualState();
        }

        private void OnDisable()
        {
            _playerInside = false;
            _questionVisible = false;
            _decisionOpen = false;
            promptUI?.Hide();
            decisionHUD?.Hide();
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (_decisionOpen) return;

            if (UsesQuestionFlow())
            {
                promptUI?.Hide();
                if (_questionVisible && UnityEngine.Input.GetKeyDown(fallbackInteractKey))
                {
                    OpenDecisionChoices();
                }

                return;
            }

            if (CanShowPrompt())
            {
                promptUI?.Show(GetPromptText());
            }
            else
            {
                promptUI?.Hide();
            }

            if (UnityEngine.Input.GetKeyDown(fallbackInteractKey))
            {
                Search();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = true;
            _questionVisible = false;
            _decisionOpen = false;

            ResolveRunner();
            ResolveObjectAnchor();
            ResolveDecisionHUD();
            ResolveVisualState();

            if (UsesQuestionFlow() && CanSearch())
            {
                ShowQuestionOnly();
                promptUI?.Hide();
                return;
            }

            if (CanShowPrompt())
            {
                promptUI?.Show(GetPromptText());
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInside = false;
            _questionVisible = false;
            _decisionOpen = false;
            promptUI?.Hide();
            decisionHUD?.Hide();
        }

        public void Search()
        {
            if (!CanSearch())
            {
                Debug.Log($"[SearchableInteractable2D] Search blocked. object='{definition?.ObjectId}' reason='{GetCannotSearchReason()}'.", this);
                return;
            }

            ResolveRunner();
            ResolveObjectAnchor();
            ResolveDecisionHUD();
            ResolveVisualState();

            if (UsesQuestionFlow())
            {
                OpenDecisionChoices();
                return;
            }

            if (definition.RequireConfirmation && decisionHUD != null)
            {
                OpenDecisionChoices();
                return;
            }

            ExecuteSearchConfirmed();
        }

        public bool CanSearch()
        {
            return GetCannotSearchReason() == "OK";
        }

        public string GetCannotSearchReason()
        {
            if (!_playerInside) return "Player is not inside trigger";
            if (definition == null) return "Definition is missing";
            if (_decisionOpen) return "Decision UI is already open";

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
            if (_questionVisible || _decisionOpen) return false;
            if (UsesQuestionFlow()) return false;
            if (definition == null) return false;

            ResolveRunner();
            if (runner == null) return false;

            if (requireExplorationState && GameStateMachine.Instance != null && GameStateMachine.Instance.Current != GameState.Exploration)
            {
                return false;
            }

            return !IsAlreadySearched();
        }

        private void OpenDecisionChoices()
        {
            if (!CanSearch())
            {
                Debug.Log($"[SearchableInteractable2D] Decision open blocked. object='{definition?.ObjectId}' reason='{GetCannotSearchReason()}'.", this);
                return;
            }

            ResolveObjectAnchor();
            ResolveDecisionHUD();

            if (decisionHUD == null)
            {
                ExecuteSearchConfirmed();
                return;
            }

            _questionVisible = false;
            _decisionOpen = true;
            promptUI?.Hide();

            decisionHUD.Show(
                objectAnchor,
                string.IsNullOrEmpty(definition.ConfirmationMessage) ? "어떻게 할까?" : definition.ConfirmationMessage,
                definition.ConfirmChoiceText,
                definition.CancelChoiceText,
                ExecuteSearchConfirmed,
                CancelSearch);
        }

        private void ExecuteSearchConfirmed()
        {
            _decisionOpen = false;
            _questionVisible = false;

            if (!CanSearch())
            {
                Debug.Log($"[SearchableInteractable2D] Confirmed search blocked. object='{definition?.ObjectId}' reason='{GetCannotSearchReason()}'.", this);
                return;
            }

            ResolveRunner();
            ResolveObjectAnchor();
            ResolveVisualState();

            if (runner.Execute(definition, objectAnchor))
            {
                visualState?.SetSearched();
                MarkSearchedIfNeeded();
            }
            else
            {
                return;
            }

            if (!disableAfterSearch) return;

            _playerInside = false;
            promptUI?.Hide();
            decisionHUD?.Hide();
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
            }
        }

        private void CancelSearch()
        {
            _decisionOpen = false;

            if (!_playerInside) return;

            if (UsesQuestionFlow() && CanSearch())
            {
                ShowQuestionOnly();
                promptUI?.Hide();
                return;
            }

            if (CanShowPrompt())
            {
                promptUI?.Show(GetPromptText());
            }
        }

        private void ShowQuestionOnly()
        {
            ResolveObjectAnchor();
            ResolveDecisionHUD();

            if (decisionHUD == null || definition == null) return;

            _questionVisible = true;
            _decisionOpen = false;
            decisionHUD.ShowQuestionOnly(objectAnchor, definition.QuestionMessage, OpenDecisionChoices);
        }

        private bool UsesQuestionFlow()
        {
            return definition != null
                && definition.ShowQuestionOnEnter
                && definition.RequireConfirmation
                && decisionHUD != null;
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

        private void ResolveObjectAnchor()
        {
            if (objectAnchor != null) return;

            objectAnchor = GetComponentInChildren<SearchObjectAnchor>();
            if (objectAnchor != null) return;

            objectAnchor = GetComponent<SearchObjectAnchor>();
        }

        private void ResolveDecisionHUD()
        {
            if (decisionHUD != null) return;

#if UNITY_2023_1_OR_NEWER
            decisionHUD = FindFirstObjectByType<SearchDecisionHUD>();
#else
            decisionHUD = FindObjectOfType<SearchDecisionHUD>();
#endif
        }

        private void ResolveVisualState()
        {
            if (visualState != null) return;

            visualState = GetComponentInChildren<SearchObjectVisualState2D>();
            if (visualState != null) return;

            visualState = GetComponent<SearchObjectVisualState2D>();
        }
    }
}
