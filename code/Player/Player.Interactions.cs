namespace BoxfishExample;

partial class Player
{
	[Property, Range( 1, 128, 1 ), Feature( "Interactions" )]
	public int MaxItemStack { get; set; } = 32;

	[Property, Range( 1f, 512f, 0.5f ), Feature( "Interactions" )]
	public float InteractionDistance { get; set; } = 32;

	public int Selected { get; set; }
	public IReadOnlyDictionary<ushort, int> Items => _items;
	private Dictionary<ushort, int> _items = new();

	public void TryGiveItem( ushort index, int amount )
	{
		_ = _items.TryGetValue( index, out var count );
		_items[index] = Math.Clamp( count + amount, 0, MaxItemStack );
		
		if ( _items[index] <= 0 ) 
			_items.Remove( index );
	}

	private void ChangeVoxel( VoxelVolume volume, Vector3Int position, Voxel voxel )
	{
		// Let's query the position, and set the voxel using SetTrackedVoxel.
		var query = volume.Query( position );
		if ( volume.IsValidVoxel( voxel ) && query.HasVoxel ) return;

		if ( volume is NetworkedVoxelVolume netVolume ) // We want to broadcast set the voxel.
			netVolume.BroadcastSet( position, voxel );
		else
			volume.SetTrackedVoxel( position, voxel );

		// Call callback from atlas item.
		if ( !volume.Atlas.TryGet( query.Voxel.Texture, out var item ) )
			return;

		// We don't actually do anything in the base atlas with these, but you can do whatever you want!
		if ( !voxel.Valid ) item.OnBlockBroken?.Invoke( query );
		else item.OnBlockPlaced?.Invoke( query );
	}

	private void UpdateInteractions()
	{
		// Get VoxelVolume instance.
		var volume = WorldGenerator.Instance?.Volume;
		if ( !volume.IsValid() )
			return;

		// Get aimed voxel.
		var trace = Scene.Trace.Ray( ViewRay, InteractionDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var offset = Vector3.One * volume.Scale / 2f; // We need this cuz we want the center of the block.
		offset *= volume.WorldRotation;

		var position = volume.WorldToVoxel( trace.EndPosition + offset - trace.Normal * 1f );
		var query = volume.Query( position );

		if ( !query.HasVoxel )
			return;

		// Highlight hovered voxel.
		var bbox = BBox.FromPositionAndSize( query.GlobalPosition * volume.Scale, volume.Scale + 0.1f );
		Gizmo.Transform = volume.WorldTransform;
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.LineThickness = 5;
		Gizmo.Draw.LineBBox( bbox );

		// Break voxels..
		if ( Input.Pressed( "MouseLeft" ) )
		{
			TryGiveItem( query.Voxel.Texture, 1 );
			ChangeVoxel( volume, position, Voxel.Empty );
		}

		// Place voxels..
		else if( Input.Pressed( "MouseRight" ) )
		{
			var kvp = Items.ElementAtOrDefault( Selected );

			var id = kvp.Key;
			var amount = kvp.Value;
			if ( amount <= 0 ) return;

			var placePosition = volume.WorldToVoxel( trace.EndPosition + volume.Scale / 2f + trace.Normal * 5f );
			TryGiveItem( id, -1 );
			ChangeVoxel( volume, placePosition, new Voxel( Color32.White, id ) );
		}
	}
}
