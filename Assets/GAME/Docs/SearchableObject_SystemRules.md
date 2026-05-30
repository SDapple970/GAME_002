# Searchable Object System Rules

- `StoryInteractable2D` remains for NPC dialogue and authored story events.
- `SearchableInteractable2D` is only for repeatable investigation props such as boxes, journals, life objects, collections, and apartment fixtures.
- `SearchDecisionHUD` handles the pre-search confirmation flow. Do not route investigation choices through `StoryDialogueHUD`.
- With `ShowQuestionOnEnter` enabled, entering range shows only `QuestionMessage`. Clicking the question bubble or pressing the interact key opens the decision step; confirm/cancel buttons must not appear before that.
- `SearchObjectAnchor` is the world-space point used by decision/result bubbles. If no explicit anchor transform is assigned, it falls back to an offset above the object.
- Search result data lives in `SearchableObjectDefinitionSO` assets. Do not create one `StoryEventDefinitionSO` per prop when weighted outcomes are enough.
- `SearchOutcome.forceWhenConditionsMet` resolves before weighted random selection. Non-forced outcomes are selected from condition-passing entries with `weight > 0`.
- MVP effects that do not have a concrete system yet log placeholders. Story flag integer and boolean effects are wired to `StoryFlagManager`.
- Loot, journal, cat, mentality, and stress search rewards are temporarily recorded through `SearchRewardManager` until the real inventory/status systems are connected.
- Search prop visuals can switch from default to searched state through `SearchObjectVisualState2D` only after a confirmed search executes successfully.
- Items such as alcohol or cigarettes should be modeled as hazards, evidence, disposal targets, or area risk signals, not as positive consumables for youth characters.
