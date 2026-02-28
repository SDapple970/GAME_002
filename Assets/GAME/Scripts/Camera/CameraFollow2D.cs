// Scripts/Camera/CameraFollow2D.cs
using UnityEngine;

namespace Game.CameraSys
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, -10f);
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 _vel;

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
        }
    }
}
