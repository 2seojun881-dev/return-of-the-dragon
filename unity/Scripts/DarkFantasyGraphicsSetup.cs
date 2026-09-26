using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 아트 바이블 기준 '다크 판타지' 그래픽 프리셋 (URP).
/// 씬에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤, 인스펙터에서 [Apply] 체크 또는 Play 하면
/// Global Volume(Tonemapping ACES / Bloom / Vignette / Color Adjustments), 안개, 환경광, 주광이 설정됩니다.
///
/// 핵심 원칙: 맵 전체는 어둡고 차가운 푸른빛/잿빛(Ambient)으로 누르고,
/// 무기·스킬·적의 눈 같은 핵심 요소만 채도 높은 붉은색/황금색 Emission + Bloom 으로 빛나게 한다.
/// </summary>
[ExecuteAlways]
public class DarkFantasyGraphicsSetup : MonoBehaviour
{
    public enum Quality { High, Medium, Low }

    [Header("Preset")]
    public Quality quality = Quality.Medium;
    public bool apply;

    [Header("Scene refs")]
    public Light sun;                 // Directional Light
    public Volume globalVolume;       // 비어 있으면 자동 생성

    [Header("Palette")]
    public Color ambientSky = new Color(0.53f, 0.60f, 0.72f);    // 차가운 푸른 잿빛
    public Color ambientEquator = new Color(0.25f, 0.24f, 0.27f);
    public Color ambientGround = new Color(0.16f, 0.13f, 0.12f);
    public Color sunColor = new Color(1.00f, 0.70f, 0.47f);      // 따뜻한 키 라이트 (저녁 햇빛)
    public Color fogColor = new Color(0.18f, 0.17f, 0.21f);

    void OnEnable() { Apply(); }

    void OnValidate()
    {
        if (apply) { apply = false; Apply(); }
    }

    public void Apply()
    {
        // ---- Environment: 안개 + 환경광 ----
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;

        // ---- Directional Light ----
        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = 1.1f;
            sun.shadows = quality == Quality.Low ? LightShadows.Hard : LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f); // 쿼터뷰에서 그림자가 캐릭터 뒤쪽으로 떨어지는 각도
        }

        // ---- URP Asset: 그림자 ----
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.shadowDistance = quality == Quality.High ? 45f : 35f;   // 쿼터뷰는 화면에 보이는 거리만큼만
            urp.shadowCascadeCount = quality == Quality.High ? 2 : 1;  // 모바일: 1~2 캐스케이드
            urp.renderScale = quality == Quality.High ? 1f : quality == Quality.Medium ? 0.85f : 0.7f;
            urp.msaaSampleCount = quality == Quality.Low ? 1 : 2;
            urp.supportsHDR = quality != Quality.Low;                   // Bloom 품질을 위해 HDR
            // 주광 그림자 해상도·Soft Shadows 는 URP Asset 인스펙터에서:
            //   High: 2048 + Soft Shadows / Medium: 1024 + Soft Shadows / Low: 1024, Soft 끔
        }

        // ---- Global Volume (Post-Processing) ----
        if (globalVolume == null)
        {
            var go = new GameObject("Global Volume (Dark Fantasy)");
            globalVolume = go.AddComponent<Volume>();
            globalVolume.isGlobal = true;
        }
        if (globalVolume.sharedProfile == null) globalVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        VolumeProfile p = globalVolume.sharedProfile;

        var tone = Get<Tonemapping>(p);
        tone.mode.Override(TonemappingMode.ACES);                  // 묵직한 필름 톤

        var bloom = Get<Bloom>(p);
        bloom.active = quality != Quality.Low;
        bloom.threshold.Override(1.0f);                            // HDR 1.0 이상(Emission)만 빛나게
        bloom.intensity.Override(quality == Quality.High ? 0.9f : 0.7f);
        bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(false);                // 모바일 발열 절감
        bloom.tint.Override(new Color(1f, 0.85f, 0.7f));

        var vig = Get<Vignette>(p);
        vig.intensity.Override(0.32f);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.02f, 0.01f, 0.04f));

        var ca = Get<ColorAdjustments>(p);
        ca.postExposure.Override(0.1f);
        ca.contrast.Override(12f);
        ca.saturation.Override(-6f);                               // 배경 채도를 눌러 강조 색을 살린다

        var smh = Get<ShadowsMidtonesHighlights>(p);
        smh.shadows.Override(new Vector4(0.92f, 0.96f, 1.08f, 0f)); // 그림자는 푸르게
        smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0f)); // 하이라이트는 따뜻하게
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c)) c = p.Add<T>(true);
        c.active = true;
        return c;
    }

    /// <summary>
    /// 스킬·무기·적 눈동자용 Emission 설정 도우미 (URP/Lit 머티리얼).
    /// intensity 는 HDR 배율: 2~4 정도면 Bloom threshold 1.0 을 넘겨 확실히 빛난다.
    /// </summary>
    public static void SetEmission(Material m, Color color, float intensity)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", color * intensity);
    }

    /// <summary>무기 쇠 질감: Metallic 높게, Smoothness 는 날(블레이드)만 높게.</summary>
    public static void SetMetal(Material m, float metallic = 0.9f, float smoothness = 0.72f)
    {
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
    }
}
