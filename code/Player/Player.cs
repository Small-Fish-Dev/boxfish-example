namespace BoxfishExample;

public sealed class Player : Component
{
	[Property] 
	public SkinnedModelRenderer Renderer { get; set; }

	[Property]
	public CameraComponent Camera { get; set; }

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}
}
