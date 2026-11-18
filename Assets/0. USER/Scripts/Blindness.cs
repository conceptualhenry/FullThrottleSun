using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Original blindness look: a soft centered sprite that grows via localScale.
/// Triggers when the camera is inside <see cref="sunZone"/> and within
/// <see cref="lookAngleDegrees"/> of the sun direction.
/// </summary>
public class Blindness : MonoBehaviour
{
    [SerializeField] Camera cam;
    [Tooltip("Only interact while the camera is inside this trigger (Car/SunZone).")]
    [SerializeField] Collider sunZone;
    [Tooltip("Sun aim point. Empty = Car/Sun_.")]
    [SerializeField] Transform sunTarget;
    [Tooltip("Look within this many degrees of the sun to start glaring.")]
    [SerializeField] float lookAngleDegrees = 18f;
    [SerializeField] float increaseRate = 1.25f;
    [SerializeField] float decreaseRate = 0.99f;
    [SerializeField] float waitTime = 5f;
    [SerializeField] float sizeMin = 0.05f;
    [SerializeField] float sizeMax = 50f;
    [Tooltip("If on, rebuilds the Blindness sprite as a soft white glow (same alpha shape).")]
    [SerializeField] bool useWhiteGlow = true;

    Image overlay;
    RectTransform rect;
    Sprite sourceSprite;
    bool shrinkingOn;
    bool restartAllowed = true;
    Coroutine blindnessTimer;
    float scaleY;

    void Start()
    {
        rect = transform as RectTransform;
        overlay = GetComponent<Image>();
        if (overlay != null)
            sourceSprite = overlay.sprite;

        if (cam == null)
            cam = Camera.main;
        if (sunZone == null)
        {
            var car = GameObject.Find("Car");
            var zone = car != null ? car.transform.Find("SunZone") : null;
            if (zone != null)
                sunZone = zone.GetComponent<Collider>();
        }
        if (sunTarget == null)
        {
            var car = GameObject.Find("Car");
            var sun = car != null ? car.transform.Find("Sun_") : null;
            if (sun != null)
                sunTarget = sun;
        }

        // Restore original centered blob layout (not full-screen stretch).
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);
        }

        if (overlay != null)
        {
            if (useWhiteGlow)
                overlay.sprite = BuildWhiteGlowFromAlpha(sourceSprite) ?? sourceSprite;
            else if (sourceSprite != null)
                overlay.sprite = sourceSprite;
            overlay.color = Color.white;
            overlay.raycastTarget = false;
        }

        scaleY = Mathf.Max(sizeMin, rect != null ? Mathf.Abs(rect.localScale.y) : sizeMin);
        ApplyScale(scaleY);
        blindnessTimer = StartCoroutine(BlindnessWait());
    }

    void Update()
    {
        if (cam == null || rect == null)
            return;

        bool burning = CameraInSunZone() && LookingAtSunByAngle();
        if (burning && scaleY < sizeMax)
        {
            scaleY += increaseRate / 100f;
            ApplyScale(scaleY);
            if (restartAllowed)
            {
                if (blindnessTimer != null)
                    StopCoroutine(blindnessTimer);
                restartAllowed = false;
                StartCoroutine(RestartTimer());
            }
        }
        else if (!burning && shrinkingOn && scaleY > sizeMin)
        {
            scaleY -= decreaseRate / 100f;
            ApplyScale(scaleY);
        }
    }

    void ApplyScale(float y)
    {
        y = Mathf.Clamp(y, sizeMin, sizeMax);
        scaleY = y;
        // Original formula: wider on X, grow from center.
        rect.localScale = new Vector3(y * 2f, y, 1f);
    }

    bool CameraInSunZone()
    {
        if (sunZone == null || !sunZone.enabled)
            return false;
        Vector3 p = cam.transform.position;
        return (sunZone.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
    }

    bool LookingAtSunByAngle()
    {
        if (sunTarget == null)
            return false;
        Vector3 toSun = sunTarget.position - cam.transform.position;
        if (toSun.sqrMagnitude < 0.01f)
            return false;
        return Vector3.Angle(cam.transform.forward, toSun) <= lookAngleDegrees;
    }

    static Sprite BuildWhiteGlowFromAlpha(Sprite src)
    {
        if (src == null || src.texture == null)
            return null;

        Texture2D srcTex = src.texture;
        Rect r = src.textureRect;
        int w = Mathf.Max(1, Mathf.RoundToInt(r.width));
        int h = Mathf.Max(1, Mathf.RoundToInt(r.height));

        var rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(srcTex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var white = new Texture2D(w, h, TextureFormat.RGBA32, false);
        white.wrapMode = TextureWrapMode.Clamp;
        white.filterMode = FilterMode.Bilinear;
        int x0 = Mathf.RoundToInt(r.x);
        int y0 = Mathf.RoundToInt(r.y);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = readable.GetPixel(x0 + x, y0 + y).a;
                white.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        white.Apply(false, true);
        Object.Destroy(readable);

        return Sprite.Create(white, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    IEnumerator BlindnessWait()
    {
        shrinkingOn = false;
        yield return new WaitForSeconds(waitTime);
        shrinkingOn = true;
    }

    IEnumerator RestartTimer()
    {
        yield return new WaitForSeconds(0.25f);
        restartAllowed = true;
        blindnessTimer = StartCoroutine(BlindnessWait());
    }
}
