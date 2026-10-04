using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Purely visual: the slot's character, the slot ring and the carrier indicator. Reads state, never changes it.
    /// Each slot shows its character (<see cref="PlayerIdentity.CharacterFor"/>, swapped in when the slot is set) and a
    /// ring at the feet in the slot colour with the slot's shape on it; the indicator bobs above the carrier's head in
    /// the carrier yellow, shaped like the carrier's slot (<see cref="PlayerShapeMesh"/>), so neither "who has the bomb"
    /// nor "which player" depends on colour alone (spec §19).
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerPresentation : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Ink = new Color32(0x24, 0x30, 0x5E, 0xFF);
        const float LightRing = 0.35f;   // above this luminance the shape on the ring is ink, else white (UIParts.Chip)

        [SerializeField, Tooltip("Where the slot's character sits (Visual).")] Transform visualRoot;
        [SerializeField, Tooltip("The character shown now (the prefab's default until the slot is set).")] PlayerCharacter character;
        [SerializeField, Tooltip("The disc at the feet, in the slot colour.")] Renderer slotRing;
        [SerializeField, Tooltip("The slot's shape lying on the ring.")] MeshFilter slotRingShape;
        [SerializeField] Transform carrierIndicator;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 4f;
        [SerializeField] float spinSpeed = 120f;

        Player player;
        BombController bomb;
        MeshFilter indicatorMesh;
        MaterialPropertyBlock block;
        Vector3 indicatorBase;
        float indicatorBaseScale;
        float catchPulse;
        bool localView;

        /// <summary>The mesh the carrier indicator shows (the slot's shape).</summary>
        public Mesh IndicatorMesh => indicatorMesh != null ? indicatorMesh.sharedMesh : null;

        /// <summary>The character the player shows.</summary>
        public PlayerCharacter Character => character;

        public Renderer SlotRing => slotRing;
        public MeshFilter SlotRingShape => slotRingShape;

        void Awake()
        {
            player = GetComponent<Player>();
            block = new MaterialPropertyBlock();
            if (carrierIndicator != null)
            {
                indicatorBase = carrierIndicator.localPosition;
                indicatorBaseScale = carrierIndicator.localScale.x;
                indicatorMesh = carrierIndicator.GetComponent<MeshFilter>();
                carrierIndicator.gameObject.SetActive(false);
            }
            ApplyIdentity();
        }

        void Update()
        {
            if (carrierIndicator == null || !carrierIndicator.gameObject.activeSelf) return;
            catchPulse = Mathf.MoveTowards(catchPulse, 0f, Time.deltaTime * 3f);
            carrierIndicator.localPosition = indicatorBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            carrierIndicator.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            carrierIndicator.localScale = Vector3.one * (indicatorBaseScale * (1f + catchPulse * 0.8f));
        }

        /// <summary>
        /// The player the camera follows must not see their own body (no model hands for now, only the bomb).
        /// Their shadow is kept so they still feel grounded. Remembered, so a character swapped in later follows it.
        /// </summary>
        public void SetLocalView(bool isLocal)
        {
            localView = isLocal;
            var mode = isLocal ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<BombController>() != null) continue;   // never hide the held bomb
                if (r is ParticleSystemRenderer) continue;                         // your own dust stays visible
                if (r == slotRing || (slotRingShape != null && r.gameObject == slotRingShape.gameObject))
                {
                    r.enabled = !isLocal;                                          // your own ring would sit under the camera
                    continue;
                }
                r.shadowCastingMode = mode;
            }
        }

        public void Bind(BombController bombController)
        {
            Unbind();
            bomb = bombController;
            bomb.CarrierChanged += OnCarrierChanged;
            bomb.BombCaught += OnBombCaught;
            // The carrier may have been set before we bound (a player who joins mid-run).
            SetCarrier(bomb.Carrier == player);
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb == null) return;
            bomb.CarrierChanged -= OnCarrierChanged;
            bomb.BombCaught -= OnBombCaught;
        }

        void OnCarrierChanged(Player carrier) => SetCarrier(carrier == player);

        void OnBombCaught(Player receiver)
        {
            if (receiver == player) PulseCatch();
        }

        /// <summary>The slot's character, its colour and shape on the ring, and its shape on the carrier indicator.</summary>
        public void ApplyIdentity()
        {
            if (player == null) return;
            if (indicatorMesh != null) indicatorMesh.sharedMesh = PlayerShapeMesh.For(player.Shape);
            ShowCharacter(PlayerIdentity.CharacterFor(player.Tuning, player.PlayerId));
            block ??= new MaterialPropertyBlock();
            if (slotRing != null)
            {
                var disc = slotRing.GetComponent<MeshFilter>();
                if (disc != null) disc.sharedMesh = PlayerShapeMesh.Flat(PlayerShape.Circle);   // runtime meshes: never saved in the prefab
                slotRing.GetPropertyBlock(block);
                block.SetColor(BaseColor, player.Color);
                slotRing.SetPropertyBlock(block);
            }
            if (slotRingShape != null)
            {
                slotRingShape.sharedMesh = PlayerShapeMesh.Flat(player.Shape);
                var r = slotRingShape.GetComponent<Renderer>();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, UIParts.Luminance(player.Color) > LightRing ? Ink : Color.white);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Swaps in <paramref name="prefab"/> unless it already shows; the animator follows it.</summary>
        void ShowCharacter(PlayerCharacter prefab)
        {
            if (prefab != null && visualRoot != null && (character == null || character.Id != prefab.Id))
            {
                if (character != null)
                {
                    character.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(character.gameObject);
                    else DestroyImmediate(character.gameObject);
                }
                character = Instantiate(prefab, visualRoot, false);
                character.name = prefab.name;
                SetLayer(character.transform, gameObject.layer);
                SetLocalView(localView);
            }
            var driver = GetComponent<PlayerAnimator>();   // Player.Awake may not have run yet
            if (driver != null && character != null) driver.Bind(character);
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        public void SetCarrier(bool isCarrier)
        {
            if (carrierIndicator != null) carrierIndicator.gameObject.SetActive(isCarrier);
        }

        /// <summary>Quick pop on the indicator so a fresh catch is unmistakable.</summary>
        public void PulseCatch() => catchPulse = 1f;
    }
}
