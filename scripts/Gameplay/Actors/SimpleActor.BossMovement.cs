using Godot;

public partial class SimpleActor : CharacterBody3D
{
	// Bosses and captured companions share the same collision-based chase steering.
	// Slide normals select a wall tangent; lack of progress flips the tangent so an
	// actor can recover from corners instead of repeatedly pushing at its target.
	private Vector3 GetChaseDirection(Vector3 directDirection, float step)
	{
		if ((!IsBoss && !_isCaptured) || directDirection.LengthSquared() <= 0.001f)
		{
			return directDirection;
		}

		Vector3 planarPosition = GlobalPosition;
		planarPosition.Y = 0.0f;
		if (_lastChasePosition == Vector3.Zero)
		{
			_lastChasePosition = planarPosition;
		}

		float progress = planarPosition.DistanceTo(_lastChasePosition);
		_lastChasePosition = planarPosition;
		_chaseAvoidRemaining = Mathf.Max(_chaseAvoidRemaining - step, 0.0f);
		if (progress < 0.035f)
		{
			_chaseStuckTime += step;
		}
		else
		{
			_chaseStuckTime = Mathf.Max(_chaseStuckTime - step * 2.5f, 0.0f);
		}

		Vector3 wallNormal = GetBlockingWallNormal();
		bool blockedByWall = wallNormal.LengthSquared() > 0.001f;
		bool stuck = _chaseStuckTime >= 0.32f;
		if ((blockedByWall && _chaseAvoidRemaining <= 0.0f) || stuck)
		{
			if (blockedByWall)
			{
				Vector3 tangentA = new(-wallNormal.Z, 0.0f, wallNormal.X);
				Vector3 tangentB = -tangentA;
				float scoreA = tangentA.Dot(directDirection);
				float scoreB = tangentB.Dot(directDirection);
				if (Mathf.Abs(scoreA - scoreB) < 0.08f)
				{
					_chaseAvoidDirection = _chaseAvoidSide > 0.0f ? tangentA : tangentB;
				}
				else
				{
					_chaseAvoidDirection = scoreA > scoreB ? tangentA : tangentB;
					_chaseAvoidSide = _chaseAvoidDirection == tangentA ? 1.0f : -1.0f;
				}

				// A small outward component keeps the enlarged boss collider from
				// continuously scraping the same tree or building corner.
				_chaseAvoidDirection = (_chaseAvoidDirection + wallNormal * 0.24f).Normalized();
			}
			else
			{
				_chaseAvoidSide *= -1.0f;
				Vector3 side = new Vector3(-directDirection.Z, 0.0f, directDirection.X) * _chaseAvoidSide;
				_chaseAvoidDirection = (side * 0.92f + directDirection * 0.22f).Normalized();
			}

			_chaseAvoidRemaining = stuck ? 0.95f : 0.72f;
			_chaseStuckTime = 0.0f;
		}

		if (_chaseAvoidRemaining > 0.0f && _chaseAvoidDirection.LengthSquared() > 0.001f)
		{
			return (_chaseAvoidDirection * 0.88f + directDirection * 0.42f).Normalized();
		}

		return directDirection;
	}

	private Vector3 GetBlockingWallNormal()
	{
		for (int index = 0; index < GetSlideCollisionCount(); index++)
		{
			KinematicCollision3D collision = GetSlideCollision(index);
			Vector3 normal = collision.GetNormal();
			normal.Y = 0.0f;
			if (normal.LengthSquared() > 0.16f)
			{
				return normal.Normalized();
			}
		}

		return Vector3.Zero;
	}

	private void ResetChaseObstacleAvoidance()
	{
		_lastChasePosition = Vector3.Zero;
		_chaseAvoidDirection = Vector3.Zero;
		_chaseStuckTime = 0.0f;
		_chaseAvoidRemaining = 0.0f;
	}
}
