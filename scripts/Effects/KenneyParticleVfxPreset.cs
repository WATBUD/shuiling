using Godot;

// Editable from the Inspector through the Kenney particle preset resource.
[Tool]
[GlobalClass]
public partial class KenneyParticleVfxPreset : Resource
{
	[Export(PropertyHint.Range, "0.25,4.0,0.05,or_greater")]
	public float ParticleTextureScale { get; set; } = 1.60f;

	[Export(PropertyHint.Range, "0.25,6.0,0.05,or_greater")]
	public float SpriteTextureScale { get; set; } = 3.45f;

	[Export(PropertyHint.Range, "0.05,2.0,0.05,or_greater")]
	public float ImpactFlashScale { get; set; } = 0.38f;

	[Export(PropertyHint.Range, "0.0,15.0,0.1,or_greater")]
	public float BrightEmissionEnergy { get; set; } = 5.8f;

	[Export(PropertyHint.Range, "0.0,10.0,0.1,or_greater")]
	public float SoftEmissionEnergy { get; set; } = 2.2f;
}
