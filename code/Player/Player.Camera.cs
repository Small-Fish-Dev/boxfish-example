namespace BoxfishExample;

partial class Player
{
	[Property, Range( 5, 100, 0.5f ), Feature( "Camera" )]
	public float MinCameraDistance { get; set; }

	[Property, Range( 5, 500, 0.5f ), Feature( "Camera" )]
	public float MaxCameraDistance { get; set; }

	public Angles EyeAngles { get; private set; }
	public bool Thirdperson { get; private set; }
	public float CameraDistance { get; private set; }

	public Ray ViewRay => new Ray( EyeWorldPosition, EyeAngles.Forward );
	public Vector3 EyePosition => Vector3.Up * 62;
	public Vector3 EyeWorldPosition => EyePosition + WorldPosition;

	private float _lerpDistance;

	private void UpdateCamera()
	{
		if ( !Camera.IsValid() || !Renderer.IsValid() )
			return;

		// Look!
		var angles = EyeAngles + Input.AnalogLook;
		angles.pitch = angles.pitch.Clamp( -89, 89 );
		EyeAngles = angles;

		if ( Input.Pressed( "Camera" ) )
			Thirdperson = !Thirdperson;

		// Thirdperson camera!
		var backward = -EyeAngles.Forward;

		if ( Thirdperson )
		{
			CameraDistance = MathX.Clamp( CameraDistance - Input.MouseWheel.y * 15f, MinCameraDistance, MaxCameraDistance );
			_lerpDistance = MathX.Lerp( _lerpDistance, CameraDistance, 15f * Time.Delta );

			var tr = Scene.Trace.Ray( new Ray( EyeWorldPosition, backward ), _lerpDistance )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "nocollide" )
				.Radius( 5f )
				.Run();

			Camera.WorldPosition = Noclip ? EyeWorldPosition + backward * _lerpDistance : tr.EndPosition;
			Camera.WorldRotation = EyeAngles.ToRotation();

			return;
		}

		// Firstperson camera!
		Camera.WorldRotation = EyeAngles.ToRotation();
		Camera.LocalPosition = EyePosition;
	}
}
