using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "RaceConfig", menuName = "MatchRacers/Race Config")]
    public sealed class RaceConfigSO : ScriptableObject
    {
        public const int CarCount = 8;
        public const int RivalCount = CarCount - 1;

        [Header("Race")]
        [Tooltip("Girdisiz aracın taban hızı (m/sn). Case sözleşmesindeki v0 budur; k tuşu bu hızı k katına çıkarır. Yarış süresini ve hız hissini belirleyen ilk ayar.")]
        [SerializeField, Min(0.1f)] private float m_BaseSpeed = 12f;
        [Tooltip("Varsayılan yarış uzunluğu (metre). Aktif pistin kendi uzunluğu varsa o kullanılır, bu değer yedektir.")]
        [SerializeField, Min(1f)] private float m_RaceLengthMeters = 1000f;
        [Tooltip("3-2-1 geri sayımının süresi. Geri sayım boyunca araçlar ilerlemez ve nitro kabul edilmez.")]
        [SerializeField, Min(0f)] private float m_CountdownSeconds = 3f;

        [Header("Track")]
        [Tooltip("İki şerit arasındaki mesafe (metre). Sekiz aracın yan yana genişliğini ve yolun toplam genişliğini belirler.")]
        [SerializeField, Min(0.5f)] private float m_LaneWidthMeters = 2.4f;
        [Tooltip("Bitiş çizgisinden sonra yolun devam ettiği mesafe (metre). Araçlar burada yavaşlayarak durur, çizgide aniden kesilmezler.")]
        [SerializeField, Min(0f)] private float m_RunoutMeters = 60f;
        [Tooltip("Şeritlerin dışında kalan asfalt payı (metre). Yol genişliği şerit sayısı ve bu payla hesaplanır.")]
        [SerializeField, Min(0f)] private float m_RoadShoulderMeters = 3f;

        [Header("Track Shape")]
        [Tooltip("Oyunun açılışta kuracağı varsayılan pist. Track Editor'deki Set As Default bu alanı yazar. Oyun içinde seçilen pist bu alanı değiştirmez.")]
        [SerializeField] private TrackLayoutSO m_TrackLayout;
        [System.NonSerialized] private TrackLayoutSO m_ActiveTrack;
        [Tooltip("Pist seçim panelinde listelenecek pistlerin kataloğu. Boşsa panel açılmaz.")]
        [SerializeField] private TrackCatalogSO m_TrackCatalog;

        [Header("Camera")]
        [Tooltip("Araçların ekranda hangi yöne gittiği. Kameranın hangi tarafta duracağını ve dekorun hangi tarafa yerleşeceğini de belirler.")]
        [SerializeField] private bool m_CarsMoveLeftToRight = true;
        [Tooltip("Kameranın yoldan yan mesafesi (metre). Küçük değer araçları büyütür, büyük değer grubun tamamını gösterir.")]
        [SerializeField, Min(1f)] private float m_CameraSideDistance = 30f;
        [Tooltip("Kameranın yerden yüksekliği (metre). Yükseldikçe kamera aşağı bakar ve gökyüzü kadrajdan çıkar.")]
        [SerializeField] private float m_CameraHeight = 7.5f;
        [Tooltip("Kameranın odak noktasının araçtan ne kadar ileriye bakacağı (metre). Hız hissini artırır ama öndeki rakipleri kadrajın kenarına iter.")]
        [SerializeField] private float m_CameraLookAhead = 6f;
        [Tooltip("Kameranın oyuncuyu takip sertliği. Yüksek değer kamerayı araca yapıştırır, düşük değer aracın öne çıkmasına izin verir. Aracın kameraya göre geride kalma payı da bu değerden türer.")]
        [SerializeField, Min(0.1f)] private float m_CameraFollowSmoothing = 5f;
        [Tooltip("Kameranın görüş açısı (derece). Genişletmek daha çok çevre gösterir ve hız hissini artırır.")]
        [SerializeField, Range(20f, 90f)] private float m_CameraFieldOfView = 52f;
        [Tooltip("Nitro seviyesi başına görüş açısına eklenen artış. Hızlanma anında kadrajın açılmasıyla atak hissi verir.")]
        [SerializeField, Range(0f, 12f)] private float m_CameraBuffFovGain = 2.25f;
        [Tooltip("Kameranın oyuncu ile grubun ortası arasında ne kadar kayacağı. 0 tamamen oyuncuyu izler, 1 grubun ortasını izler.")]
        [SerializeField, Range(0f, 1f)] private float m_CameraPackBias = 0.35f;
        [Tooltip("Nitro kabul edildiğinde kameranın ani ileri vuruşu. Hamlenin kabul edildiğini anlık olarak hissettirir.")]
        [SerializeField, Range(0f, 20f)] private float m_CameraBuffPunch = 5f;
        [Tooltip("Nitro sarsıntısının genel çarpanı. 0 sarsıntıyı tamamen kapatır.")]
        [SerializeField, Range(0f, 3f)] private float m_CameraShakeScale = 1f;
        [Tooltip("Sarsıntının titreşim hızı. Yüksek değer sinirli, düşük değer ağır bir sallanma verir.")]
        [SerializeField, Range(1f, 60f)] private float m_CameraShakeFrequency = 25f;
        [Tooltip("Basış anındaki ilk sarsıntı vuruşunun çarpanı.")]
        [SerializeField, Range(1f, 5f)] private float m_CameraShakeKick = 2f;
        [Tooltip("İlk vuruşun sönme hızı. Yüksek değer vuruşu kısa keser.")]
        [SerializeField, Min(0.1f)] private float m_CameraShakeKickDecay = 8f;
        [Tooltip("Nitro penceresi boyunca devam eden titreşimin, ilk vuruşa göre oranı.")]
        [SerializeField, Range(0f, 1f)] private float m_CameraShakeSustain = 0.45f;
        [Tooltip("Sarsıntının kameraya kattığı yatma miktarı. Sallanmanın yalnız kayma değil dönme de içermesini sağlar.")]
        [SerializeField, Range(0f, 10f)] private float m_CameraShakeRollPerMeter = 1.5f;

        [Header("Car View")]
        [Tooltip("Aracın şeridine yanaşma yumuşaklığı. Yalnız görsel; sıralama ve mesafe hesabını etkilemez.")]
        [SerializeField, Min(0.1f)] private float m_LaneChangeSmoothing = 6f;
        [Tooltip("Nitro seviyesi başına aracın yana yatma açısı (derece). Hızlanmayı gövde diliyle anlatır.")]
        [SerializeField] private float m_BankPerBuffLevel = 1.8f;
        [Tooltip("Tekerlek yarıçapı (metre). Tekerleklerin dönme hızı bu değerden hesaplanır; yanlışsa tekerlekler kayıyormuş gibi görünür.")]
        [SerializeField, Min(0.05f)] private float m_WheelRadiusMeters = 0.34f;
        [Tooltip("Nitro izinin ekranda kalma süresi (saniye). Uzun değer iz birikmesine yol açar.")]
        [SerializeField, Min(0f)] private float m_BuffTrailSeconds = 0.35f;
        [Tooltip("Oyuncu aracının üstündeki işaretin yüksekliği (metre). Sekiz araç arasında kendi aracını ayırt etmeyi sağlar.")]
        [SerializeField, Min(0f)] private float m_PlayerMarkerHeight = 2.6f;

        [Header("Finish Coast")]
        [Tooltip("Bitişten sonra aracın yavaşlama ivmesi (m/sn²). Yalnız sunumdur, sıralama çizgi geçişinde belirlenmiştir.")]
        [SerializeField, Min(0.1f)] private float m_FinishCoastDeceleration = 9f;
        [Tooltip("Bitişten sonra bir aracın süzüleceği en kısa mesafe (metre).")]
        [SerializeField, Min(0f)] private float m_FinishCoastMinMeters = 6f;
        [Tooltip("Bitişten sonra süzülmenin en uzun mesafesi (metre). Runout içinde kalmalı, yoksa araç yolun sonundan taşar.")]
        [SerializeField, Min(0f)] private float m_FinishCoastMaxMeters = 35f;
        [Tooltip("Araçların durma noktaları arasındaki rastgele fark (metre). Sekiz aracın aynı hizada durup dizilmesini engeller.")]
        [SerializeField, Min(0f)] private float m_FinishCoastSpreadMeters = 5f;

        [Header("Simulation")]
        [Tooltip("Simülasyonun saniyedeki sabit adım sayısı. Hareket bu adımlarla ilerlediği için kare hızından bağımsızdır; değiştirmek buff mesafesi yuvarlamalarını etkiler.")]
        [SerializeField, Min(1)] private int m_FixedStepHz = 120;

        [Header("Target Order Mode")]
        [Tooltip("Hedef modda iki slot arasındaki temel mesafe (metre). Botların oyuncunun etrafında dizileceği aralığı belirler.")]
        [SerializeField, Min(1f)] private float m_TargetSeparationMeters = 12f;
        [Tooltip("Yarışın başında hedef bandının genişliği (metre). Başta dar tutulur ki grup birlikte çıksın.")]
        [SerializeField, Min(0f)] private float m_TargetMarginStartMeters = 6f;
        [Tooltip("Yarış sonunda oyuncunun önündeki botlara tanınan pay (metre). Büyük değer hedefi garantiler ama bitişi seyrekleştirir.")]
        [SerializeField, Min(0f)] private float m_TargetMarginAheadEndMeters = 48f;
        [Tooltip("Yarış sonunda oyuncunun arkasındaki botlara tanınan pay (metre). Küçük değer arkadan baskıyı artırır.")]
        [SerializeField, Min(0f)] private float m_TargetMarginBehindEndMeters = 16f;
        [Tooltip("Bu ilerlemeden sonra botların atak istekleri kilitlenir. Finalde ani sıra değişimlerini engeller.")]
        [SerializeField, Range(0f, 1f)] private float m_TargetAttackLockProgress = 0.65f;
        [Tooltip("Kilit devredeyken tolere edilen en büyük fark (metre). Bundan büyük sapmada bot yine de müdahale eder.")]
        [SerializeField, Min(0f)] private float m_TargetAttackLockGapMeters = 60f;
        [Tooltip("Bandın başlangıç genişliğinden bitiş genişliğine hangi ilerlemede ulaşacağı. 1'e yaklaştıkça açılma daha geç olur.")]
        [SerializeField, Range(0.05f, 1f)] private float m_TargetMarginRampEnd = 0.75f;
        [Tooltip("Slot mesafelerine eklenen rastgele sapma oranı. Botların cetvelle dizilmiş görünmesini engeller.")]
        [SerializeField, Range(0f, 0.6f)] private float m_TargetSlotJitter = 0.22f;
        [Tooltip("Bir bota uygulanabilecek en düşük tempo çarpanı. Alt sınır, botun görünür biçimde yavaşlamasını engeller.")]
        [SerializeField, Range(0.5f, 1f)] private float m_TargetPaceMin = 0.75f;
        [Tooltip("Bir bota uygulanabilecek en yüksek tempo çarpanı. Üst sınır, açıklanamayan hız sıçramalarını engeller.")]
        [SerializeField, Range(1f, 1.6f)] private float m_TargetPaceMax = 1.3f;
        [Tooltip("Tempo çarpanının saniyede ne kadar değişebileceği. Düşük değer müdahaleyi zamana yayar ve gözden gizler.")]
        [SerializeField, Min(0.01f)] private float m_TargetPaceRatePerSecond = 0.4f;
        [Tooltip("Oyuncu hızının yumuşatma penceresi (saniye). Nitro anlarının tek tek bota yansımasını engeller.")]
        [SerializeField, Min(0.01f)] private float m_TargetPlayerSpeedEmaSeconds = 2f;
        [Tooltip("Botun hedef mesafeye yaklaşma tepkisi. Yüksek değer hızlı ama belirgin, düşük değer yavaş ama doğal düzeltme verir.")]
        [SerializeField, Min(0.01f)] private float m_TargetBiasResponse = 6f;
        [Tooltip("Yarışın başında düzeltmenin zaman sabiti. Küçük değer hızlı toparlanma demektir.")]
        [SerializeField, Min(0f)] private float m_TargetEnvelopeTauStart = 0.3f;
        [Tooltip("Yarışın sonunda düzeltmenin zaman sabiti. Sonda büyütmek finalde ani düzeltmeleri engeller.")]
        [SerializeField, Min(0f)] private float m_TargetEnvelopeTauEnd = 0.8f;
        [Tooltip("Öndeki botların hedefe göre serbest bırakıldığı pay (saniye). Bu pay içinde müdahale edilmez.")]
        [SerializeField, Min(0f)] private float m_TargetAheadSlackSeconds = 1f;
        [Tooltip("Arkadaki botların serbest payı (saniye).")]
        [SerializeField, Min(0f)] private float m_TargetBehindSlackSeconds = 0.5f;
        [Tooltip("Öndeki bot bu kadar geriye düşerse müdahale kritik sayılır ve üst sınırlar zorlanır.")]
        [SerializeField] private float m_TargetAheadCriticalSeconds = -0.3f;
        [Tooltip("Arkadaki bot bu kadar öne geçerse müdahale kritik sayılır.")]
        [SerializeField] private float m_TargetBehindCriticalSeconds = -0.3f;
        [Tooltip("Oyuncuya bu mesafeden yakın botlar tempo kapısına tam girer; hızları oyuncununkine yakın tutulur.")]
        [SerializeField, Min(0f)] private float m_TargetNearInnerMeters = 12f;
        [Tooltip("Tempo kapısının dış sınırı (metre). İki sınır arasında etki yumuşakça azalır.")]
        [SerializeField, Min(0f)] private float m_TargetNearOuterMeters = 30f;
        [Tooltip("Kapı içindeki botlara izin verilen hız farkı oranı. Küçük değer yan yana giden araçların birbirinden kopmasını engeller.")]
        [SerializeField, Range(0f, 0.2f)] private float m_TargetNearPaceBand = 0.03f;
        [Tooltip("Bir sapmanın kaç saniyede kapatılmaya çalışılacağı. Uzun ufuk, müdahaleyi daha küçük ve fark edilmez yapar.")]
        [SerializeField, Min(0.5f)] private float m_TargetCorrectionHorizonSeconds = 6f;
        [Tooltip("Açıkken arkadaki botlar slotlarının çok ötesine geçecek nitroları kullanmaz. Hedefi bozan aşırı atakları engeller.")]
        [SerializeField] private bool m_TargetSlotNitroCap = true;
        [Tooltip("Slot sınırının ötesine tanınan tolerans (metre). Küçük değer botları fazla temkinli yapar.")]
        [SerializeField, Min(0f)] private float m_TargetSlotNitroToleranceMeters = 10f;

        [Header("Target Order Challenges")]
        [Tooltip("Arkadaki botların oyuncuya atak yapıp sonra geri düşmesi. Kapatılırsa hedef yine tutar ama yarış durgunlaşır.")]
        [SerializeField] private bool m_TargetChallengeEnabled = true;
        [Tooltip("Atakların başlayabileceği en erken yarış ilerlemesi.")]
        [SerializeField, Range(0f, 1f)] private float m_TargetChallengeStartProgress = 0.08f;
        [Tooltip("Atakların kesildiği ilerleme. Finale yaklaşırken yeni atak başlatılmaz.")]
        [SerializeField, Range(0f, 1f)] private float m_TargetChallengeEndProgress = 0.5f;
        [Tooltip("İki atak arasındaki en kısa süre.")]
        [SerializeField, Min(0f)] private float m_TargetChallengeIntervalMinSeconds = 2f;
        [Tooltip("İki atak arasındaki en uzun süre. Aralık geniş tutulursa ataklar öngörülemez olur.")]
        [SerializeField, Min(0f)] private float m_TargetChallengeIntervalMaxSeconds = 6f;
        [Tooltip("Aynı anda atak yapabilecek en fazla bot sayısı. Yüksek değer kalabalık ve karmaşık görünür.")]
        [SerializeField, Range(1, RivalCount)] private int m_TargetChallengeMaxConcurrent = 2;
        [Tooltip("Atak için oyuncuya en fazla bu kadar uzaktaki botlar seçilir (metre).")]
        [SerializeField, Min(1f)] private float m_TargetChallengeRangeMeters = 60f;
        [Tooltip("Atak yapan botun oyuncunun kaç metre önüne geçmeyi hedefleyeceği.")]
        [SerializeField, Min(0f)] private float m_TargetChallengeLeadMeters = 12f;
        [Tooltip("Botun önde kalma süresi. Bu süre bitince geri düşmeye başlar.")]
        [SerializeField, Min(0f)] private float m_TargetChallengeHoldSeconds = 2f;
        [Tooltip("Atağın başlaması için hedefe göre gereken en az güvenlik payı (saniye). Pay yoksa atak açılmaz, çünkü hedef sıra riske girer.")]
        [SerializeField, Min(0f)] private float m_TargetChallengeMinHeadroomSeconds = 2f;
        [Tooltip("Geri düşerken uygulanan tempo farkı. Büyük değer hızlı ama göze batan bir geri çekilme verir.")]
        [SerializeField, Range(0f, 0.25f)] private float m_TargetChallengeFallbackPace = 0.1f;

        [Header("Target Order Spacing")]
        [Tooltip("Arkadaki botlar arasında korunmaya çalışılan en az mesafe (metre). Arka grubun tek sıra hâlinde yapışmasını engeller.")]
        [SerializeField, Min(1f)] private float m_TargetBehindSeparationMeters = 6f;
        [Tooltip("Botların slot mesafelerinde yaptığı yavaş salınımın genliği (metre). Sabit aralıklı konvoy görüntüsünü kırar.")]
        [SerializeField, Min(0f)] private float m_TargetWanderMeters = 5f;
        [Tooltip("Salınımın en kısa periyodu (saniye).")]
        [SerializeField, Min(0.5f)] private float m_TargetWanderPeriodMinSeconds = 8f;
        [Tooltip("Salınımın en uzun periyodu (saniye). Botların salınımları farklı periyotlarda olduğu için hepsi aynı anda öne çıkmaz.")]
        [SerializeField, Min(0.5f)] private float m_TargetWanderPeriodMaxSeconds = 16f;
        [Tooltip("Tempo kapısındaki botların birbirine göre yayılma payı. Kapı içinde de sıralarına göre hafif farklı hız alırlar.")]
        [SerializeField, Range(0f, 0.9f)] private float m_TargetNearBandSpread = 0.3f;
        [Tooltip("İki rakip birbirine bu kadar yaklaşırsa ayrıştırma devreye girer (metre).")]
        [SerializeField, Min(0f)] private float m_TargetRivalSpacingMeters = 6f;
        [Tooltip("Yan yana kalan iki rakibin hız farkı bu değerin altındaysa kilitlenmiş sayılır (m/sn).")]
        [SerializeField, Min(0f)] private float m_TargetRivalStuckSpeed = 1.2f;
        [Tooltip("Ayrıştırma için uygulanan tempo farkı. Küçük tutulur ki müdahale görünmesin.")]
        [SerializeField, Range(0f, 0.2f)] private float m_TargetRivalSpacingPace = 0.04f;
        [Tooltip("İki rakip bu kadar süre yapışık kalırsa öndeki nitro kullanarak mücadeleyi çözer (saniye).")]
        [SerializeField, Min(0f)] private float m_TargetRivalGlueSeconds = 1f;
        [Tooltip("Start sonrası dağılma süresi. Bu süre boyunca botlar slot düzenine değil, yelpaze gibi açılmaya çalışır.")]
        [SerializeField, Min(0f)] private float m_TargetLaunchSeconds = 3f;
        [Tooltip("Start yelpazesinin genişliği. Sekiz aracın ilk saniyelerde birbirinden ayrılmasını sağlar.")]
        [SerializeField, Range(0f, 0.3f)] private float m_TargetLaunchSpread = 0.2f;
        [Tooltip("Açıkken öndeki botlar da slot sınırını aşacak nitroları kullanmaz.")]
        [SerializeField] private bool m_TargetAheadSlotNitroCap = true;

        [Header("Target Order Comebacks")]
        [Tooltip("Öndeki botların geri gelip oyuncuyla mücadele etmesi. Kapatılırsa öndeki botlar yarış boyunca uzakta kalır.")]
        [SerializeField] private bool m_TargetComebackEnabled = true;
        [Tooltip("Geri gelişlerin başlayabileceği en erken ilerleme.")]
        [SerializeField, Range(0f, 1f)] private float m_TargetComebackStartProgress = 0.1f;
        [Tooltip("Geri gelişlerin kesildiği ilerleme.")]
        [SerializeField, Range(0f, 1f)] private float m_TargetComebackEndProgress = 0.6f;
        [Tooltip("İki geri geliş arasındaki en kısa süre.")]
        [SerializeField, Min(0f)] private float m_TargetComebackIntervalMinSeconds = 3f;
        [Tooltip("İki geri geliş arasındaki en uzun süre.")]
        [SerializeField, Min(0f)] private float m_TargetComebackIntervalMaxSeconds = 8f;
        [Tooltip("Aynı anda geri gelebilecek en fazla bot sayısı.")]
        [SerializeField, Range(1, RivalCount)] private int m_TargetComebackMaxConcurrent = 1;
        [Tooltip("Geri gelmesi istenecek botun oyuncuya en fazla uzaklığı (metre).")]
        [SerializeField, Min(1f)] private float m_TargetComebackRangeMeters = 90f;
        [Tooltip("Geri gelişin başlaması için gereken en az güvenlik payı (saniye).")]
        [SerializeField, Min(0f)] private float m_TargetComebackMinMarginSeconds = 3f;
        [Tooltip("Geri gelen botun oyuncunun kaç metre arkasına kadar düşeceği.")]
        [SerializeField, Min(0f)] private float m_TargetComebackBehindMeters = 6f;
        [Tooltip("Geri gelen botun ardından yeniden öne geçerken hedefleyeceği fark (metre).")]
        [SerializeField, Min(0f)] private float m_TargetComebackLeadMeters = 20f;
        [Tooltip("Bot slotuna bu süre içinde dönemezse zorla serbest bırakılır. Takılıp kalmayı önleyen güvenlik valfi.")]
        [SerializeField, Min(1f)] private float m_TargetComebackReturnTimeoutSeconds = 16f;

        [Header("Target Order Chase")]
        [Tooltip("Oyuncu nitro bastıktan sonra yakın botların karşılık vermesi. Oyuncunun hamlesinin yarışta yankı bulmasını sağlar.")]
        [SerializeField] private bool m_TargetChaseEnabled = true;
        [Tooltip("Her taraftan kaç botun karşılık vereceği.")]
        [SerializeField, Range(0, RivalCount)] private int m_TargetChaseSlots = 2;
        [Tooltip("Karşılık verecek botun oyuncuya en fazla uzaklığı (metre).")]
        [SerializeField, Min(0f)] private float m_TargetChaseLagMeters = 20f;
        [Tooltip("Karşılığın en kısa gecikmesi. Sıfır olsaydı botlar oyuncuyla aynı karede basıp mekanik görünürdü.")]
        [SerializeField, Min(0f)] private float m_TargetChaseReactionMinSeconds = 0.3f;
        [Tooltip("Karşılığın en uzun gecikmesi. Aralık, botların tek tek tepki vermesini sağlar.")]
        [SerializeField, Min(0f)] private float m_TargetChaseReactionMaxSeconds = 0.9f;

        [Header("Telemetry")]
        [Tooltip("Bir geçişin geçerli sollama sayılması için önde kalınması gereken süre. Yan yana gidip duran araçların sollama olarak sayılmasını engeller.")]
        [SerializeField, Min(0f)] private float m_OvertakeConfirmSeconds = 0.5f;

        [Header("Balance")]
        [Tooltip("Serbest modda oyuncuya göre yumuşak tempo dengelemesi. Kapatılırsa rakipler oyuncudan tamamen bağımsız yarışır.")]
        [SerializeField] private bool m_BalanceEnabled = true;
        [Tooltip("Fark ölçümünün yumuşatma penceresi (saniye). Anlık nitro sıçramalarının dengelemeye yansımasını engeller.")]
        [SerializeField, Min(0.01f)] private float m_GapEmaSeconds = 4f;
        [Tooltip("Bu farkın altında hiç müdahale edilmez (metre). Oyuncunun kazandığı avantajın anında geri alınmamasını sağlayan asıl koruma.")]
        [SerializeField, Min(0f)] private float m_GapDeadZoneMeters = 25f;
        [Tooltip("Müdahalenin tam güce ulaştığı fark (metre). Bunun ötesinde daha fazla artmaz.")]
        [SerializeField, Min(0.01f)] private float m_GapSaturationMeters = 120f;
        [Tooltip("Dengelemenin bir bota uygulayabileceği en düşük hız çarpanı. 1'e yakın tutulur ki yavaşlama fark edilmesin.")]
        [SerializeField, Range(0.5f, 1f)] private float m_BalanceMinMultiplier = 0.98f;
        [Tooltip("Dengelemenin uygulayabileceği en yüksek hız çarpanı. Üst sınır, botun oyuncuya yapışmasını engeller.")]
        [SerializeField, Range(1f, 1.5f)] private float m_BalanceMaxMultiplier = 1.06f;
        [Tooltip("Çarpanın saniyede ne kadar değişebileceği. Düşük değer müdahaleyi zamana yayar.")]
        [SerializeField, Min(0.001f)] private float m_BalanceRatePerSecond = 0.02f;
        [Tooltip("Oyuncu bu süre boyunca hiç nitro basmazsa dengeleme devreden çıkar. Pasif oyunun kaybetmesi gerektiği için vardır.")]
        [SerializeField, Min(0f)] private float m_PassivePlayerGraceSeconds = 20f;

        [Header("References")]
        [Tooltip("Nitro sözleşmesi ve kaynak kuralları.")]
        [SerializeField] private BuffTableSO m_BuffTable;
        [Tooltip("Sekiz aracın prefab listesi.")]
        [SerializeField] private RaceCarCatalogSO m_CarCatalog;
        [Tooltip("Araç efektleri: rölanti dumanı, seyir alevi, nitro alevi.")]
        [SerializeField] private CarVfxCatalogSO m_CarVfx;
        [Tooltip("Nitro seviyelerinin görsel ve işitsel değerleri, kamera sarsıntısı ve kalkış geri çekilmesi.")]
        [SerializeField] private NitroLevelTableSO m_NitroLevels;
        [Tooltip("Motor sesi kademeleri ve karışım ayarları.")]
        [SerializeField] private EngineAudioTableSO m_EngineAudio;
        [Tooltip("Pistin kendi ortamı yoksa kullanılacak varsayılan ortam.")]
        [SerializeField] private RaceEnvironmentSO m_Environment;
        [Tooltip("Yol, zemin ve mesafe işaretleri için URP Lit. Graphics → Always Included listesindeki shader atanır; çalışma zamanında isimle aranmaz.")]
        [SerializeField] private Shader m_LitShader;
        [Tooltip("Nitro izi ve oyuncu işareti için URP Unlit. Graphics → Always Included listesindeki shader atanır; çalışma zamanında isimle aranmaz.")]
        [SerializeField] private Shader m_UnlitShader;
        [Tooltip("Yedi rakibin AI profilleri. Aynı profili birden çok rakibe vermek onları benzer davrandırır; case en az üç ayırt edilebilir profil ister.")]
        [SerializeField] private AiProfileSO[] m_RivalProfiles = new AiProfileSO[RivalCount];

        public float BaseSpeed => m_BaseSpeed < 0.1f ? 0.1f : m_BaseSpeed;
        public float DefaultRaceLengthMeters => m_RaceLengthMeters < 1f ? 1f : m_RaceLengthMeters;

        public float RaceLengthMeters
        {
            get
            {
                TrackLayoutSO track = TrackLayout;
                float trackLength = track != null ? track.RaceLengthMeters : 0f;
                return trackLength >= 1f ? trackLength : DefaultRaceLengthMeters;
            }
        }
        public float CountdownSeconds => m_CountdownSeconds < 0f ? 0f : m_CountdownSeconds;

        public float LaneWidthMeters => m_LaneWidthMeters < 0.5f ? 0.5f : m_LaneWidthMeters;
        public float RunoutMeters => m_RunoutMeters < 0f ? 0f : m_RunoutMeters;
        public float RoadShoulderMeters => m_RoadShoulderMeters < 0f ? 0f : m_RoadShoulderMeters;
        public float RoadWidthMeters => CarCount * LaneWidthMeters + 2f * RoadShoulderMeters;

        public float GetLaneOffset(int laneIndex)
        {
            return (laneIndex - (CarCount - 1) * 0.5f) * LaneWidthMeters * -ViewSideSign;
        }

        public TrackLayoutSO TrackLayout => m_ActiveTrack != null ? m_ActiveTrack : m_TrackLayout;
        public TrackLayoutSO DefaultTrackLayout => m_TrackLayout;
        public TrackCatalogSO TrackCatalog => m_TrackCatalog;

        public void UseTrack(TrackLayoutSO layout)
        {
            m_ActiveTrack = layout;
        }
        public bool HasTrackLayout => TrackLayout != null && TrackLayout.SegmentCount > 0;

        public bool CarsMoveLeftToRight => m_CarsMoveLeftToRight;
        public float ViewSideSign => m_CarsMoveLeftToRight ? 1f : -1f;

        public float CameraSideDistance => m_CameraSideDistance < 1f ? 1f : m_CameraSideDistance;
        public float CameraHeight => m_CameraHeight;
        public float CameraLookAhead => m_CameraLookAhead;
        public float CameraFollowSmoothing => m_CameraFollowSmoothing < 0.1f ? 0.1f : m_CameraFollowSmoothing;
        public float CameraFieldOfView => Mathf.Clamp(m_CameraFieldOfView, 20f, 90f);
        public float CameraBuffFovGain => Mathf.Clamp(m_CameraBuffFovGain, 0f, 12f);
        public float CameraPackBias => Mathf.Clamp01(m_CameraPackBias);
        public float CameraBuffPunch => Mathf.Clamp(m_CameraBuffPunch, 0f, 20f);
        public float CameraShakeScale => Mathf.Clamp(m_CameraShakeScale, 0f, 3f);
        public float CameraShakeFrequency => Mathf.Clamp(m_CameraShakeFrequency, 1f, 60f);
        public float CameraShakeKick => Mathf.Clamp(m_CameraShakeKick, 1f, 5f);
        public float CameraShakeKickDecay => m_CameraShakeKickDecay < 0.1f ? 0.1f : m_CameraShakeKickDecay;
        public float CameraShakeSustain => Mathf.Clamp01(m_CameraShakeSustain);
        public float CameraShakeRollPerMeter => Mathf.Clamp(m_CameraShakeRollPerMeter, 0f, 10f);

        public float LaneChangeSmoothing => m_LaneChangeSmoothing < 0.1f ? 0.1f : m_LaneChangeSmoothing;
        public float BankPerBuffLevel => m_BankPerBuffLevel;
        public float WheelRadiusMeters => m_WheelRadiusMeters < 0.05f ? 0.05f : m_WheelRadiusMeters;
        public float BuffTrailSeconds => m_BuffTrailSeconds < 0f ? 0f : m_BuffTrailSeconds;
        public float PlayerMarkerHeight => m_PlayerMarkerHeight < 0f ? 0f : m_PlayerMarkerHeight;

        public float FinishCoastDeceleration => m_FinishCoastDeceleration < 0.1f ? 0.1f : m_FinishCoastDeceleration;
        public float FinishCoastMinMeters => Mathf.Max(0f, m_FinishCoastMinMeters);
        public float FinishCoastMaxMeters => Mathf.Max(FinishCoastMinMeters, m_FinishCoastMaxMeters);
        public float FinishCoastSpreadMeters => Mathf.Max(0f, m_FinishCoastSpreadMeters);

        public int FixedStepHz => m_FixedStepHz < 1 ? 1 : m_FixedStepHz;
        public float FixedDeltaTime => 1f / FixedStepHz;

        public float TargetSeparationMeters => m_TargetSeparationMeters < 1f ? 1f : m_TargetSeparationMeters;
        public float TargetMarginStartMeters => m_TargetMarginStartMeters < 0f ? 0f : m_TargetMarginStartMeters;
        public float TargetMarginAheadEndMeters => Mathf.Max(m_TargetMarginAheadEndMeters, TargetMarginStartMeters);
        public float TargetMarginBehindEndMeters => Mathf.Max(m_TargetMarginBehindEndMeters, TargetMarginStartMeters);
        public float TargetAttackLockProgress => Mathf.Clamp01(m_TargetAttackLockProgress);
        public float TargetAttackLockGapMeters => m_TargetAttackLockGapMeters < 0f ? 0f : m_TargetAttackLockGapMeters;
        public float TargetMarginRampEnd => Mathf.Clamp(m_TargetMarginRampEnd, 0.05f, 1f);
        public float TargetSlotJitter => Mathf.Clamp(m_TargetSlotJitter, 0f, 0.6f);
        public float TargetPaceMin => Mathf.Clamp(m_TargetPaceMin, 0.5f, 1f);
        public float TargetPaceMax => Mathf.Clamp(m_TargetPaceMax, 1f, 1.6f);
        public float TargetPaceRatePerSecond => m_TargetPaceRatePerSecond < 0.01f ? 0.01f : m_TargetPaceRatePerSecond;
        public float TargetPlayerSpeedEmaSeconds => m_TargetPlayerSpeedEmaSeconds < 0.01f ? 0.01f : m_TargetPlayerSpeedEmaSeconds;
        public float TargetBiasResponse => m_TargetBiasResponse < 0.01f ? 0.01f : m_TargetBiasResponse;
        public float TargetEnvelopeTauStart => m_TargetEnvelopeTauStart < 0f ? 0f : m_TargetEnvelopeTauStart;
        public float TargetEnvelopeTauEnd => m_TargetEnvelopeTauEnd < 0f ? 0f : m_TargetEnvelopeTauEnd;
        public float TargetAheadSlackSeconds => m_TargetAheadSlackSeconds < 0f ? 0f : m_TargetAheadSlackSeconds;
        public float TargetBehindSlackSeconds => m_TargetBehindSlackSeconds < 0f ? 0f : m_TargetBehindSlackSeconds;
        public float TargetAheadCriticalSeconds => Mathf.Min(m_TargetAheadCriticalSeconds, TargetAheadSlackSeconds);
        public float TargetBehindCriticalSeconds => Mathf.Min(m_TargetBehindCriticalSeconds, TargetBehindSlackSeconds);
        public float TargetNearInnerMeters => m_TargetNearInnerMeters < 0f ? 0f : m_TargetNearInnerMeters;
        public float TargetNearOuterMeters => Mathf.Max(m_TargetNearOuterMeters, TargetNearInnerMeters + 0.01f);
        public float TargetNearPaceBand => Mathf.Clamp(m_TargetNearPaceBand, 0f, 0.2f);
        public float TargetCorrectionHorizonSeconds => m_TargetCorrectionHorizonSeconds < 0.5f ? 0.5f : m_TargetCorrectionHorizonSeconds;
        public bool TargetSlotNitroCap => m_TargetSlotNitroCap;
        public float TargetSlotNitroToleranceMeters => m_TargetSlotNitroToleranceMeters < 0f ? 0f : m_TargetSlotNitroToleranceMeters;

        public bool TargetChallengeEnabled => m_TargetChallengeEnabled;
        public float TargetChallengeStartProgress => Mathf.Clamp01(m_TargetChallengeStartProgress);
        public float TargetChallengeEndProgress => Mathf.Clamp(m_TargetChallengeEndProgress, TargetChallengeStartProgress, 1f);
        public float TargetChallengeIntervalMinSeconds => Mathf.Max(0f, Mathf.Min(m_TargetChallengeIntervalMinSeconds, m_TargetChallengeIntervalMaxSeconds));
        public float TargetChallengeIntervalMaxSeconds => Mathf.Max(0f, Mathf.Max(m_TargetChallengeIntervalMinSeconds, m_TargetChallengeIntervalMaxSeconds));
        public int TargetChallengeMaxConcurrent => Mathf.Clamp(m_TargetChallengeMaxConcurrent, 1, RivalCount);
        public float TargetChallengeRangeMeters => m_TargetChallengeRangeMeters < 1f ? 1f : m_TargetChallengeRangeMeters;
        public float TargetChallengeLeadMeters => m_TargetChallengeLeadMeters < 0f ? 0f : m_TargetChallengeLeadMeters;
        public float TargetChallengeHoldSeconds => m_TargetChallengeHoldSeconds < 0f ? 0f : m_TargetChallengeHoldSeconds;
        public float TargetChallengeMinHeadroomSeconds => m_TargetChallengeMinHeadroomSeconds < 0f ? 0f : m_TargetChallengeMinHeadroomSeconds;
        public float TargetChallengeFallbackPace => Mathf.Clamp(m_TargetChallengeFallbackPace, 0f, 0.25f);

        public float TargetBehindSeparationMeters => m_TargetBehindSeparationMeters < 1f ? 1f : m_TargetBehindSeparationMeters;
        public float TargetWanderMeters => m_TargetWanderMeters < 0f ? 0f : m_TargetWanderMeters;
        public float TargetWanderPeriodMinSeconds => Mathf.Max(0.5f, Mathf.Min(m_TargetWanderPeriodMinSeconds, m_TargetWanderPeriodMaxSeconds));
        public float TargetWanderPeriodMaxSeconds => Mathf.Max(0.5f, Mathf.Max(m_TargetWanderPeriodMinSeconds, m_TargetWanderPeriodMaxSeconds));
        public float TargetNearBandSpread => Mathf.Clamp(m_TargetNearBandSpread, 0f, 0.9f);
        public bool TargetAheadSlotNitroCap => m_TargetAheadSlotNitroCap;
        public float TargetRivalSpacingMeters => m_TargetRivalSpacingMeters < 0f ? 0f : m_TargetRivalSpacingMeters;
        public float TargetRivalStuckSpeed => m_TargetRivalStuckSpeed < 0f ? 0f : m_TargetRivalStuckSpeed;
        public float TargetRivalSpacingPace => Mathf.Clamp(m_TargetRivalSpacingPace, 0f, 0.2f);
        public float TargetRivalGlueSeconds => m_TargetRivalGlueSeconds < 0f ? 0f : m_TargetRivalGlueSeconds;
        public float TargetLaunchSeconds => m_TargetLaunchSeconds < 0f ? 0f : m_TargetLaunchSeconds;
        public float TargetLaunchSpread => Mathf.Clamp(m_TargetLaunchSpread, 0f, 0.3f);

        public bool TargetComebackEnabled => m_TargetComebackEnabled;
        public float TargetComebackStartProgress => Mathf.Clamp01(m_TargetComebackStartProgress);
        public float TargetComebackEndProgress => Mathf.Clamp(m_TargetComebackEndProgress, TargetComebackStartProgress, 1f);
        public float TargetComebackIntervalMinSeconds => Mathf.Max(0f, Mathf.Min(m_TargetComebackIntervalMinSeconds, m_TargetComebackIntervalMaxSeconds));
        public float TargetComebackIntervalMaxSeconds => Mathf.Max(0f, Mathf.Max(m_TargetComebackIntervalMinSeconds, m_TargetComebackIntervalMaxSeconds));
        public int TargetComebackMaxConcurrent => Mathf.Clamp(m_TargetComebackMaxConcurrent, 1, RivalCount);
        public float TargetComebackRangeMeters => m_TargetComebackRangeMeters < 1f ? 1f : m_TargetComebackRangeMeters;
        public float TargetComebackMinMarginSeconds => m_TargetComebackMinMarginSeconds < 0f ? 0f : m_TargetComebackMinMarginSeconds;
        public float TargetComebackBehindMeters => m_TargetComebackBehindMeters < 0f ? 0f : m_TargetComebackBehindMeters;
        public float TargetComebackLeadMeters => m_TargetComebackLeadMeters < 0f ? 0f : m_TargetComebackLeadMeters;
        public float TargetComebackReturnTimeoutSeconds => m_TargetComebackReturnTimeoutSeconds < 1f ? 1f : m_TargetComebackReturnTimeoutSeconds;

        public bool TargetChaseEnabled => m_TargetChaseEnabled;
        public int TargetChaseSlots => Mathf.Clamp(m_TargetChaseSlots, 0, RivalCount);
        public float TargetChaseLagMeters => m_TargetChaseLagMeters < 0f ? 0f : m_TargetChaseLagMeters;
        public float TargetChaseReactionMinSeconds => Mathf.Max(0f, Mathf.Min(m_TargetChaseReactionMinSeconds, m_TargetChaseReactionMaxSeconds));
        public float TargetChaseReactionMaxSeconds => Mathf.Max(0f, Mathf.Max(m_TargetChaseReactionMinSeconds, m_TargetChaseReactionMaxSeconds));

        public float OvertakeConfirmSeconds => m_OvertakeConfirmSeconds < 0f ? 0f : m_OvertakeConfirmSeconds;

        public bool BalanceEnabled => m_BalanceEnabled;
        public float GapEmaSeconds => m_GapEmaSeconds < 0.01f ? 0.01f : m_GapEmaSeconds;
        public float GapDeadZoneMeters => m_GapDeadZoneMeters < 0f ? 0f : m_GapDeadZoneMeters;
        public float GapSaturationMeters => Mathf.Max(m_GapSaturationMeters, GapDeadZoneMeters + 0.01f);
        public float BalanceMinMultiplier => Mathf.Clamp(m_BalanceMinMultiplier, 0.5f, 1f);
        public float BalanceMaxMultiplier => Mathf.Clamp(m_BalanceMaxMultiplier, 1f, 1.5f);
        public float BalanceRatePerSecond => m_BalanceRatePerSecond < 0.001f ? 0.001f : m_BalanceRatePerSecond;
        public float PassivePlayerGraceSeconds => m_PassivePlayerGraceSeconds < 0f ? 0f : m_PassivePlayerGraceSeconds;

        public BuffTableSO BuffTable => m_BuffTable;
        public RaceCarCatalogSO CarCatalog => m_CarCatalog;
        public CarVfxCatalogSO CarVfx => m_CarVfx;
        public NitroLevelTableSO NitroLevels => m_NitroLevels;
        public EngineAudioTableSO EngineAudio => m_EngineAudio;
        public RaceEnvironmentSO Environment => m_Environment;
        public Shader LitShader => m_LitShader;
        public Shader UnlitShader => m_UnlitShader;
        public AiProfileSO[] RivalProfiles => m_RivalProfiles;

        public int BuffWindowSteps => m_BuffTable != null ? m_BuffTable.GetWindowSteps(FixedStepHz) : FixedStepHz;

        public float BaselineRaceSeconds => RaceLengthMeters / BaseSpeed;

        public bool TryGetRivalProfile(int rivalIndex, out AiProfileSO profile)
        {
            profile = null;
            if (m_RivalProfiles == null || rivalIndex < 0 || rivalIndex >= m_RivalProfiles.Length)
                return false;

            profile = m_RivalProfiles[rivalIndex];
            return profile != null;
        }
    }
}
