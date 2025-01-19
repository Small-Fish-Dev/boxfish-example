using Boxfish.Library;

namespace BoxfishExample;

public static class Extensions
{
	/// <summary>
	/// Add a footstep event listener to this SkinnedModelRenderer and query the voxel world for step sounds.
	/// </summary>
	/// <param name="renderer"></param>
	/// <param name="volumeScale"></param>
	/// <returns></returns>
	public static bool AddVoxelFootsteps( this SkinnedModelRenderer renderer, float volumeScale = 1f )
	{
		if ( !renderer.IsValid() )
			return false;

		var volume = renderer.Scene.GetComponentInChildren<VoxelVolume>( includeSelf: true );
		if ( !volume.IsValid() )
			return false;

		renderer.OnFootstepEvent = @event =>
		{
			// Query somewhere around foot position.
			var position = volume.WorldToVoxel( @event.Transform.Position + Vector3.Down * volume.Scale / 2f );
			var query = volume.Query( position );
			if ( !query.HasVoxel )
				return;

			// Look for a "stepsound" field in data of AtlasItems..
			var soundEvent = "default-step";
			if ( volume.Atlas.TryGet( query.Voxel.Texture, out var atlasItem ) )
				soundEvent = atlasItem.GetData( "stepsound", soundEvent );

			// Play the sound event!
			var handle = Sound.Play( soundEvent );
			if ( handle is null )
				return;

			handle.Transform = @event.Transform;
			handle.Volume = @event.Volume * volumeScale;

			// Hehe, we don't really need to display this.
			// volume.DebugOverlay.Text( @event.Transform.Position, $"{soundEvent}", duration: 2f );
		};

		return true;
	}

	/// <summary>
	/// Explodes a radius, this will account for if the volume is a <see cref="NetworkedVoxelVolume"/>.
	/// </summary>
	/// <param name="volume"></param>
	/// <param name="position"></param>
	/// <param name="radius"></param>
	public static void Explode( this VoxelVolume volume, Vector3 position, float radius )
	{
		if ( !volume.IsValid() )
			return;

		var voxelPosition = volume.WorldToVoxel( position );

		// Broadcast set radius of voxels..
		if ( volume is NetworkedVoxelVolume netVolume )
		{
			netVolume.BroadcastSetRadius( voxelPosition, radius, Voxel.Empty );
			return;
		}

		// Just set voxels in a radius...
		var half = (int)(radius / 2f + 0.5f);
		for ( int x = -half; x <= half; x++ )
			for ( int y = -half; y <= half; y++ )
				for ( int z = -half; z <= half; z++ )
				{
					var current = voxelPosition + new Vector3Int( x, y, z );
					var distance = current.Distance( voxelPosition );
					if ( distance >= radius / 2f )
						continue;

					volume.SetTrackedVoxel( current, Voxel.Empty );
				}
	}
}
