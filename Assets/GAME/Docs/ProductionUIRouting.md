# Production UI routing

> 2026-10-11 문서 역할 갱신: 아래 본문은 기존 상세 Setup/호환성·당시 검증 참고로 보존한다. 현재 Production 연결은 [통합 Production Guide](https://docs.google.com/document/d/1-eHB2QEf9RuW6KK_7UM4zCR1YhOJuqzJTTsAW_Fk-Jo/edit)와 [콘텐츠 제작 인덱스](GAME002_ContentAuthoring_Index.md)를 우선한다. 현행 Unity 6000.3.23f1, FinalExchange, SceneTravel→SceneFlow 위임, 저장 schema 12, B01 Flag/Party EXP 확장을 확인했다. 아래 옛 Planning·schema·기본 EXP target·미구현 설명을 현행 상태로 그대로 사용하지 않는다. 정식 콘텐츠/Scene/Player 검증은 별도로 남긴다.

`GameFlowController` requests global flow changes, `GameStateMachine` stores them, and
`UIScreenRouter` is the only Production route selector. `GameUIRootController` is a passive
facade over explicitly authored Title, Field, Dialogue, Choice, Combat, Reward, Pause, and
Loading roots. Presenters may change content beneath those roots but must not reactivate a
root hidden by the route.

Boot and Loading show Loading; Title shows Title; Exploration shows Field; Dialogue shows
Field plus Dialogue; Choice adds Choice; all Combat states show Combat; Reward shows Reward;
Cutscene shows Dialogue; UIOnly deliberately shows no routed content. Paused retains the last
content route and overlays Pause.

Full rewards render through `RewardUIPanel` while the Reward route is active. Small field
acquisition messages use `FieldRewardToast` beneath FieldRoot and replace the previous toast.
The compatibility `RewardUIPanel.TryShowFieldRewardMessage` forwards to the toast when wired.

Inspector references are the Production policy. Unique type/name discovery remains an
observable compatibility fallback only; ambiguous candidates are rejected. Run **Tools >
GAME > Validate Production UI Routing** after editing the Production UI prefab or dungeon
scenes. Do not place the Router or Root controller beneath a root they toggle.
