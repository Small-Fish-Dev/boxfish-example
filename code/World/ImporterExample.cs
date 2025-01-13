﻿
namespace BoxfishExample;

public sealed class ImporterExample : VoxelVolume, Component.ExecuteInEditor
{
	public override bool StoreEditorChunks { get; } = true;
	public override bool IgnoreOOBFaces { get; } = false; // Doesn't really work with elevation :/
	protected override async void OnStart()
	{
		base.OnStart();
		await Import( "voxel/summer_cottage.vox" ); // This is a base map included with the library.
		await GenerateMeshes( Chunks.Values );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		// Render chunk models in editor!
		if ( Game.IsPlaying || !Scene.IsEditor || Chunks == null || !Chunks.Any() )
			return;

		foreach ( var (position, model) in EditorChunks )
		{
			var transform = new Transform();
			var obj = Gizmo.Draw.Model( model, new Transform( position ) );
			SetAttributes( obj.Attributes );
		}
	}
}
