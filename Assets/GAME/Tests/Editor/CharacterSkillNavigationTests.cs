using Game.Core;
using Game.EditorTools;
using Game.Input;
using Game.Tests.Integration;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.UI
{
    public sealed class CharacterSkillNavigationTests : FilmUniqueTestFixture
    {
        private GameUIRootController _roots;
        private UIScreenRouter _router;
        private GameObject _panel;

        private void Install()
        {
            _roots = Component<GameUIRootController>();
            string[] names = { "titleRoot", "fieldRoot", "dialogueRoot", "choiceRoot", "combatRoot", "rewardRoot", "pauseRoot", "loadingRoot", "characterSkillRoot" };
            foreach (string name in names)
            {
                GameObject root = new(name);
                root.transform.SetParent(_roots.transform);
                ProductionCharacterSkillUISetup.Assign(_roots, name, root);
                if (name == "characterSkillRoot") _panel = root;
            }
            _router = Component<UIScreenRouter>();
            ProductionCharacterSkillUISetup.Assign(_router, "uiRoot", _roots, "stateMachine", State);
            Invoke(_router, "OnEnable");
            _router.ApplyCurrentRoute();
        }

        [Test]
        public void OpenClose_RoutesViaFlowAndRestoresExistingInput()
        {
            Install();
            Assert.That(_router.TryOpenCharacterSkills(), Is.True);
            Assert.That(State.Current, Is.EqualTo(GameState.UIOnly));
            Assert.That(_panel.activeSelf, Is.True);
            Assert.That(_roots.FieldVisible, Is.False);
            Assert.That(new InputRouter().AllowsExplorationInput(), Is.False);
            Assert.That(_router.TryOpenCharacterSkills(), Is.False);
            Assert.That(_router.TryCloseCharacterSkills(), Is.True);
            Assert.That(State.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(_roots.FieldVisible, Is.True);
            Assert.That(new InputRouter().AllowsExplorationInput(), Is.True);
            Assert.That(_router.TryCloseCharacterSkills(), Is.False);
        }

        [Test]
        public void OtherUIOnly_DoesNotOpenSkillPanelOrBecomeClosableBySkillRoute()
        {
            Install();
            Flow.EnterUIOnly();
            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(_router.TryCloseCharacterSkills(), Is.False);
        }

        [TestCase(GameState.Dialogue)]
        [TestCase(GameState.Choice)]
        [TestCase(GameState.Reward)]
        [TestCase(GameState.Cutscene)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.CombatTransition)]
        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        public void NonExploration_CannotOpen(GameState state)
        {
            Install();
            if (state == GameState.Choice) Flow.BeginDialogue();
            if (state == GameState.CombatResolving) Flow.EnterCombatPlanning();
            Assert.That(Flow.RequestState(state, "Navigation test"), Is.True);
            Assert.That(_router.TryOpenCharacterSkills(), Is.False);
            Assert.That(State.Current, Is.EqualTo(state));
            Assert.That(_panel.activeSelf, Is.False);
        }

        [Test]
        public void Pause_HidesPanelAndResumeRestoresRequestedUIOnly()
        {
            Install();
            _router.TryOpenCharacterSkills();
            Flow.Pause();
            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(_router.TryCloseCharacterSkills(), Is.False);
            Flow.ResumePreviousState();
            Assert.That(_panel.activeSelf, Is.True);
            Assert.That(_router.TryCloseCharacterSkills(), Is.True);
        }

        [Test]
        public void ForcedSceneFlow_ClearsRequest()
        {
            Install();
            _router.TryOpenCharacterSkills();
            Flow.BeginLoading();
            Assert.That(_panel.activeSelf, Is.False);
            Flow.EnterExploration();
            Flow.EnterUIOnly();
            Assert.That(_panel.activeSelf, Is.False);
        }
    }
}
