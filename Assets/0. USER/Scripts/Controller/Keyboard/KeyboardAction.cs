namespace FullThrottleSun.Controller
{
    /// <summary>Logical actions the keyboard can drive through <see cref="KeyboardBindings"/>.</summary>
    public enum KeyboardAction
    {
        Throttle,
        Brake,
        SteerLeft,
        SteerRight,
        Handbrake,
        LookLeft,
        LookRight,
        LookUp,
        LookDown,
        CameraSwitch,
        LightLeft,
        LightRight,
        LightUp,
        LightDown,
        Wipers,
        GearReverse,
        GearNeutral,
        GearDrive,
        /// <summary>Cycle R → N → D → R with one key.</summary>
        GearCycle,
        Menu,
        Secondary,
        GripLeft,
        GripRight
    }
}
