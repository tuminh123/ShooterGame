using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    [RequireComponent(typeof(Camera))]
    public class CameraControl : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private float cameraSize = 5f;
        [SerializeField] private Vector3 offset = new(0, 1, -10);

        private Camera _camera;

        public void Init(Transform followTarget)
        {
            target = followTarget;
            _camera = GetComponent<Camera>();
            _camera.orthographicSize = cameraSize;
            transform.position = target.position + offset;
        }

        private void LateUpdate()
        {
            if (target == null || _camera == null) return;

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            _camera.orthographicSize = cameraSize;
        }
    }
}
