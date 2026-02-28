using System;
using System.Reflection;
using UnityEngine;

namespace Game.Combat.Adapters
{
    /// <summary>
    /// GameObject에 붙어있는 컴포넌트들에서 hp/HP/maxHp/MaxHP 등을 찾아 읽고/쓸 수 있게 해주는 유틸.
    /// 기존 코드 수정 없이 필드 HP를 전투에 연결하기 위한 최소 결합 도구.
    /// </summary>
    public sealed class HpAccessor
    {
        private readonly Component _target;
        private readonly Func<int> _getHp;
        private readonly Action<int> _setHp;
        private readonly Func<int> _getMaxHp;

        public bool IsValid => _target != null && _getHp != null && _setHp != null;
        public Component SourceComponent => _target;

        private HpAccessor(Component target, Func<int> getHp, Action<int> setHp, Func<int> getMaxHp)
        {
            _target = target;
            _getHp = getHp;
            _setHp = setHp;
            _getMaxHp = getMaxHp;
        }

        public int GetHp() => _getHp != null ? _getHp() : 0;
        public void SetHp(int v) { _setHp?.Invoke(v); }
        public int GetMaxHpOrCurrent()
        {
            if (_getMaxHp == null) return GetHp();
            int m = _getMaxHp();
            return m <= 0 ? GetHp() : m;
        }

        public static HpAccessor TryCreate(GameObject go)
        {
            if (go == null) return null;

            // 모든 컴포넌트에서 hp/HP 찾기
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;

                var acc = TryCreateFromComponent(c);
                if (acc != null && acc.IsValid)
                    return acc;
            }

            return null;
        }

        private static HpAccessor TryCreateFromComponent(Component c)
        {
            var type = c.GetType();

            // 우선순위: HP/MaxHP 프로퍼티 → hp/maxHp 필드
            if (TryBindIntProperty(type, "HP", out var hpGetP, out var hpSetP) ||
                TryBindIntProperty(type, "hp", out hpGetP, out hpSetP))
            {
                Func<int> maxGet = null;
                if (TryBindIntProperty(type, "MaxHP", out var maxGetP, out _) ||
                    TryBindIntProperty(type, "maxHP", out maxGetP, out _) ||
                    TryBindIntProperty(type, "MaxHp", out maxGetP, out _) ||
                    TryBindIntProperty(type, "maxHp", out maxGetP, out _))
                {
                    maxGet = () => (int)maxGetP.Invoke(c, null);
                }

                Func<int> getHp = () => (int)hpGetP.Invoke(c, null);
                Action<int> setHp = v => hpSetP.Invoke(c, new object[] { v });

                return new HpAccessor(c, getHp, setHp, maxGet);
            }

            if (TryBindIntField(type, "HP", out var hpField) || TryBindIntField(type, "hp", out hpField))
            {
                Func<int> maxGet = null;
                if (TryBindIntField(type, "MaxHP", out var maxField) ||
                    TryBindIntField(type, "maxHP", out maxField) ||
                    TryBindIntField(type, "MaxHp", out maxField) ||
                    TryBindIntField(type, "maxHp", out maxField))
                {
                    maxGet = () => (int)maxField.GetValue(c);
                }

                Func<int> getHp = () => (int)hpField.GetValue(c);
                Action<int> setHp = v => hpField.SetValue(c, v);

                return new HpAccessor(c, getHp, setHp, maxGet);
            }

            return null;
        }

        private static bool TryBindIntField(Type t, string name, out FieldInfo field)
        {
            field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(int);
        }

        private static bool TryBindIntProperty(Type t, string name, out MethodInfo getter, out MethodInfo setter)
        {
            getter = null;
            setter = null;
            var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || prop.PropertyType != typeof(int)) return false;

            getter = prop.GetGetMethod(true);
            setter = prop.GetSetMethod(true);
            return getter != null && setter != null;
        }
    }
}
