namespace VJSystem
{
    public interface IPostFXSystem
    {
        void ApplyPreset(int slotIndex);        // 0-6
        void Randomize();
        string CaptureCurrentState(string name);  // returns JSON for save system
        string EffectTypeName { get; }
    }
}
