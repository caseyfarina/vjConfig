using UnityEngine;

namespace VJSystem
{
    public class StageController : MonoBehaviour
    {
        public DeckIdentity deck;
        public Vector3 stageOrigin;
        public DeckCameraRig cameraRig;
        public DeckLightRig lightRig;

        [Header("Content")]
        public Transform contentRoot;

        [Header("Initial Content")]
        public bool autoSpawn = false;

        static Material CreateURPMaterial(Color color, bool emissive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
            {
                Debug.LogWarning("[StageController] URP Lit shader not found, falling back to default");
                return new Material(Shader.Find("Standard"));
            }
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.5f);
            }
            return mat;
        }

        void Start()
        {
            if (autoSpawn)
                SpawnCalibrationGrid();
        }

        public void SpawnCalibrationGrid(int cols = 5, int rows = 3, float spacing = 1.5f)
        {
            ClearContent();
            EnsureContentRoot();

            float startX = -(cols - 1) * spacing * 0.5f;
            float startZ = -(rows - 1) * spacing * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 pos = stageOrigin + new Vector3(
                        startX + c * spacing,
                        0.5f,
                        startZ + r * spacing
                    );
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = $"Grid_{r}_{c}";
                    obj.transform.position = pos;
                    obj.transform.localScale = Vector3.one * 0.6f;
                    obj.transform.SetParent(contentRoot);

                    bool even = (r + c) % 2 == 0;
                    Color col = even ? Color.white : Color.HSVToRGB((float)c / cols, 0.8f, 1f);
                    obj.GetComponent<Renderer>().sharedMaterial = CreateURPMaterial(col, true);
                }
            }
        }

        public void SpawnTestContent(PrimitiveType type, int count, float radius = 3f)
        {
            ClearContent();
            EnsureContentRoot();

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 pos = stageOrigin + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.5f,
                    Mathf.Sin(angle) * radius
                );
                var obj = GameObject.CreatePrimitive(type);
                obj.name = $"{type}_{i}";
                obj.transform.position = pos;
                obj.transform.localScale = Vector3.one * 0.8f;
                obj.transform.SetParent(contentRoot);

                Color col = Color.HSVToRGB((float)i / count, 0.7f, 0.9f);
                obj.GetComponent<Renderer>().sharedMaterial = CreateURPMaterial(col);

                var spin = obj.AddComponent<SpinCube>();
                spin.rotationSpeed = new Vector3(
                    Random.Range(-30f, 30f),
                    Random.Range(20f, 60f),
                    Random.Range(-20f, 20f)
                );
            }
        }

        public void ClearContent()
        {
            if (contentRoot == null) return;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(contentRoot.GetChild(i).gameObject);
        }

        void EnsureContentRoot()
        {
            if (contentRoot != null) return;
            var go = new GameObject("Content");
            go.transform.SetParent(transform);
            go.transform.position = stageOrigin;
            contentRoot = go.transform;
        }

    }
}
