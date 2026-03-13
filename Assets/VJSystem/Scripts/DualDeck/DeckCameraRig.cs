using UnityEngine;

namespace VJSystem
{
    public enum CameraBehavior { Still, Orbit, Push }

    public class DeckCameraRig : MonoBehaviour
    {
        public Camera cam1;
        public Camera cam2;
        public Vector3 stageOrigin;

        [Header("Shared Settings")]
        public float perimeterRadius = 10f;
        public float cameraHeight = 2f;
        [Range(1f, 45f)] public float orbitSpeed = 10f;
        public float orbitRadius = 8f;
        public float orbitHeight = 3f;
        public float pushStartDistance = 12f;
        public float pushEndDistance = 3f;
        public float pushDuration = 4f;
        public float pushPause = 0.5f;

        Vector3 LookTarget => stageOrigin + Vector3.up;

        CameraBehavior[] _behaviors = new CameraBehavior[2];
        float[] _orbitAngles = new float[2];
        float[] _pushProgress = new float[2];
        bool[] _pushForward = { true, true };
        float[] _pushPauseTimer = new float[2];
        float[] _pushDirectionAngle = new float[2];
        RenderTexture[] _rts = new RenderTexture[2];

        public CameraBehavior GetBehavior(int camIndex) => _behaviors[Mathf.Clamp(camIndex, 0, 1)];
        public RenderTexture GetRT(int camIndex) => _rts[Mathf.Clamp(camIndex, 0, 1)];

        Camera GetCam(int index) => index == 0 ? cam1 : cam2;

        void Start()
        {
            for (int i = 0; i < 2; i++)
            {
                _rts[i] = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _rts[i].name = $"RT_{(stageOrigin.x < 1000 ? "A" : "B")}_Cam{i + 1}";
                _rts[i].Create();
                var cam = GetCam(i);
                if (cam != null) cam.targetTexture = _rts[i];
            }

            SetBehavior(0, CameraBehavior.Still);
            SetBehavior(1, CameraBehavior.Still);
        }

        public void SetBehavior(int camIndex, CameraBehavior behavior)
        {
            int idx = Mathf.Clamp(camIndex, 0, 1);
            _behaviors[idx] = behavior;

            switch (behavior)
            {
                case CameraBehavior.Still:
                    SnapToRandomPerimeter(idx);
                    break;
                case CameraBehavior.Orbit:
                    _orbitAngles[idx] = Random.Range(0f, 360f);
                    break;
                case CameraBehavior.Push:
                    _pushDirectionAngle[idx] = Random.Range(0f, 360f);
                    _pushProgress[idx] = 0f;
                    _pushForward[idx] = true;
                    _pushPauseTimer[idx] = 0f;
                    PositionPush(idx);
                    break;
            }
        }

        void SnapToRandomPerimeter(int idx)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            var cam = GetCam(idx);
            if (cam == null) return;
            cam.transform.position = stageOrigin + new Vector3(
                Mathf.Cos(angle) * perimeterRadius,
                cameraHeight,
                Mathf.Sin(angle) * perimeterRadius
            );
            cam.transform.LookAt(LookTarget);
        }

        void Update()
        {
            for (int i = 0; i < 2; i++)
            {
                if (GetCam(i) == null) continue;
                switch (_behaviors[i])
                {
                    case CameraBehavior.Orbit: UpdateOrbit(i); break;
                    case CameraBehavior.Push: UpdatePush(i); break;
                }
            }
        }

        void UpdateOrbit(int idx)
        {
            _orbitAngles[idx] += orbitSpeed * Time.deltaTime;
            float rad = _orbitAngles[idx] * Mathf.Deg2Rad;
            var cam = GetCam(idx);
            cam.transform.position = stageOrigin + new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                orbitHeight,
                Mathf.Sin(rad) * orbitRadius
            );
            cam.transform.LookAt(LookTarget);
        }

        void UpdatePush(int idx)
        {
            if (_pushPauseTimer[idx] > 0)
            {
                _pushPauseTimer[idx] -= Time.deltaTime;
                return;
            }

            float speed = 1f / pushDuration;
            _pushProgress[idx] += (_pushForward[idx] ? 1 : -1) * speed * Time.deltaTime;

            if (_pushProgress[idx] >= 1f)
            {
                _pushProgress[idx] = 1f;
                _pushForward[idx] = false;
                _pushPauseTimer[idx] = pushPause;
            }
            else if (_pushProgress[idx] <= 0f)
            {
                _pushProgress[idx] = 0f;
                _pushForward[idx] = true;
                _pushPauseTimer[idx] = pushPause;
            }

            PositionPush(idx);
        }

        void PositionPush(int idx)
        {
            float t = Mathf.SmoothStep(0, 1, _pushProgress[idx]);
            float dist = Mathf.Lerp(pushStartDistance, pushEndDistance, t);
            float rad = _pushDirectionAngle[idx] * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));
            var cam = GetCam(idx);
            cam.transform.position = stageOrigin + dir * dist + Vector3.up * cameraHeight;
            cam.transform.LookAt(LookTarget);
        }

        void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_rts[i] != null)
                {
                    var cam = GetCam(i);
                    if (cam != null) cam.targetTexture = null;
                    _rts[i].Release();
                    Object.Destroy(_rts[i]);
                }
            }
        }
    }
}
