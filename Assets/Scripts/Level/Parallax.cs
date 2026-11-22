using Cinemachine;
using UnityEngine;

namespace Level
{
    // One background layer, moving with the camera by a share of the camera's own movement: 1 = moves
    // with it (stays put on screen, like the sky), 0 = stays in the world (like the ground). Far things
    // get a big share and seem to move slowly; near ones a small share and seem to move faster. That
    // difference is the depth ("parallax").
    // A repeating layer is a strip whose ends join (drawn Tiled, three strips wide): each time the
    // camera gets half a strip away from its middle, it jumps one strip width, which looks the same,
    // so the strip never ends. Clouds can also drift by themselves (driftSpeed).
    // Runs after Cinemachine has moved the camera (CameraUpdatedEvent), or the layer would lag a frame
    // behind and shake; LateUpdate too, for a camera without Cinemachine.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Parallax : MonoBehaviour
    {
        [SerializeField] private Transform followedCamera;   // empty: the main camera
        [SerializeField, Range(0f, 1f)] private float followX = 0.5f;
        [SerializeField, Range(0f, 1f)] private float followY = 0.5f;
        [SerializeField] private bool repeat = true;
        [SerializeField] private float driftSpeed;           // units a second, + = to the right

        private SpriteRenderer _renderer;
        private Vector3 _start;
        private Vector3 _cameraStart;
        private float _drift;
        private bool _started;

        public float FollowX => followX;
        public float FollowY => followY;
        public bool Repeats => repeat;

        // One strip's width in world units (0 if it doesn't repeat).
        public float StripWidth
        {
            get
            {
                var r = _renderer != null ? _renderer : GetComponent<SpriteRenderer>();
                if (!repeat || r.sprite == null)
                {
                    return 0f;
                }

                return r.sprite.rect.width / r.sprite.pixelsPerUnit * Mathf.Abs(transform.lossyScale.x);
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (followedCamera == null && Camera.main != null)
            {
                followedCamera = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        // Where the layer and the camera are when play starts: the layer is placed (in the Editor) for
        // the camera as it is then.
        private void Start()
        {
            if (followedCamera == null)
            {
                Debug.LogError("Parallax: no camera to follow", this);
                enabled = false;
                return;
            }

            _start = transform.position;
            _cameraStart = followedCamera.position;
            _started = true;
            Follow();
        }

        private void LateUpdate()
        {
            _drift += driftSpeed * Time.deltaTime;
            Follow();
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            Follow();
        }

        private void Follow()
        {
            if (!_started || followedCamera == null)
            {
                return;
            }

            var camera = followedCamera.position;
            var x = GetLayerX(camera.x, _cameraStart.x, _start.x + _drift, followX, StripWidth);
            var y = GetLayerY(camera.y, _cameraStart.y, _start.y, followY);
            transform.position = new Vector3(x, y, _start.z);
        }

        // Rules, without components: Edit Mode tests call them directly.

        // Across: moved by `follow` of the camera's movement since the start, then, for a strip
        // stripWidth wide, by whole strips to the copy nearest the camera (within half a strip).
        public static float GetLayerX(float cameraX, float cameraStartX, float startX, float follow, float stripWidth)
        {
            var x = startX + (cameraX - cameraStartX) * follow;
            if (stripWidth <= 0f)
            {
                return x;
            }

            return x + Mathf.Round((cameraX - x) / stripWidth) * stripWidth;
        }

        // Up and down: the same share, without repeating.
        public static float GetLayerY(float cameraY, float cameraStartY, float startY, float follow)
        {
            return startY + (cameraY - cameraStartY) * follow;
        }
    }
}
