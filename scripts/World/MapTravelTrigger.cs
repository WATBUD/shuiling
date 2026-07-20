using Godot;

// A walk-in map transition. When the player's body enters this area it asks the World
// to travel to TargetMap — no interaction key required. Used so caves connect to the
// overworld by simply walking into the entrance/passage instead of pressing E.
public partial class MapTravelTrigger : Area3D
{
	private World _world = null!;
	private string _ownerMapId = string.Empty;
	private string _targetMap = string.Empty;

	public void Configure(World world, string ownerMapId, string targetMap, Vector3 boxSize, Vector3 offset)
	{
		_world = world;
		_ownerMapId = ownerMapId;
		_targetMap = targetMap;
		Monitoring = true;
		Monitorable = false;
		// Detect the player regardless of which physics layer it sits on; the handler
		// filters to the player node.
		CollisionMask = uint.MaxValue;
		AddChild(new CollisionShape3D
		{
			Position = offset,
			Shape = new BoxShape3D { Size = boxSize },
		});
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController && IsInstanceValid(_world))
		{
			_world.TryTriggerWalkInTravel(_ownerMapId, _targetMap);
		}
	}
}
