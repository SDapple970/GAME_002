using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Input;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Explicit, Editor-only Production smoke run. No fixture data, save writes or debug runtime dependencies.</summary>
    [InitializeOnLoad]
    public static class CharacterSkillUIPlayModeVerification
    {
        private const string Key = "GAME002.CharacterSkillUIVerification.";
        private static readonly Vector2Int[] Resolutions = { new(1280, 720), new(1920, 1080), new(2560, 1440) };
        private static readonly List<string> Errors = new();
        private static int _readyFrame;
        private static CharacterSkillUIHost _oldHost;

        static CharacterSkillUIPlayModeVerification()
        {
            if (SessionState.GetBool(Key + "Running", false)) Subscribe();
        }

        [MenuItem("GAME/Verification/Run Production Skill UI Smoke")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start verification outside Play Mode.");
            EditorSceneManager.OpenScene(ProductionCharacterSkillUISetup.ScenePath);
            ProductionCharacterSkillUISetup.ValidateScene(SceneManager.GetActiveScene());
            PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
            SessionState.SetInt(Key + "OriginalWidth", (int)width);
            SessionState.SetInt(Key + "OriginalHeight", (int)height);
            SessionState.SetBool(Key + "Running", true);
            SessionState.SetInt(Key + "Stage", 0);
            SessionState.SetInt(Key + "Index", 0);
            SessionState.SetInt(Key + "Repeat", 0);
            SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key + "Failed", false);
            Directory.CreateDirectory("Logs");
            Subscribe();
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            SetResolution(0);
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= RecordLog; Application.logMessageReceived += RecordLog;
        }

        private static void RecordLog(string message, string trace, LogType type)
        {
            if (Application.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Errors.Add(message + "\n" + trace);
        }

        private static void Tick()
        {
            try
            {
                int stage = SessionState.GetInt(Key + "Stage", 0);
                if (stage == 5)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    PlayModeWindow.SetCustomRenderingResolution((uint)Math.Max(1, SessionState.GetInt(Key + "OriginalWidth", 1920)),
                        (uint)Math.Max(1, SessionState.GetInt(Key + "OriginalHeight", 1080)), "Skill UI verification");
                    SessionState.SetBool(Key + "Running", false);
                    EditorApplication.update -= Tick;
                    Application.logMessageReceived -= RecordLog;
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
                    return;
                }
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 150)
                    throw new InvalidOperationException("Production UI verification timed out.");
                if (!Application.isPlaying || EditorApplication.isCompiling || Time.frameCount < 15 || Time.frameCount < _readyFrame) return;
                int index = SessionState.GetInt(Key + "Index", 0);
                UIScreenRouter router = One<UIScreenRouter>();
                CharacterSkillUIHost host = One<CharacterSkillUIHost>();
                CharacterSkillPanelView panel = One<CharacterSkillPanelView>();
                GameUIRootController roots = One<GameUIRootController>();
                Button entry = (Button)new SerializedObject(host).FindProperty("openButton").objectReferenceValue;
                if (stage == 0)
                {
                    Require(GameStateMachine.Instance.Current == GameState.Exploration, "Production did not enter Exploration.");
                    Require(PartyRuntime.Instance.Members.Count == 0 && CharacterSkillSaveParticipant.Instance.Runtime.GetSharedFilms().Count == 0,
                        "Verification expects actual empty Production content, not fixture data.");
                    Require(entry.interactable && entry.gameObject.activeInHierarchy, "Field skill entry is unavailable.");
                    Click(entry);
                    Next(1, 8);
                }
                else if (stage == 1)
                {
                    Require(GameStateMachine.Instance.Current == GameState.UIOnly && roots.CharacterSkillVisible && panel.gameObject.activeInHierarchy,
                        "Serialized entry click did not open the routed skill panel.");
                    Require(host.HasPresenter && panel.LastModel != null && panel.LastModel.CharacterIds.Count == 0, "Production presenter or empty-party view is missing.");
                    Require(!new InputRouter().AllowsExplorationInput(), "Exploration input remained allowed in UIOnly.");
                    Require(panel.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "보유한 캐릭터가 없습니다."), "Empty state is not visible.");
                    if (index == 0)
                    {
                        GameFlowController.Instance.Pause();
                        Require(!roots.CharacterSkillVisible && !host.HasPresenter, "Pause did not release the skill presenter.");
                        GameFlowController.Instance.ResumePreviousState();
                        Require(roots.CharacterSkillVisible && host.HasPresenter, "Pause resume did not restore the requested panel.");
                    }
                    Canvas.ForceUpdateCanvases();
                    Vector2Int resolution = Resolutions[index];
                    Require(Screen.width == resolution.x && Screen.height == resolution.y, $"Rendering resolution mismatch: {Screen.width}x{Screen.height}.");
                    CheckBounds((RectTransform)panel.transform.Find("Panel"));
                    CheckBounds((RectTransform)panel.CloseButton.transform);
                    ScreenCapture.CaptureScreenshot(ScreenshotPath(index));
                    Next(2, 12);
                }
                else if (stage == 2)
                {
                    if (!File.Exists(ScreenshotPath(index))) return;
                    Texture2D screenshot = new(2, 2);
                    try
                    {
                        Require(screenshot.LoadImage(File.ReadAllBytes(ScreenshotPath(index))), "Screenshot is not a valid rendered PNG.");
                        Require(screenshot.width == Resolutions[index].x && screenshot.height == Resolutions[index].y, "Screenshot dimensions mismatch.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(screenshot); }
                    Click(panel.CloseButton);
                    Require(GameStateMachine.Instance.Current == GameState.Exploration && !roots.CharacterSkillVisible && roots.FieldVisible,
                        "Close did not restore the field route.");
                    Require(!host.HasPresenter && new InputRouter().AllowsExplorationInput(), "Close did not dispose presenter/restore input permission.");
                    if (++index < Resolutions.Length)
                    {
                        SessionState.SetInt(Key + "Index", index); SetResolution(index); Next(0, 12);
                    }
                    else Next(3, 5);
                }
                else if (stage == 3)
                {
                    Click(entry); Require(host.HasPresenter, "Repeated open failed.");
                    Next(6, 3);
                }
                else if (stage == 6)
                {
                    Click(panel.CloseButton); Require(!host.HasPresenter, "Repeated close leaked presenter.");
                    int repeat = SessionState.GetInt(Key + "Repeat", 0) + 1;
                    SessionState.SetInt(Key + "Repeat", repeat);
                    Next(repeat < 10 ? 3 : 7, 3);
                }
                else if (stage == 7)
                {
                    GameFlowController.Instance.EnterUIOnly();
                    Require(!roots.CharacterSkillVisible, "Another UIOnly consumer opened the skill panel.");
                    GameFlowController.Instance.EnterExploration();
                    Next(8, 3);
                }
                else if (stage == 8)
                {
                    Click(entry);
                    _oldHost = host;
                    GameFlowController.Instance.BeginLoading();
                    SceneManager.LoadScene("Dungeon_1_Production", LoadSceneMode.Single);
                    Next(4, 20);
                }
                else if (stage == 4)
                {
                    Require(_oldHost == null && !host.HasPresenter && !roots.CharacterSkillVisible, "Scene reload retained old UI/presenter.");
                    Require(GameStateMachine.Instance.Current == GameState.Exploration && new InputRouter().AllowsExplorationInput(), "Scene bootstrap did not restore Exploration.");
                    One<EventSystem>();
                    Require(Errors.Count == 0, "Production Console errors:\n" + string.Join("\n", Errors));
                    Finish(false, "PASS: actual Dungeon_1_Production; serialized pointer/raycast Open/Close at 1280x720, 1920x1080, 2560x1440; empty states; input permission block/recovery; pause/resume; 10 repeated opens/closes; other UIOnly isolation; scene reload; singleton owners/EventSystem; 0 runtime Console errors.\nScreenshots: Combat17C5_ProductionSkills_{resolution}.png\nNot covered: hardware movement/attack input, NPC dialogue, contact/field-attack/reward Play Mode E2E, authored Film/Unique content E2E.");
                }
            }
            catch (Exception exception) { Finish(true, "FAIL: " + exception); }
        }

        private static void Next(int stage, int frames)
        {
            SessionState.SetInt(Key + "Stage", stage);
            _readyFrame = Time.frameCount + frames;
        }

        private static void Finish(bool failed, string result)
        {
            File.WriteAllText("Logs/Combat17C5_PlayMode.txt", result + "\n");
            SessionState.SetBool(Key + "Failed", failed);
            SessionState.SetInt(Key + "Stage", 5);
            Debug.Log("[CharacterSkillUIVerification] " + result);
            EditorApplication.isPlaying = false;
        }

        private static T One<T>() where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(found.Length == 1, $"Expected one {typeof(T).Name}, found {found.Length}.");
            return found[0];
        }

        private static void SetResolution(int index) => PlayModeWindow.SetCustomRenderingResolution((uint)Resolutions[index].x, (uint)Resolutions[index].y, "Skill UI verification");
        private static string ScreenshotPath(int index) => $"Logs/Combat17C5_ProductionSkills_{Resolutions[index].x}x{Resolutions[index].y}.png";
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)button.transform;
            PointerEventData pointer = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            List<RaycastResult> hits = new(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject == button.gameObject,
                $"Button '{button.name}' is occluded or cannot receive raycasts at {pointer.position}. Hits: {string.Join(", ", hits.Select(hit => hit.gameObject.name))}.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void CheckBounds(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Require(corners.All(point => point.x >= -1 && point.y >= -1 && point.x <= Screen.width + 1 && point.y <= Screen.height + 1),
                $"'{rect.name}' exceeds the rendered screen bounds.");
        }
    }
}
