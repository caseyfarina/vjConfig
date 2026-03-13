using UnityEngine;
using System.Collections.Generic;

namespace VJSystem
{
    public class DeckLightRig : MonoBehaviour
    {
        public Vector3 stageOrigin;

        [Range(0, 50)] public int activeLightCount = 0;
        [Range(0f, 360f)] public float hue = 0f;
        [Range(0f, 100f)] public float hueSpread = 50f;

        [Header("Light Properties")]
        public float lightRadius = 5f;
        public float lightRange = 15f;
        public float lightIntensity = 0f;
        [Range(0f, 1f)] public float lightSaturation = 0f;  // 0 = white, 1 = full hue colour

        [Header("Flash Lights (MF64 Row 3, Cols 1-4 — hold to flash)")]
        public float flashIntensity = 80f;
        public float flashRange = 25f;

        const int MAX_LIGHTS = 50;
        const int FLASH_COUNT = 4;
        readonly List<Light> _lights = new List<Light>();
        readonly List<Light> _flashLights = new List<Light>();
        readonly Vector3[] _positions = new Vector3[MAX_LIGHTS];
        Transform _container;

        void Start()
        {
            _container = new GameObject("PointLights").transform;
            _container.SetParent(transform);
            _container.position = stageOrigin;

            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                var go = new GameObject($"PtLight_{i}");
                go.transform.SetParent(_container);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = lightRange;
                light.intensity = lightIntensity;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;
                go.SetActive(false);
                _lights.Add(light);
            }

            for (int i = 0; i < FLASH_COUNT; i++)
            {
                var go = new GameObject($"FlashLight_{i}");
                go.transform.SetParent(_container);
                var fl = go.AddComponent<Light>();
                fl.type            = LightType.Point;
                fl.color           = Color.white;
                fl.range           = flashRange;
                fl.intensity       = flashIntensity;
                fl.shadows         = LightShadows.Soft;
                fl.bounceIntensity = 0f;
                go.SetActive(false);
                _flashLights.Add(fl);
            }

            RandomizePositions(MAX_LIGHTS);
            UpdateLights();
        }

        void OnValidate() => UpdateLights();

        void RandomizePositions(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float cosTheta = Random.Range(0.35f, 1.0f); // keep lights elevated, avoid near-equator
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi = Random.value * Mathf.PI * 2f;
                _positions[i] = stageOrigin + Vector3.up * 5f + new Vector3(
                    sinTheta * Mathf.Cos(phi),
                    cosTheta,
                    sinTheta * Mathf.Sin(phi)
                ) * lightRadius;
            }
        }

        public void RandomizeAndUpdate()
        {
            int count = Mathf.Clamp(activeLightCount, 0, MAX_LIGHTS);
            RandomizePositions(count);
            UpdateLights();
        }

        /// <summary>Randomize positions only — does not touch colour or intensity.</summary>
        public void RandomizePositionsOnly()
        {
            int count = Mathf.Clamp(activeLightCount, 0, MAX_LIGHTS);
            RandomizePositions(count);
            for (int i = 0; i < count; i++)
                _lights[i].transform.position = _positions[i];
        }

        /// <summary>Activate/deactivate a flash light. On activation, randomizes hemisphere position.</summary>
        public void SetFlash(int index, bool on)
        {
            if (index < 0 || index >= _flashLights.Count) return;
            var fl = _flashLights[index];
            if (on)
            {
                float cosTheta = Random.Range(0.35f, 1.0f);
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi      = Random.value * Mathf.PI * 2f;
                fl.transform.position = stageOrigin + Vector3.up * 5f + new Vector3(
                    sinTheta * Mathf.Cos(phi),
                    cosTheta,
                    sinTheta * Mathf.Sin(phi)) * lightRadius;
                fl.intensity = flashIntensity;
                fl.range     = flashRange;
            }
            fl.gameObject.SetActive(on);
        }

        public void SetAllWhite()
        {
            foreach (var light in _lights)
                if (light != null && light.gameObject.activeSelf)
                    light.color = Color.white;
        }

        public void UpdateLights()
        {
            if (_lights.Count == 0) return;

            int count = Mathf.Clamp(activeLightCount, 0, MAX_LIGHTS);

            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                bool active = i < count;
                _lights[i].gameObject.SetActive(active);

                if (!active) continue;

                _lights[i].transform.position = _positions[i];

                float hueOffset = hueSpread / 100f * ((float)i / Mathf.Max(1, count) - 0.5f);
                float h = (hue / 360f + hueOffset) % 1f;
                if (h < 0) h += 1f;
                _lights[i].color = Color.HSVToRGB(h, lightSaturation, 1f);
                _lights[i].intensity = lightIntensity;
                _lights[i].range = lightRange;
                _lights[i].shadows = LightShadows.Soft;
            }
        }
    }
}
