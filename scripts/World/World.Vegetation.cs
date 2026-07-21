using Godot;
using System.Collections.Generic;

// Vegetation batching: grass blades and flowers are by far the highest-count
// visual elements (hundreds of patches x several blades each). Building one
// MeshInstance3D per blade meant thousands of draw calls per map. Instead we
// collect every blade/flower transform during a map build and emit them as a
// few MultiMeshInstance3D nodes — GPU-instanced, so each vegetation kind is a
// single draw call regardless of instance count. Colour variety is preserved
// via per-instance colours (vertex-colour albedo).
public partial class World
{
	private enum VegKind
	{
		GrassBlade,
		FlowerStem,
		FlowerHead,
	}

	private sealed class VegBucket
	{
		public readonly List<Transform3D> Transforms = new();
		public readonly List<Color> Colors = new();
	}

	// Non-null only while a map is being built; scatter helpers push into it.
	private Dictionary<VegKind, VegBucket>? _vegBatch;
	private Node3D? _vegBatchParent;

	// Shared unit meshes (height/scale applied per instance) + one shared
	// vertex-colour material. Created once, reused across every map.
	private static BoxMesh? _vegGrassMesh;
	private static CylinderMesh? _vegStemMesh;
	private static SphereMesh? _vegFlowerHeadMesh;
	private static StandardMaterial3D? _vegMaterial;

	private void BeginVegetationBatch(Node3D parent)
	{
		_vegBatchParent = parent;
		_vegBatch = new Dictionary<VegKind, VegBucket>
		{
			[VegKind.GrassBlade] = new VegBucket(),
			[VegKind.FlowerStem] = new VegBucket(),
			[VegKind.FlowerHead] = new VegBucket(),
		};
	}

	private void EndVegetationBatch()
	{
		if (_vegBatch == null || _vegBatchParent == null)
		{
			return;
		}

		_vegGrassMesh ??= new BoxMesh { Size = new Vector3(0.05f, 1.0f, 0.02f) };
		_vegStemMesh ??= new CylinderMesh { TopRadius = 0.014f, BottomRadius = 0.02f, Height = 1.0f, RadialSegments = 5 };
		_vegFlowerHeadMesh ??= new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 8, Rings = 4 };
		_vegMaterial ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f };

		EmitVegetationMultiMesh(VegKind.GrassBlade, _vegGrassMesh, "GrassBatch");
		EmitVegetationMultiMesh(VegKind.FlowerStem, _vegStemMesh, "FlowerStemBatch");
		EmitVegetationMultiMesh(VegKind.FlowerHead, _vegFlowerHeadMesh, "FlowerHeadBatch");

		_vegBatch = null;
		_vegBatchParent = null;
	}

	private void EmitVegetationMultiMesh(VegKind kind, Mesh mesh, string nodeName)
	{
		VegBucket bucket = _vegBatch![kind];
		int count = bucket.Transforms.Count;
		if (count == 0)
		{
			return;
		}

		var multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = mesh,
			InstanceCount = count,
		};
		for (int index = 0; index < count; index++)
		{
			multiMesh.SetInstanceTransform(index, bucket.Transforms[index]);
			multiMesh.SetInstanceColor(index, bucket.Colors[index]);
		}

		var instance = new MultiMeshInstance3D
		{
			Name = nodeName,
			Multimesh = multiMesh,
			MaterialOverride = _vegMaterial,
		};
		_vegBatchParent!.AddChild(instance);
	}

	// Returns true when a blade/flower was queued into the active batch. When
	// no batch is active (defensive), the caller builds the old node version.
	private bool TryBatchGrassPatch(Vector3 position)
	{
		if (_vegBatch == null)
		{
			return false;
		}

		VegBucket grass = _vegBatch[VegKind.GrassBlade];
		int bladeCount = _rng.RandiRange(5, 10);
		for (int index = 0; index < bladeCount; index++)
		{
			float height = (float)_rng.RandfRange(0.36f, 0.78f);
			float offsetX = (float)_rng.RandfRange(-0.48f, 0.48f);
			float offsetZ = (float)_rng.RandfRange(-0.48f, 0.48f);
			var basis = Basis.FromEuler(new Vector3(
				Mathf.DegToRad((float)_rng.RandfRange(-10.0f, 10.0f)),
				Mathf.DegToRad((float)_rng.RandfRange(0.0f, 360.0f)),
				Mathf.DegToRad((float)_rng.RandfRange(-18.0f, 18.0f))));
			basis = basis.Scaled(new Vector3(1.0f, height, 1.0f));
			grass.Transforms.Add(new Transform3D(basis, position + new Vector3(offsetX, height * 0.5f, offsetZ)));
			grass.Colors.Add(_rng.Randf() < 0.55f ? _grassBrightColor : _grassDarkColor);
		}

		return true;
	}

	private bool TryBatchFlowerPatch(Vector3 position)
	{
		if (_vegBatch == null)
		{
			return false;
		}

		TryBatchGrassPatch(position);
		VegBucket stems = _vegBatch[VegKind.FlowerStem];
		VegBucket heads = _vegBatch[VegKind.FlowerHead];
		int flowerCount = _rng.RandiRange(2, 5);
		for (int index = 0; index < flowerCount; index++)
		{
			float offsetX = (float)_rng.RandfRange(-0.45f, 0.45f);
			float offsetZ = (float)_rng.RandfRange(-0.45f, 0.45f);
			float stemHeight = (float)_rng.RandfRange(0.28f, 0.5f);
			var stemBasis = Basis.Identity.Scaled(new Vector3(1.0f, stemHeight, 1.0f));
			stems.Transforms.Add(new Transform3D(stemBasis, position + new Vector3(offsetX, stemHeight * 0.5f, offsetZ)));
			stems.Colors.Add(_grassDarkColor);

			heads.Transforms.Add(new Transform3D(Basis.Identity, position + new Vector3(offsetX, stemHeight + 0.04f, offsetZ)));
			heads.Colors.Add(_rng.Randf() < 0.5f ? _flowerWarmColor : _flowerCoolColor);
		}

		return true;
	}

	private static readonly Color _grassBrightColor = new(0.36f, 0.64f, 0.24f);
	private static readonly Color _grassDarkColor = new(0.11f, 0.34f, 0.17f);
	private static readonly Color _flowerWarmColor = new(1.0f, 0.63f, 0.24f);
	private static readonly Color _flowerCoolColor = new(0.62f, 0.72f, 1.0f);
}
