namespace BoxfishExample;

/// <summary>
/// A silly little class I added to this example to render AtlasResource items as blocks for UI.
/// </summary>
public static class VoxelIcon
{
	public static readonly Vector2 IconSize = new Vector2( 128, 128 );
	private static readonly Dictionary<string, Texture> _cache = new();

	private static Model GenerateVoxel( AtlasResource atlas, AtlasResource.AtlasItem item )
	{
		// Generate mesh vertices and indices.
		var material = Material.FromShader( "shaders/voxel_icon.shader" );
		var mesh = new Mesh( material );
		var vertices = new List<VoxelVertex>();
		var indices = new List<int>();
		var voxel = new Voxel( Color32.White, item.Index );

		for ( var i = 0; i < 3; i++ )
		{
			var face = i == 0 ? 0 : i + 2;
			for ( var j = 0; j < 4; ++j )
			{
				var vertexIndex = VoxelUtils.FaceIndices[(face * 4) + j];
				vertices.Add( new VoxelVertex( 0, 0, 0, vertexIndex, (byte)face, 0, voxel ) );
			}

			indices.Add( i * 4 + 0 );
			indices.Add( i * 4 + 2 );
			indices.Add( i * 4 + 1 );
			indices.Add( i * 4 + 2 );
			indices.Add( i * 4 + 0 );
			indices.Add( i * 4 + 3 );
		}

		// Create the model itself.
		mesh.CreateVertexBuffer<VoxelVertex>( vertices.Count, VoxelVertex.Layout, vertices.ToArray() );
		mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );

		return Model.Builder
			.AddMesh( mesh )
			.Create();
	}

	private static (SceneWorld world, SceneCamera camera) SetupScene( AtlasResource atlas, AtlasResource.AtlasItem item )
	{
		// Setup all the scene stuff.
		var world = new SceneWorld();
		var camera = new SceneCamera()
		{
			Size = IconSize,
			AntiAliasing = true,
			World = world,
			FieldOfView = 40f,
			AmbientLightColor = Color.White,
			BackgroundColor = Color.Transparent,
			ZFar = 1000f,
			ZNear = 1f,
			Position = Vector3.Forward * 2.5f,
			ClearFlags = ClearFlags.All,
			Rotation = Rotation.FromYaw( -180f )
		};

		_ = new SceneDirectionalLight( world, Rotation.From( -45, -90, 0 ), Color.White );

		// The voxel SceneObject.
		var voxel = GenerateVoxel( atlas, item );
		var voxelObject = new SceneObject( world, voxel );
		voxelObject.Flags.CastShadows = false;
		voxelObject.Rotation = Rotation.From( 20, -45, -20 );

		// Set Voxel attributes.
		var attributes = voxelObject.Attributes;
		attributes.Set( "VoxelScale", VoxelUtils.METER / 2.5f );
		attributes.Set( "VoxelAtlas", atlas.Texture );

		return (world, camera);
	}

	/// <summary>
	/// Fetch a voxel icon for a specific item inside of an atlas.
	/// </summary>
	/// <param name="atlas"></param>
	/// <param name="atlasItem"></param>
	/// <returns></returns>
	public static Texture Fetch( AtlasResource atlas, AtlasResource.AtlasItem atlasItem )
	{
		// Fetch from cache if possible.
		var id = $"{atlasItem.Name}_{atlasItem.Index}";
		if ( _cache.TryGetValue( id, out var cached ) ) 
			return cached;

		// Add our item to the queue.
		var renderTarget = Texture.CreateRenderTarget( $"{id}VoxelIcon", ImageFormat.RGBA8888, IconSize );
		var (world, camera) = SetupScene( atlas, atlasItem );
		Graphics.RenderToTexture( camera, renderTarget );
		
		_cache.Add( id, renderTarget );
		return renderTarget;
	}
}
