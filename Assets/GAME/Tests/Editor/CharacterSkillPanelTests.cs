using System.Linq;
using Game.Combat.Data;
using Game.EditorTools;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Tests.Integration;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests.UI
{
    public sealed class CharacterSkillPanelTests : FilmUniqueTestFixture
    {
        private CharacterSkillPanelView _view;
        private CharacterSkillPresenter _presenter;

        private void Install(params SkillDefinitionSO[] skills)
        {
            Register(skills);
            _view = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCharacterSkillUISetup.PanelPath)).GetComponent<CharacterSkillPanelView>();
            _view.gameObject.SetActive(true);
            Invoke(_view, "OnEnable");
            FilmEquipApplication application = new(Participant.Runtime, Party, skills, State, Entry);
            _presenter = new CharacterSkillPresenter(_view, application, Participant.Runtime, Party, CharacterProgressionService.Instance, State, skills);
        }

        [TearDown]
        public void DisposeView()
        {
            _presenter?.Dispose();
            _presenter = null;
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
        }

        [Test]
        public void RealButtons_EquipSelectAndUnequipIndependentCharacterLoadouts()
        {
            EventSystem events = Component<EventSystem>();
            Invoke(events, "OnEnable");
            SkillDefinitionSO film = Skill("internal.film.test", 11, SkillOwnershipCategory.Film);
            film.displayName = "시험 필름";
            Party.SetLeader("hero.b");
            Install(film);
            Assert.That(_view.LastModel.SelectedCharacterId, Is.EqualTo("hero.b"));
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            ButtonAt("filmContent").onClick.Invoke();
            Assert.That(Participant.Runtime.IsEquipped("hero.b", film.PersistentKey), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonAt("equippedContent").gameObject));
            Assert.That(Text("feedbackLabel"), Is.EqualTo("필름을 장착했습니다."));
            Assert.That(ButtonAt("filmContent").interactable, Is.False);
            Content("characterContent").GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(_view.LastModel.SelectedCharacterId, Is.EqualTo("hero.a"));
            Assert.That(_view.LastModel.EquippedFilmKeys, Is.Empty);
            ButtonAt("filmContent").onClick.Invoke();
            ButtonAt("equippedContent").onClick.Invoke();
            Assert.That(Participant.Runtime.IsEquipped("hero.a", film.PersistentKey), Is.False);
            Assert.That(Participant.Runtime.IsEquipped("hero.b", film.PersistentKey), Is.True);
            Assert.That(Text("feedbackLabel"), Is.EqualTo("필름을 해제했습니다."));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonAt("filmContent").gameObject));
            Assert.That(VisibleText(), Does.Contain("시험 필름").And.Not.Contain("internal.film.test"));
        }

        [Test]
        public void SaveRestoreAndNewGame_RefreshActualListsAndEmptyStates()
        {
            SkillDefinitionSO film = Skill("film.saved", 11, SkillOwnershipCategory.Film);
            Install(film);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            ButtonAt("filmContent").onClick.Invoke();
            GameSaveData save = new();
            Participant.CaptureSaveData(save);
            Participant.ResetForNewGame();
            Assert.That(Content("filmContent").childCount, Is.Zero);
            Assert.That(ObjectAt("filmEmptyLabel").activeSelf, Is.True);
            Participant.RestoreSaveData(SaveSerializer.FromGameSaveJson(SaveSerializer.ToJson(save)));
            Assert.That(Content("equippedContent").childCount, Is.EqualTo(1));
            Assert.That(VisibleText(), Does.Contain("이름 없는 필름").And.Not.Contain("film.saved"));
            Party.ResetForNewGame();
            Assert.That(ObjectAt("characterEmptyLabel").activeSelf, Is.True);
            Assert.That(_view.gameObject.activeSelf, Is.True, "Empty parties must still be able to close the routed screen.");
            Assert.That(ButtonAt("filmContent").interactable, Is.False);
        }

        [Test]
        public void StaleRowClicksAndDisposedPresenter_DoNotMutateAnotherSelection()
        {
            SkillDefinitionSO film = Skill("film.stale", 11, SkillOwnershipCategory.Film);
            Install(film);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            Button.ButtonClickedEvent oldClick = ButtonAt("filmContent").onClick;
            Content("characterContent").GetChild(1).GetComponent<Button>().onClick.Invoke();
            oldClick.Invoke();
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.b"), Is.Empty);
            Button.ButtonClickedEvent currentClick = ButtonAt("filmContent").onClick;
            _view.gameObject.SetActive(false);
            Invoke(_view, "OnDisable");
            currentClick.Invoke();
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.b"), Is.Empty);
            _presenter.Dispose();
            CharacterSkillViewModel previous = _view.LastModel;
            Participant.ResetForNewGame();
            Assert.That(_view.LastModel, Is.SameAs(previous));
        }

        [Test]
        public void UniqueRows_ShowNamesLevelLocksPendingAndProgressionRefresh()
        {
            SkillDefinitionSO first = Skill("unique.hidden.key", 21, SkillOwnershipCategory.Unique, "hero.a");
            SkillDefinitionSO pending = Skill("unique.pending.key", 22, SkillOwnershipCategory.Unique, "hero.a");
            first.displayName = "첫 고유 스킬"; pending.displayName = "미정 고유 스킬";
            CharacterProgressionDefinitionSO definition = Asset<CharacterProgressionDefinitionSO>();
            SerializedObject data = new(definition);
            data.FindProperty("characterId").stringValue = "hero.a";
            data.FindProperty("startingLevel").intValue = 4;
            data.FindProperty("experienceRequiredByLevel").arraySize = 1;
            data.FindProperty("experienceRequiredByLevel").GetArrayElementAtIndex(0).intValue = 10;
            SerializedProperty entries = data.FindProperty("uniqueSkillUnlocks"); entries.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("enabled").boolValue = true;
                item.FindPropertyRelative("persistentSkillKey").stringValue = i == 0 ? first.PersistentKey : pending.PersistentKey;
                item.FindPropertyRelative("condition").enumValueIndex = i == 0 ? 1 : 0;
                item.FindPropertyRelative("unlockLevel").intValue = i == 0 ? 5 : 0;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, definition);
            Install(first, pending);
            Assert.That(VisibleText(), Does.Contain("첫 고유 스킬").And.Contain("잠금 · Lv.5").And.Contain("조건 미정").And.Not.Contain("unique.hidden.key"));
            Assert.That(Text("nextUnlockLabel"), Is.EqualTo("다음 해금: 레벨 5"));
            progression.ApplyExperience("hero.a", 10);
            Assert.That(VisibleText(), Does.Contain("해금됨").And.Contain("조건 미정"));
            Assert.That(Content("uniqueContent").GetComponentsInChildren<Button>().Length, Is.Zero);
        }

        [Test]
        public void LargeFilmList_UsesSerializedScrollContentAndKeepsEquipOrder()
        {
            SkillDefinitionSO[] films = Enumerable.Range(1, 40).Select(i => Skill($"film.{i}", i + 10, SkillOwnershipCategory.Film)).ToArray();
            Install(films);
            foreach (SkillDefinitionSO film in films) Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            Assert.That(Content("filmContent").childCount, Is.EqualTo(40));
            string first = _view.LastModel.SharedFilms[17].PersistentSkillKey;
            string second = _view.LastModel.SharedFilms[3].PersistentSkillKey;
            ButtonAt("filmContent", 17).onClick.Invoke();
            ButtonAt("filmContent", 3).onClick.Invoke();
            Assert.That(_view.LastModel.EquippedFilmKeys, Is.EqualTo(new[] { first, second }));
            Assert.That(Content("filmContent").GetComponentInParent<ScrollRect>().vertical, Is.True);
        }

        [Test]
        public void LongDisplayNames_AreBoundedWithoutExposingKeysOrHidingActions()
        {
            SkillDefinitionSO film = Skill("never.show.this.key", 11, SkillOwnershipCategory.Film);
            film.displayName = string.Concat(Enumerable.Repeat("아주 긴 필름 이름 ", 20));
            Install(film);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            TMP_Text label = Content("filmContent").GetChild(0).GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Name");
            Assert.That(label.text, Is.EqualTo(film.displayName));
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(label.maxVisibleLines, Is.EqualTo(2));
            Assert.That(ButtonAt("filmContent").interactable, Is.True);
            Assert.That(VisibleText(), Does.Not.Contain(film.PersistentKey));
        }

        private UnityEngine.Object Reference(string field) => new SerializedObject(_view).FindProperty(field).objectReferenceValue;
        private Transform Content(string field) => ((RectTransform)Reference(field)).transform;
        private Button ButtonAt(string field, int row = 0) => Content(field).GetChild(row).GetComponentInChildren<Button>();
        private string Text(string field) => ((TMP_Text)Reference(field)).text;
        private GameObject ObjectAt(string field) => ((Component)Reference(field)).gameObject;
        private string VisibleText() => string.Join("|", _view.GetComponentsInChildren<TMP_Text>().Select(text => text.text));
    }
}
