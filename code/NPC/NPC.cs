﻿using System.Diagnostics;
using System.Threading;

namespace BoxfishExample;

// Shrimple NPC that uses the voxel pathfinding!
public class NPC : Component
{
	[Property, Feature( "Components" )]
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Feature( "Components" )]
	public BoxCollider Collider { get; set; }

	[Property, Feature( "Components" )]
	public CitizenAnimationHelper AnimationHelper { get; set; }

	[Property, Feature( "NPC" )]
	public string State
	{
		get => _state;
		set
		{
			if ( value == _state )
				return;

			if ( !_states.TryGetValue( value, out _ ) )
			{
				Log.Warning( $"State {value} does not exist, defaulting to first state." );
				_state = _states.FirstOrDefault().Key ?? string.Empty;
				return;
			}

			_state = value;
			SinceStateChanged = 0f;
			StateChanged = true;
		}
	}
	private string _state;

	[Property, Feature( "NPC" )]
	public float PathRetraceFrequency { get; set; } = 1f;

	[Property, Range( 100, 800, 0.5f ), Feature( "Movement" )]
	public float WalkSpeed { get; set; }

	[Property, Range( 0f, 32f, 0.5f ), Feature( "Movement" )]
	public float StepHeight { get; set; }

	[Property, Range( 100, 800, 0.5f ), Feature( "Movement" )]
	public float JumpForce { get; set; }

	[Property, Range( 0f, 32f, 0.5f ), Feature( "Movement" )]
	public Vector3 Gravity { get; set; }

	public bool HasArrivedDestination { get; private set; } = true;
	public VoxelVolume.AStarPath CurrentPath
	{
		get => _currentPath;
		set
		{
			if ( !_currentPath.IsEmpty && _currentPath.Nodes == value.Nodes )
				return;
			_currentPath = value;
			HasArrivedDestination = false;
		}
	}
	private VoxelVolume.AStarPath _currentPath { get; set; }

	public TimeSince SinceStateChanged { get; private set; }
	public TimeUntil NextTraceCheck { get; private set; }
	public bool StateChanged { get; private set; }

	public Vector3 LookAt { get; set; }
	public Vector3 WishVelocity { get; private set; }
	public Vector3 Velocity { get; set; }
	public GameObject GroundObject { get; private set; }
	public bool IsGrounded => GroundObject.IsValid();
	public BBox Box => Collider.IsValid()
		? BBox.FromPositionAndSize( Collider.Center, Collider.Scale )
		: default;

	private CancellationTokenSource LastUsedPathToken { get; set; } = new();
	public Vector3 ResetPostion { get; private set; }

	public IReadOnlyDictionary<string, Action> States => _states;
	private Dictionary<string, Action> _states;

	private VoxelBounds _bounds;
	private VoxelVolume _volume;

	protected override void OnAwake()
	{
		_states = new()
		{
			["idle"] = Idle,
			["stalk"] = Stalk,
			["navigate"] = Navigate
		};

		State = _states.FirstOrDefault().Key ?? string.Empty;
	}

	protected override void OnStart()
	{
		base.OnStart();
		ResetPostion = WorldPosition;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) // maybe we want multiplayer in a future example...?
			return;

		WishVelocity = Vector3.Zero;

		if ( _states.TryGetValue( State, out var stateAction ) )
			stateAction?.Invoke();

		StateChanged = false;

		UpdateMovement();
		UpdateAnimations();
		RenderPath();

		// Reset position if we fall out of the world.
		if ( WorldPosition.z <= -10f )
			WorldPosition = ResetPostion;
	}

	public static NPC SpawnToWorld( string prefab, VoxelVolume volume, VoxelBounds bounds, int height = 3 )
	{
		if ( !volume.IsValid() || volume.Chunks?.Values is null )
			return null;

		using ( Game.ActiveScene.Push() )
		{
			var gameObject = PrefabScene.GetPrefab( prefab ).Clone();
			var npc = gameObject.GetOrAddComponent<NPC>();
			npc._volume = volume;
			npc._bounds = bounds;

			// Find a spawn.
			var pos = new Vector3Int(
				Game.Random.Int( bounds.Mins.x, bounds.Maxs.x ),
				Game.Random.Int( bounds.Mins.y, bounds.Maxs.y ),
				0
			);

			var query = default( VoxelVolume.VoxelQueryData );
			while ( pos.z++ < bounds.Maxs.z )
			{
				query = volume.Query( pos );
				if ( !query.HasVoxel )
				{
					// Find space for height?
					var blocked = false;
					var start = pos.z;
					for ( ; pos.z <= start + height; pos.z++ )
						blocked |= volume.Query( pos ).HasVoxel;

					if ( !blocked )
						break;
				}
			}

			// We have a position!
			gameObject.WorldPosition = volume.VoxelToWorld( query.GlobalPosition ) + Vector3.Up;

			return npc;
		}
	}

	private void RenderPath()
	{
		if ( CurrentPath.IsEmpty || State != "navigate" )
			return;

		Gizmo.Transform = global::Transform.Zero;
		for ( int i = 1; i < CurrentPath.Count; i++ )
		{
			var node = CurrentPath.Nodes.ElementAtOrDefault( i );
			var previous = CurrentPath.Nodes.ElementAtOrDefault( i - 1 );

			var nodePosition = _volume.VoxelToWorld( node.Data.GlobalPosition + Vector3Int.Up );
			var previousPosition = _volume.VoxelToWorld( previous.Data.GlobalPosition + Vector3Int.Up );

			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Draw.Color = Color.Blue;
			Gizmo.Draw.Line( previousPosition, nodePosition );
		}
	}

	private void UpdateAnimations()
	{
		if ( !AnimationHelper.IsValid() )
			return;

		AnimationHelper.EyesWeight = 1f;
		AnimationHelper.IsGrounded = IsGrounded;

		AnimationHelper.WithVelocity( Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );

		AnimationHelper.WithLook( LookAt );
	}

	// States
	void Idle()
	{
		if ( SinceStateChanged > 4f )
			State = "stalk";

		WorldRotation = WorldRotation * Rotation.FromYaw( Time.Delta * 500f );
		LookAt = WorldRotation.Forward;
	}

	private Player _stalkTarget;
	void Stalk()
	{
		// Pick a stalk target..
		if ( StateChanged || !_stalkTarget.IsValid() )
			_stalkTarget = Scene.GetComponentInChildren<Player>( true );

		if ( !_stalkTarget.IsValid() ) return;

		var direction = (_stalkTarget.WorldPosition - WorldPosition).Normal;
		var rotation = Rotation.LookAt( direction );
		WorldRotation = Rotation.FromYaw( rotation.Yaw() );
		LookAt = direction;

		if ( SinceStateChanged > 2f )
			State = "navigate";
	}

	void Navigate()
	{
		if ( StateChanged || NextTraceCheck )
		{
			if ( _stalkTarget.IsValid() )
				GeneratePathTo( _stalkTarget.WorldPosition );

			NextTraceCheck = 0f;
		}

		// Keep checking node status and move towards next node.
		if ( CurrentPath.IsEmpty )
			return;

		if ( HasArrivedDestination || SinceStateChanged > 10f )
			State = "idle";

		var minDistanceTillNext = _volume.Scale * 1.2f;
		var voxelCenter = ((Vector3)(_volume.Scale / 2f)).WithZ( _volume.Scale );

		var currentNode = CurrentPath.Nodes[0];
		var lastNode = CurrentPath.Nodes[^1];
		var nextNode = CurrentPath.Nodes[Math.Min( 1, CurrentPath.Count - 1 )];

		if ( WorldPosition.WithZ( 0 ).Distance( _volume.VoxelToWorld( nextNode.Data.GlobalPosition ).WithZ( 0 ) + voxelCenter ) <= minDistanceTillNext )
			CurrentPath.Nodes.RemoveAt( 0 );

		if ( currentNode == lastNode || currentNode is null )
			HasArrivedDestination = true;

		MoveTowards( nextNode );
	}

	void MoveTowards( VoxelVolume.AStarNode node )
	{
		// Set WishVelocity and rotate.
		var pos = _volume.VoxelToWorld( node.Data.GlobalPosition );
		var direction = (pos - WorldPosition).WithZ( 0f ).Normal;
		WishVelocity = direction;
		WishVelocity *= WalkSpeed;
		WorldRotation = Rotation.LookAt( direction );
	}

	void UpdateMovement()
	{
		// Apply gravity!
		if ( !IsGrounded ) Velocity += Gravity * Time.Delta;

		// Character controller helper...!
		var trace = Scene.Trace.Box( Box, new Ray( 0, Vector3.Up ), 0f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "nocollide" );

		var helper = new CharacterControllerHelper( trace, WorldPosition, Velocity );
		helper.TryMoveWithStep( Time.Delta, StepHeight );

		Velocity = helper.Velocity;
		WorldPosition = helper.Position;

		// Apply WishVelocity to Velocity.
		Velocity = WishVelocity.WithZ( Velocity.z );

		// Figure out GroundObject.
		var tr = helper.TraceFromTo( WorldPosition, WorldPosition + Vector3.Down * 2f );
		if ( tr.Hit ) GroundObject = tr.GameObject;
		else GroundObject = null;

		// Jump if we hit something...
		if ( !WishVelocity.IsNearlyZero() )
		{
			var pos = WorldPosition + _volume.Scale / 2f * Vector3.Up;
			var direction = WishVelocity.WithZ( 0f ).Normal * _volume.Scale * 1.5f;
			var jumpTr = helper.TraceFromTo( pos, pos + direction );
			if ( jumpTr.Hit ) 
				Jump();
		}
	}

	void Jump()
	{
		if ( !IsGrounded )
			return;

		Velocity += JumpForce;
		GroundObject = null;
	}

	private void GeneratePathTo( Vector3 target )
	{
		const int MAX_DURATION_MS = 300;
		const int MAX_DISTANCE = 150; // In UNITS.

		try
		{
			LastUsedPathToken?.Cancel();
		}
		catch ( ObjectDisposedException )
		{
			// Let's just ignore it
		}

		LastUsedPathToken = new CancellationTokenSource();
		LastUsedPathToken.CancelAfter( MAX_DURATION_MS );

		GameTask.RunInThreadAsync( async () =>
		{
			var tokenSource = LastUsedPathToken;

			try
			{
				if ( !_volume.IsValid() || !this.IsValid() || Transform is null ) return;

				var startingPos = _volume.GetClosestWalkable( _volume.WorldToVoxel( WorldPosition ), MAX_DISTANCE, tokenSource );
				var targetPos = _volume.GetClosestWalkable( _volume.WorldToVoxel( target ), MAX_DISTANCE, tokenSource );

				if ( startingPos == null || targetPos == null || startingPos.Value.GlobalPosition == targetPos.Value.GlobalPosition ) return;

				var startingNode = new VoxelVolume.AStarNode( startingPos.Value ) { Volume = _volume };
				var targetNode = new VoxelVolume.AStarNode( targetPos.Value ) { Volume = _volume };

				// You could check line of sight between starting node and target node, if it's true, we can just generate a path on 2 blocks!
				var computedPath = await _volume.ComputePath( startingNode, targetNode, tokenSource.Token );

				if ( computedPath.IsEmpty || computedPath.Count < 1 )
					return;

				computedPath.Simplify();
				CurrentPath = computedPath;
			}
			catch ( OperationCanceledException )
			{
				Log.Info( "Previous path token cancelled" );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Exception in NavigateTo: {ex}" );
			}

			tokenSource.Dispose();
		} );
	}
}
