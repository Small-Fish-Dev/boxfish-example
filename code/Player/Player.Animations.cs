namespace BoxfishExample;

partial class Player
{
	private void StartAnimations()
	{
		if ( !Renderer.IsValid() )
			return;

		/*
		// https://i.imgur.com/R1fav6k.png
		// The following code shows you how you would implement custom step sounds for different textures.
		// Look at the image above to see what it would look like in the AtlasResource.
		// 

		var volume = Scene.GetComponentInChildren<VoxelVolume>( includeSelf: true );
		Renderer.OnFootstepEvent = @event =>
		{
			if ( !volume.IsValid() ) 
				return;

			var position = volume.WorldToVoxel( @event.Transform.Position + Vector3.Down * volume.Scale / 2f );
			var query = volume.Query( position );
			if ( !query.HasVoxel ) 
				return;

			if ( !volume.Atlas.TryGet( query.Voxel.Texture, out var atlasItem ) )
				return;

			var soundEvent = atlasItem.GetData( "stepsound" );
			if ( soundEvent == null )
				return;

			DebugOverlay.Text( @event.Transform.Position, $"{soundEvent}", duration: 2f );
		};*/
	}

	private void UpdateAnimations()
	{
		if ( !AnimationHelper.IsValid() )
			return;

		AnimationHelper.IsGrounded = IsGrounded;

		AnimationHelper.WithVelocity( Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );

		AnimationHelper.WithLook( EyeAngles.Forward * 50f );
	}
}
