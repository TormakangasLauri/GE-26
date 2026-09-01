using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;


public class PlayerMove : MonoBehaviour
{
    public InputActionReference move;
    public InputActionReference rightStick;

    private enum movementState
    {
        ground,
        air,
        wallrun,
        slide
    }

    private movementState currentState = movementState.ground;
    private movementState previousState = movementState.ground;

    private Rigidbody rb;
    private GameObject body;
    private GameObject head;
    private GameObject cameraObject;

    [Header("Movement")]
    public float acceleration = 70;
    public float maxSpeed = 10;
    public float deceleration = 20;
    public float airAcc = 20;
    public float airDeceleration = 100;
    public float airTurnSpeed = 50;
    private Vector3 airMovementDirection;
    private Vector2 moveInputDirection;
    
    [Header("Slope movement")]
    public float maxSlopeAngle = 45;
    public bool onSlope;
    private RaycastHit groundHit;

    [Header("Crouch")]
    public float crouchAcc = 70;
    public float maxCrouchSpeed = 10;
    public bool crouching;
    public bool pressingCrouch;

    [Header("Jump")]
    public float jumpForce = 12;
    public float landingGracePeriod = 0.2f;
    public bool pressingJump;
    private bool waitingToJump;

    [Header("Wallrun")]
    public bool wallRunning;
    public int wallRunDirection;

    [Header("Slide")]
    public float slideAcc = 10;
    public float maxSlideSpeed = 30;
    public float slideTurnSpeed = 50;
    public float slideForce = 20;
    public bool sliding;

    [Header("Dash")]
    public bool canDash = true;
    public bool dashing;
    public float dashDistance = 5;
    private float dashCoolDown = 0.1f;

    [Header("Camera")]
    public float mouseSensitivity = 2f;
    public float FOVChangeMult = 1;
    public float FOVChangeSpeed = 1f;
    public bool useHeadBobbing = true;
    public float amplitude = 0.015f;
    public float yAmplitude = 0.7f;
    public float xAmplitude = 1f;
    public float frequency = 0.5f;
    private float headVerticalRotation;
    private float headHorizontalRotation;

    [Header("Checks")]
    public GameObject groundCheck;
    public LayerMask terrainLayer;
    public bool grounded;
    public float timeSinceGrounded;
    public LayerMask wallLayer;
    public bool walled;
    [HideInInspector] public List<Collider> wallColList = new List<Collider>();
    private Vector3 dirToWall;

    // Other

    public float gravity = 20;
    public bool enterState = true;
    public bool underTerrain;
    public bool slamming;

    private Vector3 originalBodyScale;
    private float originalHeadHeight;

    private Vector3 velocityBeforePause = Vector3.positiveInfinity;
    private bool velocityStored = false;

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        rb = GetComponent<Rigidbody>();
        body = transform.GetChild(0).gameObject;
        head = transform.GetChild(1).gameObject;
        cameraObject = head.transform.GetChild(0).gameObject;

        originalBodyScale = body.transform.localScale;
        originalHeadHeight = head.transform.localPosition.y;

        head.transform.localRotation = Quaternion.Euler(0, 0, 0);
    }

    private void Update()
    
    {
        /*
        if (TimeController.paused) // On pause
        {
            if (!velocityStored)
            {
                velocityBeforePause = rb.linearVelocity; // Store velocity if not already
                velocityStored = true;
                rb.isKinematic = true;
            }
            return;
        }
        else if (!TimeController.paused && velocityStored) // On unpause
        {
            rb.linearVelocity = velocityBeforePause; // Return the stored velocity
            velocityStored = false;
            rb.isKinematic = false;
        }
        */

        moveInputDirection = move.action.ReadValue<Vector2>();

        GroundCheck();
        Walled();
        SlopeCheck();
        if (crouching || sliding) RoofCheck();

        CameraRotation();
        //CameraMovement();

        // BigAssBall is real?!?!?!

        // Dash
        dashCoolDown -= Time.deltaTime;
        if ((grounded || wallRunning) && !dashing && dashCoolDown < 0) canDash = true;
    }

    private void FixedUpdate()
    {
        // Define movement state
        if (grounded) {
            // Grounded
            if (pressingCrouch && rb.linearVelocity.magnitude > maxCrouchSpeed * 1.5) currentState = movementState.slide; // Slide
            else if (pressingCrouch){
                crouching = true;
                currentState = movementState.ground;
            }
            else currentState = movementState.ground; // Ground
        }
        else {
            // Not grounded

            currentState = movementState.air; // Air

            // MOVEMENT WITH WALLRUN
            //if (walled) {
            //    if (pressingJump && !pressingCrouch && !slamming && timeSinceGrounded > 0.2) currentState = movementState.wallrun; // Wallrun
            //    else currentState = movementState.air; // Air
            //}else currentState = movementState.air;
        }

        // Reset enterState when switching movement states
        if (currentState != previousState)
        {
            enterState = true;
        }
        previousState = currentState;

        switch (currentState) {
            case movementState.ground: GroundMovement(); break;
            case movementState.air: AirMovement(); break;
            case movementState.wallrun: Wallrun(); break;
            case movementState.slide: SlideMovement(); break;
        }

        if (currentState == movementState.air) rb.AddForce(Vector3.down * gravity);
    }

    /* Movement
     __  __                                            _
    |  \/  |                                          | |
    | \  / |  ___ __   __ ___  _ __ ___    ___  _ __  | |_
    | |\/| | / _ \\ \ / // _ \| '_ ` _ \  / _ \| '_ \ | __|
    | |  | || (_) |\ V /|  __/| | | | | ||  __/| | | || |_
    |_|  |_| \___/  \_/  \___||_| |_| |_| \___||_| |_| \__|
    */

    void GroundMovement()
    {
        if (enterState)
        {
            enterState = false;
        }
        
        Physics.Raycast(transform.position, Vector3.down, out groundHit, 1.3f, terrainLayer);

        // Movement direction on flat ground
        Vector3 flatMovementDirection = transform.rotation * new Vector3(moveInputDirection.x, 0, moveInputDirection.y);
        // Movement direction projected onto the ground
        Vector3 movementDirection = Vector3.ProjectOnPlane(flatMovementDirection, groundHit.normal);

        // Velocity on x and z-axes projected onto the ground
        Vector3 velocityAlongGround = Vector3.ProjectOnPlane(new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z), groundHit.normal);
        float velocityMovementDirAngle = Vector3.Angle(velocityAlongGround, movementDirection);
        
        // Adjust movement direction to avoid getting stuck to walls
        if (walled && Vector3.Angle(dirToWall, movementDirection) < 90) movementDirection = Vector3.ProjectOnPlane(movementDirection, dirToWall);

        
        if (!crouching) // Normal movement
        {
            rb.AddForce(movementDirection * acceleration);
            // Speed limit - apply force to the opposite direction of velocity
            if (velocityAlongGround.magnitude > maxSpeed) rb.AddForce(-velocityAlongGround.normalized * acceleration
            * (1 + velocityAlongGround.magnitude - maxSpeed) * 0.15f);
            // Slow down when no movement inputs or if trying to move against the direction of velocity
            else if (movementDirection == Vector3.zero || velocityMovementDirAngle > 90) rb.AddForce(-velocityAlongGround * deceleration);
            else rb.AddForce(Vector3.ProjectOnPlane(-velocityAlongGround, movementDirection) * deceleration);
        }
        else // Crouching
        {
            rb.AddForce(movementDirection * crouchAcc);
            // Speed limit
            if (velocityAlongGround.magnitude > maxCrouchSpeed) rb.AddForce(-velocityAlongGround.normalized * crouchAcc);
            // Slow down when no movement inputs or if trying to move against the direction of velocity
            else if (movementDirection == Vector3.zero || velocityMovementDirAngle > 90) rb.AddForce(-velocityAlongGround * deceleration);
            else rb.AddForce(Vector3.ProjectOnPlane(-velocityAlongGround, movementDirection) * deceleration);
        }
    }

    void AirMovement()
    {
        if (enterState)
        {
            enterState = false;
            airMovementDirection = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).normalized;
        }

        Vector3 velocityAlongXZ = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 inputDirection = transform.rotation * new Vector3(moveInputDirection.x, 0, moveInputDirection.y);
        float velocityMovementDirAngle = Vector3.Angle(velocityAlongXZ, airMovementDirection);
        float inputMovementAngle = Vector3.Angle(inputDirection, airMovementDirection);


        // Adjust movement direction to avoid getting stuck to walls
        if (walled && Vector3.Angle(dirToWall, airMovementDirection) < 90) airMovementDirection = Vector3.ProjectOnPlane(airMovementDirection, dirToWall);

        // Movement
        if (inputMovementAngle < 130 || velocityAlongXZ.magnitude < 2)
        {
            if (velocityAlongXZ.magnitude >= 2) airMovementDirection = Vector3.Lerp(velocityAlongXZ.normalized, inputDirection, airTurnSpeed * Time.fixedDeltaTime);
            else airMovementDirection = inputDirection;

            if (velocityAlongXZ.magnitude < maxSpeed) rb.AddForce(airMovementDirection * inputDirection.magnitude * airAcc);
            else rb.AddForce(Vector3.ProjectOnPlane(airMovementDirection, velocityAlongXZ) * inputDirection.magnitude * airAcc);
        }else rb.AddForce(-velocityAlongXZ.normalized * airDeceleration * Mathf.Clamp01((velocityAlongXZ.magnitude - maxSpeed)/maxSpeed + 1) * 0.05f);
    }

    void Wallrun()
    {
        Vector3 rightFromWall = (Quaternion.Euler(0,90,0) * dirToWall).normalized;
        
        if (enterState)
        {
            enterState = false;
            // Wallrundirection 1 = right and -1 = left
            wallRunDirection = Vector3.Angle(rb.linearVelocity, rightFromWall) < 90 ? 1 : -1;
        }
        
        if (rb.linearVelocity.magnitude < maxSpeed) rb.AddForce(rightFromWall * acceleration * wallRunDirection);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
    }

    void SlideMovement() // Base for this is literally copied from AirMovement and this uses some variables meant for air movement
    {
        if (enterState)
        {
            enterState = false;
            airMovementDirection = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).normalized;
            rb.AddForce(Vector3.ProjectOnPlane(airMovementDirection, groundHit.normal).normalized * slideForce, ForceMode.Impulse);
        }

        Vector3 velocityAlongXZ = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 inputDirection = transform.rotation * new Vector3(moveInputDirection.x, 0, moveInputDirection.y);
        float velocityMovementDirAngle = Vector3.Angle(velocityAlongXZ, airMovementDirection);
        float inputMovementAngle = Vector3.Angle(inputDirection, airMovementDirection);


        // Adjust movement direction to avoid getting stuck to walls
        if (walled && Vector3.Angle(dirToWall, airMovementDirection) < 90) airMovementDirection = Vector3.ProjectOnPlane(airMovementDirection, dirToWall);

        // Movement
        if (inputMovementAngle < 130 || velocityAlongXZ.magnitude < 1)
        {
            if (velocityAlongXZ.magnitude >= 1) airMovementDirection = Vector3.Lerp(velocityAlongXZ.normalized, inputDirection, slideTurnSpeed * Time.fixedDeltaTime);
            else airMovementDirection = inputDirection;

            if (groundHit.normal != Vector3.up && rb.linearVelocity.magnitude < maxSlideSpeed) rb.AddForce(-Vector3.ProjectOnPlane(Vector3.up, groundHit.normal).normalized * slideAcc);
            rb.AddForce(Vector3.ProjectOnPlane(airMovementDirection, velocityAlongXZ) * inputDirection.magnitude * airAcc);
        }
        else rb.AddForce(-velocityAlongXZ.normalized * airDeceleration * Mathf.Clamp01((velocityAlongXZ.magnitude - maxSpeed) / maxSpeed + 1) * 0.05f);
    }

    void EnterCrouch()
    {
        crouching = true;
        body.transform.localScale = new Vector3(originalBodyScale.x, originalBodyScale.y/2, originalBodyScale.z);
        head.transform.localPosition = new Vector3(0, originalHeadHeight/2, 0);
        if (!grounded) transform.position = transform.position + Vector3.up * 0.75f;
    }

    void ExitCrouch()
    {
        crouching = false;
        body.transform.localScale = originalBodyScale;
        head.transform.localPosition = new Vector3(0, originalHeadHeight, 0);
        if (!grounded) transform.position = transform.position + Vector3.down * 0.75f;
    }
    
    void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
    
    void WallJump(Vector3 velocityBeforeWall)
    {
        Vector3 velocity = rb.linearVelocity;
        
        // Walljumping makes the player jump away from the wall regardless of directional movement inputs
        //rb.velocity = new Vector3(velocity.x, 0, velocity.z) * 1.1f - dirToWall * 8 + Vector3.up * jumpForce * 0.6f;

        Vector3 projectedVel = Vector3.ProjectOnPlane(velocityBeforeWall, dirToWall);
        rb.linearVelocity = new Vector3(projectedVel.x * 1.1f, projectedVel.y < 0 ? projectedVel.y / 2 : projectedVel.y, projectedVel.z * 1.1f) - dirToWall * 15 + Vector3.up * jumpForce;
    }
    
    /* Inputs
     _____                       _        
    |_   _|                     | |       
      | |   _ __   _ __   _   _ | |_   ___ 
      | |  | '_ \ | '_ \ | | | || __|/ __|
     _| |_ | | | || |_) || |_| || |_ \__ \
    |_____||_| |_|| .__/  \__,_| \__||___/
                  | |
                  |_|
    */
    
    // Call path: player (object in scene hierarchy) -> Player Input (component) -> Events -> player
    public void JumpInput(InputAction.CallbackContext action)
    {
        if (action.performed)
        {
            if (!waitingToJump) StartCoroutine(WaitToJump());
            pressingJump = true;
        }
        else if (action.canceled)
        {
            pressingJump = false;
            if (currentState == movementState.wallrun) WallJump(Vector3.zero);
            wallRunning = false;
        }
        
        IEnumerator WaitToJump() // Wait until the player is grounded to jump
        {
            waitingToJump = true;
            float i = landingGracePeriod;
            Vector3 velocityBeforeWall = rb.linearVelocity;
            yield return new WaitUntil(() =>
            {
                if (grounded) { Jump(); return true; }
                else if (walled) { WallJump(velocityBeforeWall); return true; }
                velocityBeforeWall = rb.linearVelocity;
                i -= Time.deltaTime;
                return i <= 0;
            });
            waitingToJump = false;
        }
    }
    
    public void CrouchInput(InputAction.CallbackContext action)
    {
        if (action.performed)
        {
            pressingCrouch = true;
            EnterCrouch();
        }
        else if (action.canceled)
        {
            pressingCrouch = false;
            if (!underTerrain) ExitCrouch();
            else StartCoroutine(Exit());
            
            IEnumerator Exit()
            {
                yield return new WaitUntil(() => { return !underTerrain; });
                ExitCrouch();
            }
        }
    }
    
    public void DashInput(InputAction.CallbackContext action)
    {
        // Dash if not touching terrain or performing dash or slam
        if (action.performed && canDash && !dashing && !grounded && !walled && !slamming)
        {
            StartCoroutine(Dash());
        }
    }
    
    IEnumerator Dash()
    {
        rb.useGravity = false;
        canDash = false;
        dashing = true;
        dashCoolDown = 0.1f;

        Vector3 startPos = transform.position;
        Vector3 dashDirection = head.transform.rotation * Vector3.forward;
        float startVelocity = rb.linearVelocity.magnitude;
        
        rb.AddForce(dashDirection * (dashDistance * 10), ForceMode.Impulse);

        yield return new WaitForSeconds(0.2f);
        yield return new WaitUntil(delegate
        {
            // Slow down when reaching max dash distance or touching terrain
            if (Vector3.Distance(transform.position, startPos) > dashDistance || grounded || walled) rb.linearVelocity *= 0.9f;
            // Return when slowing down and velocity is the same as before dashing
            return (Vector3.Distance(transform.position, startPos) > dashDistance || grounded || walled || underTerrain) && rb.linearVelocity.magnitude < startVelocity;
        });
        
        rb.useGravity = true;
        dashing = false;
    }
    
    void CameraRotation()
    {
        // Rotate the Camera around its local X axis
        float inputY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        headVerticalRotation -= inputY;
        headVerticalRotation = Mathf.Clamp(headVerticalRotation, -90f, 90f);
        head.transform.localRotation = Quaternion.Euler(headVerticalRotation, 0f, 0f);
        
        // Rotate the Player Object around its Y axis
        float inputX = Input.GetAxis("Mouse X") * mouseSensitivity;
        headHorizontalRotation += inputX;
        transform.rotation = Quaternion.Euler(0f, headHorizontalRotation, 0f);

        // Gamepad input
        Vector2 input = rightStick.action.ReadValue<Vector2>();
        headVerticalRotation -= input.y;
        headVerticalRotation = Mathf.Clamp(headVerticalRotation, -90f, 90f);
        head.transform.localRotation = Quaternion.Euler(headVerticalRotation, 0, 0);

        headHorizontalRotation += input.x;
        transform.rotation = Quaternion.Euler(0f, headHorizontalRotation, 0f);
    }

    void CameraMovement()
    {
        Camera cam = cameraObject.GetComponent<Camera>();

        float speed = rb.linearVelocity.magnitude;
        Vector3 projectedVel = Vector3.Project(rb.linearVelocity, head.transform.forward);

        float angle = Vector3.Angle(rb.linearVelocity, head.transform.forward);
        float angleMult = angle < 90 ? (90 - angle) / 90 : (90 - angle) / 180; // Forwards 1, sideways 0, backwards -0.5

        // FOV change
        //if (speed > maxSpeed) cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, 90 + Mathf.Pow((speed - maxSpeed), 1.5f) * angleMult * 0.2f, ref speed, FOVChangeSpeed * 6 * Time.deltaTime);
        //else cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, 90, ref speed, FOVChangeSpeed * Time.deltaTime);

        // Head bobbing
        if (useHeadBobbing && currentState == movementState.ground && speed > 1) // Head bobbing
        {
            float speedMult = speed / maxSpeed;
            Vector3 pos = Vector3.zero;
            pos.y = Mathf.Abs(Mathf.Sin(Time.time * frequency * Mathf.PI * speedMult) * yAmplitude * 2) - yAmplitude / 2;
            pos.x = (2 * Mathf.Abs(2 * (Time.time / 2 * frequency * speedMult - Mathf.Floor(Time.time / 2 * frequency * speedMult + 0.5f))) - 1) * xAmplitude;

            cameraObject.transform.localPosition = pos;
            //cameraObject.transform.localPosition = Vector3.Lerp(cameraObject.transform.localPosition, pos, Time.deltaTime);

            //pos.y += Mathf.Sin(Time.time * frequency * speedMult) * amplitude;
            //pos.x += Mathf.Cos(Time.time * frequency * speedMult / 2) * amplitude * 2;

            //cameraObject.transform.localPosition -= pos;
        }
        else if (cameraObject.transform.localPosition != Vector3.zero) // Reset camera position
            cameraObject.transform.localPosition = Vector3.Lerp(cameraObject.transform.localPosition, Vector3.zero, 5 * Time.deltaTime);
    }

    // ENVIROMENT CHECKS

    void GroundCheck()
    {
        if (Physics.OverlapBox(groundCheck.transform.position, groundCheck.transform.localScale, Quaternion.identity, terrainLayer).Length > 0)
        {
            grounded = true;
            wallRunning = false;
            dashing = false;
            timeSinceGrounded = 0;
        }
        else // Not grounded
        {
            grounded = false;
            timeSinceGrounded += Time.deltaTime;
        }
    }

    void SlopeCheck()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out groundHit, 1.3f, terrainLayer) && Vector3.Angle(Vector3.up, groundHit.normal) < maxSlopeAngle && Vector3.Angle(Vector3.up, groundHit.normal) > 0)
            onSlope = true;
        else onSlope = false;
    }

    void RoofCheck()
    {
        bool checkTrue = false;
        Vector3 rayPos = new Vector3(0.5f,0,0);
        for (int i = 0; i < 8; i++)
        {
            Quaternion rotation = Quaternion.Euler(0,45 * i,0);
            if (Physics.Raycast(transform.position + rotation * rayPos, Vector3.up, 2, terrainLayer))
                checkTrue = true;
        }

        underTerrain = checkTrue;
    }

    void Walled()
    {
        if (walled)
        { 
            dirToWall = wallColList.Count > 0 ? (wallColList[0].ClosestPoint(transform.position) - transform.position).normalized : Vector3.zero;
            if (dirToWall == Vector3.zero) // Failed to get the closest point on the wall
            {
                RaycastHit hit = new RaycastHit();
                RaycastHit hit0 = new RaycastHit();
                for (int i = 0; i < 8; i++)
                {
                    if (i == 0) Physics.Raycast(transform.position, transform.forward, out hit, 2, LayerMask.GetMask("Terrain"));
                    else Physics.Raycast(transform.position, Quaternion.Euler(0, 45 * i, 0) * transform.forward, out hit0, 2, LayerMask.GetMask("Terrain"));
                    hit = Vector3.Distance(transform.position, hit0.point) < Vector3.Distance(transform.position, hit.point) ? hit0 : hit; // Use the closest hit
                }
                dirToWall = -hit.normal;
            }
            dashing = false;
        }
    }
}
