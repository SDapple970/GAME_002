using System;
using System.Reflection;
using UnityEngine;

namespace GAME.Player.Overworld
{
    /// <summary>
    /// 기존 코드 수정 없이,
    /// Input(OverworldInputAdapter/PlayerInputUnity 등) -> PlayerMotor2D / OverworldAttack2D로 연결해주는 Glue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverworldPlayerGlue : MonoBehaviour
    {
        [Header("References (Drag & Drop 권장)")]
        [Tooltip("입력 소스. 보통 Input 폴더의 OverworldInputAdapter 또는 PlayerInputUnity를 지정.")]
        [SerializeField] private MonoBehaviour inputSource;

        [Tooltip("Player/PlayerMotor2D")]
        [SerializeField] private MonoBehaviour playerMotor;

        [Tooltip("Player/Overworld/OverworldAttack2D")]
        [SerializeField] private MonoBehaviour overworldAttack;

        [Header("Auto Find (비어있으면 자동으로 찾음)")]
        [SerializeField] private bool autoFindOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool logBindings = true;

        // 캐시된 바인딩
        private Func<Vector2> _getMove;
        private Func<bool> _getJumpDown;
        private Func<bool> _getJumpUp;
        private Func<bool> _getAttackDown;

        private Action<Vector2> _setMoveToMotor;
        private Action _jumpToMotor;
        private Action _attackCall;

        private void Awake()
        {
            if (autoFindOnAwake)
            {
                AutoFindRefs();
            }

            BindAll();

            if (logBindings)
            {
                PrintBindingResult();
            }
        }

        private void Update()
        {
            if (inputSource == null) return;

            // 1) Move 전달
            if (_getMove != null && _setMoveToMotor != null)
            {
                var move = _getMove.Invoke();
                _setMoveToMotor.Invoke(move);
            }

            // 2) Jump Down/Up 처리
            if (_getJumpDown != null && _jumpToMotor != null && _getJumpDown.Invoke())
            {
                _jumpToMotor.Invoke();
            }

            // JumpUp을 motor에 따로 전달해야 하는 프로젝트도 있을 수 있어서 훅만 만들어둠
            // (필요하면 아래처럼 JumpUp 메서드도 바인딩 후보에 추가 가능)
            // if (_getJumpUp != null && _getJumpUp.Invoke()) { ... }

            // 3) Attack
            if (_getAttackDown != null && _attackCall != null && _getAttackDown.Invoke())
            {
                _attackCall.Invoke();
            }
        }

        private void AutoFindRefs()
        {
            // inputSource는 보통 플레이어에 붙어있지 않을 수도 있어서 자동 탐색은 "같은 오브젝트" 우선
            if (inputSource == null)
            {
                // PlayerInputUnity가 플레이어에 붙어있는 경우
                inputSource = GetComponentInChildren<MonoBehaviour>(true);
                // 위가 너무 광범위할 수 있어, 아래에서 정확히 다시 바인딩 실패 시 로그로 알려줌
            }

            if (playerMotor == null)
            {
                // 같은 오브젝트 혹은 자식에서 Motor 찾기
                playerMotor = FindByTypeNameOnSelfOrChildren("PlayerMotor2D");
            }

            if (overworldAttack == null)
            {
                overworldAttack = FindByTypeNameOnSelfOrChildren("OverworldAttack2D");
            }
        }

        private MonoBehaviour FindByTypeNameOnSelfOrChildren(string typeName)
        {
            var all = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var m in all)
            {
                if (m == null) continue;
                if (m.GetType().Name == typeName) return m;
            }
            return null;
        }

        private void BindAll()
        {
            // Input 바인딩
            _getMove = BindVector2Getter(inputSource, new[] { "Move", "MoveInput", "Movement", "move", "moveInput", "movement" });
            _getJumpDown = BindBoolGetter(inputSource, new[] { "JumpDown", "JumpPressed", "Jump", "jumpDown", "jumpPressed" });
            _getJumpUp = BindBoolGetter(inputSource, new[] { "JumpUp", "JumpReleased", "jumpUp", "jumpReleased" });
            _getAttackDown = BindBoolGetter(inputSource, new[] { "AttackDown", "AttackPressed", "Attack", "attackDown", "attackPressed" });

            // Motor 바인딩 (Vector2 또는 float을 받는 케이스 둘 다 대응)
            _setMoveToMotor = BindMoveSetter(playerMotor);

            // Jump 바인딩 (버튼 다운 시 1회 호출)
            _jumpToMotor = BindVoidMethod(playerMotor, new[]
            {
                "Jump", "DoJump", "PressJump", "OnJump", "TryJump"
            });

            // Attack 바인딩
            _attackCall = BindVoidMethod(overworldAttack, new[]
            {
                "Attack", "TryAttack", "DoAttack", "OnAttack"
            });
        }

        private void PrintBindingResult()
        {
            Debug.Log($"[OverworldPlayerGlue] inputSource={(inputSource ? inputSource.GetType().Name : "NULL")}", this);
            Debug.Log($"[OverworldPlayerGlue] playerMotor={(playerMotor ? playerMotor.GetType().Name : "NULL")}", this);
            Debug.Log($"[OverworldPlayerGlue] overworldAttack={(overworldAttack ? overworldAttack.GetType().Name : "NULL")}", this);

            Debug.Log($"[OverworldPlayerGlue] GetMove={(_getMove != null)}  GetJumpDown={(_getJumpDown != null)}  GetAttackDown={(_getAttackDown != null)}", this);
            Debug.Log($"[OverworldPlayerGlue] SetMoveToMotor={(_setMoveToMotor != null)}  JumpCall={(_jumpToMotor != null)}  AttackCall={(_attackCall != null)}", this);

            if (_setMoveToMotor == null)
                Debug.LogWarning("[OverworldPlayerGlue] Motor 이동 메서드 바인딩 실패. PlayerMotor2D의 이동 메서드 이름을 확인해야 함.", this);
            if (_attackCall == null)
                Debug.LogWarning("[OverworldPlayerGlue] Attack 메서드 바인딩 실패. OverworldAttack2D의 공격 메서드 이름을 확인해야 함.", this);
        }

        // ----------------------------
        // Binding helpers
        // ----------------------------

        private static Func<Vector2> BindVector2Getter(MonoBehaviour src, string[] names)
        {
            if (src == null) return null;
            var t = src.GetType();

            // property
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(Vector2))
                    return () => (Vector2)p.GetValue(src);
            }

            // field
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(Vector2))
                    return () => (Vector2)f.GetValue(src);
            }

            // float만 있는 경우(좌우축)도 대응
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(float))
                    return () => new Vector2((float)p.GetValue(src), 0f);
            }
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(float))
                    return () => new Vector2((float)f.GetValue(src), 0f);
            }

            return null;
        }

        private static Func<bool> BindBoolGetter(MonoBehaviour src, string[] names)
        {
            if (src == null) return null;
            var t = src.GetType();

            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(bool))
                    return () => (bool)p.GetValue(src);
            }

            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                    return () => (bool)f.GetValue(src);
            }

            return null;
        }

        private static Action<Vector2> BindMoveSetter(MonoBehaviour motor)
        {
            if (motor == null) return null;
            var t = motor.GetType();

            // Vector2 받는 메서드 후보
            var vecCandidates = new[]
            {
                "SetMove", "SetMoveInput", "Move", "OnMove", "ApplyMove"
            };

            foreach (var name in vecCandidates)
            {
                var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector2) }, null);
                if (m != null)
                    return (v) => m.Invoke(motor, new object[] { v });
            }

            // float 받는 메서드 후보 (좌우 축만 받는 형태)
            var floatCandidates = new[]
            {
                "SetMoveX", "SetHorizontal", "SetAxis", "SetMove", "Move"
            };

            foreach (var name in floatCandidates)
            {
                var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
                if (m != null)
                    return (v) => m.Invoke(motor, new object[] { v.x });
            }

            // 프로퍼티로 받는 형태도 흔해서 지원
            var propCandidates = new[]
            {
                "MoveInput", "Move", "Movement"
            };

            foreach (var name in propCandidates)
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(Vector2))
                    return (v) => p.SetValue(motor, v);
                if (p != null && p.CanWrite && p.PropertyType == typeof(float))
                    return (v) => p.SetValue(motor, v.x);
            }

            return null;
        }

        private static Action BindVoidMethod(MonoBehaviour target, string[] names)
        {
            if (target == null) return null;
            var t = target.GetType();

            foreach (var n in names)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null)
                    return () => m.Invoke(target, null);
            }

            return null;
        }
    }
}