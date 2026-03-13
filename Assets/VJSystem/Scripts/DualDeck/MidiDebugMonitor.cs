using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MidiFighter64;

namespace VJSystem
{
    /// <summary>
    /// Listens to MidiEventManager, MidiGridRouter, and MidiMixRouter and maintains
    /// a live snapshot of all MIDI state for the debug tab in DualDeckGUI.
    /// Also polls InputSystem each frame so the GUI can show raw device registration.
    /// </summary>
    public class MidiDebugMonitor : MonoBehaviour
    {
        public const int LOG_SIZE = 24;

        // ---- MF64 grid state ----
        public bool[,] GridState { get; } = new bool[8, 8];

        // ---- MIDI Mix state ----
        public float[,] KnobValues  { get; } = new float[3, 8];
        public float[]  FaderValues { get; } = new float[8];
        public float    MasterFader { get; private set; }
        public bool[]   MuteState   { get; } = new bool[8];
        public bool[]   RecArmState { get; } = new bool[8];

        // ---- General ----
        public IReadOnlyList<string> EventLog   => _log;
        public IReadOnlyList<string> DeviceList => _deviceList;

        public string DeviceName => MidiEventManager.Instance != null
                                        ? MidiEventManager.Instance.DeviceName
                                        : "(MidiEventManager not found)";
        public int TotalEvents { get; private set; }

        readonly List<string> _log        = new(LOG_SIZE + 1);
        readonly List<string> _deviceList = new();

        // Stored delegates so we can unsubscribe them
        Action<int, bool>  _onMute;
        Action<int, bool>  _onRecArm;
        Action             _onBankLeft;
        Action             _onBankRight;

        // ------------------------------------------------------------------ //

        void OnEnable()
        {
            MidiEventManager.OnNoteOn        += HandleNoteOn;
            MidiEventManager.OnNoteOff       += HandleNoteOff;
            MidiEventManager.OnControlChange += HandleCC;

            _onMute      = (ch, on) => { MuteState[ch - 1]   = on; AddLog($"MUTE   Ch{ch} {(on ? "ON" : "OFF")}"); };
            _onRecArm    = (ch, on) => { RecArmState[ch - 1] = on; AddLog($"RECARM Ch{ch} {(on ? "ON" : "OFF")}"); };
            _onBankLeft  = ()       => AddLog("BANK LEFT");
            _onBankRight = ()       => AddLog("BANK RIGHT");

            MidiMixRouter.OnMute      += _onMute;
            MidiMixRouter.OnRecArm    += _onRecArm;
            MidiMixRouter.OnBankLeft  += _onBankLeft;
            MidiMixRouter.OnBankRight += _onBankRight;
        }

        void OnDisable()
        {
            MidiEventManager.OnNoteOn        -= HandleNoteOn;
            MidiEventManager.OnNoteOff       -= HandleNoteOff;
            MidiEventManager.OnControlChange -= HandleCC;

            MidiMixRouter.OnMute      -= _onMute;
            MidiMixRouter.OnRecArm    -= _onRecArm;
            MidiMixRouter.OnBankLeft  -= _onBankLeft;
            MidiMixRouter.OnBankRight -= _onBankRight;
        }

        void Update()
        {
            // Poll InputSystem devices every frame so the GUI always shows current state
            _deviceList.Clear();
            foreach (var dev in InputSystem.devices)
            {
                bool isMidi = dev is Minis.MidiDevice;
                _deviceList.Add($"{(isMidi ? "[MIDI] " : "       ")}{dev.displayName}  ({dev.GetType().Name})");
            }
        }

        // ------------------------------------------------------------------ //

        void HandleNoteOn(int note, float vel)
        {
            TotalEvents++;
            if (MidiFighter64InputMap.IsInRange(note))
            {
                var btn = MidiFighter64InputMap.FromNote(note);
                GridState[btn.row - 1, btn.col - 1] = true;
                AddLog($"ON   MF R{btn.row} C{btn.col}  #{note}  vel={vel:F2}");
            }
            else
            {
                AddLog($"ON   #{note}  vel={vel:F2}");
            }
        }

        void HandleNoteOff(int note)
        {
            TotalEvents++;
            if (MidiFighter64InputMap.IsInRange(note))
            {
                var btn = MidiFighter64InputMap.FromNote(note);
                GridState[btn.row - 1, btn.col - 1] = false;
                AddLog($"OFF  MF R{btn.row} C{btn.col}  #{note}");
            }
            else
            {
                AddLog($"OFF  #{note}");
            }
        }

        void HandleCC(int cc, float value)
        {
            TotalEvents++;
            if (MidiMixInputMap.TryGetKnob(cc, out var knob))
            {
                KnobValues[knob.row - 1, knob.channel - 1] = value;
                AddLog($"CC   MIX Knob R{knob.row} Ch{knob.channel}  cc={cc}  val={value:F2}");
            }
            else if (MidiMixInputMap.TryGetFader(cc, out var fader))
            {
                if (fader.isMaster) MasterFader = value;
                else FaderValues[fader.channel - 1] = value;
                string label = fader.isMaster ? "Master" : $"Ch{fader.channel}";
                AddLog($"CC   MIX Fader {label}  cc={cc}  val={value:F2}");
            }
            else
            {
                AddLog($"CC   cc={cc}  val={value:F2}");
            }
        }

        void AddLog(string entry)
        {
            _log.Insert(0, entry);
            if (_log.Count > LOG_SIZE)
                _log.RemoveAt(_log.Count - 1);
        }

        public void ClearLog()
        {
            _log.Clear();
            TotalEvents = 0;
            System.Array.Clear(GridState,   0, GridState.Length);
            System.Array.Clear(KnobValues,  0, KnobValues.Length);
            System.Array.Clear(FaderValues, 0, FaderValues.Length);
            System.Array.Clear(MuteState,   0, MuteState.Length);
            System.Array.Clear(RecArmState, 0, RecArmState.Length);
            MasterFader = 0f;
        }
    }
}
