using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual-only camera/rig owner. Never instantiate Player.prefab or register TeamMember in a preview.
public class RuntimePlayerPreview : MonoBehaviour,IDragHandler,IScrollHandler
{
    GameObject root,model;
    Camera previewCamera;
    RenderTexture texture;
    PlayerVisualAssembler assembler;
    PlayerAppearanceDefinition appearance;
    FrontEndSettings settings;
    float yaw,zoom=4.6f;
    bool headView;
    public string Diagnostics { get; private set; }
    public void Initialize(FrontEndSettings configuration)
    {
        settings=configuration;
        root=new GameObject("Visual-only preview");root.transform.position=new Vector3(1000,1,0);
        model=Instantiate(settings.visualPrefab,root.transform,false);
        assembler=root.AddComponent<PlayerVisualAssembler>();assembler.enabled=false;
        assembler.catalog=settings.appearances;assembler.rig=model.GetComponent<PlayerVisualRig>();
        appearance=ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();
        texture=new RenderTexture(1024,1024,24);texture.Create();GetComponent<RawImage>().texture=texture;
        var cameraObject=new GameObject("Preview camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
        previewCamera=cameraObject.GetComponent<Camera>();previewCamera.targetTexture=texture;
        previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=FrontEndUI.Ink;
        previewCamera.cullingMask=1<<31;previewCamera.fieldOfView=30;previewCamera.nearClipPlane=.05f;previewCamera.farClipPlane=20f;
        Light("Key",Quaternion.Euler(35,-30,0),1.7f);Light("Rim",Quaternion.Euler(30,155,0),1.1f);
        assembler.rig.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        Frame();
    }
    void Light(string name,Quaternion rotation,float intensity)
    {
        var go=new GameObject(name,typeof(Light));go.transform.SetParent(root.transform,false);go.transform.rotation=rotation;
        var light=go.GetComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.cullingMask=1<<31;
    }
    public bool Apply(RosterPlayer player,TeamIdentity team)
    {
        player.appearance.Apply(appearance,settings.appearances,string.IsNullOrEmpty(player.record.id)?"draft":player.record.id);
        bool valid=assembler.TryApply(appearance,team,player.record.jerseyNumber);
        Diagnostics=assembler.LastDiagnostics;
        foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
        assembler.rig.animator.Update(0f);Frame();return valid;
    }
    public void Rotate(float degrees) {yaw+=degrees;model.transform.localRotation=Quaternion.Euler(0,yaw,0);}
    public void Zoom(float amount) {zoom=Mathf.Clamp(zoom+amount,headView?.7f:2.5f,8f);Frame();}
    public void SetHeadView(bool close) {headView=close;zoom=close?1.2f:4.6f;Frame();}
    public void ResetCamera() {yaw=0;model.transform.localRotation=Quaternion.identity;SetHeadView(false);}
    public void Animation(float locomotion) {assembler.rig.animator.SetFloat("Locomotion",locomotion);}
    void Frame()
    {
        if(previewCamera==null)return;
        Vector3 target=root.transform.position;
        if(headView && assembler.rig.animator.isHuman) target=assembler.rig.animator.GetBoneTransform(HumanBodyBones.Head).position;
        previewCamera.transform.position=target+new Vector3(0,.12f,zoom);
        previewCamera.transform.LookAt(target);
    }
    public void OnDrag(PointerEventData data) => Rotate(-data.delta.x*.4f);
    public void OnScroll(PointerEventData data) => Zoom(-data.scrollDelta.y*.2f);
    void OnDisable() {if(root!=null)root.SetActive(false);}
    void OnEnable() {if(root!=null)root.SetActive(true);}
    void OnDestroy()
    {
        if(root!=null){root.SetActive(false);Destroy(root);}
        if(appearance!=null)Destroy(appearance);
        if(texture!=null){texture.Release();Destroy(texture);}
    }
}
