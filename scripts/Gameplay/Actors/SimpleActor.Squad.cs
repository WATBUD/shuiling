using Godot;

public partial class SimpleActor : CharacterBody3D
{
	private void FollowCapturedTarget(Vector3 velocity, float step)
	{
		if (!_isInActiveParty || _followTarget == null || !IsInstanceValid(_followTarget))
		{
			SetFollowLagBubbleVisible(false);
			Velocity = SlowToStop(velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);
		UpdatePetDialogue(distanceToPlayer, step);

		if (TryUseSupportBuild(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.2f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		if (TryCompanionCombat(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.6f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		UpdateSquadActivity(step);
		Vector3 destination = GetLivingSquadDestination();
		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;

		float followSpeed = GetLivingSquadMoveSpeed(distanceToPlayer);
		float stopDistance = _squadActivity == SquadActivity.Rest ? 0.85f : 0.55f;
		if (toDestination.Length() > stopDistance)
		{
			Vector3 direction = toDestination.Normalized();
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * followSpeed, followSpeed * 7.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * followSpeed, followSpeed * 7.0f * step);
			FaceDirection(direction, step);
		}
		else
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(GetLivingSquadLookDirection(destination), step);
		}

		Velocity = velocity;
		MoveAndSlideWithEffects(step);
	}

	private Vector3 GetFollowDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		return _followTarget.GlobalPosition + PlayerLocalToWorld(GetFormationLocalOffset());
	}

	private Vector3 GetLivingSquadDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		Vector3 formationOffset = GetFormationLocalOffset();
		Vector3 localOffset = _squadActivity switch
		{
			SquadActivity.Guard => formationOffset * 0.72f + _squadActivityLocalOffset * 0.28f,
			SquadActivity.Scout => _squadActivityLocalOffset,
			SquadActivity.Gather => _squadActivityLocalOffset,
			SquadActivity.Roam => _squadActivityLocalOffset,
			SquadActivity.Rest => formationOffset * 0.82f + _squadActivityLocalOffset * 0.18f,
			_ => formationOffset,
		};

		return _followTarget.GlobalPosition + PlayerLocalToWorld(localOffset);
	}

	private void UpdateSquadActivity(float step)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);
		if (distanceToPlayer > GetFormationRegroupDistance())
		{
			_squadActivity = SquadActivity.Follow;
			_squadActivityRemaining = 1.0f;
			_squadThinkRemaining = 1.2f;
			return;
		}

		_squadActivityRemaining = Mathf.Max(_squadActivityRemaining - step, 0.0f);
		_squadThinkRemaining = Mathf.Max(_squadThinkRemaining - step, 0.0f);
		if (_squadActivityRemaining > 0.0f || _squadThinkRemaining > 0.0f)
		{
			return;
		}

		ChooseSquadActivity();
	}

	private void ChooseSquadActivity()
	{
		BuildStats stats = CurrentBuildStats;
		float roll = _rng.Randf();
		_squadActivity = ChooseLivingSquadActivity(stats, roll);
		_squadActivityLocalOffset = MakeActivityLocalOffset(_squadActivity);
		_squadActivityRemaining = (float)_rng.RandfRange(2.4f, 6.8f);
		_squadThinkRemaining = (float)_rng.RandfRange(0.3f, 1.2f);
	}

	private SquadActivity ChooseLivingSquadActivity(BuildStats stats, float roll)
	{
		// Idle behavior no longer varies by combat mode: companions loosely follow the
		// player and occasionally guard, roam, or rest.
		return roll < 0.36f
			? SquadActivity.Follow
			: roll < 0.62f
				? SquadActivity.Guard
				: roll < 0.82f
					? SquadActivity.Roam
					: SquadActivity.Rest;
	}

	private Vector3 MakeActivityLocalOffset(SquadActivity activity)
	{
		Vector3 formation = GetFormationLocalOffset();
		float side = _followSlot % 2 == 0 ? -1.0f : 1.0f;
		return activity switch
		{
			SquadActivity.Guard => formation + new Vector3(side * (float)_rng.RandfRange(0.45f, 1.15f), 0.0f, (float)_rng.RandfRange(-0.5f, 1.0f)),
			SquadActivity.Scout => new Vector3(side * (float)_rng.RandfRange(1.0f, 3.8f), 0.0f, (float)_rng.RandfRange(4.8f, 8.0f)),
			SquadActivity.Gather => RandomLocalRingOffset(3.4f, 6.4f),
			SquadActivity.Roam => RandomLocalRingOffset(3.0f, 7.2f),
			SquadActivity.Rest => formation + RandomLocalRingOffset(0.2f, 0.9f),
			_ => formation,
		};
	}

	private Vector3 RandomLocalRingOffset(float minRadius, float maxRadius)
	{
		float angle = (float)_rng.RandfRange(-Mathf.Pi, Mathf.Pi);
		float radius = (float)_rng.RandfRange(minRadius, maxRadius) * CurrentBuildStats.FollowDistanceMultiplier;
		return new Vector3(Mathf.Sin(angle) * radius, 0.0f, Mathf.Cos(angle) * radius);
	}

	private Vector3 GetFormationLocalOffset()
	{
		Vector3 offset = _followTarget != null && IsInstanceValid(_followTarget)
			? _followTarget.GetFormationLocalOffset(this)
			: Vector3.Zero;
		if (offset.LengthSquared() <= 0.001f)
		{
			offset = new Vector3(0.0f, 0.0f, MinimumCompanionFormationDistance);
		}

		return KeepFormationOffsetOutsidePlayer(offset * CurrentBuildStats.FollowDistanceMultiplier);
	}

	private static Vector3 KeepFormationOffsetOutsidePlayer(Vector3 offset)
	{
		float distance = new Vector2(offset.X, offset.Z).Length();
		if (distance >= MinimumCompanionFormationDistance || distance <= 0.001f)
		{
			return offset;
		}

		float scale = MinimumCompanionFormationDistance / distance;
		return new Vector3(offset.X * scale, offset.Y, offset.Z * scale);
	}

	private Vector3 PlayerLocalToWorld(Vector3 localOffset)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return localOffset;
		}

		Vector3 forward = -_followTarget.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;

		Vector3 right = _followTarget.GlobalTransform.Basis.X;
		right.Y = 0.0f;
		right = right.LengthSquared() > 0.001f ? right.Normalized() : Vector3.Right;
		return right * localOffset.X + forward * localOffset.Z;
	}

	private float GetLivingSquadMoveSpeed(float distanceToPlayer)
	{
		float multiplier = _squadActivity switch
		{
			SquadActivity.Scout => 1.10f,
			SquadActivity.Gather or SquadActivity.Roam => 0.92f,
			SquadActivity.Guard => 1.0f,
			SquadActivity.Rest => 0.68f,
			_ => 1.05f,
		};

		float normalSpeed = Mathf.Max(EffectiveMoveSpeed * multiplier, 4.2f);
		float catchUpBonus = Mathf.Clamp((distanceToPlayer - 6.5f) * 0.58f, 0.0f, 5.2f);
		return normalSpeed + catchUpBonus;
	}

	private void SetFollowLagBubbleVisible(bool visible)
	{
		if (!visible)
		{
			if (_followLagBubble != null)
			{
				_followLagBubble.Visible = false;
			}

			return;
		}

		if (_followLagBubble == null)
		{
			float visualTop = GetVisualTopY(this);
			_followLagBubble = new Node3D
			{
				Name = "FollowLagBubble",
				Position = new Vector3(0.0f, Mathf.Max(visualTop + 0.78f, 2.75f), 0.0f),
			};
			AddChild(_followLagBubble);
			CreateFollowLagBubbleVisual(_followLagBubble, _petDialogueText);
		}

		_followLagBubble.Visible = true;
	}

	private void UpdatePetDialogue(float distanceToPlayer, float step)
	{
		bool isInCombat = IsPetInCombat();
		if (isInCombat && _showingLagDialogue)
		{
			ClearLagDialogue();
			_nextPetDialogueDelay = 0.0f;
			return;
		}

		if (!isInCombat && distanceToPlayer > 12.0f)
		{
			_showingLagDialogue = true;
			ShowPetDialogue("主人等等我QQ....");
			_petDialogueRemaining = 0.0f;
			return;
		}

		if (_showingLagDialogue)
		{
			ClearLagDialogue();
			_nextPetDialogueDelay = (float)_rng.RandfRange(7.0f, 15.0f);
			return;
		}

		if (_petDialogueRemaining > 0.0f)
		{
			_petDialogueRemaining = Mathf.Max(_petDialogueRemaining - step, 0.0f);
			SetFollowLagBubbleVisible(_petDialogueRemaining > 0.0f);
			return;
		}

		SetFollowLagBubbleVisible(false);
		_nextPetDialogueDelay -= step;
		if (_nextPetDialogueDelay > 0.0f)
		{
			return;
		}

		string[] quotePool = isInCombat ? PetCombatQuotes : PetDailyQuotes;
		string quote = quotePool[_rng.RandiRange(0, quotePool.Length - 1)];
		ShowPetDialogue(quote);
		_petDialogueRemaining = (float)_rng.RandfRange(2.6f, 4.0f);
		_nextPetDialogueDelay = (float)_rng.RandfRange(7.0f, 15.0f);
	}

	private void ClearLagDialogue()
	{
		_showingLagDialogue = false;
		_petDialogueText = string.Empty;
		_petDialogueRemaining = 0.0f;
		SetFollowLagBubbleVisible(false);
	}

	private bool IsPetInCombat()
	{
		if (_combatTarget != null && IsInstanceValid(_combatTarget) && _combatTarget.IsHostileToPlayer)
		{
			return true;
		}

		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return false;
		}

		SimpleActor? focusedTarget = _followTarget.FocusedTarget;
		return focusedTarget != null
			&& IsInstanceValid(focusedTarget)
			&& focusedTarget.IsHostileToPlayer
			&& (GlobalPosition.DistanceTo(focusedTarget.GlobalPosition) <= Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f)
				|| _followTarget.GlobalPosition.DistanceTo(focusedTarget.GlobalPosition) <= Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f));
	}

	private void ShowPetDialogue(string text)
	{
		if (_petDialogueText != text)
		{
			_petDialogueText = text;
			if (_followLagBubble != null)
			{
				_followLagBubble.QueueFree();
				_followLagBubble = null;
			}
		}

		SetFollowLagBubbleVisible(true);
	}

	private static void CreateFollowLagBubbleVisual(Node3D bubble, string bubbleText)
	{
		const int fontSize = 80;
		const int horizontalPadding = 36;
		const int verticalPadding = 22;
		const int outerMargin = 3;
		const int tailHeight = 20;
		Vector2 measuredText = ThemeDB.FallbackFont.GetStringSize(bubbleText, HorizontalAlignment.Left, -1, fontSize);
		int panelWidth = Mathf.CeilToInt(measuredText.X) + horizontalPadding;
		int panelHeight = Mathf.CeilToInt(measuredText.Y) + verticalPadding;
		int viewportWidth = panelWidth + outerMargin * 2;
		int viewportHeight = panelHeight + outerMargin + tailHeight;
		float centerX = viewportWidth * 0.5f;

		var viewport = new SubViewport
		{
			Name = "BubbleViewport",
			Size = new Vector2I(viewportWidth, viewportHeight),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
		};
		bubble.AddChild(viewport);

		var root = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(viewportWidth, viewportHeight),
		};
		viewport.AddChild(root);

		var tail = new Polygon2D
		{
			Polygon = new Vector2[]
			{
				new(centerX - 14.0f, outerMargin + panelHeight - 2.0f),
				new(centerX + 14.0f, outerMargin + panelHeight - 2.0f),
				new(centerX, viewportHeight - 1.0f),
			},
			Color = new Color(0.96f, 0.94f, 0.88f, 0.80f),
		};
		root.AddChild(tail);

		var panel = new PanelContainer
		{
			Position = new Vector2(outerMargin, outerMargin),
			Size = new Vector2(panelWidth, panelHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = true,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.96f, 0.94f, 0.88f, 0.55f),
			BorderColor = new Color(0.27f, 0.22f, 0.20f, 0.80f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 13,
			CornerRadiusTopRight = 13,
			CornerRadiusBottomLeft = 13,
			CornerRadiusBottomRight = 13,
			ContentMarginLeft = 12.0f,
			ContentMarginRight = 12.0f,
			ContentMarginTop = 5.0f,
			ContentMarginBottom = 5.0f,
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		root.AddChild(panel);

		var bubbleGradient = new Gradient
		{
			Offsets = new float[] { 0.0f, 0.52f, 1.0f },
			Colors = new Color[]
			{
				new(1.0f, 0.99f, 0.95f, 0.56f),
				new(0.98f, 0.94f, 0.84f, 0.56f),
				new(0.93f, 0.82f, 0.63f, 0.56f),
			},
		};
		var gradientTexture = new GradientTexture2D
		{
			Gradient = bubbleGradient,
			Width = panelWidth,
			Height = panelHeight,
			FillFrom = new Vector2(0.5f, 0.0f),
			FillTo = new Vector2(0.5f, 1.0f),
		};
		var gradientLayer = new TextureRect
		{
			Texture = gradientTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		panel.AddChild(gradientLayer);

		var label = new Label
		{
			Text = bubbleText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", new Color(0.20f, 0.16f, 0.15f, 1.0f));
		panel.AddChild(label);

		var sprite = new Sprite3D
		{
			Name = "BubbleSprite",
			Texture = viewport.GetTexture(),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
			PixelSize = 0.0055f,
			Position = new Vector3(0.0f, 0.0f, 0.0f),
		};
		bubble.AddChild(sprite);
	}

	private Vector3 GetLivingSquadLookDirection(Vector3 destination)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return destination - GlobalPosition;
		}

		return _squadActivity switch
		{
			SquadActivity.Guard or SquadActivity.Scout => GlobalPosition - _followTarget.GlobalPosition,
			SquadActivity.Gather or SquadActivity.Roam => destination - GlobalPosition,
			_ => GetFollowTargetFacingDirection(),
		};
	}

	private Vector3 GetFollowTargetFacingDirection()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return Vector3.Forward;
		}

		Vector3 forward = -_followTarget.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		return forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
	}

	private void ResetSquadActivity()
	{
		_squadActivity = SquadActivity.Follow;
		_squadActivityLocalOffset = GetFormationLocalOffset();
		_squadActivityRemaining = 0.0f;
		_squadThinkRemaining = (float)_rng.RandfRange(0.15f, 1.1f);
	}

	private float GetFormationRegroupDistance()
	{
		return 12.5f;
	}
}
