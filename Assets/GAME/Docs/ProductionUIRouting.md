# Production UI routing

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
