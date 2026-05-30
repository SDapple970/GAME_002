using System.Collections.Generic;
using Game.Search.Data;
using Game.Search.UI;
using UnityEngine;

namespace Game.Search
{
    public sealed class SearchResultRunner : MonoBehaviour
    {
        [SerializeField] private SearchResultHUD resultHUD;
        [SerializeField] private ItemAcquisitionHUD itemAcquisitionHUD;

        public SearchOutcome Roll(SearchableObjectDefinitionSO definition)
        {
            if (definition == null) return null;

            IReadOnlyList<SearchOutcome> outcomes = definition.Outcomes;
            if (outcomes == null || outcomes.Count == 0)
            {
                Debug.LogWarning($"[SearchResultRunner] No outcomes configured for searchable object='{definition.ObjectId}'.", definition);
                return null;
            }

            foreach (SearchOutcome outcome in outcomes)
            {
                if (outcome != null && outcome.ForceWhenConditionsMet && outcome.AreConditionsMet())
                {
                    return outcome;
                }
            }

            List<SearchOutcome> candidates = new();
            int totalWeight = 0;

            foreach (SearchOutcome outcome in outcomes)
            {
                if (outcome == null || outcome.ForceWhenConditionsMet || outcome.Weight <= 0 || !outcome.AreConditionsMet())
                {
                    continue;
                }

                candidates.Add(outcome);
                totalWeight += outcome.Weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0)
            {
                Debug.LogWarning($"[SearchResultRunner] No valid outcomes for searchable object='{definition.ObjectId}'.", definition);
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cursor = 0;

            foreach (SearchOutcome outcome in candidates)
            {
                cursor += outcome.Weight;
                if (roll < cursor)
                {
                    return outcome;
                }
            }

            return candidates[candidates.Count - 1];
        }

        public bool Execute(SearchableObjectDefinitionSO definition)
        {
            return Execute(definition, null);
        }

        public bool Execute(SearchableObjectDefinitionSO definition, SearchObjectAnchor anchor)
        {
            if (definition == null) return false;

            SearchOutcome selected = Roll(definition);
            if (selected == null) return false;

            List<SearchRewardProposal> rewardProposals = new();
            ApplyEffects(selected, rewardProposals);

            if (rewardProposals.Count > 0)
            {
                ResolveItemAcquisitionHUD();
                ShowRewardProposal(rewardProposals, 0);
            }

            ResolveHUD();
            resultHUD?.ShowMessage(selected.ResultMessage, anchor, definition.ResultMessageSeconds);
            return true;
        }

        private void ApplyEffects(SearchOutcome outcome, List<SearchRewardProposal> rewardProposals)
        {
            if (outcome?.Effects == null) return;

            foreach (SearchEffect effect in outcome.Effects)
            {
                if (effect == null) continue;

                if (effect.TryCreateRewardProposal(out SearchRewardProposal proposal))
                {
                    rewardProposals.Add(proposal);
                    continue;
                }

                effect.ApplyImmediate();
            }
        }

        private void ShowRewardProposal(IReadOnlyList<SearchRewardProposal> proposals, int index)
        {
            if (proposals == null || index >= proposals.Count) return;

            SearchRewardProposal proposal = proposals[index];
            if (itemAcquisitionHUD == null)
            {
                Debug.LogWarning($"[SearchResultRunner] ItemAcquisitionHUD missing. Reward proposal ignored. name='{proposal?.RewardName}'.", this);
                return;
            }

            itemAcquisitionHUD.Show(
                proposal,
                () =>
                {
                    if (SearchRewardManager.Instance != null)
                    {
                        SearchRewardManager.Instance.AcceptReward(proposal);
                    }
                    else
                    {
                        Debug.LogWarning($"[SearchResultRunner] SearchRewardManager missing. Accepted reward was not recorded. name='{proposal?.RewardName}'.", this);
                    }

                    ShowRewardProposal(proposals, index + 1);
                },
                () =>
                {
                    Debug.Log($"[SearchResultRunner] Reward rejected. name='{proposal?.RewardName}' amount={proposal?.Amount}.", this);
                    ShowRewardProposal(proposals, index + 1);
                });
        }

        private void ResolveHUD()
        {
            if (resultHUD != null) return;

#if UNITY_2023_1_OR_NEWER
            resultHUD = FindFirstObjectByType<SearchResultHUD>();
#else
            resultHUD = FindObjectOfType<SearchResultHUD>();
#endif
        }

        private void ResolveItemAcquisitionHUD()
        {
            if (itemAcquisitionHUD != null) return;

#if UNITY_2023_1_OR_NEWER
            itemAcquisitionHUD = FindFirstObjectByType<ItemAcquisitionHUD>();
#else
            itemAcquisitionHUD = FindObjectOfType<ItemAcquisitionHUD>();
#endif
        }
    }
}
