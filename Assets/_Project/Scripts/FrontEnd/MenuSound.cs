using UnityEngine;
using UnityEngine.EventSystems;

// A quiet, generated click makes the SFX control testable without adding a sound library.
public class MenuSound : MonoBehaviour
{
    public const string VolumeKey = "Blacktop.SfxVolume";
    AudioSource source;
    AudioClip click;
    public static float Volume => PlayerPrefs.GetFloat(VolumeKey, .65f);
    public static void SetVolume(float value)
    {
        PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.ignoreListenerPause = true;
        const int samples = 1800;
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / 44100f;
            data[i] = Mathf.Sin(2f * Mathf.PI * (620f * t - 1800f * t * t)) * Mathf.Exp(-t * 110f) * .12f;
        }
        click = AudioClip.Create("Menu click", samples, 1, 44100, false);
        click.SetData(data, 0);
    }
    public void Play() { if (source != null && click != null) source.PlayOneShot(click, Volume); }
    void OnDestroy() { if (click != null) Destroy(click); }
}

// Resolve the owner locally; menu audio does not become a persistent global manager.
public class MenuSoundCue : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public void OnPointerClick(PointerEventData data) => GetComponentInParent<FrontEndController>()?.Sound?.Play();
    public void OnSubmit(BaseEventData data) => GetComponentInParent<FrontEndController>()?.Sound?.Play();
}
