using Sandbox;

namespace BoxfishExample;

public enum PhysicsRotationMode
{
	Towards,
	Roll,
	None
}

public struct PhysicsMoveStep
{
	public Vector3 Position;
	public Vector3 Velocity;
	public bool Collided;
}

public sealed class Explosive : Component
{
	[Property, Range( 1f, 32f, 0.25f )]
	public float VoxelRadius { get; set; }

	/// <summary> Additional damage dealt upon direct impact. </summary>
	[Property, Range( 0f, 200f, step: 1f )]
	[ShowIf( nameof( ExplodeOnImpact ), true )]
	public int DirectImpactDamage { get; set; } = 50;

	[Property]
	public bool ExplodeOnImpact { get; set; } = false;

	[Property, Range( 0f, 30f )]
	public float ExplodeCooldown { get; set; } = 5f;

	[Property, Range( 1f, 32f, 0.25f )]
	public float EffectRadius { get; set; } = 8f;

	[Property]
	public GameObject ParticleEffect { get; set; }

	[Property]
	public BBox Bounds { get; set; } = BBox.FromPositionAndSize( 0f, 10f );

	[Property, Category( "Physics" )]
	public float GravityScale { get; set; } = 1f;

	[Property, Category( "Physics" )]
	public float Friction { get; set; } = 0.99f;

	[Property, Category( "Physics" )]
	public Curve? ForceToExplosives { get; set; } = new Curve( new( 0f, 1f ), new( 1f, 0f ) )
	{
		ValueRange = new( 200f, 400f )
	};

	[Property, Category( "Physics" )]
	public bool AllowBounces { get; set; } = true;

	[Property, Category( "Physics" ), ShowIf( nameof( AllowBounces ), true )]
	public float BounceVelocityLoss { get; set; } = 0.5f;

	[Property, Category( "Physics" )]
	public PhysicsRotationMode RotationMode { get; set; } = PhysicsRotationMode.Towards;

	[Property, Category( "Physics" ), ShowIf( nameof( RotationMode ), PhysicsRotationMode.Roll )]
	public float Speed { get; set; } = 5f;

	[Property, Category( "Physics" ), ShowIf( nameof( RotationMode ), PhysicsRotationMode.Roll )]
	public Angles RotationAxis { get; set; } = Angles.Zero;

	[Property, Category( "Physics" )]
	public bool LockRotation { get; set; } = false;

	[Property, Category( "Sounds" )]
	public SoundEvent ExplodeSound { get; set; }

	public Vector3 Velocity { get; set; }
	public TimeSince SinceCreated { get; private set; }

	private bool _exploded;
	private GameObject _collidedWith;
	private GameObject _groundObject;
	private VoxelVolume _volume;

	protected override void OnStart()
	{
		base.OnStart();

		SinceCreated = 0f;

		if ( !LockRotation )
			WorldRotation *= Rotation.LookAt( Velocity );

		_volume = Scene.GetComponentInChildren<VoxelVolume>( includeSelf: true );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		using ( Gizmo.Scope( Id + "bounds" ) )
		{
			Gizmo.Draw.Color = Color.Orange;
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Draw.LineBBox( Bounds );
		}
	}

	[Rpc.Broadcast]
	private void CreateParticleEffect( Vector3 pos, Rotation r, float radius )
	{
		if ( !ParticleEffect.IsValid() )
			return;

		if ( !_volume.IsValid() )
			return;

		var obj = ParticleEffect.Clone();
		obj.WorldPosition = pos;
		obj.WorldRotation = r;

		if ( obj.Components.TryGet<ParticleSphereEmitter>( out var emitter, FindMode.EverythingInSelf ) )
			emitter.Radius = radius * _volume.Scale / 2f;
	}

	[Rpc.Broadcast]
	private void CreateSound( SoundEvent sound, Vector3 position, float volume )
	{
		if ( !sound.IsValid() )
			return;

		var handle = Sound.Play( sound );
		handle.Position = position;
		handle.Volume = volume;
	}

	public void Explode()
	{
		if ( _exploded || !GameObject.IsValid() )
			return;

		if ( ExplodeSound.IsValid() )
			CreateSound( ExplodeSound, WorldPosition, 1f );

		// Apply force to nearby explosives.
		var radius = _volume.Scale / 2f * VoxelRadius;
		var sphere = new Sphere( WorldPosition, radius );
		var objects = Scene.FindInPhysics( sphere );
		Log.Error( objects.Count() );
		if ( ForceToExplosives.HasValue )
		{
			var curve = ForceToExplosives.Value;
			DebugOverlay.Sphere( sphere, duration: 5f );

			foreach ( var obj in objects )
			{
				var explosive = obj.Components.Get<Explosive>( FindMode.EverythingInSelfAndParent );
				if ( !explosive.IsValid() )
					continue;

				var normal = (explosive.WorldPosition - WorldPosition).Normal;
				var distance = explosive.WorldPosition.Distance( WorldPosition ) / radius;
				distance = distance.Clamp( 0, 1 );
				Log.Error( $"xd {normal} {distance}" );

				var force = curve.Evaluate( distance );
				explosive.Velocity += normal * force;
				DebugOverlay.Text( obj.WorldPosition, $"{distance}", duration: 5f );
			}
		}

		// Damage the VoxelVolume.
		// This is an extension method, see code/Extensions.cs for more.
		_volume.Explode( WorldPosition + Vector3.Down * _volume.Scale / 2f, VoxelRadius );

		// Broadcast particle effect.
		if ( ParticleEffect.IsValid() )
			CreateParticleEffect( WorldPosition, WorldRotation, EffectRadius );

		// Self-destruct.
		GameObject.Destroy();
		_exploded = true;
	}

	public PhysicsMoveStep Move( Vector3 velocity, Vector3 position, ref GameObject groundObject, float delta, bool isObjectInstance = false )
	{
		// Apply gravity.
		if ( !groundObject.IsValid() )
			velocity += Scene.PhysicsWorld.Gravity * GravityScale * delta;

		// Helper
		var trace = Scene.Trace
			.Box( Bounds, new Ray( position, Vector3.Down ), 1f )
			.WithoutTags( "nocollide" );

		// Hack for impact explosives sliding along walls.
		if ( ExplodeOnImpact )
		{
			var extraTraceDist = Velocity.Normal * 8f;
			var tr = trace.FromTo( WorldPosition, WorldPosition + (Velocity * delta) + extraTraceDist ).Run();

			if ( tr.Hit )
			{
				_collidedWith = tr.GameObject;

				return new PhysicsMoveStep()
				{
					Position = tr.HitPosition,
					Velocity = Velocity,
					Collided = true
				};
			}
		}

		var helper = new CharacterControllerHelper( trace, position, velocity );

		if ( groundObject.IsValid() ) // Friction
			helper.Velocity *= Friction;

		helper.TryMove( delta );

		// Let's apply some bounce.
		var hitTrace = helper.TraceFromTo( helper.Position, helper.Position + helper.Velocity * delta );
		var collided = hitTrace.Hit;
		_collidedWith = hitTrace.GameObject;

		// Bounce.
		if ( collided && AllowBounces )
		{
			helper.Velocity = Vector3.Reflect( helper.Velocity.Normal, hitTrace.Normal )
				* velocity.Length
				* (1f - BounceVelocityLoss);

			if ( isObjectInstance && !LockRotation )
				WorldRotation *= Rotation.LookAt( Velocity );
		}

		// Handle grounding.
		groundObject = helper.TraceFromTo( position, position + Vector3.Down * 2f ).GameObject;

		return new PhysicsMoveStep()
		{
			Position = helper.Position,
			Velocity = helper.Velocity,
			Collided = collided || _collidedWith.IsValid()
		};
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( IsProxy || _exploded )
			return;

		// Move physics explosive.
		var move = Move( Velocity, WorldPosition, ref _groundObject, Time.Delta, true );
		Velocity = move.Velocity;
		WorldPosition = move.Position;

		// Rotate.
		if ( !Velocity.IsNearlyZero( 1f ) )
			WorldRotation = RotationMode switch
			{
				PhysicsRotationMode.Towards => Rotation.LookAt( Velocity ),
				PhysicsRotationMode.Roll => WorldRotation * (Rotation.From( RotationAxis.pitch, RotationAxis.yaw, RotationAxis.roll ).Normal * Velocity.Length * Time.Delta * Speed),
				_ => WorldRotation
			};

		// Handle explosion.
		if ( ExplodeCooldown != 0 && SinceCreated > ExplodeCooldown )
			Explode();

		if ( ExplodeOnImpact && (move.Collided || _collidedWith.IsValid() || _groundObject.IsValid()) )
			Explode();
	}
}
