using Godot;

public partial class SimpleActor : CharacterBody3D
{
	private Vector3 GetBossChaseDirection(Vector3 directDirection, float step)
	{
		if (!IsBoss || directDirection.LengthSquared() <= 0.001f)
		{
			return directDirection;
		}

		Vector3 planarPosition = GlobalPosition;
		planarPosition.Y = 0.0f;
		if (_bossLastChasePosition == Vector3.Zero)
		{
			_bossLastChasePosition = planarPosition;
		}

		float progress = planarPosition.DistanceTo(_bossLastChasePosition);
		_bossLastChasePosition = planarPosition;
		_bossAvoidRemaining = Mathf.Max(_bossAvoidRemaining - step, 0.0f);
		if (progress < 0.035f)
		{
			_bossStuckTime += step;
		}
		else
		{
			_bossStuckTime = Mathf.Max(_bossStuckTime - step * 2.5f, 0.0f);
		}

		Vector3 wallNormal = GetBossBlockingWallNormal();
		bool blockedByWall = wallNormal.LengthSquared() > 0.001f;
		bool stuck = _bossStuckTime >= 0.32f;
		if ((blockedByWall && _bossAvoidRemaining <= 0.0f) || stuck)
		{
			if (blockedByWall)
			{
				Vector3 tangentA = new(-wallNormal.Z, 0.0f, wallNormal.X);
				Vector3 tangentB = -tangentA;
				float scoreA = tangentA.Dot(directDirection);
				float scoreB = tangentB.Dot(directDirection);
				if (Mathf.Abs(scoreA - scoreB) < 0.08f)
				{
					_bossAvoidDirection = _bossAvoidSide > 0.0f ? tangentA : tangentB;
				}
				else
				{
					_bossAvoidDirection = scoreA > scoreB ? tangentA : tangentB;
					_bossAvoidSide = _bossAvoidDirection == tangentA ? 1.0f : -1.0f;
				}

				// A small outward component keeps the enlarged boss collider from
				// continuously scraping the same tree or building corner.
				_bossAvoidDirection = (_bossAvoidDirection + wallNormal * 0.24f).Normalized();
			}
			else
			{
				_bossAvoidSide *= -1.0f;
				Vector3 side = new Vector3(-directDirection.Z, 0.0f, directDirection.X) * _bossAvoidSide;
				_bossAvoidDirection = (side * 0.92f + directDirection * 0.22f).Normalized();
			}

			_bossAvoidRemaining = stuck ? 0.95f : 0.72f;
			_bossStuckTime = 0.0f;
		}

		if (_bossAvoidRemaining > 0.0f && _bossAvoidDirection.LengthSquared() > 0.001f)
		{
			return (_bossAvoidDirection * 0.88f + directDirection * 0.42f).Normalized();
		}

		return directDirection;
	}

	private Vector3 GetBossBlockingWallNormal()
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

	private void ResetBossObstacleAvoidance()
	{
		if (!IsBoss)
		{
			return;
		}

		_bossLastChasePosition = Vector3.Zero;
		_bossAvoidDirection = Vector3.Zero;
		_bossStuckTime = 0.0f;
		_bossAvoidRemaining = 0.0f;
	}
}
