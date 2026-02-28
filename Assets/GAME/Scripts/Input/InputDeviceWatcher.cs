using UnityEngine;
using UnityEngine.InputSystem;

public class InputDeviceWatcher : MonoBehaviour
{
    public enum DeviceType { KeyboardMouse, Gamepad }
    public static DeviceType Current { get; private set; } = DeviceType.KeyboardMouse;

    void OnEnable()
    {
        InputSystem.onActionChange += OnActionChange;
    }

    void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
    }

    void OnActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;

        if (obj is InputAction action && action.activeControl != null)
        {
            var device = action.activeControl.device;
            Current = device is Gamepad ? DeviceType.Gamepad : DeviceType.KeyboardMouse;
        }
    }
}
