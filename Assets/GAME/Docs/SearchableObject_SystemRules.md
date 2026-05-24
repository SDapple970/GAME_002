# Searchable Object System Rules

- `StoryInteractable2D` remains for NPC dialogue and authored story events.
- `SearchableInteractable2D` is only for repeatable investigation props such as boxes, journals, life objects, collections, and apartment fixtures.
- Search result data lives in `SearchableObjectDefinitionSO` assets. Do not create one `StoryEventDefinitionSO` per prop when weighted outcomes are enough.
- `SearchOutcome.forceWhenConditionsMet` resolves before weighted random selection. Non-forced outcomes are selected from condition-passing entries with `weight > 0`.
- MVP effects that do not have a concrete system yet log placeholders. Story flag integer and boolean effects are wired to `StoryFlagManager`.
- Inventory, journal, cat, map, and battle integrations should be connected in later passes through `SearchEffect.Apply`, without changing prop data shape.
- Items such as alcohol or cigarettes should be modeled as hazards, evidence, disposal targets, or area risk signals, not as positive consumables for youth characters.
