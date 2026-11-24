
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIDustParticles : MonoBehaviour
{
    [Header("Dust Amount")]
    [Range(10, 900)]
    public int particleCount = 500;

    [Header("Particle Size")]
    [Range(0.5f, 10f)]
    public float minSize = 2.5f;

    [Range(0.5f, 14f)]
    public float maxSize = 7f;

    [Header("Floating Motion")]
    [Range(0.05f, 8f)]
    public float minSpeed = 0.4f;

    [Range(0.1f, 12f)]
    public float maxSpeed = 1.8f;

    [Range(0f, 5f)]
    public float wobbleAmount = 0.5f;

    [Range(0.05f, 3f)]
    public float wobbleSpeed = 0.35f;

    [Header("Appearance")]
    [Range(0.01f, 1f)]
    public float minOpacity = 0.12f;

    [Range(0.01f, 1f)]
    public float maxOpacity = 0.55f;

    [Range(0.1f, 5f)]
    public float fadeTime = 2.2f;

    [Header("Drift Direction")]
    [Tooltip("Shared drift direction. Leave at zero for still air, where every speck wanders on its own slow heading.")]
    public Vector2 windDirection = new Vector2(-0.3f, 1f);

    [Header("Optional")]
    public bool randomizeOnStart = true;
    public bool useUnscaledTime = true;

    private RectTransform area;
    private readonly List<DustParticle> particles =
        new List<DustParticle>();

    private Sprite dustSprite;
    private Texture2D dustTexture;
    private Vector2 normalizedWind;

    private class DustParticle
    {
        public RectTransform rect;
        public Image image;

        public Vector2 position;
        public Vector2 velocity;

        public float size;
        public float opacity;
        public float age;
        public float lifetime;

        public float phase;
        public float wobble;
    }

    private void Awake()
    {
        area = GetComponent<RectTransform>();

        CreateDustTexture();

        // Zero means still air, not a constant upward wind: the dust should hover where it is.
        normalizedWind = windDirection.sqrMagnitude > 0.001f
            ? windDirection.normalized
            : Vector2.zero;
    }

    private void Start()
    {
        CreateParticles();
    }

    private void Update()
    {
        if (area == null || particles.Count == 0)
            return;

        float dt = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        Rect bounds = area.rect;

        foreach (DustParticle p in particles)
        {
            p.age += dt;

            // Respawn particles after their lifetime expires.
            if (p.age >= p.lifetime)
            {
                ResetParticle(p, bounds, false);
                continue;
            }

            float time = useUnscaledTime
                ? Time.unscaledTime
                : Time.time;

            // Natural floating movement with gentle turbulence: two sines that do not share a
            // period, so the path wanders instead of repeating a clean loop.
            float wave = time * wobbleSpeed + p.phase;

            Vector2 drift = new Vector2(
                Mathf.Sin(wave) * p.wobble
                    + Mathf.Sin(wave * 0.37f + p.phase * 1.7f) * p.wobble * 0.5f,
                Mathf.Cos(wave * 0.73f + p.phase) * p.wobble
            );

            // The air turns each mote by a degree or so a second, so its path curves. A straight
            // line is what made the old specks read as a starfield drifting past.
            float turn = Mathf.Sin(time * 0.17f + p.phase) * 0.12f * dt;
            float turnCos = Mathf.Cos(turn);
            float turnSin = Mathf.Sin(turn);

            p.velocity = new Vector2(
                p.velocity.x * turnCos - p.velocity.y * turnSin,
                p.velocity.x * turnSin + p.velocity.y * turnCos
            );

            p.position += (p.velocity + drift) * dt;

            // A speck that drifts out of the field is retired once it is fully out of sight and
            // fades back in somewhere else. Wrapping it to the opposite edge would snap it across
            // the screen in full view.
            float margin = p.size * 2f;

            if (p.position.x < bounds.xMin - margin || p.position.x > bounds.xMax + margin
                || p.position.y < bounds.yMin - margin || p.position.y > bounds.yMax + margin)
            {
                ResetParticle(p, bounds, false);
                continue;
            }

            p.rect.anchoredPosition = p.position;

            // Fade in and out smoothly.
            float fadeIn = Mathf.Clamp01(p.age / fadeTime);

            float fadeOut = Mathf.Clamp01(
                (p.lifetime - p.age) / fadeTime
            );

            // Smoothstep prevents abrupt opacity changes.
            fadeIn = fadeIn * fadeIn * (3f - 2f * fadeIn);
            fadeOut = fadeOut * fadeOut * (3f - 2f * fadeOut);

            float alpha = Mathf.Min(fadeIn, fadeOut) * p.opacity;

            p.image.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void CreateParticles()
    {
        Rect bounds = area.rect;

        for (int i = 0; i < particleCount; i++)
        {
            GameObject obj = new GameObject(
                "Dust Speck",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            obj.layer = gameObject.layer;
            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            Image img = obj.GetComponent<Image>();
            img.sprite = dustSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;

            DustParticle p = new DustParticle
            {
                rect = rt,
                image = img
            };

            particles.Add(p);

            ResetParticle(p, bounds, true);
        }
    }

    private void ResetParticle(
        DustParticle p,
        Rect bounds,
        bool initialSpawn
    )
    {
        // Small motes catch the light while the big ones read as out of focus, so size and
        // brightness pull against each other instead of every speck being an equal bright dot.
        float focus = Random.value;
        p.size = Mathf.Lerp(minSize, maxSize, focus * focus);
        p.opacity = Mathf.Lerp(maxOpacity, minOpacity, focus) * Random.Range(0.7f, 1f);

        p.lifetime = Random.Range(12f, 30f);
        p.age = initialSpawn && randomizeOnStart
            ? Random.Range(0f, p.lifetime)
            : 0f;

        p.phase = Random.Range(0f, Mathf.PI * 2f);
        p.wobble = Random.Range(0.1f, wobbleAmount);

        // Each speck receives its own slightly different trajectory. With no wind the field is
        // left still, so every speck wanders on its own slow heading instead of streaming toward
        // one edge.
        Vector2 wind = normalizedWind;

        if (wind.sqrMagnitude > 0.001f)
        {
            Vector2 sideways = new Vector2(-wind.y, wind.x);

            p.velocity =
                wind * Random.Range(minSpeed, maxSpeed) +
                sideways * Random.Range(-maxSpeed * 0.3f,
                                         maxSpeed * 0.3f);
        }
        else
        {
            float heading = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(minSpeed, maxSpeed);

            p.velocity = new Vector2(
                Mathf.Cos(heading) * speed,
                Mathf.Sin(heading) * speed
            );
        }

        // Specks are scattered over the whole field on every spawn. Pinning respawns to the edge
        // opposite the wind is what drained the rest of the screen and left the dust banked up
        // along one side.
        p.position = new Vector2(
            Random.Range(bounds.xMin, bounds.xMax),
            Random.Range(bounds.yMin, bounds.yMax)
        );

        p.rect.sizeDelta = new Vector2(p.size, p.size);
        p.rect.anchoredPosition = p.position;
        p.rect.localRotation = Quaternion.identity;

        // Start invisible; the normal fade logic reveals it.
        p.image.color = new Color(1f, 1f, 1f, 0f);
    }

    private void CreateDustTexture()
    {
        const int resolution = 32;

        dustTexture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );

        dustTexture.name = "Procedural Soft Dust";
        dustTexture.filterMode = FilterMode.Bilinear;
        dustTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[resolution * resolution];

        // A soft, uneven mote: one noise layer bends the outline, a finer one grains it. The
        // falloff is a wide gaussian with no hard rim and no pinpoint of full white, which is what
        // made the old sprite read as a sharp star.
        float center = (resolution - 1) * 0.5f;
        float radius = resolution * 0.47f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float nx = (x - center) / radius;
                float ny = (y - center) / radius;

                float warp = Mathf.PerlinNoise(
                    nx * 2.1f + 31.7f,
                    ny * 2.1f + 9.2f
                ) - 0.5f;

                float grain = Mathf.PerlinNoise(
                    nx * 6.4f + 4.1f,
                    ny * 6.4f + 18.3f
                ) - 0.5f;

                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float shape = distance / (1f + warp * 0.45f);

                float core = Mathf.Exp(-shape * shape * 4.2f);
                float halo = Mathf.Exp(-shape * shape * 1.1f);

                float alpha = Mathf.Clamp01(
                    (core * 0.34f + halo * 0.30f) * (0.78f + grain * 0.44f)
                ) * 0.9f;

                // Take the last of the haze out at the square's edge, so a large mote never
                // shows the bounds of its texture.
                alpha *= 1f - Mathf.SmoothStep(0.7f, 1f, distance);

                pixels[y * resolution + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        dustTexture.SetPixels(pixels);
        dustTexture.Apply(false, true);

        dustSprite = Sprite.Create(
            dustTexture,
            new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            resolution
        );

        dustSprite.name = "Procedural Dust Sprite";
    }

    private void OnDestroy()
    {
        foreach (DustParticle p in particles)
        {
            if (p.rect != null)
                Destroy(p.rect.gameObject);
        }

        particles.Clear();

        if (dustSprite != null)
            Destroy(dustSprite);

        if (dustTexture != null)
            Destroy(dustTexture);
    }

    private void OnValidate()
    {
        minSize = Mathf.Min(minSize, maxSize);
        minSpeed = Mathf.Min(minSpeed, maxSpeed);
        minOpacity = Mathf.Min(minOpacity, maxOpacity);
        fadeTime = Mathf.Max(0.1f, fadeTime);
        wobbleSpeed = Mathf.Max(0.1f, wobbleSpeed);
    }
}
