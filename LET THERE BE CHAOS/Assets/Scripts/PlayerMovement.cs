﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public enum PlayerStates
    {
        Grounded,//on the ground
        InAir, //in the air
        OnWalls, //running on the walls
        LedgeGrab, //pulling up a ledge
    }

    private PlayerCollision Coli;
    private Rigidbody Rigid;
    private CapsuleCollider Cap;
    private Animator Anim;

    [Header("Physics")]
    public float MaxSpeed; //how fast we run forward
    public float BackwardsSpeed; //how fast we run backwards
    public float InAirControl; //how much control you have over your movement direction when in air

    private float ActSpeed; //how much speed is applied to the rigidbody
    public float Acceleration; //how fast we build speed
    public float Decceleration; //how fast we slow down
    public float DirectionControl = 8; //how much control we have over changing direction
    public PlayerStates CurrentState; //the current state the player is in
    private float InAirTimer; //how long we are in the air for (this is for use when wall running or falling off the ground
    private float OnGroundTimer;
    private float AdjustmentAmt; //the amount added to our player acceleration, this is used for adjusting to new speeds such as when we slide


    [Header("Turning")]
    public float TurnSpeed; //how fast we turn when on the ground
    public float TurnSpeedInAir; //how fast we turn when in air
    public float TurnSpeedOnWalls; //how fast we turn when on the walls
    public float LookUpSpeed; //how fast we look up and down
    public Camera Head; //what will function as our players head to tilt up and down (this is a pivot point in our model that the cameras are children of
    private float YTurn; //how much we have turned left and right
    private float XTurn; //how much we have turned Up or Down
    public float MaxLookAngle = 65; //how much we can look up
    public float MinLookAngle = -30; //how much we can look down

    [Header("Jumping")]
    private int wallJump;
    private float wallJumpReset;
    public float JumpHeight; //how high we jump

    [Header("Wall Runs")]
    public float WallRunTime = 2f; //how long we can run on walls
    private float ActWallRunTime = 0; //how long we are actually on a wall
    public float TimeBeforeWallRun = 0.2f; //how long we have to be in the air before we can wallrun
    public float WallRunUpwardsMovement = 2f; //how much we move up a wall when running on it (make this 0 to just slightly move down a wall we run on
    public float WallRunSpeedAcceleration = 2f; //how quickly we build speed to run up walls

    [Header("Crouching")]
    public float CrouchSpeed = 10; //how fast we move when crouching
    public float CrouchHeight = 1.5f; //how tall our capsule will be when crouched
    private float StandingHeight = 2f; //this is how tall our capsule is
    private bool Crouch;

    [Header("Sliding")]
    public float SlideAmt; //how far we slide when pressing crouch
    public float SlideSpeedLimit; //how fast we have to be traveling before a crouch will trigger a slide
    public float SlideControl; //how much we adjust to our slide speed and regain player control

    [Header("WallGrabbing")]
    public float PullUpTime; //the time it takes to pull onto a ledge
    private float ActPullTm; //the actual time it takes to pull up a ledge
    private Vector3 OrigPos; //the original Position before grabbing a ledge
    private Vector3 LedgePos; //the ledge position to move to

    [Header("FOV")]
    public float MaxFov;
    private float MinFov;
    public float FOVSpeed; //how fast we must go before we reach max fov

    // Start is called before the first frame update
    void Start()
    {
        Coli = GetComponent<PlayerCollision>();
        Rigid = GetComponent<Rigidbody>();
        Anim = GetComponentInChildren<Animator>();
        MinFov = Head.fieldOfView;
        Cap = GetComponent<CapsuleCollider>();
        StandingHeight = Cap.height;

        AdjustmentAmt = 1;
    }

    // Update is called once per frame
    void Update()
    {
        float XMove = Input.GetAxis("Horizontal");
        float YMove = Input.GetAxis("Vertical");

        if (CurrentState == PlayerStates.Grounded)
        {
            //if we press jump
            if (Input.GetButtonDown("Jump"))
            {
                //jump upwards
                JumpUp();
            }
        }
        else if (CurrentState == PlayerStates.InAir)
        {
            //check for ledge grabs
            if (Input.GetButton("Grab"))
            {
                Vector3 LedgePos = Coli.CheckLedges();
                if (LedgePos != Vector3.zero)
                {
                    LedgeGrab(LedgePos);
                }
            }

            //Check if there is a wall to run on
            bool Wall = CheckWalls(XMove, YMove);

            //we are on the wall
            if (Wall)
            {
                if (InAirTimer > TimeBeforeWallRun)
                {
                    SetOnWall();
                    return;
                }
            }

            //check for the ground 
            bool Grounded = Coli.CheckFloor(-transform.up);

            //we are on the ground (and have been in the air for a short time, to prevent multiple jump glitched
            if (Grounded && InAirTimer > 0.25f)
            {
                SetOnGround();
            }
        }
        else if (CurrentState == PlayerStates.OnWalls)
        {
            //check for ledge grabs
            if (Input.GetButton("Grab"))
            {
                Vector3 LedgePos = Coli.CheckLedges();

                if (LedgePos != Vector3.zero)
                {
                    LedgeGrab(LedgePos);
                }
            }

            //Check if there is a wall to run on
            bool Wall = CheckWalls(XMove, YMove);

            //we are no longer on the wall, fall off it
            if (!Wall)
            {
                SetInAir();
                return;
            }

            //check for the ground 
            bool Grounded = Coli.CheckFloor(-transform.up);

            //we are on the ground
            if (Grounded)
            {
                SetOnGround();
            }
        }
        else if (CurrentState == PlayerStates.LedgeGrab)
        {
            //clamp our rigid velocity to nothing
            Rigid.velocity = Vector3.zero;
        }

        //AnimCtrl();
    }

    void AnimCtrl()
    {
        int State = 0;
        if (CurrentState == PlayerStates.InAir)
            State = 1;
        else if (CurrentState == PlayerStates.OnWalls)
            State = 2;
        else if (CurrentState == PlayerStates.LedgeGrab)
            State = 3;

        Anim.SetInteger("State", State);
        Anim.SetBool("Crouching", Crouch);

        Vector3 Vel = transform.InverseTransformDirection(Rigid.velocity);
        Anim.SetFloat("XVelocity", Vel.x);
        Anim.SetFloat("ZVelocity", Vel.z);
        Anim.SetFloat("YVelocity", Rigid.velocity.y);
        Anim.SetFloat("XInput", Input.GetAxis("Horizontal"));
    }

    private void FixedUpdate()
    {
        float Del = Time.deltaTime;

        //get our players rotation amount for turning
        float CamX = Input.GetAxis("MouseX");
        float CamY = Input.GetAxis("MouseY");

        //have our player look up and down
        LookUpDown(CamY, Del);

        //handle our fov
        HandleFov(Del);

        //get inputs
        float horInput = Input.GetAxis("Horizontal");
        float verInput = Input.GetAxis("Vertical");

        if (CurrentState == PlayerStates.Grounded)
        {
            wallJump = 0;
            //tick our ground timer
            if (OnGroundTimer < 10)
                OnGroundTimer += Del;


            //get magnituded of our inputs
            float InputMagnitude = new Vector2(horInput, verInput).normalized.magnitude;
            //get the amount of speed, based on if we press forwards or backwards
            float TargetSpd = Mathf.Lerp(BackwardsSpeed, MaxSpeed, verInput); //using the vertical input as a lerp from if forward is being pressed
            //if we are crouching our target speed is our crouch speed
            if (Crouch)
                TargetSpd = CrouchSpeed;

            LerpSpeed(InputMagnitude, Del, TargetSpd);

            MovePlayer(horInput, verInput, Del);
            TurnPlayer(CamX, Del, TurnSpeed);

            //check for crouching 
            if (Input.GetButton("Crouching"))
            {
                //start crouching
                if (!Crouch)
                {
                    StartCrouch();
                }
            }
            else
            {
                //stand up
                bool check = Coli.CheckRoof(transform.up);
                if (!check)
                {
                    StopCrouching();
                }
            }

            //add to our player adjustment
            if (AdjustmentAmt < 1)
                AdjustmentAmt += Del * SlideControl;
            else
                AdjustmentAmt = 1;

            //check for the ground 
            bool Grounded = Coli.CheckFloor(-transform.up);

            //we are in the air
            if (!Grounded)
            {
                if (InAirTimer < 0.2f)
                    InAirTimer += Del;
                else
                {
                    SetInAir();
                    return;
                }
            }
            else
            {
                //we are on the ground to remove any increase in the air timer
                InAirTimer = 0;
            }
        }
        else if (CurrentState == PlayerStates.InAir)
        {
            if (wallJumpReset > 0)
            {
                if (wallJump > 0)
                {
                    if (Input.GetButtonDown("Jump"))
                    {
                        //reduce our velocity on the y axis so our jump force can be added
                        Vector3 VelAmt = Rigid.velocity;
                        VelAmt.y = 0;
                        Rigid.velocity = VelAmt;
                        //add our jump force
                        Rigid.AddForce(transform.up * JumpHeight, ForceMode.Impulse);
                        Rigid.AddForce(transform.forward * 1.25f * JumpHeight, ForceMode.Impulse);
                        //we are now in the air
                        SetInAir();
                        wallJump = 0;
                    }
                    wallJumpReset -= Time.deltaTime;
                }
            }
            //tick our Air timer
            if (InAirTimer < 10)
                InAirTimer += Del;

            MoveInAir(horInput, verInput, Del);

            //turn our player with the in air modifier
            TurnPlayer(CamX, Del, TurnSpeedInAir);
        }
        else if (CurrentState == PlayerStates.OnWalls)
        {
            wallJump = 1;
            wallJumpReset = 1;
            //tick our wall run timer
            ActWallRunTime += Del;
            Debug.Log(ActWallRunTime);
            //turn our player with the in air modifier
            TurnPlayer(CamX, Del, TurnSpeedOnWalls);

            //move our player when on a wall
            WallMove(verInput, Del);
            
        }
        else if (CurrentState == PlayerStates.LedgeGrab)
        {
            //tick ledge grab time 
            ActPullTm += Del;

            //pull up the ledge
            float PullUpLerp = ActPullTm / PullUpTime;

            if (PullUpLerp < 0.5)
            {
                //lerp our player upwards to the leges y position
                float LAmt = PullUpLerp * 2;
                transform.position = Vector3.Lerp(OrigPos, new Vector3(OrigPos.x, LedgePos.y, OrigPos.z), LAmt);
            }
            else if (PullUpLerp <= 1)
            {
                //set new pull up position
                if (OrigPos.y != LedgePos.y)
                    OrigPos = new Vector3(transform.position.x, LedgePos.y, transform.position.z);


                //move to the ledge position
                float LAmt = (PullUpLerp - 0.5f) * 2;
                transform.position = Vector3.Lerp(OrigPos, LedgePos, PullUpLerp);
            }
            else
            {
                //we have finished pulling up!
                SetOnGround();
            }
        }
    }


    //lerp our current speed to our set max speed, by how much we are pressing the horizontal and vertical input
    void LerpSpeed(float InputMag, float D, float TargetSpeed)
    {
        //multiply our speed by our input amount
        float LerpAmt = TargetSpeed * InputMag;
        //get our acceleration (if we should speed up or slow down
        float Accel = Acceleration;
        if (InputMag == 0)
            Accel = Decceleration;
        //lerp by a factor of our acceleration
        ActSpeed = Mathf.Lerp(ActSpeed, LerpAmt, D * Accel);
    }

    //when in the air or on a wall, we set our action speed to the velocity magnitude, this is so that when we reach the ground again, our speed will carry over our momentum
    void SetSpeedToVelocity()
    {
        float Mag = new Vector2(Rigid.velocity.x, Rigid.velocity.z).magnitude;
        ActSpeed = Mag;
    }

    bool CheckWalls(float X, float Y)
    {
        if (X == 0 && Y == 0) //if no direction input we are not wall running
            return false;

        if (ActWallRunTime >= WallRunTime) //if our wall run timer is more than the amount we can run on walls for, we cannot wall run
            return false;

        //check the collision direction for any walls
        float ClampedY = Mathf.Clamp(Y, 0, 1);
        Vector3 Dir = transform.forward * ClampedY + transform.right * X;

        bool WallCol = Coli.CheckWall(Dir);

        return WallCol;
    }

    void SetInAir()
    {
        StopCrouching(); //cannot crouch in air

        OnGroundTimer = 0; //remove the on ground timer
        CurrentState = PlayerStates.InAir;
    }

    void SetOnGround()
    {
        //set our current speed to our velocity
        SetSpeedToVelocity();

        ActWallRunTime = 0; //we are on the ground again, our wall run timer is reset
        InAirTimer = 0; //remove the in air timer
        CurrentState = PlayerStates.Grounded;
    }

    void SetOnWall()
    {
        OnGroundTimer = 0; //remove the on ground timer
        InAirTimer = 0; //remove the in air timer
        CurrentState = PlayerStates.OnWalls;
    }

    void LedgeGrab(Vector3 Ledge)
    {
        //set our ledge position
        LedgePos = Ledge;
        OrigPos = transform.position;
        //reset ledge grab time
        ActPullTm = 0;
        //remove speed and velocity
        Rigid.velocity = Vector3.zero;
        ActSpeed = 0;
        //start ledge grabs
        CurrentState = PlayerStates.LedgeGrab;
    }

    void StartCrouch()
    {
        Crouch = true;
        Cap.height = CrouchHeight;

        if (ActSpeed > SlideSpeedLimit)
            SlideSelf();
    }

    void StopCrouching()
    {
        Crouch = false;
        Cap.height = StandingHeight;
    }

    void TurnPlayer(float Hor, float D, float turn)
    {
        //add our inputs to our turn value
        YTurn += (Hor * D) * turn;
        //turn our character
        transform.rotation = Quaternion.Euler(0, YTurn, 0);
    }

    void LookUpDown(float Ver, float D)
    {
        //add our inputs to our look angle
        XTurn -= (Ver * D) * LookUpSpeed;
        XTurn = Mathf.Clamp(XTurn, MinLookAngle, MaxLookAngle);
        //look up and down
        Head.transform.localRotation = Quaternion.Euler(XTurn, 0, 0);
    }

    void MovePlayer(float Hor, float Ver, float D)
    {
        //find the direction to move in, based on the direction inputs
        Vector3 MovementDirection = (transform.forward * Ver) + (transform.right * Hor);
        MovementDirection = MovementDirection.normalized;
        //if we are no longer pressing and input, carryon moving in the last direction we were set to move in
        if (Hor == 0 && Ver == 0)
            MovementDirection = Rigid.velocity.normalized;

        MovementDirection = MovementDirection * ActSpeed;

        //apply Gravity and Y velocity to the movement direction 
        MovementDirection.y = Rigid.velocity.y;

        //apply adjustment to acceleration
        float Acel = DirectionControl * AdjustmentAmt;
        Vector3 LerpVelocity = Vector3.Lerp(Rigid.velocity, MovementDirection, Acel * D);
        Rigid.velocity = LerpVelocity;
    }

    void MoveInAir(float Hor, float Ver, float D)
    {
        //find the direction to move in, based on the direction inputs
        Vector3 MovementDirection = (transform.forward * Ver) + (transform.right * Hor);
        MovementDirection = MovementDirection.normalized;
        //if we are no longer pressing and input, carryon moving in the last direction we were set to move in
        if (Hor == 0 && Ver == 0)
            MovementDirection = Rigid.velocity.normalized;

        MovementDirection = MovementDirection * ActSpeed;

        //apply Gravity and Y velocity to the movement direction 
        MovementDirection.y = Rigid.velocity.y;

        //lerp to our movement direction based on how much airal control we have
        Vector3 LerpVelocity = Vector3.Lerp(Rigid.velocity, MovementDirection, InAirControl * D);
        Rigid.velocity = LerpVelocity;

    }

    void WallMove(float Ver, float D)
    {
        //get the direction to run up this wall if we press forward (keep in mind this only works if the wall is infront or to the side of the player as we run along on, on walls to our immediate right or left we slide down
        Vector3 MovementDirection = transform.up * Ver;
        MovementDirection = MovementDirection * WallRunUpwardsMovement;

        //our x z velocity are our momentum applied to our forward direction
        MovementDirection += transform.forward * ActSpeed;

        Vector3 LerpVelocity = Vector3.Lerp(Rigid.velocity, MovementDirection, WallRunSpeedAcceleration * D);
        Rigid.velocity = LerpVelocity;
    }

    void JumpUp()
    {
        //only jump if we are still on the ground
        if (CurrentState == PlayerStates.Grounded)
        {
            //reduce our velocity on the y axis so our jump force can be added
            Vector3 VelAmt = Rigid.velocity;
            VelAmt.y = 0;
            Rigid.velocity = VelAmt;
            //add our jump force
            Rigid.AddForce(transform.up * JumpHeight, ForceMode.Impulse);
            //we are now in the air
            SetInAir();
        }
    }

    //increase our fov at high speed and reduce it at low speed
    void HandleFov(float D)
    {
        //get our velocity magniture
        float mag = new Vector2(Rigid.velocity.x, Rigid.velocity.z).magnitude;
        //get appropritate fov 
        float LerpAmt = mag / FOVSpeed;
        float FieldView = Mathf.Lerp(MinFov, MaxFov, LerpAmt);
        //ease into this fov
        Head.fieldOfView = Mathf.Lerp(Head.fieldOfView, FieldView, 4 * D);
    }

    //slide our character forwards
    void SlideSelf()
    {
        //reduce our speed
        ActSpeed = SlideSpeedLimit;

        //remove any control from player 
        AdjustmentAmt = 0;

        //find direction
        Vector3 Dir = Rigid.velocity.normalized;
        Dir.y = 0;

        //slide in direction
        Rigid.AddForce(transform.forward * SlideAmt, ForceMode.Impulse);
    }
}
