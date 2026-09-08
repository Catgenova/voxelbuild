using UnityEngine;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.Sim;

    /// <summary>Unity presentation of a <see cref="ColonistCore"/>: a capsule body, a name label and a carried-goods marker.</summary>
    public sealed class ColonistView : MonoBehaviour
    {
        public const int ColonistLayer = 7;

        public ColonistCore Core { get; private set; }

        /// <summary>Body dimensions in metres, independent of block size.</summary>
        public const float BodyHeight = 1.7f;
        public const float BodyWidth = 0.45f;

        private WorldRenderer worldRenderer;
        private Transform body;
        private Transform pack;
        private TextMesh label;
        private Renderer[] renderers;
        private Camera cam;
        private float blockSize;

        public void Init(ColonistCore core, WorldRenderer worldRenderer, Font font)
        {
            Core = core;
            this.worldRenderer = worldRenderer;
            blockSize = worldRenderer.BlockSize;
            gameObject.layer = ColonistLayer;
            name = "Colonist " + core.Name;

            float height = BodyHeight;
            float width = BodyWidth;

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(capsule.GetComponent<Collider>());
            capsule.name = "Body";
            capsule.layer = ColonistLayer;
            body = capsule.transform;
            body.SetParent(transform, false);
            body.localScale = new Vector3(width, height * 0.5f, width);
            body.localPosition = new Vector3(0f, height * 0.5f, 0f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial =
                BlockAtlas.CreateSolidMaterial("Colonist " + core.Name, BlockAtlas.ToColor(core.Color), 0.4f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.name = "Head";
            head.layer = ColonistLayer;
            head.transform.SetParent(body, false);
            head.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            head.GetComponent<MeshRenderer>().sharedMaterial =
                BlockAtlas.CreateSolidMaterial("Skin", new Color(0.9f, 0.75f, 0.62f), 0.3f);

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(nose.GetComponent<Collider>());
            nose.name = "Nose";
            nose.layer = ColonistLayer;
            nose.transform.SetParent(head.transform, false);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.4f);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = head.GetComponent<MeshRenderer>().sharedMaterial;

            var packGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(packGo.GetComponent<Collider>());
            packGo.name = "Pack";
            packGo.layer = ColonistLayer;
            pack = packGo.transform;
            pack.SetParent(body, false);
            pack.localScale = new Vector3(0.8f, 0.35f, 0.5f);
            pack.localPosition = new Vector3(0f, 0.1f, -0.7f);
            packGo.GetComponent<MeshRenderer>().sharedMaterial =
                BlockAtlas.CreateSolidMaterial("Pack", new Color(0.45f, 0.3f, 0.18f), 0.2f);

            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.height = height;
            collider.radius = width * 0.5f;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, height + 0.25f, 0f);
            label = labelGo.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 48;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.text = core.Name;
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (font != null && font.material != null) labelRenderer.sharedMaterial = font.material;

            renderers = GetComponentsInChildren<Renderer>();
            cam = Camera.main;
            Sync();
        }

        private void Update()
        {
            Sync();
        }

        private void Sync()
        {
            var m = Core.Mover;
            transform.position = new Vector3(m.PosX, m.PosY, m.PosZ) * blockSize;
            var facing = new Vector3(Core.FacingX, 0f, Core.FacingZ);
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            // Lie down when sleeping.
            float targetTilt = Core.IsSleeping ? 80f : 0f;
            var e = body.localEulerAngles;
            float tilt = Mathf.MoveTowardsAngle(e.x, targetTilt, Time.unscaledDeltaTime * 240f);
            body.localEulerAngles = new Vector3(tilt, 0f, 0f);
            body.localPosition = Core.IsSleeping
                ? new Vector3(0f, BodyWidth * 0.5f, 0f)
                : new Vector3(0f, BodyHeight * 0.5f, 0f);

            pack.gameObject.SetActive(!Core.Inventory.IsEmpty);

            bool visible = Core.Cell.y <= worldRenderer.SliceY;
            foreach (var r in renderers) r.enabled = visible;

            if (cam == null) cam = Camera.main;
            if (cam != null && label != null)
            {
                label.transform.rotation = cam.transform.rotation;
                label.text = Core.Name;
            }
        }

        public Bounds Bounds => new Bounds(transform.position + Vector3.up * (BodyHeight * 0.5f), new Vector3(BodyWidth, BodyHeight, BodyWidth));
    }
}
