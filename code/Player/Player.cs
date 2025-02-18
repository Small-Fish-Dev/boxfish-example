﻿namespace BoxfishExample;

public sealed partial class Player : Component
{
	public static Player Local { get; private set; }

	[Property, Feature( "Explosive" )]
	public GameObject ExplosivePrefab { get; set; }

	[Property, Feature( "Explosive" )]
	public float LaunchForce { get; set; } = 500f;

	[Property, Feature( "Components" )] 
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Feature( "Components" )]
	public CameraComponent Camera { get; set; }

	[Property, Feature( "Components" )]
	public BoxCollider Collider { get; set; }

	[Property, Feature( "Components" )]
	public CitizenAnimationHelper AnimationHelper { get; set; }

	public Connection Connection => Connection.Find( ConnectionId );
	[Sync] public Guid ConnectionId { get; private set; }

	public ClothingContainer Clothing { get; private set; }
	public Vector3 ResetPostion { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( !IsProxy )
		{
			ConnectionId = Connection.Local.Id;
			Local = this;
		}

		ResetPostion = WorldPosition;
		
		if ( Camera.IsValid() )
			Camera.Enabled = !IsProxy;

		if ( !IsProxy )
		{
			var json = ClothingContainer
				.CreateFromLocalUser()
				.Serialize();

			ApplyClothing( json );
		}

		if ( Renderer.IsValid() )
		{
			/*
			// https://i.imgur.com/R1fav6k.png
			// The following code shows you how you would implement custom step sounds for different textures.
			// Look at the image above to see what it would look like in the AtlasResource.
			// Please look at code/Extensions.cs if you wish to see the source.
			*/
			Renderer.AddVoxelFootsteps( 0.2f );
		}
	}

	[Rpc.Broadcast]
	public void ApplyClothing( string json )
	{
		Clothing = ClothingContainer.CreateFromJson( json );

		if ( Renderer.IsValid() )
			Clothing.Apply( Renderer );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		UpdateAnimations();
		UpdateModel();

		if ( IsProxy )
			return;

		UpdateHUD();
		UpdateCamera();
		UpdateMovement();
		UpdateInteractions();

		// Spawn explosives.
		if ( Input.Pressed( "Interact" ) && ExplosivePrefab.IsValid() )
		{
			var gameObject = ExplosivePrefab.Clone();
			gameObject.WorldPosition = ViewRay.Position + ViewRay.Forward * 5f;

			var explosive = gameObject.Components.Get<Explosive>( true );
			if ( explosive.IsValid() )
				explosive.Velocity = ViewRay.Forward * LaunchForce;

			gameObject.NetworkSpawn();
		}

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
	
		// Rotate model.
		Renderer.WorldRotation = Rotation.FromYaw( EyeAngles.yaw );

		// Update RenderType.
		var renderType = IsProxy || Thirdperson ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;
		Renderer.RenderType = renderType;

		// Update child RenderType.
		var renderers = Renderer.GameObject.GetComponentsInChildren<ModelRenderer>();
		foreach ( var child in renderers )
			child.RenderType = renderType;
	}
}
