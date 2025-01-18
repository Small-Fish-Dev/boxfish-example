namespace BoxfishExample;

public sealed partial class Player : Component
{
	public static Player Local { get; private set; }

	[Property, Feature( "Components" )] 
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Feature( "Components" )]
	public CameraComponent Camera { get; set; }

	[Property, Feature( "Components" )]
	public BoxCollider Collider { get; set; }

	[Property, Feature( "Components" )]
	public CitizenAnimationHelper AnimationHelper { get; set; }

	public ClothingContainer Clothing { get; private set; }
	public Vector3 ResetPostion { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();
		
		Local = this;
		ResetPostion = WorldPosition;
		Clothing = ClothingContainer.CreateFromLocalUser();

		if ( Renderer.IsValid() )
		{
			Clothing.Apply( Renderer );

			/*
			// https://i.imgur.com/R1fav6k.png
			// The following code shows you how you would implement custom step sounds for different textures.
			// Look at the image above to see what it would look like in the AtlasResource.
			// Please look at code/Extensions.cs if you wish to see the source.
			*/
			Renderer.AddVoxelFootsteps();
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		UpdateHUD();
		UpdateCamera();
		UpdateMovement();
		UpdateAnimations();
		UpdateModel();
		UpdateInteractions();

		// Reset position if we fall out of the world.
		if ( WorldPosition.z <= -10f )
			WorldPosition = ResetPostion;
	}

	private void UpdateHUD()
	{
		if ( !Camera.IsValid() )
			return;

		// Crosshair.
		Camera.Hud.DrawCircle( Screen.Size / 2f, 5f, Color.White.WithAlpha( 0.5f ) );
	}

	private void UpdateModel()
	{
		if ( !Renderer.IsValid() )
			return;

		// Rotate renderer.
		Renderer.WorldRotation = Rotation.FromYaw( EyeAngles.yaw );

		// Update RenderType.
		var renderType = Thirdperson ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;
		Renderer.RenderType = renderType;

		// Update child RenderType.
		var renderers = Renderer.GameObject.GetComponentsInChildren<ModelRenderer>();
		foreach ( var child in renderers )
			child.RenderType = renderType;
	}
}
