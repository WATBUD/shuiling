using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public SimpleActor? FocusedTarget => IsValidFocusedTarget(_focusedTarget) ? _focusedTarget : null;
	private void TrySelectActorTarget()
	{
		if (TryRaycastActor(out SimpleActor actor))
		{
			SetSelectedActor(actor);
			return;
		}

		ClearSelectedActor();
	}

	private void TrySelectActorTarget(Vector2 screenPosition)
	{
		if (TryRaycastActor(screenPosition, out SimpleActor actor))
		{
			SetSelectedActor(actor);
			return;
		}

		ClearSelectedActor();
	}

	private bool TryRaycastActor(out SimpleActor actor)
	{
		Vector3 origin = _camera.GlobalPosition;
		Vector3 end = origin + GetCameraAimDirection() * TargetInfoRange;
		return TryRaycastActor(origin, end, out actor);
	}

	private bool TryRaycastActor(Vector2 screenPosition, out SimpleActor actor)
	{
		Vector3 origin = _camera.ProjectRayOrigin(screenPosition);
		Vector3 direction = _camera.ProjectRayNormal(screenPosition);
		Vector3 end = origin + direction * Mathf.Max(TargetInfoRange * 4.0f, 120.0f);
		return TryRaycastActor(origin, end, out actor);
	}

	private bool TryRaycastActor(Vector3 origin, Vector3 end, out SimpleActor actor)
	{
		actor = null!;
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
		if (result.TryGetValue("collider", out Variant colliderVariant) && colliderVariant.AsGodotObject() is SimpleActor hitActor)
		{
			actor = hitActor;
			return true;
		}

		return false;
	}

	private void SetSelectedActor(SimpleActor actor)
	{
		_selectedActor = actor;
		bool isAttackCommandTarget = IsAttackCommandTarget(actor);
		_focusedTarget = isAttackCommandTarget ? actor : null;
		EnsureSelectedTargetMarker();
		UpdateSelectedTargetMarkerColors(isAttackCommandTarget);
		if (_selectedTargetMarker != null)
		{
			_selectedTargetMarker.Visible = true;
		}
	}

	private void ClearSelectedActor()
	{
		_selectedActor = null;
		_focusedTarget = null;
		if (_selectedTargetMarker != null)
		{
			_selectedTargetMarker.Visible = false;
		}
	}

	private bool IsValidFocusedTarget(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && IsAttackCommandTarget(actor) && GlobalPosition.DistanceTo(actor.GlobalPosition) <= TargetInfoRange * 1.6f;
	}

	private bool IsValidSelectedActor(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && !actor.IsDefeated && GlobalPosition.DistanceTo(actor.GlobalPosition) <= TargetInfoRange * 1.6f;
	}

	private bool IsAttackCommandTarget(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.IsHostileToPlayer;
	}

	private void EnsureSelectedTargetMarker()
	{
		if (_selectedTargetMarker != null && IsInstanceValid(_selectedTargetMarker))
		{
			return;
		}

		_selectedTargetMarker = new Node3D { Name = "SelectedTargetMarker", Visible = false };
		Node parent = GetTree().CurrentScene ?? GetParent();
		parent.AddChild(_selectedTargetMarker);

		_selectedTargetRingMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.78f, 0.16f, 0.78f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.35f,
		};
		_selectedTargetArrowMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.32f, 0.18f, 0.9f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.25f,
		};

		_selectedTargetOuterRing = AddMarkerMesh("SelectedRingOuter", new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.15f, Height = 0.035f, RadialSegments = 48 }, new Vector3(0.0f, 0.035f, 0.0f), Vector3.Zero, Vector3.One, _selectedTargetRingMaterial);
		_selectedTargetInnerRing = AddMarkerMesh("SelectedRingInner", new CylinderMesh { TopRadius = 0.82f, BottomRadius = 0.82f, Height = 0.04f, RadialSegments = 48 }, new Vector3(0.0f, 0.045f, 0.0f), Vector3.Zero, Vector3.One, _selectedTargetRingMaterial);
		_selectedTargetArrow = AddMarkerMesh("SelectedArrow", new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.18f, Height = 0.42f, RadialSegments = 3 }, new Vector3(0.0f, 2.65f, 0.0f), new Vector3(180.0f, 30.0f, 0.0f), Vector3.One, _selectedTargetArrowMaterial);
	}

	private MeshInstance3D? AddMarkerMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		if (_selectedTargetMarker == null)
		{
			return null;
		}

		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position * PlayerVisualScale,
			RotationDegrees = rotationDegrees,
			Scale = scale * PlayerVisualScale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		_selectedTargetMarker.AddChild(meshInstance);
		return meshInstance;
	}

	private void UpdateSelectedTargetMarkerColors(bool isHostile)
	{
		if (_selectedTargetRingMaterial == null || _selectedTargetArrowMaterial == null)
		{
			return;
		}

		_selectedTargetRingMaterial.AlbedoColor = isHostile
			? new Color(1.0f, 0.78f, 0.16f, 0.78f)
			: new Color(0.34f, 0.72f, 1.0f, 0.70f);
		_selectedTargetArrowMaterial.AlbedoColor = isHostile
			? new Color(1.0f, 0.32f, 0.18f, 0.9f)
			: new Color(0.36f, 0.92f, 1.0f, 0.86f);
	}

	private void UpdateFocusedTargetMarker(float step)
	{
		if (!IsValidSelectedActor(_selectedActor))
		{
			ClearSelectedActor();
			return;
		}

		EnsureSelectedTargetMarker();
		if (_selectedTargetMarker == null || _selectedActor == null)
		{
			return;
		}

		bool isAttackCommandTarget = IsAttackCommandTarget(_selectedActor);
		_focusedTarget = isAttackCommandTarget ? _selectedActor : null;
		UpdateSelectedTargetMarkerColors(isAttackCommandTarget);
		if (_selectedTargetInnerRing != null)
		{
			_selectedTargetInnerRing.Visible = isAttackCommandTarget;
		}

		_selectedTargetMarker.Visible = true;
		_selectedTargetMarker.GlobalPosition = _selectedActor.GlobalPosition + Vector3.Up * 0.03f;
		_selectedTargetMarker.RotationDegrees += new Vector3(0.0f, (isAttackCommandTarget ? 120.0f : 72.0f) * step, 0.0f);
		float pulse = 1.0f + Mathf.Sin(Time.GetTicksMsec() * 0.008f) * 0.08f;
		_selectedTargetMarker.Scale = new Vector3(pulse, 1.0f, pulse);
		if (_selectedTargetArrow != null)
		{
			_selectedTargetArrow.Position = new Vector3(0.0f, isAttackCommandTarget ? 2.65f : 2.45f, 0.0f);
		}
	}

}
