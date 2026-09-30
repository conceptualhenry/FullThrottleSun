namespace FullThrottleSun.Controller
{
    public interface IControllerSource
    {
        string DeviceName { get; }
        bool IsAvailable { get; }
        void Enable();
        void Disable();
        VirtualControllerState GetState(ControllerHand hand);
    }
}
