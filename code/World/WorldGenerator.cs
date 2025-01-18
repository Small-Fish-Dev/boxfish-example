namespace BoxfishExample;

public sealed class WorldGenerator : Component
{
	public static WorldGenerator Instance { get; private set; }

	[Property]
	public VoxelVolume Volume { get; set; }

	[Property]
	public PrefabFile NPCPrefab { get; set; }

	[Property, Range( 0, 64, 1 )]
	public int MaxTrees { get; set; } = 16;

	[Property, Range( 1, 20, 1 )]
	public int WorldRadius { get; set; } = 4;

	[Property]
	public int? OverrideSeed { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();
		Instance = this;
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor ) return;

		// Generate some perlin noise terrain.
		var chunks = new Dictionary<Vector3Int, VoxelVolume.Chunk>();
		var seed = OverrideSeed ?? Game.Random.Int( 0, int.MaxValue - 1 );
		var heightMap = new List<Vector3Int>();

		VoxelVolume.Chunk CreatePerlinChunk( int x, int y )
		{
			var chunk = new VoxelVolume.Chunk( x, y, 0, chunks );

			for ( byte i = 0; i < VoxelUtils.CHUNK_SIZE; i++ )
				for ( byte j = 0; j < VoxelUtils.CHUNK_SIZE; j++ )
				{
					var position = new Vector2Int( x * VoxelUtils.CHUNK_SIZE + i, y * VoxelUtils.CHUNK_SIZE + j );
					var noise = Sandbox.Utility.Noise.Perlin( position.x, position.y, seed );
					var height = (int)Math.Clamp( noise * VoxelUtils.CHUNK_SIZE, 1, VoxelUtils.CHUNK_SIZE );
					heightMap.Add( new Vector3Int( position.x, position.y, height ) );

					for ( byte k = 0; k < height; k++ )
					{
						const ushort GRASS = 1;
						const ushort DIRT = 2;
						var tex = k >= height - 1 ? GRASS : DIRT;

						chunk.SetVoxel( i, j, k, new Voxel( Color32.White, tex ) );
					}
				}

			return chunk;
		}

		for ( int x = -WorldRadius; x < WorldRadius; x++ )
			for ( int y = -WorldRadius; y < WorldRadius; y++ )
			{
				var chunk = CreatePerlinChunk( x, y );
				chunks.Add( new Vector3Int( x, y, 0 ), chunk );
			};

		Volume.SetChunks( chunks );

		// Place some trees.
		void PlaceTree( int x, int y, int z )
		{
			const ushort LOGS = 4;
			const ushort LEAVES = 3;

			// Leaves
			var center = new Vector3Int( x, y, z + 3 );
			for ( int i = -4; i < 4; i++ )
				for ( int j = -4; j < 4; j++ )
					for ( int k = 0; k < 5; k++ )
					{
						var pos = center + new Vector3Int( i, j, k );
						if ( pos.Distance( center ) >= 2.25f ) // freaky float
							continue;

						Volume.SetVoxel( pos.x, pos.y, pos.z, new Voxel( Color32.White, LEAVES ) );
					}

			// Trunk
			Volume.SetVoxel( x, y, z, new Voxel( Color32.White, LOGS ) );
			Volume.SetVoxel( x, y, z + 1, new Voxel( Color32.White, LOGS ) );
			Volume.SetVoxel( x, y, z + 2, new Voxel( Color32.White, LOGS ) );
			Volume.SetVoxel( x, y, z + 3, new Voxel( Color32.White, LOGS ) );
		}

		for ( int i = 0; i < MaxTrees; i++ )
		{
			Game.SetRandomSeed( seed + 69 + i ); // hehe,,,
			var random = Game.Random.FromList( heightMap );
			PlaceTree( random.x, random.y, random.z );
		}

		// Set volume's chunks and generate them.
		await Volume.GenerateMeshes( chunks.Values );

		// Spawn our spooky NPC...
		if ( NPCPrefab.IsValid() )
			NPC.SpawnToWorld( NPCPrefab.ResourcePath, Volume, Volume.ComputeBounds() );
	}
}
