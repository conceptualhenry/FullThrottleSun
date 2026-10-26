namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Deprecated alias — use <see cref="SettingsMenu"/> (Esc / F1).
    /// Kept so existing scene references do not break.
    /// </summary>
    [System.Obsolete("Use SettingsMenu instead.")]
    public class KeyboardBindingMenu : UnityEngine.MonoBehaviour
    {
        void Awake()
        {
            SettingsMenu.Show();
            Destroy(this);
        }
    }
}
