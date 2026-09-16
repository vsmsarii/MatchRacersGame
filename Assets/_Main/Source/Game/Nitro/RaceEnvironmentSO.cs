using UnityEngine;
using UnityEngine.Rendering;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "RaceEnvironment", menuName = "MatchRacers/Race Environment")]
    public sealed class RaceEnvironmentSO : ScriptableObject
    {
        [Header("Sun")]
        [Tooltip("Ana ışığın açısı (derece). Kamera yandan baktığı için ışığın yönü araç siluetini ve gölge boyunu doğrudan belirler.")]
        [SerializeField] private Vector3 m_SunEuler = new Vector3(48f, -35f, 0f);
        [Tooltip("Ana ışığın rengi. Gece haritasında maviye, gün batımında turuncuya çekilir.")]
        [SerializeField] private Color m_SunColor = new Color(1f, 0.97f, 0.9f);
        [Tooltip("Ana ışığın şiddeti. Nitro parlaması araç siluetini yakmayacak biçimde ayarlanır.")]
        [SerializeField, Min(0f)] private float m_SunIntensity = 1.1f;
        [Tooltip("Gölge tipi: kapalı, sert ya da yumuşak.")]
        [SerializeField] private LightShadows m_SunShadows = LightShadows.Soft;
        [Tooltip("Gölgenin koyuluğu. 1 tam siyah gölge demektir; koyu sahnelerde rakipler kaybolmasın diye kısılır.")]
        [SerializeField, Range(0f, 1f)] private float m_ShadowStrength = 0.65f;

        [Header("Ambient")]
        [Tooltip("Yukarıdan gelen ortam ışığı rengi. Araçların üst yüzeylerini boyar.")]
        [SerializeField] private Color m_AmbientSky = new Color(0.32f, 0.36f, 0.42f);
        [Tooltip("Ufuk hizasından gelen ortam ışığı rengi.")]
        [SerializeField] private Color m_AmbientEquator = new Color(0.22f, 0.24f, 0.28f);
        [Tooltip("Yerden yansıyan ortam ışığı rengi. Araçların alt yüzeylerinin tamamen kararmasını engeller.")]
        [SerializeField] private Color m_AmbientGround = new Color(0.10f, 0.11f, 0.13f);

        [Header("Camera Background")]
        [Tooltip("Gökyüzü malzemesi yoksa kameranın temizleme rengi.")]
        [SerializeField] private Color m_BackgroundColor = new Color(0.09f, 0.11f, 0.14f);

        [Header("Fog")]
        [Tooltip("Sis açık mı. Uzak pist ve arka plan silüetlerinin havaya karışmasını sağlar.")]
        [SerializeField] private bool m_FogEnabled = true;
        [Tooltip("Sis rengi. Ufuk rengine yakın seçilir ki uzak pist koyu bir duvara değil gökyüzüne erisin.")]
        [SerializeField] private Color m_FogColor = new Color(0.10f, 0.12f, 0.15f);
        [Tooltip("Sisin başladığı mesafe (metre). Bundan yakını net görünür.")]
        [SerializeField, Min(0f)] private float m_FogStart = 120f;
        [Tooltip("Sisin tamamen kapattığı mesafe (metre). Kameranın uzak kırpma düzleminden küçük tutulur.")]
        [SerializeField, Min(0f)] private float m_FogEnd = 420f;

        [Header("Post Process")]
        [Tooltip("URP post-process profili: tonemapping, bloom, renk ve vinyet. Her ortamın kendi profili vardır.")]
        [SerializeField] private VolumeProfile m_VolumeProfile;
        [Tooltip("Profilin uygulanma ağırlığı. 0 post-process'i tamamen kapatır.")]
        [SerializeField, Min(0f)] private float m_VolumeWeight = 1f;

        [Header("Sky")]
        [Tooltip("Gökyüzü malzemesi. Çalışma zamanında kopyası kullanılır, asset'e yazılmaz.")]
        [SerializeField] private Material m_Skybox;
        [Tooltip("Açıkken yalnız gökyüzünü çeken bir yansıma probu kurulur; araçların metal yüzeyleri ortamın rengini alır.")]
        [SerializeField] private bool m_SkyReflections = true;
        [Tooltip("Yansımanın şiddeti. Yüksek değer araçları fazla parlak gösterir.")]
        [SerializeField, Range(0f, 1f)] private float m_ReflectionIntensity = 1f;

        [Header("Backdrop")]
        [Tooltip("Pistin uzak tarafındaki silüetin türü: yok, şehir blokları ya da tepe sıraları.")]
        [SerializeField] private EBackdropStyle m_BackdropStyle = EBackdropStyle.None;
        [Tooltip("Silüetlerin malzemesi. Sisli unlit shader; şehir stilinde pencere ışıklarını da bu malzeme üretir.")]
        [SerializeField] private Material m_BackdropMaterial;
        [Tooltip("Silüet üretiminin rastgelelik tohumu. Aynı değer aynı şehri ya da tepeleri üretir.")]
        [SerializeField] private int m_BackdropSeed = 7;
        [Tooltip("Uzaklık kademeleri. Her kademe kendi mesafesi, yüksekliği ve rengiyle derinlik hissi üretir; öndekiler alçak tutulursa arkadakiler aralardan görünür.")]
        [SerializeField] private BackdropLayer[] m_BackdropLayers = new BackdropLayer[0];

        [Header("Ground")]
        [Tooltip("Açıkken zemine kenarları kesintisiz bir gürültü dokusu üretilir. Kapatılırsa zemin düz tek renk kalır.")]
        [SerializeField] private bool m_GroundPattern = true;
        [Tooltip("Zeminin ana rengi.")]
        [SerializeField] private Color m_GroundColor = new Color(0.23f, 0.27f, 0.22f);
        [Tooltip("Zemindeki büyük yamaların rengi. Ana renkle arasındaki fark ne kadar açıksa desen o kadar belirgin olur.")]
        [SerializeField] private Color m_GroundPatchColor = new Color(0.28f, 0.31f, 0.24f);
        [Tooltip("Zemindeki küçük beneklerin rengi. Yakın planda dokuyu kırar.")]
        [SerializeField] private Color m_GroundDetailColor = new Color(0.17f, 0.19f, 0.16f);
        [Tooltip("Zemin dokusunun kaç metrede bir tekrarlayacağı. Küçük değer tekrarı belli eder, büyük değer deseni siler.")]
        [SerializeField, Min(8f)] private float m_GroundTileMeters = 160f;
        [Tooltip("Zeminin parlaklığı. Yüksek değer ışığın zeminde parlamasına yol açar.")]
        [SerializeField, Range(0f, 1f)] private float m_GroundSmoothness = 0.1f;
        [Tooltip("Yol kenarındaki şeridin genişliği (metre). Yol ile zemin arasındaki geçişi belirginleştirir; 0 kapatır.")]
        [SerializeField, Min(0f)] private float m_VergeWidth = 4f;
        [Tooltip("Yol kenarı şeridinin rengi.")]
        [SerializeField] private Color m_VergeColor = new Color(0.16f, 0.17f, 0.18f);

        public Vector3 SunEuler => m_SunEuler;
        public Color SunColor => m_SunColor;
        public float SunIntensity => m_SunIntensity < 0f ? 0f : m_SunIntensity;
        public LightShadows SunShadows => m_SunShadows;
        public float ShadowStrength => Mathf.Clamp01(m_ShadowStrength);

        public Color AmbientSky => m_AmbientSky;
        public Color AmbientEquator => m_AmbientEquator;
        public Color AmbientGround => m_AmbientGround;
        public Color BackgroundColor => m_BackgroundColor;

        public bool FogEnabled => m_FogEnabled;
        public Color FogColor => m_FogColor;
        public float FogStart => m_FogStart;
        public float FogEnd => Mathf.Max(m_FogEnd, m_FogStart + 1f);

        public VolumeProfile VolumeProfile => m_VolumeProfile;
        public float VolumeWeight => Mathf.Clamp01(m_VolumeWeight);

        public Material Skybox => m_Skybox;
        public bool SkyReflections => m_SkyReflections;
        public float ReflectionIntensity => Mathf.Clamp01(m_ReflectionIntensity);

        public EBackdropStyle BackdropStyle => m_BackdropStyle;
        public Material BackdropMaterial => m_BackdropMaterial;
        public int BackdropSeed => m_BackdropSeed;
        public int BackdropLayerCount => m_BackdropLayers != null ? m_BackdropLayers.Length : 0;
        public BackdropLayer GetBackdropLayer(int index) => m_BackdropLayers[index];

        public bool GroundPattern => m_GroundPattern;
        public Color GroundColor => m_GroundColor;
        public Color GroundPatchColor => m_GroundPatchColor;
        public Color GroundDetailColor => m_GroundDetailColor;
        public float GroundTileMeters => Mathf.Max(8f, m_GroundTileMeters);
        public float GroundSmoothness => Mathf.Clamp01(m_GroundSmoothness);
        public float VergeWidth => Mathf.Max(0f, m_VergeWidth);
        public Color VergeColor => m_VergeColor;
    }
}
