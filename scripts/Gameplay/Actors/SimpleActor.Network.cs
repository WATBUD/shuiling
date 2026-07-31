using Godot;

// Network-puppet lifecycle/sync for SimpleActor, split out of the core file
// (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md). Pure code move: the
// _isNetworkPuppet/_networkId/_netTarget* field declarations and the
// _PhysicsProcess dispatch gate stay in the core file and are reached as the
// same partial class. Combat-coupled network methods (ResolveHostileTarget,
// TryAttackRemotePlayer) intentionally remain with the combat cluster.
public partial class SimpleActor : CharacterBody3D
{
	// Turns this actor into a network puppet (multiplayer client): the host
	// drives position/health; local AI and local damage application stop.
	public void SetNetworkPuppet(int networkId)
	{
		_isNetworkPuppet = true;
		_networkId = networkId;
		NetworkMonsterId = networkId;
		_netTargetPosition = GlobalPosition;
		_netTargetYaw = Rotation.Y;
	}

	public void ReleaseNetworkPuppet()
	{
		_isNetworkPuppet = false;
		_networkId = -1;
		NetworkMonsterId = -1;
		_networkCaptureReady = false;
	}

	public void ApplyNetworkState(Vector3 position, float yaw, int health, bool captureReady)
	{
		_netTargetPosition = position;
		_netTargetYaw = yaw;
		int clamped = Mathf.Clamp(health, 0, EffectiveMaxHealth);
		bool stateChanged = clamped != CurrentHealth || captureReady != _networkCaptureReady;
		CurrentHealth = clamped;
		_networkCaptureReady = captureReady;
		if (stateChanged)
		{
			RefreshNameplate();
		}
	}

	private void UpdateNetworkPuppet(float step)
	{
		float weight = Mathf.Min(step * 10.0f, 1.0f);
		Vector3 toTarget = _netTargetPosition - GlobalPosition;
		if (toTarget.Length() > 12.0f)
		{
			GlobalPosition = _netTargetPosition;
			Velocity = Vector3.Zero;
		}
		else
		{
			GlobalPosition += toTarget * weight;
			Velocity = toTarget * 10.0f;
		}

		Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, _netTargetYaw, weight), 0.0f);
		UpdateMovementAnimation(step);
		Velocity = Vector3.Zero;
	}
}
