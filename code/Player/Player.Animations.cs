namespace BoxfishExample;

partial class Player
{
	private void UpdateAnimations()
	{
		if ( !AnimationHelper.IsValid() || !Renderer.IsValid() )
			return;

		Renderer.WorldRotation = Rotation.FromYaw( EyeAngles.yaw );

		AnimationHelper.IsGrounded = IsGrounded;

		AnimationHelper.WithVelocity( Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );

		AnimationHelper.WithLook( EyeAngles.Forward * 50f );
	}
}
