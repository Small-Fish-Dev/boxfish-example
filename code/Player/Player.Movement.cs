namespace BoxfishExample;

partial class Player
{
	[Property, Range( 100, 800, 0.5f ), Feature( "Movement" )]
	public float WalkSpeed { get; set; }

	[Property, Range( 100, 800, 0.5f ), Feature( "Movement" )]
	public float NoclipSpeed { get; set; }

	[Property, Range( 100, 800, 0.5f ), Feature( "Movement" )]
	public float JumpForce { get; set; }

	[Property, Range( 0f, 32f, 0.5f ), Feature( "Movement" )]
	public float StepHeight { get; set; }

	[Property, Range( 0f, 32f, 0.5f ), Feature( "Movement" )]
	public Vector3 Gravity { get; set; }

	public bool Noclip { get; private set; }
	public Vector3 WishVelocity { get; private set; }
	public Vector3 Velocity { get; set; }
	public GameObject GroundObject { get; private set; }
	public bool IsGrounded => GroundObject.IsValid();
	public BBox Box => Collider.IsValid() 
		? BBox.FromPositionAndSize( Collider.Center, Collider.Scale )
		: default;

	private void UpdateMovement()
	{
		// Noclip!
		if ( Input.Pressed( "Noclip" ) )
			Noclip = !Noclip;

		if ( Noclip )
		{
			var up = Input.Down( "Jump" ) ? 1 : 0;
			WishVelocity = Input.AnalogMove.WithZ( up );
			if ( !WishVelocity.IsNearlyZero() )
			{
				WishVelocity *= EyeAngles.ToRotation();
				WishVelocity = WishVelocity.Normal;
				WishVelocity *= NoclipSpeed;
			}

			WorldPosition += WishVelocity * Time.Delta;

			Velocity = Vector3.Zero;
			GroundObject = null;

			return;
		}

		// Figure out WishVelocity...
		WishVelocity = Input.AnalogMove;
		if ( !WishVelocity.IsNearlyZero() )
		{
			WishVelocity *= Renderer.WorldRotation;
			WishVelocity = WishVelocity.Normal;
			WishVelocity *= WalkSpeed;
		}

		// Apply gravity!
		if ( !IsGrounded ) Velocity += Gravity * Time.Delta;

		// Character controller helper...!
		var trace = Scene.Trace.Box( Box, new Ray( 0, Vector3.Up ), 0f )
			.IgnoreGameObjectHierarchy( GameObject );

		var helper = new CharacterControllerHelper( trace, WorldPosition, Velocity );
		helper.TryMoveWithStep( Time.Delta, StepHeight );
		
		Velocity = helper.Velocity;
		WorldPosition = helper.Position;

		// Apply WishVelocity to Velocity.
		Velocity = WishVelocity.WithZ( Velocity.z );

		// Figure out GroundObject.
		var tr = helper.TraceFromTo( WorldPosition, WorldPosition + Vector3.Down );
		if ( tr.Hit ) GroundObject = tr.GameObject;
		else GroundObject = null;

		// Jumping!
		if ( Input.Down( "Jump" ) && IsGrounded ) 
			Velocity += JumpForce;
	}
}
