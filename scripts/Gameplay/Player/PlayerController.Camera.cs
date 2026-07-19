using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public Vector3 MinimapForward => GetCameraPlanarForward();
	public void SetCameraMode(CameraViewMode mode)
	{
		if (_cameraMode == mode)
		{
			return;
		}

		if (_cameraMode == CameraViewMode.ThirdPerson)
		{
			_thirdPersonCameraYaw = _cameraYaw;
		}
		else
		{
			_godViewCameraYaw = _cameraYaw;
		}

		_cameraMode = mode;
		_cameraYaw = mode == CameraViewMode.ThirdPerson ? _thirdPersonCameraYaw : _godViewCameraYaw;
		ApplyCameraModeSettings();
		UpdateMouseModeForPanels();
		UpdateCamera();
	}

	private void ConfigureThirdPersonCamera()
	{
		_cameraYaw = 0.0f;
		_cameraPitch = 0.08f;
		_camera.TopLevel = true;
		_camera.Near = 0.05f;
		_camera.HOffset = 0.0f;
		_camera.VOffset = 0.0f;
		_cameraPivot.Position = Vector3.Zero;
		ApplyCameraModeSettings();
		UpdateCamera();
	}

	private void ApplyCameraModeSettings()
	{
		if (_camera == null || !IsInstanceValid(_camera))
		{
			return;
		}

		_camera.Fov = _cameraMode == CameraViewMode.GodView ? 48.0f : 58.0f;
	}

	private void UpdateCamera()
	{
		if (_cameraMode == CameraViewMode.GodView)
		{
			UpdateGodViewCamera();
			return;
		}

		UpdateThirdPersonCamera();
	}

	private void UpdateThirdPersonCamera()
	{
		Vector3 backward = GetCameraPlanarBackward();
		float distance = Mathf.Max(ThirdPersonDistance, 1.0f);
		float horizontalDistance = Mathf.Max(Mathf.Cos(_cameraPitch) * distance, 1.2f);
		float cameraHeight = Mathf.Max(ThirdPersonCameraHeight + Mathf.Sin(_cameraPitch) * distance, 0.85f);
		float lookHeight = Mathf.Clamp(ThirdPersonLookHeight - Mathf.Sin(_cameraPitch) * 0.45f, 1.35f, 2.75f);
		Vector3 intendedCameraPosition = GlobalPosition + backward * horizontalDistance + Vector3.Up * cameraHeight;
		Vector3 boundedCameraPosition = ClampCameraInsideMap(intendedCameraPosition);
		Vector3 lookTarget = GlobalPosition + Vector3.Up * lookHeight;

		SetBoundedCameraTransform(intendedCameraPosition, boundedCameraPosition, lookTarget);
	}

	private void UpdateGodViewCamera()
	{
		Vector3 backward = GetCameraPlanarBackward();
		float distance = Mathf.Max(GodViewDistance, 6.0f);
		float height = Mathf.Max(GodViewCameraHeight, 8.0f);
		Vector3 intendedCameraPosition = GlobalPosition + backward * distance + Vector3.Up * height;
		Vector3 boundedCameraPosition = ClampCameraInsideMap(intendedCameraPosition);
		Vector3 lookTarget = GlobalPosition + Vector3.Up * 0.85f;

		SetBoundedCameraTransform(intendedCameraPosition, boundedCameraPosition, lookTarget);
	}

	private void AdjustGodViewZoom(float amount)
	{
		float minZoom = Mathf.Max(GodViewMinZoom, 6.0f);
		float maxZoom = Mathf.Max(GodViewMaxZoom, minZoom);
		GodViewDistance = Mathf.Clamp(GodViewDistance + amount, minZoom, maxZoom);
		UpdateGodViewCamera();
	}

	private Vector3 ClampCameraInsideMap(Vector3 position)
	{
		float halfExtent = Mathf.Max(CameraWorldHalfExtent, 8.0f);
		position.X = Mathf.Clamp(position.X, -halfExtent, halfExtent);
		position.Z = Mathf.Clamp(position.Z, -halfExtent, halfExtent);
		position.Y = Mathf.Max(position.Y, GlobalPosition.Y + 2.8f);
		return position;
	}

	private void SetBoundedCameraTransform(Vector3 intendedPosition, Vector3 boundedPosition, Vector3 lookTarget)
	{
		Vector3 intendedLookDirection = lookTarget - intendedPosition;
		if (intendedLookDirection.LengthSquared() <= 0.001f)
		{
			intendedLookDirection = GetCameraPlanarForward();
		}

		// Preserve the user's intended yaw and pitch even when the camera origin
		// reaches the map boundary. Looking at the player from the clamped origin
		// would rotate the camera automatically and offset mouse picking.
		Basis viewBasis = Basis.LookingAt(intendedLookDirection.Normalized(), Vector3.Up);
		_camera.GlobalTransform = new Transform3D(viewBasis, boundedPosition);
	}

	private Vector3 GetCameraPlanarBackward()
	{
		return new Vector3(Mathf.Sin(_cameraYaw), 0.0f, Mathf.Cos(_cameraYaw)).Normalized();
	}

	private Vector3 GetCameraPlanarForward()
	{
		return -GetCameraPlanarBackward();
	}

	private Vector3 GetCameraAimDirection()
	{
		if (_camera == null || !IsInstanceValid(_camera))
		{
			return GetCameraPlanarForward();
		}

		return -_camera.GlobalTransform.Basis.Z.Normalized();
	}

	private Vector3 GetCaptureThrowDirection()
	{
		Vector3 facing = -GlobalTransform.Basis.Z;
		facing.Y = 0.0f;
		return facing.LengthSquared() > 0.001f ? facing.Normalized() : Vector3.Forward;
	}

	private static string CameraModeToSaveId(CameraViewMode mode)
	{
		return mode == CameraViewMode.GodView ? GodViewCameraModeId : ThirdPersonCameraModeId;
	}

	private static CameraViewMode CameraModeFromSaveId(string modeId)
	{
		return modeId == ThirdPersonCameraModeId ? CameraViewMode.ThirdPerson : CameraViewMode.GodView;
	}

}
