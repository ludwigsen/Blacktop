using UnityEngine;
using UnityEngine.UI;

// Two full-screen layers crossfade without allocating textures or interrupting the slideshow.
public class FrontEndBackdrop : MonoBehaviour
{
    FrontEndSettings settings;
    readonly RawImage[] layers = new RawImage[2];
    readonly Material[] materials = new Material[2];
    RenderTexture[] softened;
    Image shade;
    float elapsed, blur, targetBlur;
    int current;
    public int CurrentIndex => current;
    public float Blur => blur;

    public void Initialize(FrontEndSettings configuration)
    {
        settings = configuration;
        softened = new RenderTexture[settings.menuFields.Length];
        if(settings.menuBlurFilterShader != null)
        {
            // The visible material must have only its composite pass; overlays draw all material passes.
            var filter = new Material(settings.menuBlurFilterShader);
            for(int i=0;i<softened.Length;i++)
            {
                var field=settings.menuFields[i];if(field==null)continue;
                var temp=RenderTexture.GetTemporary(256,144,0);
                softened[i]=new RenderTexture(256,144,0){filterMode=FilterMode.Bilinear,name="Soft menu field "+i,wrapMode=TextureWrapMode.Clamp};
                softened[i].Create();
                temp.filterMode=FilterMode.Bilinear;Graphics.Blit(field,softened[i]);
                // Low-resolution cached passes avoid a full-screen blur kernel every frame.
                filter.SetVector("_Direction",new Vector4(settings.submenuBlurPixels/field.width/1.7f,0,0,0));
                Graphics.Blit(softened[i],temp,filter,0);
                filter.SetVector("_Direction",new Vector4(0,settings.submenuBlurPixels/field.height/1.7f,0,0));
                Graphics.Blit(temp,softened[i],filter,0);RenderTexture.ReleaseTemporary(temp);
            }
            Destroy(filter);
        }
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Field layer " + i, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(transform, false);
            FrontEndUI.Anchor((RectTransform)go.transform, Vector2.zero, Vector2.one);
            layers[i] = go.GetComponent<RawImage>();
            layers[i].raycastTarget = false;
            if (settings.menuBlurShader != null)
            {
                materials[i] = new Material(settings.menuBlurShader);
                layers[i].material = materials[i];
            }
        }
        shade = FrontEndUI.Panel(transform, "Field shade", Vector2.zero, Vector2.one,
            new Color(.015f, .025f, .025f, .28f)).GetComponent<Image>();
        shade.raycastTarget = false;
        Bind();
    }

    public void SetSubmenu(bool submenu) => targetBlur = submenu ? 1f : 0f;

    void Bind()
    {
        int count = settings.menuFields.Length;
        for (int i = 0; i < 2; i++)
        {
            int index=(current+i)%Mathf.Max(1,count);
            layers[i].texture=count==0?Texture2D.blackTexture:settings.menuFields[index];
            if(materials[i]!=null&&count>0)materials[i].SetTexture("_BlurTex",softened[index]);
        }
    }

    void Update()
    {
        if (settings == null) return;
        float hold = Mathf.Max(1f, settings.fieldHoldSeconds);
        float fade = Mathf.Max(.1f, settings.fieldBlendSeconds);
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= hold + fade)
        {
            elapsed %= hold + fade;
            current = (current + 1) % Mathf.Max(1, settings.menuFields.Length);
            Bind();
        }
        float mix = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - hold) / fade));
        layers[1].color = new Color(1, 1, 1, mix);layers[1].enabled=mix>0;
        blur = Mathf.MoveTowards(blur, targetBlur, Time.unscaledDeltaTime * 3.5f);
        var tint=UITheme.Color(UIColorRole.BackgroundPrimary);
        tint.a=Mathf.Lerp(UITheme.Current.id==UIThemeId.AfterDark?.46f:.28f,.79f,blur);shade.color=tint;
        for (int i = 0; i < layers.Length; i++)
        {
            if (materials[i] != null) materials[i].SetFloat("_Blur", blur);
            // Cover the viewport without stretching the original field photography.
            var texture = layers[i].texture;
            float sourceAspect = texture != null ? (float)texture.width / texture.height : 1.77778f;
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float width = Mathf.Min(1f, screenAspect / sourceAspect);
            float height = Mathf.Min(1f, sourceAspect / screenAspect);
            layers[i].uvRect = new Rect((1f - width) * .5f, (1f - height) * .5f, width, height);
        }
    }

    void OnDestroy()
    {
        if(softened!=null)foreach(var texture in softened)if(texture!=null){texture.Release();Destroy(texture);}
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
