using Sandbox.Rendering;
using System.Linq;

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

	private VoxelVolume _volume;

	public void TryGiveItem( ushort index, int amount )
	{
		_ = _items.TryGetValue( index, out var count );
		_items[index] = Math.Clamp( count + amount, 0, MaxItemStack );
	}

	private void ChangeVoxel( Vector3Int position, Voxel voxel )
	{
		// Let's query the position, set the voxel, and update all affected neighboring chunks' meshes.
		var query = _volume.Query( position );
		_volume.SetVoxel( position, voxel );

		var neighbors = query.Chunk.GetNeighbors( query.LocalPosition.x, query.LocalPosition.y, query.LocalPosition.z );
		Task.RunInThreadAsync( () => _volume.GenerateMeshes( neighbors ) );
	}

	private void UpdateInteractions()
	{
		// Get VoxelVolume instance.
		_volume ??= Scene.GetComponentInChildren<VoxelVolume>( includeSelf: true );
		if ( !_volume.IsValid() )
		{
			_volume = null;
			return;
		}

		// Get aimed voxel.
		var trace = Scene.Trace.Ray( ViewRay, InteractionDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var position = _volume.WorldToVoxel( trace.EndPosition + _volume.Scale / 2f - trace.Normal * 1f );
		var query = _volume.Query( position );

		if ( !query.HasVoxel )
			return;

		// Highlight hovered voxel.
		var bbox = BBox.FromPositionAndSize( query.GlobalPosition * _volume.Scale, _volume.Scale + 0.1f );
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.LineThickness = 5;
		Gizmo.Draw.LineBBox( bbox );

		// Break voxels..
		if ( Input.Pressed( "MouseLeft" ) )
		{
			TryGiveItem( query.Voxel.Texture, 1 );
			ChangeVoxel( position, Voxel.Empty );
		}

		// Place voxels..
		else if( Input.Pressed( "MouseRight" ) )
		{
			var kvp = Items.ElementAtOrDefault( Selected );

			var id = kvp.Key;
			var amount = kvp.Value;
			if ( amount <= 0 ) return;

			var placePosition = _volume.WorldToVoxel( trace.EndPosition + _volume.Scale / 2f + trace.Normal * 5f );
			TryGiveItem( id, -1 );
			ChangeVoxel( placePosition, new Voxel( Color32.White, id ) );
		}
	}
}
