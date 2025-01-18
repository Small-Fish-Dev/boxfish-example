namespace BoxfishExample;

partial class Player
{
	private void UpdateAnimations()
	{
		if ( !AnimationHelper.IsValid() )
			return;

		AnimationHelper.IsGrounded = IsGrounded;

		AnimationHelper.WithVelocity( Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );

		AnimationHelper.WithLook( EyeAngles.Forward * 50f );
	}
}
