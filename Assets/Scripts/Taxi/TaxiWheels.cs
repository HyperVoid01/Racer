using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spins the taxi's wheels with road speed, steers the front wheels into lane changes and keeps the wheels
/// planted while <see cref="TaxiFeel"/> leans, squats and dives the body. Added automatically by TaxiController.
///
/// The taxi models store all four wheels as ONE mesh (e.g. "OldRims" / "NewRims") on an object with a
/// non-uniform import scale, so the wheels can't be rotated as authored. When a model is spawned this component
/// splits that mesh into one mesh per wheel (by corner of the car), bakes the scale away and gives each wheel
/// its own pivot at the hub. The original wheel renderer is hidden; nothing in the assets is modified.
///
/// The wheel FBX files need Read/Write enabled for this to work in player builds (the editor can always read).
/// </summary>
[DefaultExecutionOrder(100)] // After TaxiFeel, so the wheels follow this frame's body pose.
public class TaxiWheels : MonoBehaviour
{
    [Header("Finding the wheels")]
    [Tooltip("A mesh is treated as wheels if its name contains any of these (case-insensitive).")]
    [SerializeField] private string[] wheelMeshKeywords = { "Rim", "Wheel" };
    [Tooltip("...unless its name also contains one of these.")]
    [SerializeField] private string[] excludeKeywords = { "NoWheel", "WithWheel" };

    [Tooltip("Hide any part of the body mesh that sits entirely inside a wheel. Some body exports still contain " +
             "wheels (TaxiNoWheel.fbx has two), which would otherwise show as a second, frozen set.")]
    [SerializeField] private bool removeWheelsBakedIntoBody = true;
    [Tooltip("How far (as a fraction of the wheel radius) a body part may poke out of a wheel and still be removed.")]
    [SerializeField] private float bakedWheelTolerance = 0.15f;

    [Header("Spin")] [Tooltip("Flip if the wheels roll backwards.")] 
    [SerializeField] private bool invertSpin = true;
    [Tooltip("Visual cap on spin (degrees/s). Above roughly half a spoke per frame the rims strobe and look like " +
             "they spin backwards, so very high speeds are clamped.")]
    [SerializeField] private float maxSpinDegreesPerSecond = 1800f;
    [Tooltip("How fast free-spinning wheels slow down after a crash (units/s²).")]
    [SerializeField] private float crashSpinDown = 25f;

    [Header("Steering")]
    [SerializeField] private float maxSteerAngle = 22f;
    [SerializeField] private float steerResponse = 12f;

    [Header("Body")]
    [Tooltip("0 = wheels stay level while the body rolls and pitches (suspension). 1 = wheels lean with the body.")]
    [Range(0f, 1f)]
    [SerializeField] private float followBodyLean = 0.2f;

    private sealed class Wheel
    {
        public Transform Hub;     // Pivot at the wheel centre; steering yaw.
        public Transform Spinner; // Child of the hub; rolling rotation.
        public float Radius;
        public float Angle;
        public bool IsFront;
    }

    private readonly List<Wheel> _wheels = new List<Wheel>(4);
    private readonly List<Mesh> _createdMeshes = new List<Mesh>(4);
    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>(1);
    private readonly List<Bounds> _wheelBounds = new List<Bounds>(4); // Rig space.
    private readonly List<(MeshFilter filter, Mesh original)> _cleanedBodies = new List<(MeshFilter, Mesh)>(1);

    private TaxiVisuals _visuals;
    private TaxiController _taxi;
    private Transform _rig;
    private float _wheelSpeed;
    private float _steer;

    // Corner indices: bit 0 = right side, bit 1 = front.
    private static readonly string[] CornerNames = { "Wheel RL", "Wheel RR", "Wheel FL", "Wheel FR" };

    private void Awake()
    {
        _visuals = GetComponent<TaxiVisuals>();
        _taxi = GetComponentInParent<TaxiController>();
    }

    private void OnEnable()
    {
        if (_visuals == null) return;
        _visuals.ModelChanged += Rebuild;
        if (_visuals.CurrentModel != null) Rebuild();
    }

    private void OnDisable()
    {
        if (_visuals != null) _visuals.ModelChanged -= Rebuild;
    }

    private void OnDestroy()
    {
        Clear();
    }

    // ------------------------------------------------------------------ Build

    private void Rebuild()
    {
        Clear();

        GameObject model = _visuals != null ? _visuals.CurrentModel : null;
        if (model == null) return;

        var sources = new List<MeshFilter>(1);
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !IsWheelMesh(mesh.name)) continue;
            if (!filter.TryGetComponent(out MeshRenderer _)) continue;

            if (!mesh.isReadable && !Application.isEditor)
            {
                Debug.LogWarning($"TaxiWheels: enable Read/Write on the import settings of '{mesh.name}' " +
                                 "so the wheels can spin in builds.", filter);
                continue;
            }

            sources.Add(filter);
        }

        if (sources.Count == 0) return;

        // The rig sits next to the body (not inside it) so TaxiFeel's lean doesn't tip the wheels.
        Transform body = transform;
        Transform parent = body.parent != null ? body.parent : body;

        _rig = new GameObject("Wheels (runtime)").transform;
        _rig.SetParent(parent, false);
        if (parent != body)
        {
            _rig.localPosition = body.localPosition;
            _rig.localRotation = body.localRotation;
            _rig.localScale = body.localScale;
        }

        BuildWheels(sources);

        // Only hide the originals once the replacements exist.
        if (_wheels.Count == 0)
        {
            Clear();
            return;
        }

        foreach (MeshFilter source in sources)
        {
            var renderer = source.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            _hiddenRenderers.Add(renderer);
        }

        if (removeWheelsBakedIntoBody) RemoveWheelsBakedIntoBody(model, sources);

        SyncRigToBody();
    }

    private bool IsWheelMesh(string meshName)
    {
        foreach (string exclude in excludeKeywords)
        {
            if (!string.IsNullOrEmpty(exclude) &&
                meshName.IndexOf(exclude, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        }

        foreach (string keyword in wheelMeshKeywords)
        {
            if (!string.IsNullOrEmpty(keyword) &&
                meshName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>Vertex data of one source mesh, transformed into rig space.</summary>
    private sealed class BakedSource
    {
        public MeshFilter Filter;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector4[] Tangents;
        public Vector2[] Uv0;
        public Vector2[] Uv1;
        public Color[] Colors;
        public int[][] SubmeshTriangles;
    }

    private void BuildWheels(List<MeshFilter> sources)
    {
        Matrix4x4 rigFromWorld = _rig.worldToLocalMatrix;

        // 1. Bake every source into rig space and find the centre of all the wheels together.
        var baked = new List<BakedSource>(sources.Count);
        var allBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshFilter filter in sources)
        {
            BakedSource source = Bake(filter, rigFromWorld * filter.transform.localToWorldMatrix);
            baked.Add(source);

            foreach (Vector3 p in source.Positions)
            {
                if (!hasBounds) { allBounds = new Bounds(p, Vector3.zero); hasBounds = true; }
                else allBounds.Encapsulate(p);
            }
        }

        if (!hasBounds) return;
        Vector3 centre = allBounds.center;

        // 2. Sort triangles into the four corners of the car (rig space: +X right, +Z forward).
        //    corner -> source index -> submesh -> triangle indices (into that source's arrays)
        var cornerTriangles = new List<int>[4][][];
        var cornerBounds = new Bounds[4];
        var cornerHasBounds = new bool[4];

        for (int c = 0; c < 4; c++)
        {
            cornerTriangles[c] = new List<int>[baked.Count][];
            for (int s = 0; s < baked.Count; s++)
            {
                int submeshCount = baked[s].SubmeshTriangles.Length;
                cornerTriangles[c][s] = new List<int>[submeshCount];
                for (int m = 0; m < submeshCount; m++) cornerTriangles[c][s][m] = new List<int>();
            }
        }

        for (int s = 0; s < baked.Count; s++)
        {
            BakedSource source = baked[s];
            for (int m = 0; m < source.SubmeshTriangles.Length; m++)
            {
                int[] tris = source.SubmeshTriangles[m];
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 a = source.Positions[tris[t]];
                    Vector3 b = source.Positions[tris[t + 1]];
                    Vector3 d = source.Positions[tris[t + 2]];
                    Vector3 centroid = (a + b + d) / 3f;

                    int corner = (centroid.x >= centre.x ? 1 : 0) | (centroid.z >= centre.z ? 2 : 0);
                    List<int> list = cornerTriangles[corner][s][m];
                    list.Add(tris[t]);
                    list.Add(tris[t + 1]);
                    list.Add(tris[t + 2]);

                    if (!cornerHasBounds[corner])
                    {
                        cornerBounds[corner] = new Bounds(a, Vector3.zero);
                        cornerHasBounds[corner] = true;
                    }
                    cornerBounds[corner].Encapsulate(a);
                    cornerBounds[corner].Encapsulate(b);
                    cornerBounds[corner].Encapsulate(d);
                }
            }
        }

        // 3. One hub per corner, pivoted at the wheel centre, with the corner's pieces under a spinner.
        for (int corner = 0; corner < 4; corner++)
        {
            if (!cornerHasBounds[corner]) continue;

            Bounds bounds = cornerBounds[corner];
            Vector3 hubCentre = bounds.center;

            var hub = new GameObject(CornerNames[corner]).transform;
            hub.SetParent(_rig, false);
            hub.localPosition = hubCentre;

            var spinner = new GameObject("Spin").transform;
            spinner.SetParent(hub, false);

            for (int s = 0; s < baked.Count; s++)
            {
                Mesh mesh = BuildCornerMesh(baked[s], cornerTriangles[corner][s], hubCentre,
                                            $"{baked[s].Filter.sharedMesh.name} {CornerNames[corner]}");
                if (mesh == null) continue;
                _createdMeshes.Add(mesh);
                CreateRendererObject(mesh, baked[s].Filter, spinner);
            }

            // Axle runs along X, so the tyre's radius shows in Y and Z.
            float radius = (bounds.extents.y + bounds.extents.z) * 0.5f;
            _wheelBounds.Add(bounds);

            _wheels.Add(new Wheel
            {
                Hub = hub,
                Spinner = spinner,
                Radius = Mathf.Max(0.05f, radius),
                IsFront = (corner & 2) != 0
            });
        }
    }

    private static BakedSource Bake(MeshFilter filter, Matrix4x4 toRig)
    {
        Mesh mesh = filter.sharedMesh;
        Matrix4x4 normalMatrix = toRig.inverse.transpose;

        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;

        for (int i = 0; i < positions.Length; i++) positions[i] = toRig.MultiplyPoint3x4(positions[i]);

        for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;

        for (int i = 0; i < tangents.Length; i++)
        {
            Vector4 t = tangents[i];
            Vector3 dir = toRig.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
            tangents[i] = new Vector4(dir.x, dir.y, dir.z, t.w);
        }

        var submeshTriangles = new int[mesh.subMeshCount][];
        for (int m = 0; m < mesh.subMeshCount; m++)
        {
            // Points/lines can't be wheels; only triangles are rebuilt.
            submeshTriangles[m] = mesh.GetTopology(m) == MeshTopology.Triangles
                ? mesh.GetTriangles(m)
                : System.Array.Empty<int>();
        }

        return new BakedSource
        {
            Filter = filter,
            Positions = positions,
            Normals = normals,
            Tangents = tangents,
            Uv0 = mesh.uv,
            Uv1 = mesh.uv2,
            Colors = mesh.colors,
            SubmeshTriangles = submeshTriangles
        };
    }

    private static Mesh BuildCornerMesh(BakedSource source, List<int>[] submeshes, Vector3 hubCentre, string name)
    {
        var remap = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        var colors = new List<Color>();

        bool hasNormals = source.Normals.Length == source.Positions.Length;
        bool hasTangents = source.Tangents.Length == source.Positions.Length;
        bool hasUv0 = source.Uv0.Length == source.Positions.Length;
        bool hasUv1 = source.Uv1.Length == source.Positions.Length;
        bool hasColors = source.Colors.Length == source.Positions.Length;

        var newSubmeshes = new List<int>[submeshes.Length];
        int triangleCount = 0;

        for (int m = 0; m < submeshes.Length; m++)
        {
            var indices = new List<int>(submeshes[m].Count);
            foreach (int oldIndex in submeshes[m])
            {
                if (!remap.TryGetValue(oldIndex, out int newIndex))
                {
                    newIndex = positions.Count;
                    remap.Add(oldIndex, newIndex);

                    positions.Add(source.Positions[oldIndex] - hubCentre);
                    if (hasNormals) normals.Add(source.Normals[oldIndex]);
                    if (hasTangents) tangents.Add(source.Tangents[oldIndex]);
                    if (hasUv0) uv0.Add(source.Uv0[oldIndex]);
                    if (hasUv1) uv1.Add(source.Uv1[oldIndex]);
                    if (hasColors) colors.Add(source.Colors[oldIndex]);
                }
                indices.Add(newIndex);
            }
            newSubmeshes[m] = indices;
            triangleCount += indices.Count;
        }

        if (triangleCount == 0) return null;

        var mesh = new Mesh { name = name };
        if (positions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(positions);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasTangents) mesh.SetTangents(tangents);
        if (hasUv0) mesh.SetUVs(0, uv0);
        if (hasUv1) mesh.SetUVs(1, uv1);
        if (hasColors) mesh.SetColors(colors);

        mesh.subMeshCount = newSubmeshes.Length;
        for (int m = 0; m < newSubmeshes.Length; m++) mesh.SetTriangles(newSubmeshes[m], m, false);

        mesh.RecalculateBounds();
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.UploadMeshData(true); // Free the CPU copy; it's never read again.
        return mesh;
    }

    private static void CreateRendererObject(Mesh mesh, MeshFilter source, Transform parent)
    {
        var go = new GameObject(mesh.name);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(parent, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var sourceRenderer = source.GetComponent<MeshRenderer>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;
        renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
        renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
    }

    /// <summary>
    /// Finds loose pieces of the body mesh (connected parts) that lie entirely inside one of the new wheels and
    /// swaps the body to a runtime copy without them. Bumpers, arches and the shell are never fully inside a
    /// wheel, so only leftover wheel geometry is removed.
    /// </summary>
    private void RemoveWheelsBakedIntoBody(GameObject model, List<MeshFilter> wheelSources)
    {
        if (_wheelBounds.Count == 0) return;

        var expanded = new List<Bounds>(_wheelBounds.Count);
        foreach (Bounds wheel in _wheelBounds)
        {
            float radius = (wheel.extents.y + wheel.extents.z) * 0.5f;
            var grown = new Bounds(wheel.center, wheel.size);
            grown.Expand(Mathf.Max(0.01f, radius * bakedWheelTolerance) * 2f);
            expanded.Add(grown);
        }

        Matrix4x4 rigFromWorld = _rig.worldToLocalMatrix;

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (wheelSources.Contains(filter)) continue;

            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !filter.TryGetComponent(out MeshRenderer _)) continue;
            if (!mesh.isReadable && !Application.isEditor) continue; // Can't inspect it in a build; leave it.

            Mesh cleaned = BuildBodyWithoutWheels(mesh, rigFromWorld * filter.transform.localToWorldMatrix, expanded);
            if (cleaned == null) continue;

            _createdMeshes.Add(cleaned);
            _cleanedBodies.Add((filter, mesh));
            filter.sharedMesh = cleaned;
        }
    }

    /// <summary>Returns a copy of <paramref name="mesh"/> without parts inside the wheels, or null if nothing was removed.</summary>
    private static Mesh BuildBodyWithoutWheels(Mesh mesh, Matrix4x4 toRig, List<Bounds> wheels)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length == 0) return null;

        // FBX import splits vertices along UV/normal seams, so weld by position (0.1 mm) before finding parts.
        var weldIds = new int[vertices.Length];
        var weldLookup = new Dictionary<Vector3Int, int>(vertices.Length);
        var rigPositions = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var key = new Vector3Int(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f),
                                     Mathf.RoundToInt(v.z * 10000f));
            if (!weldLookup.TryGetValue(key, out int id))
            {
                id = weldLookup.Count;
                weldLookup.Add(key, id);
            }
            weldIds[i] = id;
            rigPositions[i] = toRig.MultiplyPoint3x4(v);
        }

        // Union-find over welded vertices, joined by triangles.
        var parent = new int[weldLookup.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        var submeshes = new int[mesh.subMeshCount][];
        for (int m = 0; m < submeshes.Length; m++)
        {
            submeshes[m] = mesh.GetTopology(m) == MeshTopology.Triangles ? mesh.GetTriangles(m) : null;
            if (submeshes[m] == null) continue;

            int[] tris = submeshes[m];
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = Find(weldIds[tris[t]]);
                parent[Find(weldIds[tris[t + 1]])] = a;
                parent[Find(weldIds[tris[t + 2]])] = a;
            }
        }

        // Rig-space bounds of every part.
        var partBounds = new Dictionary<int, Bounds>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int part = Find(weldIds[i]);
            if (partBounds.TryGetValue(part, out Bounds b))
            {
                b.Encapsulate(rigPositions[i]);
                partBounds[part] = b;
            }
            else
            {
                partBounds.Add(part, new Bounds(rigPositions[i], Vector3.zero));
            }
        }

        var removedParts = new HashSet<int>();
        foreach (KeyValuePair<int, Bounds> entry in partBounds)
        {
            foreach (Bounds wheel in wheels)
            {
                if (wheel.Contains(entry.Value.min) && wheel.Contains(entry.Value.max))
                {
                    removedParts.Add(entry.Key);
                    break;
                }
            }
        }

        if (removedParts.Count == 0 || removedParts.Count == partBounds.Count) return null;

        Mesh cleaned = Instantiate(mesh);
        cleaned.name = mesh.name + " (no baked wheels)";

        var kept = new List<int>();
        for (int m = 0; m < submeshes.Length; m++)
        {
            int[] tris = submeshes[m];
            if (tris == null) continue;

            kept.Clear();
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                if (removedParts.Contains(Find(weldIds[tris[t]]))) continue;
                kept.Add(tris[t]);
                kept.Add(tris[t + 1]);
                kept.Add(tris[t + 2]);
            }
            cleaned.SetTriangles(kept, m, false);
        }

        cleaned.RecalculateBounds();
        return cleaned;
    }

    private void Clear()
    {
        foreach (Renderer hidden in _hiddenRenderers)
        {
            if (hidden != null) hidden.enabled = true;
        }
        _hiddenRenderers.Clear();

        foreach ((MeshFilter filter, Mesh original) in _cleanedBodies)
        {
            if (filter != null) filter.sharedMesh = original;
        }
        _cleanedBodies.Clear();
        _wheelBounds.Clear();

        foreach (Mesh mesh in _createdMeshes)
        {
            if (mesh != null) Destroy(mesh);
        }
        _createdMeshes.Clear();

        if (_rig != null) Destroy(_rig.gameObject);
        _rig = null;
        _wheels.Clear();
    }

    // ------------------------------------------------------------------ Animate

    private void LateUpdate()
    {
        if (_rig == null || _wheels.Count == 0) return;

        SyncRigToBody();

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateWheelSpeed(dt);
        UpdateSteer(dt);

        float direction = invertSpin ? -1f : 1f;
        float steerAngle = _steer;

        foreach (Wheel wheel in _wheels)
        {
            float degreesPerSecond = _wheelSpeed / wheel.Radius * Mathf.Rad2Deg;
            degreesPerSecond = Mathf.Clamp(degreesPerSecond, -maxSpinDegreesPerSecond, maxSpinDegreesPerSecond);

            wheel.Angle = Mathf.Repeat(wheel.Angle + degreesPerSecond * direction * dt, 360f);
            wheel.Spinner.localRotation = Quaternion.Euler(wheel.Angle, 0f, 0f);
            wheel.Hub.localRotation = wheel.IsFront ? Quaternion.Euler(0f, steerAngle, 0f) : Quaternion.identity;
        }
    }

    private void UpdateWheelSpeed(float dt)
    {
        GameManager gm = GameManager.Instance;

        if (_taxi != null && _taxi.HasCrashed)
        {
            // Airborne / tumbling: the wheels keep their momentum and spin down on their own.
            _wheelSpeed = Mathf.MoveTowards(_wheelSpeed, 0f, crashSpinDown * dt);
        }
        else
        {
            // On the road the wheels roll exactly with the world (no slip).
            _wheelSpeed = gm != null ? gm.GameSpeed : 0f;
        }
    }

    private void UpdateSteer(float dt)
    {
        float target = 0f;
        if (_taxi != null && !_taxi.HasCrashed && _taxi.StrafeSpeed > 0f)
        {
            target = Mathf.Clamp(_taxi.LateralVelocity / _taxi.StrafeSpeed, -1f, 1f) * maxSteerAngle;
        }

        _steer = Mathf.Lerp(_steer, target, 1f - Mathf.Exp(-steerResponse * dt));
    }

    /// <summary>
    /// Follow the body's position (bumps, idle shake) and yaw (so the wheels stay in the arches), but only a
    /// little of its roll and pitch, so the body visibly moves on its suspension.
    /// </summary>
    private void SyncRigToBody()
    {
        Transform body = transform;
        if (_rig.parent == body) return;

        _rig.localPosition = body.localPosition;
        _rig.localScale = body.localScale;

        Quaternion full = body.localRotation;
        Vector3 forward = full * Vector3.forward;
        forward.y = 0f;
        Quaternion yawOnly = forward.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : full;

        _rig.localRotation = Quaternion.Slerp(yawOnly, full, followBodyLean);
    }
}
