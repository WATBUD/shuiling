using Godot;

public partial class SimpleActor : CharacterBody3D
{
	public void ApplyElementStatusFromPlayer(string elementId)
	{
		ApplyElementStatus(elementId, null);
	}

	private void ApplyElementStatus(string elementId, SimpleActor? source)
	{
		_statusSource = source;
		switch (elementId)
		{
			case "ice":
				_slowRemaining = Mathf.Max(_slowRemaining, 3.0f);
				break;
			case "lightning":
				_stunRemaining = Mathf.Max(_stunRemaining, 0.9f);
				break;
			case "poison":
				_poisonRemaining = Mathf.Max(_poisonRemaining, 5.0f);
				break;
			case "fire":
				_burnRemaining = Mathf.Max(_burnRemaining, 4.0f);
				break;
		}
	}

	private void UpdateStatusEffects(float step)
	{
		_slowRemaining = Mathf.Max(_slowRemaining - step, 0.0f);
		_stunRemaining = Mathf.Max(_stunRemaining - step, 0.0f);
		_poisonRemaining = Mathf.Max(_poisonRemaining - step, 0.0f);
		_burnRemaining = Mathf.Max(_burnRemaining - step, 0.0f);
		if (_poisonRemaining <= 0.0f && _burnRemaining <= 0.0f)
		{
			_statusTickRemaining = 0.0f;
			return;
		}

		_statusTickRemaining -= step;
		if (_statusTickRemaining > 0.0f || _isDefeated)
		{
			return;
		}

		_statusTickRemaining = 1.0f;
		int damage = (_poisonRemaining > 0.0f ? Mathf.Max(2, Mathf.RoundToInt(EffectiveMaxHealth * 0.025f)) : 0)
			+ (_burnRemaining > 0.0f ? Mathf.Max(3, Mathf.RoundToInt(EffectiveMaxHealth * 0.035f)) : 0);
		ReceiveDamage(damage, _statusSource);
	}
}
