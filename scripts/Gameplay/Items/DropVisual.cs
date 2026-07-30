using Godot;

// Root script for the drop visual scenes (GoldDrop / ItemDrop / CardDrop).
//
// The scene owns everything visual — mesh, glow, particles, and a self-running
// AnimationPlayer for the spin/bob — so artists can open the .tscn and preview
// it in the editor without running the game. This script only feeds in the
// runtime-dynamic bits: the label text/colour and (for item drops) a per-item
// body tint. It also starts/stops particles as a pooled instance is reused.
//
// Child nodes are looked up by fixed name (see the .tscn files):
//   "Label"     — Label3D shown above the drop (optional)
//   "Particles" — GpuParticles3D emitted while the drop is active (optional)
//   "Body"      — MeshInstance3D whose surface 0 is tinted when Tintable (optional)
public partial class DropVisual : Node3D
{
	// When true the "Body" mesh is recoloured per drop via DropPalette. Item
	// drops set this; gold and card drops keep their authored materials.
	[Export] public bool Tintable { get; set; }

	private Label3D? _label;
	private GpuParticles3D? _particles;
	private MeshInstance3D? _body;
	private bool _resolved;

	public override void _Ready()
	{
		ResolveNodes();
	}

	private void ResolveNodes()
	{
		if (_resolved)
		{
			return;
		}

		_label = GetNodeOrNull<Label3D>("Label");
		_particles = FindChild("Particles", recursive: true, owned: false) as GpuParticles3D;
		_body = FindChild("Body", recursive: true, owned: false) as MeshInstance3D;
		_resolved = true;
	}

	// Applies the runtime-dynamic label and tint. Safe to call before _Ready.
	public void Configure(string labelText, Color tint)
	{
		ResolveNodes();

		if (_label != null)
		{
			_label.Text = labelText;
			_label.Modulate = tint.Lightened(0.2f);
		}

		if (Tintable && _body != null)
		{
			_body.SetSurfaceOverrideMaterial(0, DropPalette.GetBodyMaterial(tint));
		}
	}

	// Called when a pooled drop becomes active again.
	public void OnActivated()
	{
		ResolveNodes();
		if (_particles != null)
		{
			// Restart clears any left-over particles from the previous life and
			// re-seeds emission; deferred so the RenderingServer has registered
			// the instance before the first draw.
			_particles.CallDeferred(GpuParticles3D.MethodName.Restart);
			_particles.Emitting = true;
		}
	}

	// Called when the drop is recycled back into the pool.
	public void OnRecycled()
	{
		if (_particles != null)
		{
			_particles.Emitting = false;
		}
	}
}
