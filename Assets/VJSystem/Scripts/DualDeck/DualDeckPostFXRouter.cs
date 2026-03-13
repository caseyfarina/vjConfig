using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using KinoGlitch;
using MidiFighter64;

namespace VJSystem
{
    /// <summary>
    /// Routes Akai MIDI Mix channel faders to post-processing effects.
    ///
    ///   Ch 1  AnalogGlitch  ScanLineJitter   (live deck cameras only)
    ///   Ch 2  AnalogGlitch  VerticalJump     (live deck cameras only)
    ///   Ch 3  AnalogGlitch  ColorDrift       (live deck cameras only)
    ///   Ch 4  DigitalGlitch Intensity        (live deck cameras only)
    ///   Ch 5  ScreenSpaceLensFlare intensity (live deck local volume)
    ///   Ch 6  ScreenSpaceLensFlare firstFlareIntensity + secondaryFlareIntensity + warpedFlareIntensity (live deck local volume)
    ///   Ch 7  ScreenSpaceLensFlare streaksIntensity    (live deck local volume)
    ///   Ch 8  Bloom intensity                (live deck local volume)
    ///
    /// Volume effects always target the live deck's local volume. On Take(), the faders
    /// continue driving the new live deck. Standby retains its last state.
    ///
    /// Setup requirements:
    ///   - Add AnalogGlitchRendererFeature and DigitalGlitchRendererFeature to VJ_Renderer.asset
    ///   - Create a local Volume + BoxCollider around each stage and assign volumeA / volumeB
    ///   - Assign rigA, rigB in the Inspector
    ///   - MidiMixRouter must be active in the scene
    /// </summary>
    public class DualDeckPostFXRouter : MonoBehaviour
    {
        [Header("Deck References")]
        public DeckCameraRig rigA;
        public DeckCameraRig rigB;

        [Header("Per-Deck Local Volumes")]
        public Volume volumeA;
        public Volume volumeB;

        [Header("Glitch Fader Scales (Ch 1-4)")]
        [Range(0f, 1f)] public float ch1Scale = 1f;  // ScanLineJitter max
        [Range(0f, 1f)] public float ch2Scale = 1f;  // VerticalJump max
        [Range(0f, 1f)] public float ch3Scale = 1f;  // ColorDrift max
        [Range(0f, 1f)] public float ch4Scale = 1f;  // DigitalGlitch max

        [Header("Volume Effect Scales")]
        public float lensFlareMax       = 5f;
        public float bloomMax           = 5f;
        public float flareMultiplierMax = 3f;   // Ch 6: firstFlare + secondaryFlare + warpedFlare max
        public float streaksMax         = 3f;   // Ch 7: streaksIntensity max

        [Header("Master Fade (MIDI Mix Master Fader)")]
        public Volume globalVolume;

        [Header("Fog (Knob Ch 8: Row1=Hue, Row2=Brightness, Row3=Density)")]
        [Range(0f, 1f)] public float fogSaturation = 0.8f;

        [Header("Capacity VFX (Knob Ch 1: Row1=SpawnRate, Row2=Hue, Row3=Brightness)")]
        public VisualEffect capacityVfxA;
        public VisualEffect capacityVfxB;
        public float spawnRateMax = 5000f;

        [Header("Petals VFX (Knob Ch 2: Row1=SpawnRate 0-500)")]
        public VisualEffect petalsVfxA;
        public VisualEffect petalsVfxB;
        public float petalsSpawnRateMax = 500f;

        [Header("Directional Lights (Knob Ch 3: Row1=Brightness 0-5, Row2=AngleX 0-180, Row3=AngleY 0-180)")]
        public Light directionalLightA;
        public Light directionalLightB;

        [Header("Light Rigs (MF64 Row1=LightCount 1-8, Knob Ch4: Row1=Intensity 0-20, Row2=Hue, Row3=HueSpread)")]
        public DeckLightRig lightRigA;
        public DeckLightRig lightRigB;

        // ------------------------------------------------------------------ //
        // Per-deck cached volume components

        struct DeckFX
        {
            public ScreenSpaceLensFlare lensFlare;
            public Bloom                bloom;
        }

        DeckFX _fxA, _fxB;

        // Glitch controllers — two cameras per rig
        AnalogGlitchController[]  _analogA  = new AnalogGlitchController[2];
        AnalogGlitchController[]  _analogB  = new AnalogGlitchController[2];
        DigitalGlitchController[] _digitalA = new DigitalGlitchController[2];
        DigitalGlitchController[] _digitalB = new DigitalGlitchController[2];

        // Last fader values so glitch and volume can reapply after Take()
        float _ch1, _ch2, _ch3, _ch4, _ch5, _ch6, _ch7, _ch8;

        // Fog knob state (ch8, rows 1-3)
        float _fogHue = 0f, _fogBrightness = 0.4f, _fogDensity = 0f;

        // Capacity VFX knob state (ch1, rows 1-3)
        float _vfxSpawnRate = 0f, _vfxHue = 0f, _vfxBrightness = 1f;

        // Master fade
        ColorAdjustments _masterColorAdj;

        // Primitive spawning — rows 3-6 each have their own list and coroutine set
        static readonly PrimitiveType[] _primitiveTypes =
        {
            PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Cylinder,
            PrimitiveType.Capsule, PrimitiveType.Quad, PrimitiveType.Plane
        };
        readonly List<GameObject>[] _spawnedByRow   = { new(), new(), new(), new() };
        readonly Dictionary<int, Coroutine> _spawnCoroutines = new();

        // Per-deck stored values (so standby deck retains its last state)
        float _ch5A, _ch6A, _ch7A, _ch8A;
        float _ch5B, _ch6B, _ch7B, _ch8B;

        // ------------------------------------------------------------------ //
        // Public API for GUI

        /// <summary>Snapshot of PostFX values for a specific deck.</summary>
        public struct DeckFXSnapshot
        {
            public float scanLineJitter, verticalJump, colorDrift, digitalIntensity;
            public float lensFlare, flareMultipliers, streaks, bloom;
        }

        /// <summary>Returns current PostFX values for the given deck.</summary>
        public DeckFXSnapshot GetSnapshot(DeckIdentity deck)
        {
            bool isA = deck == DeckIdentity.A;
            var analog  = isA ? _analogA  : _analogB;
            var digital = isA ? _digitalA : _digitalB;
            return new DeckFXSnapshot
            {
                scanLineJitter  = analog[0]  != null ? analog[0].ScanLineJitter  : 0f,
                verticalJump    = analog[0]  != null ? analog[0].VerticalJump    : 0f,
                colorDrift      = analog[0]  != null ? analog[0].ColorDrift      : 0f,
                digitalIntensity= digital[0] != null ? digital[0].Intensity      : 0f,
                lensFlare        = isA ? _ch5A : _ch5B,
                flareMultipliers = isA ? _ch6A : _ch6B,
                streaks          = isA ? _ch7A : _ch7B,
                bloom            = isA ? _ch8A : _ch8B,
            };
        }

        /// <summary>
        /// Sets a PostFX parameter for a specific deck directly (GUI control).
        /// Channels 1-4 = glitch (live deck only — standby glitch is always zero).
        /// Channels 5-8 = volume effects on that deck's local volume.
        /// </summary>
        public void SetDeckFX(DeckIdentity deck, int ch, float value)
        {
            bool isA = deck == DeckIdentity.A;
            ref DeckFX fx = ref (isA ? ref _fxA : ref _fxB);

            switch (ch)
            {
                case 1: if (isA == AIsLive) { _ch1 = value; ApplyGlitch(); } break;
                case 2: if (isA == AIsLive) { _ch2 = value; ApplyGlitch(); } break;
                case 3: if (isA == AIsLive) { _ch3 = value; ApplyGlitch(); } break;
                case 4: if (isA == AIsLive) { _ch4 = value; ApplyGlitch(); } break;
                case 5:
                    if (isA) _ch5A = value; else _ch5B = value;
                    if (isA == AIsLive) _ch5 = value;
                    RefreshLensFlareActive(isA);
                    SetLensFlare(ref fx, value); break;
                case 6:
                    if (isA) _ch6A = value; else _ch6B = value;
                    if (isA == AIsLive) _ch6 = value;
                    RefreshLensFlareActive(isA);
                    SetFlareMultipliers(ref fx, value); break;
                case 7:
                    if (isA) _ch7A = value; else _ch7B = value;
                    if (isA == AIsLive) _ch7 = value;
                    RefreshLensFlareActive(isA);
                    SetStreaks(ref fx, value); break;
                case 8:
                    if (isA) _ch8A = value; else _ch8B = value;
                    if (isA == AIsLive) _ch8 = value;
                    SetBloom(ref fx, value); break;
            }
        }

        // ------------------------------------------------------------------ //

        bool AIsLive => DualDeckManager.Instance == null
                     || DualDeckManager.Instance.liveDeck == DeckIdentity.A;

        ref DeckFX LiveFX   => ref (AIsLive ? ref _fxA : ref _fxB);
        ref DeckFX StandbyFX => ref (AIsLive ? ref _fxB : ref _fxA);

        // ------------------------------------------------------------------ //

        void Start()
        {
            InitGlitchOnRig(rigA, _analogA, _digitalA);
            InitGlitchOnRig(rigB, _analogB, _digitalB);
            InitDeckFX(volumeA, ref _fxA);
            InitDeckFX(volumeB, ref _fxB);
            InitMasterFade();
        }

        void InitMasterFade()
        {
            if (globalVolume == null)
                globalVolume = FindFirstObjectByType<Volume>();
            if (globalVolume == null) { Debug.LogWarning("[DualDeckPostFXRouter] No global Volume found for master fade."); return; }

            var p = globalVolume.profile;
            if (!p.TryGet(out _masterColorAdj))
                _masterColorAdj = p.Add<ColorAdjustments>(overrides: true);

            _masterColorAdj.postExposure.overrideState = true;
            _masterColorAdj.postExposure.value         = 0f;
        }

        void OnEnable()
        {
            MidiMixRouter.OnChannelFader  += HandleFader;
            MidiMixRouter.OnRecArm        += HandleRecArm;
            MidiMixRouter.OnKnob          += HandleKnob;
            MidiMixRouter.OnMasterFader   += HandleMasterFader;
            MidiMixRouter.OnMute          += HandleMute;
            MidiGridRouter.OnRow1         += HandleMF64Row1;
            MidiGridRouter.OnGridButton   += HandleMF64GridButton;

            if (DualDeckManager.Instance != null)
                DualDeckManager.Instance.OnTakeCompleted += OnTakeCompleted;
        }

        void OnDisable()
        {
            MidiMixRouter.OnChannelFader  -= HandleFader;
            MidiMixRouter.OnRecArm        -= HandleRecArm;
            MidiMixRouter.OnKnob          -= HandleKnob;
            MidiMixRouter.OnMasterFader   -= HandleMasterFader;
            MidiMixRouter.OnMute          -= HandleMute;
            MidiGridRouter.OnRow1         -= HandleMF64Row1;
            MidiGridRouter.OnGridButton   -= HandleMF64GridButton;

            foreach (var co in _spawnCoroutines.Values)
                if (co != null) StopCoroutine(co);
            _spawnCoroutines.Clear();

            if (DualDeckManager.Instance != null)
                DualDeckManager.Instance.OnTakeCompleted -= OnTakeCompleted;
        }

        // ------------------------------------------------------------------ //

        void InitGlitchOnRig(DeckCameraRig rig,
                              AnalogGlitchController[]  analog,
                              DigitalGlitchController[] digital)
        {
            if (rig == null) return;
            Camera[] cams = { rig.cam1, rig.cam2 };
            for (int i = 0; i < 2; i++)
            {
                if (cams[i] == null) continue;
                analog[i]  = cams[i].GetComponent<AnalogGlitchController>();
                digital[i] = cams[i].GetComponent<DigitalGlitchController>();

                if (analog[i] == null)
                    Debug.LogWarning($"[DualDeckPostFXRouter] AnalogGlitchController missing on {cams[i].name}. Run AddGlitchControllers editor script.");
                if (digital[i] == null)
                    Debug.LogWarning($"[DualDeckPostFXRouter] DigitalGlitchController missing on {cams[i].name}. Run AddGlitchControllers editor script.");

                if (analog[i] != null)  { analog[i].ScanLineJitter = 0f; analog[i].VerticalJump = 0f; analog[i].ColorDrift = 0f; }
                if (digital[i] != null) { digital[i].Intensity = 0f; }
            }
        }

        void InitDeckFX(Volume vol, ref DeckFX fx)
        {
            if (vol == null || vol.profile == null)
            {
                Debug.LogWarning($"[DualDeckPostFXRouter] Volume not assigned or has no profile.");
                return;
            }

            var p = vol.profile;

            if (!p.TryGet(out fx.lensFlare)) fx.lensFlare = p.Add<ScreenSpaceLensFlare>(overrides: true);
            if (!p.TryGet(out fx.bloom))     fx.bloom     = p.Add<Bloom>(overrides: true);

            // Configure Bloom — low threshold so it catches scene lighting
            fx.bloom.threshold.overrideState = true;
            fx.bloom.threshold.value         = 0.5f;
            fx.bloom.scatter.overrideState   = true;
            fx.bloom.scatter.value           = 0.7f;
            fx.bloom.intensity.overrideState = true;
            fx.bloom.intensity.value         = 0f;

            // Configure LensFlare — all intensities start at zero
            fx.lensFlare.intensity.overrideState               = true;
            fx.lensFlare.intensity.value                       = 0f;
            fx.lensFlare.firstFlareIntensity.overrideState     = true;
            fx.lensFlare.firstFlareIntensity.value             = 0f;
            fx.lensFlare.secondaryFlareIntensity.overrideState = true;
            fx.lensFlare.secondaryFlareIntensity.value         = 0f;
            fx.lensFlare.warpedFlareIntensity.overrideState    = true;
            fx.lensFlare.warpedFlareIntensity.value            = 0f;
            fx.lensFlare.streaksIntensity.overrideState        = true;
            fx.lensFlare.streaksIntensity.value                = 0f;

            // All start disabled
            fx.lensFlare.active = false;
            fx.bloom.active     = false;
        }

        // ------------------------------------------------------------------ //

        void HandleFader(int channel, float value)
        {
            switch (channel)
            {
                case 1: _ch1 = value; ApplyGlitch(); break;
                case 2: _ch2 = value; ApplyGlitch(); break;
                case 3: _ch3 = value; ApplyGlitch(); break;
                case 4: _ch4 = value; ApplyGlitch(); break;
                case 5: _ch5 = value; StoreLive(ref _ch5A, ref _ch5B, value); RefreshLensFlareActive(AIsLive); SetLensFlare(ref LiveFX, value); break;
                case 6: _ch6 = value; StoreLive(ref _ch6A, ref _ch6B, value); RefreshLensFlareActive(AIsLive); SetFlareMultipliers(ref LiveFX, value); break;
                case 7: _ch7 = value; StoreLive(ref _ch7A, ref _ch7B, value); RefreshLensFlareActive(AIsLive); SetStreaks(ref LiveFX, value); break;
                case 8: _ch8 = value; StoreLive(ref _ch8A, ref _ch8B, value); SetBloom(ref LiveFX, value); break;
            }
        }

        void HandleRecArm(int channel, bool isNoteOn)
        {
            if (!isNoteOn || channel < 1 || channel > 4) return;

            switch (channel)
            {
                case 1: _ch1 = Random.Range(0f, ch1Scale); break;
                case 2: _ch2 = Random.Range(0f, ch2Scale); break;
                case 3: _ch3 = Random.Range(0f, ch3Scale); break;
                case 4: _ch4 = Random.Range(0f, ch4Scale); break;
            }
            ApplyGlitch();
        }

        void HandleMasterFader(float value)
        {
            if (_masterColorAdj == null) return;
            // fader=1 → 0 EV (full brightness), fader=0 → -10 EV (black)
            _masterColorAdj.postExposure.value = Mathf.Lerp(-10f, 0f, value);
        }

        void HandleMF64Row1(int col)
        {
            // Col 1-8 sets light count on both rigs and randomizes positions
            if (lightRigA != null) { lightRigA.activeLightCount = col; lightRigA.RandomizeAndUpdate(); }
            if (lightRigB != null) { lightRigB.activeLightCount = col; lightRigB.RandomizeAndUpdate(); }
        }

        void HandleKnob(int channel, int row, float value)
        {
            if (channel == 1)
            {
                switch (row)
                {
                    case 1: _vfxSpawnRate = value; break;
                    case 2: _vfxHue       = value; break;
                    case 3: _vfxBrightness = value; break;
                }
                ApplyCapacityVFX();
                return;
            }

            if (channel == 2 && row == 1)
            {
                float rate = value * petalsSpawnRateMax;
                if (petalsVfxA != null) petalsVfxA.SetFloat("spawnRate", rate);
                if (petalsVfxB != null) petalsVfxB.SetFloat("spawnRate", rate);
                return;
            }

            if (channel == 4)
            {
                switch (row)
                {
                    case 1:
                        if (lightRigA != null) { lightRigA.lightIntensity = value * 20f; lightRigA.UpdateLights(); }
                        if (lightRigB != null) { lightRigB.lightIntensity = value * 20f; lightRigB.UpdateLights(); }
                        break;
                    case 2:
                        if (lightRigA != null) { lightRigA.hue = value * 360f; lightRigA.UpdateLights(); }
                        if (lightRigB != null) { lightRigB.hue = value * 360f; lightRigB.UpdateLights(); }
                        break;
                    case 3:
                        if (lightRigA != null) { lightRigA.hueSpread = value * 100f; lightRigA.UpdateLights(); }
                        if (lightRigB != null) { lightRigB.hueSpread = value * 100f; lightRigB.UpdateLights(); }
                        break;
                }
                return;
            }

            if (channel == 3)
            {
                switch (row)
                {
                    case 1:
                        if (directionalLightA != null) directionalLightA.intensity = value * 5f;
                        if (directionalLightB != null) directionalLightB.intensity = value * 5f;
                        break;
                    case 2:
                        float angleX = value * 180f;
                        if (directionalLightA != null) directionalLightA.transform.eulerAngles = new Vector3(angleX, directionalLightA.transform.eulerAngles.y, directionalLightA.transform.eulerAngles.z);
                        if (directionalLightB != null) directionalLightB.transform.eulerAngles = new Vector3(angleX, directionalLightB.transform.eulerAngles.y, directionalLightB.transform.eulerAngles.z);
                        break;
                    case 3:
                        float angleY = value * 180f;
                        if (directionalLightA != null) directionalLightA.transform.eulerAngles = new Vector3(directionalLightA.transform.eulerAngles.x, angleY, directionalLightA.transform.eulerAngles.z);
                        if (directionalLightB != null) directionalLightB.transform.eulerAngles = new Vector3(directionalLightB.transform.eulerAngles.x, angleY, directionalLightB.transform.eulerAngles.z);
                        break;
                }
                return;
            }

            if (channel != 8) return;
            switch (row)
            {
                case 1: _fogHue        = value; break;
                case 2: _fogBrightness = value; break;
                case 3: _fogDensity    = value;
                    RenderSettings.fog        = value > 0.001f;
                    RenderSettings.fogDensity = value;
                    break;
            }
            float sat = fogSaturation * (1f - _fogBrightness);
            RenderSettings.fogColor = Color.HSVToRGB(_fogHue, sat, _fogBrightness);
        }

        void ApplyCapacityVFX()
        {
            float rate  = _vfxSpawnRate * spawnRateMax;
            Color color = Color.HSVToRGB(_vfxHue, 1f - _vfxBrightness, _vfxBrightness);

            if (capacityVfxA != null)
            {
                capacityVfxA.SetFloat("spawnRate", rate);
                capacityVfxA.SetVector4("partCOlor", color);
            }
            if (capacityVfxB != null)
            {
                capacityVfxB.SetFloat("spawnRate", rate);
                capacityVfxB.SetVector4("partCOlor", color);
            }
        }

        void StoreLive(ref float a, ref float b, float value)
        {
            if (AIsLive) a = value; else b = value;
        }

        void RefreshLensFlareActive(bool isA)
        {
            ref DeckFX fx = ref (isA ? ref _fxA : ref _fxB);
            if (fx.lensFlare == null) return;
            float ch5 = isA ? _ch5A : _ch5B;
            float ch6 = isA ? _ch6A : _ch6B;
            float ch7 = isA ? _ch7A : _ch7B;
            fx.lensFlare.active = ch5 > 0.001f || ch6 > 0.001f || ch7 > 0.001f;
        }

        void OnTakeCompleted()
        {
            // Glitch redirects to new live deck cameras
            ApplyGlitch();

            // Volume faders now drive the new live deck — reapply current values
            RefreshLensFlareActive(AIsLive);
            SetLensFlare(ref LiveFX, _ch5);
            SetFlareMultipliers(ref LiveFX, _ch6);
            SetStreaks(ref LiveFX, _ch7);
            SetBloom(ref LiveFX, _ch8);
        }

        // ------------------------------------------------------------------ //
        // Glitch — live deck cameras only

        void ApplyGlitch()
        {
            ApplyGlitchToRig(_analogA, _digitalA, active: AIsLive);
            ApplyGlitchToRig(_analogB, _digitalB, active: !AIsLive);
        }

        void ApplyGlitchToRig(AnalogGlitchController[]  analog,
                               DigitalGlitchController[] digital,
                               bool active)
        {
            float jitter = active ? _ch1 * ch1Scale : 0f;
            float jump   = active ? _ch2 * ch2Scale : 0f;
            float drift  = active ? _ch3 * ch3Scale : 0f;
            float digi   = active ? _ch4 * ch4Scale : 0f;

            foreach (var c in analog)
            {
                if (c == null) continue;
                c.ScanLineJitter = jitter;
                c.VerticalJump   = jump;
                c.ColorDrift     = drift;
            }
            foreach (var c in digital)
            {
                if (c == null) continue;
                c.Intensity = digi;
            }
        }

        // ------------------------------------------------------------------ //
        // Volume effects — operate on whichever DeckFX is passed in

        void SetLensFlare(ref DeckFX fx, float v)
        {
            if (fx.lensFlare == null) return;
            fx.lensFlare.intensity.overrideState = true;
            fx.lensFlare.intensity.value = v * lensFlareMax;
        }

        void SetFlareMultipliers(ref DeckFX fx, float v)
        {
            if (fx.lensFlare == null) return;
            float val = v * flareMultiplierMax;
            fx.lensFlare.firstFlareIntensity.overrideState     = true;
            fx.lensFlare.firstFlareIntensity.value             = val;
            fx.lensFlare.secondaryFlareIntensity.overrideState = true;
            fx.lensFlare.secondaryFlareIntensity.value         = val;
            fx.lensFlare.warpedFlareIntensity.overrideState    = true;
            fx.lensFlare.warpedFlareIntensity.value            = val;
        }

        void SetStreaks(ref DeckFX fx, float v)
        {
            if (fx.lensFlare == null) return;
            fx.lensFlare.streaksIntensity.overrideState = true;
            fx.lensFlare.streaksIntensity.value = v * streaksMax;
        }

        void SetBloom(ref DeckFX fx, float v)
        {
            if (fx.bloom == null) return;
            fx.bloom.active = v > 0.001f;
            fx.bloom.intensity.overrideState = true;
            fx.bloom.intensity.value = v * bloomMax;
        }

        // ------------------------------------------------------------------ //
        // MIDI Mix — Mute

        void HandleMute(int channel, bool isNoteOn)
        {
            if (channel != 4 || !isNoteOn) return;
            lightRigA?.SetAllWhite();
            lightRigB?.SetAllWhite();
        }

        // ------------------------------------------------------------------ //
        // MF64 — rows 2-6

        void HandleMF64GridButton(GridButton btn, bool isNoteOn)
        {
            if (btn.row == 2 && isNoteOn)
            {
                rigA?.SetBehavior(0, CameraBehavior.Still);
                rigA?.SetBehavior(1, CameraBehavior.Still);
                rigB?.SetBehavior(0, CameraBehavior.Still);
                rigB?.SetBehavior(1, CameraBehavior.Still);
                return;
            }

            if (btn.row < 3 || btn.row > 6) return;
            int listIdx = btn.row - 3;

            if (btn.col == 8)
            {
                if (isNoteOn) ClearSpawned(listIdx);
                return;
            }

            int key = btn.row * 10 + btn.col;
            if (isNoteOn)
            {
                if (!_spawnCoroutines.ContainsKey(key))
                    _spawnCoroutines[key] = StartCoroutine(SpawnLoop(listIdx));
            }
            else
            {
                if (_spawnCoroutines.TryGetValue(key, out var co))
                {
                    if (co != null) StopCoroutine(co);
                    _spawnCoroutines.Remove(key);
                }
            }
        }

        IEnumerator SpawnLoop(int listIdx)
        {
            while (true)
            {
                SpawnPrimitive(listIdx);
                yield return new WaitForSeconds(0.2f);
            }
        }

        void SpawnPrimitive(int listIdx)
        {
            Vector3[] origins =
            {
                rigA != null ? rigA.stageOrigin : Vector3.zero,
                rigB != null ? rigB.stageOrigin : new Vector3(5000f, 0f, 0f)
            };

            foreach (var origin in origins)
            {
                var type = _primitiveTypes[Random.Range(0, _primitiveTypes.Length)];
                var go   = GameObject.CreatePrimitive(type);

                go.transform.position   = origin + new Vector3(
                    Random.Range(-4f, 4f), Random.Range(0.2f, 3f), Random.Range(-4f, 4f));
                go.transform.rotation   = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(0.2f, 1.2f);

                var mat   = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
                mat.color = color;

                if (Random.value > 0.5f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * Random.Range(2f, 8f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                go.GetComponent<Renderer>().material = mat;
                _spawnedByRow[listIdx].Add(go);
            }
        }

        void ClearSpawned(int listIdx)
        {
            foreach (var go in _spawnedByRow[listIdx])
                if (go != null) Destroy(go);
            _spawnedByRow[listIdx].Clear();
        }
    }
}
