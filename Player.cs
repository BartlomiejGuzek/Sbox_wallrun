using ball.weapons;
using Sandbox;
using System;
using System.Linq;

partial class DeathmatchPlayer : Player
{
	TimeSince timeSinceDropped;
	
	//Wallrun properties and variables
	public bool isWallRunning { get; set; } = false;
	public bool isWallRunningLeft { get; set; } = false;
	public bool isWallRunningRight { get; set; } = false;
	public Vector3 wallRunTraceLeft;
	public Vector3 wallRunTraceRight;
	public Trace traceLeft;
	public Trace traceRight;
	private bool jumped;
	public Rotation faceDirection = new Rotation();
	public TraceResult traceLeftResult;
	public TraceResult traceRightResult;
	//Template vector for wallrun direction and speed
	public Vector3 wallrunVector { get; set; } = new Vector3( 0, 0, 800 * .5f );

	//[ReplicatedVar( "debug_ballplayercontroller" )]
	public static bool Debug { get; set; } = true;

	public bool SupressPickupNotices { get; private set; }

	public DeathmatchPlayer()
	{
		Inventory = new DmInventory( this );
	}

	public override void Respawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );

		Controller = new WalkController();
		Animator = new StandardPlayerAnimator();
		Camera = new FirstPersonCamera();


		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Dress();
		ClearAmmo();

		SupressPickupNotices = true;

		//Inventory.Add( new Pistol(), true );
		//Inventory.Add( new Shotgun() );
		//Inventory.Add( new SMG() );
		//Inventory.Add( new Crossbow() );
		Inventory.Add( new GravGun() );

		GiveAmmo( AmmoType.GravGun, 100 );
		//GiveAmmo( AmmoType.Buckshot, 8 );
		//GiveAmmo( AmmoType.Crossbow, 4 );

		SupressPickupNotices = false;
		Health = 100;

		base.Respawn();
	}
	public override void OnKilled()
	{
		base.OnKilled();

		Inventory.DropActive();
		Inventory.DeleteContents();

		BecomeRagdollOnClient( LastDamage.Force, GetHitboxBone( LastDamage.HitboxIndex ) );

		Controller = null;
		Camera = new SpectateRagdollCamera();

		EnableAllCollisions = false;
		EnableDrawing = false;
	}


	public override void Simulate( Client cl )
	{
		if ( Debug )
		{
			var lineOffset = 0;
			if ( Host.IsServer ) lineOffset = 10;
			DebugOverlay.ScreenText( lineOffset + 0, $"        Position: {Position}" );
			DebugOverlay.ScreenText( lineOffset + 1, $"        Velocity: {Velocity}" );
			DebugOverlay.ScreenText( lineOffset + 2, $"		   BaseVelocity: {BaseVelocity}" );
			DebugOverlay.ScreenText( lineOffset + 3, $"		   GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]" );
			DebugOverlay.ScreenText( lineOffset + 4, $"------------------------------------" );
			DebugOverlay.ScreenText( lineOffset + 5, $"        Wallrun velocity:" );
			DebugOverlay.ScreenText( lineOffset + 6, $"        Is wallruning: {isWallRunning}" );
			DebugOverlay.ScreenText( lineOffset + 7, $"        Wallrun left: {isWallRunningLeft}" );
			DebugOverlay.ScreenText( lineOffset + 8, $"        Wallrun right: {isWallRunningRight}" );
			DebugOverlay.ScreenText( lineOffset + 9, $"        Wallrun right: {faceDirection}  " );
		}
		WallRun();






		//if ( cl.NetworkIdent == 1 )
		//	return;

		base.Simulate( cl );

		//
		// Input requested a weapon switch
		//
		if ( Input.ActiveChild != null )
		{
			ActiveChild = Input.ActiveChild;
		}

		if ( LifeState != LifeState.Alive )
			return;

		TickPlayerUse();

		if ( Input.Pressed( InputButton.View ) )
		{
			if ( Camera is ThirdPersonCamera )
			{
				Camera = new FirstPersonCamera();
			}
			else
			{
				Camera = new ThirdPersonCamera();
			}
		}

		if ( Input.Pressed( InputButton.Drop ) )
		{
			var dropped = Inventory.DropActive();
			if ( dropped != null )
			{
				if ( dropped.PhysicsGroup != null )
				{
					dropped.PhysicsGroup.Velocity = Velocity + (EyeRot.Forward + EyeRot.Up) * 300;
				}

				timeSinceDropped = 0;
				SwitchToBestWeapon();
			}
		}



		if ( Input.Pressed( InputButton.Jump ) )
		{
			jumped = true;
		}

		SimulateActiveChild( cl, ActiveChild );

		//
		// If the current weapon is out of ammo and we last fired it over half a second ago
		// lets try to switch to a better wepaon
		//
		if ( ActiveChild is BaseDmWeapon weapon && !weapon.IsUsable() && weapon.TimeSincePrimaryAttack > 0.5f && weapon.TimeSinceSecondaryAttack > 0.5f )
		{
			SwitchToBestWeapon();
		}
	}



	/// <summary>
	/// Perform ray trace checks to see if player is next to the wall
	/// </summary>
	public bool WallRunCheck()
	{
		//Cast ray on the left and right                            * Ray distance
		wallRunTraceLeft = Position + Vector3.Left * Input.Rotation * 30;
		wallRunTraceRight = Position + Vector3.Right * Input.Rotation * 30;
		//Ignore player when casting
		traceLeft = Trace.Ray( Position, wallRunTraceLeft ).Ignore( this );
		traceRight = Trace.Ray( Position, wallRunTraceRight ).Ignore( this );
		//Debug trace lines
		DebugOverlay.Line( Position, wallRunTraceLeft, Color.Red, 4 );
		DebugOverlay.Line( Position, wallRunTraceRight, Color.Blue, 4 );
		//Run traces
		traceLeftResult = traceLeft.Run();
		traceRightResult = traceRight.Run();
		//Check if left or right ray hit something also check if player is moving in any direction. We dont want standing players run up the walls
		if ( traceLeftResult.Hit && Velocity.x !=0 && Velocity.y != 0)
		{
			isWallRunningLeft = true;
			isWallRunning = true;
			//Play footstep sound - No idea how to use this method in legit way
			this.OnAnimEventFootstep( traceLeftResult.EndPos, 1, 1 );
			return isWallRunning;
		}
		else if ( traceRightResult.Hit && Velocity.x != 0 && Velocity.y != 0 )
		{
			isWallRunningRight = true;
			isWallRunning = true;
			//Play footstep sound - No idea how to use this method in legit way
			this.OnAnimEventFootstep( traceRightResult.EndPos, 1, 1 );
			return isWallRunning;
		}
		else
		{
			isWallRunning = false;
			return isWallRunning;
		}
	}

	/// <summary>
	/// Perform wallRun
	/// </summary>
	public void WallRun()
	{
		//Check if player is in the air and pressed jump button
		if ( GroundEntity == null && jumped )
		{
			//Perform wallrun check - traces etc
			if ( WallRunCheck() )
			{
				//Add wallrunVector to current velocity + add some speed in forward direction - TODO add a vector that will stick player to the wall if he didnt start a run at the start of geo
				Velocity += (Velocity.ClampLength( 40 ) + wallrunVector) * Time.Delta;
				//Velocity += wallrunVector * traceLeftResult.Normal * Time.Delta;
				//Watch for another jump button press
				if ( Input.Pressed( InputButton.Jump ) )
				{
					WallRunJump();
				}
			}
			else
			{
				//If not wallruning but in the air and pressed jump button - perform wallrun jump anyway
				if ( Input.Pressed( InputButton.Jump ) )
				{

					WallRunJump();
				}
			}
		}
		else
		{
			//If not wallrunning reset bools
			WallRunResetBools();
		}
	}


	/// <summary>
	/// Reset bools
	/// </summary>
	public void WallRunResetBools()
	{
		isWallRunning = false;
		isWallRunningLeft = false;
		isWallRunningRight = false;
		jumped = false;
	}
	/// <summary>
	/// Perform Wallrun jump
	/// </summary>
	public void WallRunJump()
	{
		//Trace behind the player and look for some geo to launch from
		var wallRunTraceBehind = Position + Vector3.Backward * Input.Rotation * 60;
		var behindTrace = Trace.Ray( Position, wallRunTraceBehind ).Ignore( this );
		var traceBehindResult = behindTrace.Run();
		jumped = true;
		//If trace was successful and player is in the air 
		if ( traceBehindResult.Hit && GroundEntity == null )
		{
			//Debug line
			DebugOverlay.Line( Position, wallRunTraceBehind, Color.Green, 8 );
			//Reset player velocity in order to stop the momentum
			Velocity = new Vector3( 0, 0, 200 );
			//Apply some force in the direction the player is currently looking at
			Velocity += Vector3.Forward * Input.Rotation * 400;
			//lean = lean.LerpTo( Velocity.Dot(Rotation.Backward) * 0.3f, Time.Delta * 0.8f );
		}
	}


	public void SwitchToBestWeapon()
	{
		var best = Children.Select( x => x as BaseDmWeapon )
			.Where( x => x.IsValid() && x.IsUsable() )
			.OrderByDescending( x => x.BucketWeight )
			.FirstOrDefault();

		if ( best == null ) return;

		ActiveChild = best;
	}

	public override void StartTouch( Entity other )
	{
		if ( timeSinceDropped < 1 ) return;

		base.StartTouch( other );
	}

	Rotation lastCameraRot = Rotation.Identity;

	public override void PostCameraSetup( ref CameraSetup setup )
	{
		base.PostCameraSetup( ref setup );

		if ( lastCameraRot == Rotation.Identity )
			lastCameraRot = setup.Rotation;

		var angleDiff = Rotation.Difference( lastCameraRot, setup.Rotation );
		var angleDiffDegrees = angleDiff.Angle();
		var allowance = 20.0f;

		if ( angleDiffDegrees > allowance )
		{
			// We could have a function that clamps a rotation to within x degrees of another rotation?
			lastCameraRot = Rotation.Lerp( lastCameraRot, setup.Rotation, 1.0f - (allowance / angleDiffDegrees) );
		}
		else
		{
			//lastCameraRot = Rotation.Lerp( lastCameraRot, Camera.Rotation, Time.Delta * 0.2f * angleDiffDegrees );
		}

		// uncomment for lazy cam
		//camera.Rotation = lastCameraRot;

		if ( setup.Viewer != null )
		{
			AddCameraEffects( ref setup );
		}
	}

	float walkBob = 0;
	float lean = 0;
	float fov = 0;

	private void AddCameraEffects( ref CameraSetup setup )
	{
		var speed = Velocity.Length.LerpInverse( 0, 320 );
		var forwardspeed = Velocity.Normal.Dot( setup.Rotation.Forward );

		var left = setup.Rotation.Left;
		var up = setup.Rotation.Up;

		if ( GroundEntity != null )
		{
			walkBob += Time.Delta * 25.0f * speed;
		}

		//Add wallrun camera effect
		if ( isWallRunning )
		{
			if ( isWallRunningLeft )
			{
				lean = lean.LerpTo( Velocity.Dot( setup.Rotation.Forward ) * 0.03f, Time.Delta * 15.0f );
			}
			else
			{
				lean = lean.LerpTo( Velocity.Dot( setup.Rotation.Forward ) * -0.03f, Time.Delta * 15.0f );
			}

		}



		setup.Position += up * MathF.Sin( walkBob ) * speed * 2;
		setup.Position += left * MathF.Sin( walkBob * 0.6f ) * speed * 1;

		// Camera lean - left to right
		lean = lean.LerpTo( Velocity.Dot( setup.Rotation.Right ) * 0.00f, Time.Delta * 15.0f );

		var appliedLean = lean;
		appliedLean += MathF.Sin( walkBob ) * speed * 0.2f;
		setup.Rotation *= Rotation.From( 0, 0, appliedLean );

		speed = (speed - 0.7f).Clamp( 0, 1 ) * 3.0f;

		fov = fov.LerpTo( speed * 20 * MathF.Abs( forwardspeed ), Time.Delta * 2.0f );

		setup.FieldOfView += fov;

		//	var tx = new Sandbox.UI.PanelTransform();
		//	tx.AddRotation( 0, 0, lean * -0.1f );

		//	Hud.CurrentPanel.Style.Transform = tx;
		//	Hud.CurrentPanel.Style.Dirty(); 

	}

	DamageInfo LastDamage;


	public override void TakeDamage( DamageInfo info )
	{
		LastDamage = info;

		// hack - hitbox 0 is head
		// we should be able to get this from somewhere
		if ( info.HitboxIndex == 0 )
		{
			info.Damage *= 2.0f;
		}

		base.TakeDamage( info );

		if ( info.Attacker is DeathmatchPlayer attacker && attacker != this )
		{
			// Note - sending this only to the attacker!
			attacker.DidDamage( To.Single( attacker ), info.Position, info.Damage, Health.LerpInverse( 100, 0 ) );

			TookDamage( To.Single( this ), info.Weapon.IsValid() ? info.Weapon.Position : info.Attacker.Position );
		}
	}

	[ClientRpc]
	public void DidDamage( Vector3 pos, float amount, float healthinv )
	{
		Sound.FromScreen( "dm.ui_attacker" )
			.SetPitch( 1 + healthinv * 1 );

		HitIndicator.Current?.OnHit( pos, amount );
	}

	[ClientRpc]
	public void TookDamage( Vector3 pos )
	{
		//DebugOverlay.Sphere( pos, 5.0f, Color.Red, false, 50.0f );

		DamageIndicator.Current?.OnHit( pos );
	}
}
