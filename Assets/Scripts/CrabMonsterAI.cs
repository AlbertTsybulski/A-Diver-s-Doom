using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class CrabMonsterAI : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Transform player;
    private NavMeshAgent navMeshAgent;
    private PlayerHealth playerHealth;
    
    [Header("Detection Settings")]
    public float detectionRange = 15f; // Increased from 12f for earlier detection
    public float chaseRange = 18f; 
    public float attackRange = 2.5f;
    public LayerMask playerLayer = 1;
    
    [Header("Movement Settings")]
    public float wanderSpeed = 3f;
    public float chaseSpeed = 8f;
    public float wanderRadius = 8f;
    public float wanderInterval = 1f; // Reduced from 3f to 1f for faster re-detection
    
    [Header("Intimidation Settings")]
    public string[] intimidateAnimations = { "Intimidate_1", "Intimidate_2", "Intimidate_3" };
    public float intimidationDuration = 4f; // Increased from 2f to 4f
    public AudioClip[] intimidateSounds;
    
    [Header("Water Escape Settings")]
    public LayerMask waterLayer = 1 << 4; // Default water layer
    public float waterCheckRadius = 1f; // Radius to check for water
    public float exitWaterSpeed = 6f; // Speed when trying to get out of water
    public float shoreSearchRadius = 15f; // How far to look for shore/dry land
    
    [Header("Climbing Settings")]
    public LayerMask climbableLayer = -1; // What layers can be climbed (default: everything)
    public float climbSpeed = 4f; // Speed when climbing
    public float climbDetectionDistance = 1.5f; // How far to detect climbable surfaces
    public float maxClimbAngle = 60f; // Maximum angle (degrees) that can be climbed
    public float rotationSpeed = 8f; // How fast crab rotates to match terrain
    public bool enableClimbing = true; // Toggle climbing on/off
    
    [Header("Attack Settings")]
    public float attackDamage = 25f;
    public float attackCooldown = 0.8f; // Reduced from 2f for more aggressive attacking
    public string[] attackAnimations = { "Attack_1", "Attack_2", "Attack_3", "Attack_4", "Attack_5" };
    
    [Header("Animation Parameters")]
    public string walkingSlowParam = "Walk_Cycle_2";
    public string walkingFastParam = "Walk_Cycle_1";
    public string idleParam = "Fight_Idle_1";
    
    // State management
    public enum CrabState
    {
        Wandering,
        Intimidating,
        Chasing,
        Attacking,
        ExitingWater, // New state for getting out of water
        Climbing, // New state for climbing surfaces
        Idle
    }
    
    public CrabState currentState = CrabState.Wandering;
    private Vector3 wanderTarget;
    private float wanderTimer;
    private float attackTimer;
    private bool isAttacking = false;
    private bool isIntimidating = false;
    private bool hasValidNavMesh = false;
    
    // Water detection variables
    private bool isInWater = false;
    private Vector3 nearestShorePoint;
    private bool hasFoundShore = false;
    
    // Climbing variables
    private bool isClimbing = false;
    private Vector3 climbDirection;
    private Vector3 targetSurfaceNormal;
    private RaycastHit currentClimbSurface;
    private bool hasClimbTarget = false;
    
    // Audio
    [Header("Audio")]
    public AudioClip[] attackSounds;
    public AudioClip[] chaseSounds;
    private AudioSource audioSource;
    
    [Header("Debug")]
    public bool enableDebugLogs = true; // Enable by default to see what's happening

    void Start()
    {
        // Get components
        navMeshAgent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        
        // Find player if not assigned
        if (player == null)
        {
            FirstPersonController playerController = FindFirstObjectByType<FirstPersonController>();
            if (playerController != null)
            {
                player = playerController.transform;
                playerHealth = playerController.GetComponent<PlayerHealth>();
            }
        }
        else
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
        
        // Initialize NavMesh Agent with better settings
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = wanderSpeed;
            navMeshAgent.acceleration = 12f; // Faster acceleration
            navMeshAgent.angularSpeed = 180f; // Faster turning
            navMeshAgent.stoppingDistance = attackRange * 0.7f;
            navMeshAgent.autoBraking = true;
            navMeshAgent.updateRotation = true;
            navMeshAgent.updatePosition = true;
            
            // Check if we're on NavMesh
            hasValidNavMesh = navMeshAgent.isOnNavMesh;
            if (!hasValidNavMesh)
            {
                Debug.LogWarning($"Crab {gameObject.name} is not on NavMesh! Position: {transform.position}");
            }
        }
        
        // Initialize audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1f; // 3D audio
        
        // Set initial wander target
        SetNewWanderTarget();
        wanderTimer = wanderInterval;
        
        // Check if spawned in water - this becomes primary objective
        CheckWaterStatus();
        if (isInWater)
        {
            SetState(CrabState.ExitingWater);
            Debug.Log($"Crab {gameObject.name}: Spawned in water - primary objective: get to dry land!");
        }
        
        Debug.Log($"Crab {gameObject.name}: Initialization complete - Player found: {player != null}");
    }
    
    void Update()
    {
        if (player == null || playerHealth == null || !playerHealth.IsAlive())
        {
            SetState(CrabState.Idle);
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Always check water status - this is the primary objective
        CheckWaterStatus();
        
        // Check for climbable surfaces and update rotation
        if (enableClimbing)
        {
            CheckClimbability();
            UpdateTerrainRotation();
        }
        
        if (enableDebugLogs && Time.time % 2f < 0.1f) // Log every 2 seconds
        {
            Debug.Log($"Crab {gameObject.name}: State={currentState}, Distance={distanceToPlayer:F1}m, InWater={isInWater}, NavAgent.isOnNavMesh={navMeshAgent.isOnNavMesh}");
        }
        
        // Update timers
        wanderTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        
        // State machine
        switch (currentState)
        {
            case CrabState.Wandering:
                HandleWandering(distanceToPlayer);
                break;
                
            case CrabState.Intimidating:
                HandleIntimidating(distanceToPlayer);
                break;
                
            case CrabState.Chasing:
                HandleChasing(distanceToPlayer);
                break;
                
            case CrabState.Attacking:
                HandleAttacking(distanceToPlayer);
                break;
                
            case CrabState.Idle:
                HandleIdle(distanceToPlayer);
                break;
                
            case CrabState.ExitingWater:
                HandleExitingWater(distanceToPlayer);
                break;
                
            case CrabState.Climbing:
                HandleClimbing(distanceToPlayer);
                break;
        }
        
        // Update animation based on current state
        UpdateAnimations();
    }
    
    void HandleWandering(float distanceToPlayer)
    {
        // Check if player is within detection range
        if (distanceToPlayer <= detectionRange && CanSeePlayer())
        {
            SetState(CrabState.Intimidating); // Changed from Chasing to Intimidating
            return;
        }
        
        // Continue wandering
        if (wanderTimer <= 0f || (navMeshAgent.hasPath && navMeshAgent.remainingDistance < 1f))
        {
            SetNewWanderTarget();
            wanderTimer = wanderInterval;
        }
    }
    
    void HandleIntimidating(float distanceToPlayer)
    {
        // Ensure we're completely stopped during intimidation
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.velocity = Vector3.zero;
            }
        }
        
        // Face the player continuously during intimidation
        if (player != null && !isIntimidating)
        {
            Vector3 lookDirection = (player.position - transform.position).normalized;
            lookDirection.y = 0f;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        
        // Check if player moved too far during intimidation
        if (distanceToPlayer > chaseRange && !isIntimidating)
        {
            SetState(CrabState.Wandering);
            return;
        }
        
        // Start intimidation if not already intimidating
        if (!isIntimidating)
        {
            StartCoroutine(PerformIntimidation());
        }
    }
    
    void HandleChasing(float distanceToPlayer)
    {
        // Check if player is too far away
        if (distanceToPlayer > chaseRange)
        {
            SetState(CrabState.Wandering);
            return;
        }
        
        // Check if close enough to attack
        if (distanceToPlayer <= attackRange && attackTimer <= 0f && !isAttacking)
        {
            SetState(CrabState.Attacking);
            return;
        }
        
        // Ensure NavMesh agent is properly configured for chasing
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            if (navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = chaseSpeed;
            }
            
            // Continuously update destination while chasing
            navMeshAgent.SetDestination(player.position);
        }
    }
    
    void HandleAttacking(float distanceToPlayer)
    {
        // Ensure we're completely stopped during attack
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.velocity = Vector3.zero;
            }
        }
        
        if (!isAttacking)
        {
            StartCoroutine(PerformAttack());
        }
    }
    
    void HandleIdle(float distanceToPlayer)
    {
        // Stop moving
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.ResetPath();
        }
        
        // Check if player comes back to life or gets close
        if (playerHealth != null && playerHealth.IsAlive() && distanceToPlayer <= detectionRange && CanSeePlayer())
        {
            SetState(CrabState.Intimidating); // Changed from Chasing to Intimidating
        }
    }
    
    void SetState(CrabState newState)
    {
        if (currentState == newState) return;
        
        CrabState previousState = currentState;
        currentState = newState;
        
        // Update NavMesh Agent settings based on state
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            switch (newState)
            {
                case CrabState.Wandering:
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = wanderSpeed;
                    break;
                    
                case CrabState.Intimidating:
                    navMeshAgent.isStopped = true;
                    navMeshAgent.velocity = Vector3.zero; // Force stop immediately
                    break;
                    
                case CrabState.Chasing:
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = chaseSpeed;
                    break;
                    
                case CrabState.Attacking:
                    navMeshAgent.isStopped = true;
                    navMeshAgent.velocity = Vector3.zero; // Force stop immediately
                    break;
                    
                case CrabState.Idle:
                    navMeshAgent.isStopped = true;
                    navMeshAgent.velocity = Vector3.zero; // Force stop immediately
                    break;
                    
                case CrabState.ExitingWater:
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = exitWaterSpeed;
                    navMeshAgent.autoBraking = false; // Keep moving aggressively to shore
                    break;
                    
                case CrabState.Climbing:
                    navMeshAgent.isStopped = true; // Disable NavMesh when climbing manually
                    break;
            }
        }
        
        Debug.Log($"Crab {gameObject.name}: {previousState} → {newState}");
    }
    
    void SetNewWanderTarget()
    {
        if (navMeshAgent == null || !navMeshAgent.isActiveAndEnabled) return;
        
        for (int i = 0; i < 10; i++) // Try up to 10 times to find a valid position
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y; // Keep same height
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
            {
                wanderTarget = hit.position;
                navMeshAgent.SetDestination(wanderTarget);
                return;
            }
        }
        
        // Fallback: stay in place
        Debug.LogWarning($"Crab {gameObject.name}: Could not find valid wander target");
    }
    
    bool CanSeePlayer()
    {
        if (player == null) return false;
        
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        
        // Check if player is in front (wider field of view)
        if (angle < 120f) // Increased from 90f for better detection
        {
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 1.5f; // Higher ray start
            if (Physics.Raycast(rayStart, directionToPlayer, out hit, detectionRange))
            {
                return hit.transform == player || hit.transform.IsChildOf(player);
            }
        }
        
        return false;
    }

    // Keep existing methods for water, climbing, etc...
    void CheckWaterStatus()
    {
        // Check if crab is currently in water using sphere check
        Collider[] waterColliders = Physics.OverlapSphere(transform.position, waterCheckRadius, waterLayer);
        bool wasInWater = isInWater;
        isInWater = waterColliders.Length > 0;
        
        // Log water status changes
        if (wasInWater != isInWater && enableDebugLogs)
        {
            Debug.Log($"Crab {gameObject.name}: Water status changed - InWater: {isInWater}");
        }
        
        // If we just got out of water, reset shore finding
        if (wasInWater && !isInWater)
        {
            hasFoundShore = false;
        }
    }
    
    void CheckClimbability()
    {
        if (!enableClimbing) return;
        
        // Cast multiple rays around the crab to detect climbable surfaces
        Vector3[] directions = {
            transform.forward,
            transform.right,
            -transform.right,
            transform.forward + transform.right,
            transform.forward - transform.right
        };
        
        bool foundClimbableSurface = false;
        RaycastHit bestHit = new RaycastHit();
        float closestDistance = float.MaxValue;
        
        foreach (Vector3 direction in directions)
        {
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            
            if (Physics.Raycast(rayStart, direction, out hit, climbDetectionDistance, climbableLayer))
            {
                // Check if the surface angle is climbable
                float surfaceAngle = Vector3.Angle(Vector3.up, hit.normal);
                
                if (surfaceAngle > 15f && surfaceAngle <= maxClimbAngle) // Not too flat, not too steep
                {
                    float distance = hit.distance;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        bestHit = hit;
                        foundClimbableSurface = true;
                    }
                }
            }
        }
        
        if (foundClimbableSurface)
        {
            currentClimbSurface = bestHit;
            hasClimbTarget = true;
            climbDirection = bestHit.normal;
            targetSurfaceNormal = bestHit.normal;
        }
        else
        {
            hasClimbTarget = false;
        }
    }
    
    bool ShouldStartClimbing()
    {
        if (!enableClimbing || !hasClimbTarget) return false;
        
        // Check if NavMesh path is blocked or if there's a better climbing route
        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            // If we have a clear NavMesh path, don't climb unless the path is very long
            float pathDistance = GetPathDistance();
            float directDistance = Vector3.Distance(transform.position, navMeshAgent.destination);
            
            // Only climb if the path is significantly longer than direct distance
            if (pathDistance < directDistance * 1.5f)
            {
                return false;
            }
        }
        
        // Check if climbing would help reach the target
        Vector3 targetPosition = Vector3.zero;
        if (currentState == CrabState.Chasing && player != null)
        {
            targetPosition = player.position;
        }
        else if (currentState == CrabState.Wandering)
        {
            targetPosition = wanderTarget;
        }
        
        if (targetPosition != Vector3.zero)
        {
            // Check if climbing direction gets us closer to target
            Vector3 toTarget = (targetPosition - transform.position).normalized;
            float climbAlignment = Vector3.Dot(climbDirection, toTarget);
            
            return climbAlignment > 0.3f; // Climbing direction is somewhat aligned with target
        }
        
        return false;
    }
    
    float GetPathDistance()
    {
        if (navMeshAgent == null || !navMeshAgent.hasPath) return 0f;
        
        float distance = 0f;
        Vector3[] corners = navMeshAgent.path.corners;
        
        for (int i = 1; i < corners.Length; i++)
        {
            distance += Vector3.Distance(corners[i - 1], corners[i]);
        }
        
        return distance;
    }
    
    void HandleClimbing(float distanceToPlayer)
    {
        // Water escape always takes priority
        if (isInWater)
        {
            SetState(CrabState.ExitingWater);
            return;
        }
        
        // Check if we should stop climbing
        if (!hasClimbTarget || !ShouldContinueClimbing())
        {
            // Stop climbing and return to appropriate state
            isClimbing = false;
            if (distanceToPlayer <= detectionRange)
            {
                SetState(CrabState.Chasing);
                PlayChaseSound();
            }
            else
            {
                SetState(CrabState.Wandering);
            }
            return;
        }
        
        // Perform climbing movement
        PerformClimbMovement();
        
        // Update climbing status
        if (!isClimbing)
        {
            isClimbing = true;
            if (enableDebugLogs) Debug.Log($"Crab {gameObject.name}: Started climbing surface at angle {Vector3.Angle(Vector3.up, targetSurfaceNormal):F1}°");
        }
    }
    
    bool ShouldContinueClimbing()
    {
        // Continue climbing if we still have a target and it's beneficial
        if (!hasClimbTarget) return false;
        
        // Stop climbing if we've reached a flat surface
        float surfaceAngle = Vector3.Angle(Vector3.up, targetSurfaceNormal);
        if (surfaceAngle < 15f) return false;
        
        // Stop climbing if we're no longer against a climbable surface
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(rayStart, transform.forward, out hit, climbDetectionDistance * 0.5f, climbableLayer))
        {
            return false;
        }
        
        return true;
    }
    
    void PerformClimbMovement()
    {
        if (!hasClimbTarget) return;
        
        // Calculate climbing movement direction
        Vector3 upDirection = -targetSurfaceNormal; // Direction away from surface (up relative to surface)
        Vector3 forwardDirection = Vector3.Cross(upDirection, transform.right).normalized;
        
        // Determine target direction based on current state
        Vector3 targetDirection = Vector3.zero;
        
        if (currentState == CrabState.Chasing && player != null)
        {
            Vector3 toPlayer = (player.position - transform.position).normalized;
            targetDirection = Vector3.ProjectOnPlane(toPlayer, targetSurfaceNormal).normalized;
        }
        else
        {
            // Default climbing direction - mostly up the surface
            targetDirection = upDirection;
        }
        
        // Move in the calculated direction
        Vector3 moveDirection = targetDirection * climbSpeed * Time.deltaTime;
        transform.position += moveDirection;
        
        // Keep the crab on the surface
        RaycastHit surfaceHit;
        Vector3 rayStart = transform.position + targetSurfaceNormal * 0.5f;
        if (Physics.Raycast(rayStart, -targetSurfaceNormal, out surfaceHit, 1f, climbableLayer))
        {
            transform.position = surfaceHit.point + targetSurfaceNormal * 0.1f; // Stay slightly off the surface
            targetSurfaceNormal = surfaceHit.normal; // Update surface normal
        }
    }
    
    void UpdateTerrainRotation()
    {
        if (!enableClimbing) return;
        
        // Cast a ray downward to detect the ground surface
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 2f))
        {
            // Calculate the desired rotation to match the terrain
            Vector3 surfaceNormal = hit.normal;
            Vector3 forward = transform.forward;
            
            // Project forward direction onto the surface plane
            Vector3 projectedForward = Vector3.ProjectOnPlane(forward, surfaceNormal).normalized;
            
            // Create rotation that aligns with the surface
            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, surfaceNormal);
            
            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // If no ground detected, gradually return to upright position
            Vector3 forward = transform.forward;
            forward.y = 0; // Remove any vertical component
            forward.Normalize();
            
            Quaternion uprightRotation = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, uprightRotation, rotationSpeed * 0.5f * Time.deltaTime);
        }
    }
    
    void HandleExitingWater(float distanceToPlayer)
    {
        // If we're not in water anymore, we've succeeded!
        if (!isInWater)
        {
            if (enableDebugLogs) Debug.Log($"Crab {gameObject.name}: Successfully exited water! Returning to normal behavior.");
            hasFoundShore = false;
            
            // Decide what to do next based on player proximity
            if (distanceToPlayer <= detectionRange)
            {
                SetState(CrabState.Chasing);
                PlayChaseSound();
            }
            else
            {
                SetState(CrabState.Wandering);
            }
            return;
        }
        
        // We're still in water - find the nearest shore
        if (!hasFoundShore || Vector3.Distance(transform.position, nearestShorePoint) < 2f)
        {
            FindNearestShore();
        }
        
        // Move toward shore
        if (hasFoundShore && navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.SetDestination(nearestShorePoint);
            
            if (enableDebugLogs && Time.time % 3f < 0.1f) // Log every 3 seconds
            {
                Debug.Log($"Crab {gameObject.name}: Moving to shore at {nearestShorePoint} (Distance: {Vector3.Distance(transform.position, nearestShorePoint):F1}m)");
            }
        }
    }
    
    void FindNearestShore()
    {
        Vector3 bestShorePoint = transform.position;
        float bestDistance = float.MaxValue;
        bool foundShore = false;
        
        // Cast rays in multiple directions to find dry land
        int rayCount = 16; // Check 16 directions around the crab
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            
            // Cast ray outward to find the edge of water
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            
            if (Physics.Raycast(rayStart, direction, out hit, shoreSearchRadius))
            {
                // Check if the hit point is not in water
                Vector3 testPoint = hit.point + Vector3.up * 0.1f; // Slightly above hit point
                Collider[] waterTest = Physics.OverlapSphere(testPoint, waterCheckRadius * 0.5f, waterLayer);
                
                if (waterTest.Length == 0) // No water detected at this point
                {
                    // This is potential dry land
                    float distance = Vector3.Distance(transform.position, hit.point);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestShorePoint = hit.point;
                        foundShore = true;
                    }
                }
            }
            else
            {
                // No obstacle hit - try a point at max distance
                Vector3 testPoint = rayStart + direction * shoreSearchRadius;
                Collider[] waterTest = Physics.OverlapSphere(testPoint, waterCheckRadius * 0.5f, waterLayer);
                
                if (waterTest.Length == 0)
                {
                    float distance = Vector3.Distance(transform.position, testPoint);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestShorePoint = testPoint;
                        foundShore = true;
                    }
                }
            }
        }
        
        if (foundShore)
        {
            nearestShorePoint = bestShorePoint;
            hasFoundShore = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"Crab {gameObject.name}: Found shore at {nearestShorePoint} (Distance: {bestDistance:F1}m)");
            }
        }
        else
        {
            // Fallback: just try to move in a random direction away from current position
            Vector3 randomDirection = Random.onUnitSphere;
            randomDirection.y = 0;
            nearestShorePoint = transform.position + randomDirection.normalized * shoreSearchRadius * 0.5f;
            hasFoundShore = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"Crab {gameObject.name}: No shore found, trying random direction");
            }
        }
    }
    
    IEnumerator PerformAttack()
    {
        isAttacking = true;
        attackTimer = attackCooldown;
        
        // Ensure we're completely stopped
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
        
        // Face the player
        if (player != null)
        {
            Vector3 lookDirection = (player.position - transform.position).normalized;
            lookDirection.y = 0f;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        
        // DON'T touch animation bools - let the UpdateAnimations handle idle state
        // Just trigger the attack animation directly
        yield return new WaitForSeconds(0.2f);
        
        // Play random attack animation
        if (animator != null && attackAnimations.Length > 0)
        {
            string attackAnim = attackAnimations[Random.Range(0, attackAnimations.Length)];
            animator.SetTrigger(attackAnim);
            Debug.Log($"Crab {gameObject.name}: Playing attack animation {attackAnim}");
        }
        
        // Play attack sound
        PlayAttackSound();
        
        // Wait much longer before applying damage to let animation play
        yield return new WaitForSeconds(1.5f);
        
        // Apply damage if player is still in range
        if (player != null && playerHealth != null)
        {
            float damageDistance = Vector3.Distance(transform.position, player.position);
            if (damageDistance <= attackRange)
            {
                playerHealth.TakeDamage(attackDamage);
                Debug.Log($"Crab {gameObject.name}: Dealt {attackDamage} damage to player");
            }
        }
        
        // Wait for attack animation to complete - significantly increased duration
        yield return new WaitForSeconds(3f);
        
        isAttacking = false;
        
        // Decide next state after attack
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= chaseRange && playerHealth != null && playerHealth.IsAlive())
            {
                SetState(CrabState.Chasing);
            }
            else
            {
                SetState(CrabState.Wandering);
            }
        }
        else
        {
            SetState(CrabState.Idle);
        }
    }
    
    IEnumerator PerformIntimidation()
    {
        isIntimidating = true;
        
        // Ensure we're completely stopped
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
        
        // Face the player
        if (player != null)
        {
            Vector3 lookDirection = (player.position - transform.position).normalized;
            lookDirection.y = 0f;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        
        // DON'T touch animation bools - let the UpdateAnimations handle idle state
        // Just trigger the intimidation animation directly
        yield return new WaitForSeconds(0.2f);
        
        // Play random intimidation animation
        if (animator != null && intimidateAnimations.Length > 0)
        {
            string intimidateAnim = intimidateAnimations[Random.Range(0, intimidateAnimations.Length)];
            animator.SetTrigger(intimidateAnim);
            Debug.Log($"Crab {gameObject.name}: Playing intimidation animation {intimidateAnim}");
        }
        
        // Play intimidation sound
        PlayIntimidationSound();
        
        // Wait for intimidation animation to complete - much longer duration
        yield return new WaitForSeconds(intimidationDuration);
        
        isIntimidating = false;
        
        // After intimidation, start chasing if player is still in range
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= chaseRange && playerHealth != null && playerHealth.IsAlive())
            {
                SetState(CrabState.Chasing);
                PlayChaseSound(); // Play chase sound after intimidation
            }
            else
            {
                SetState(CrabState.Wandering);
            }
        }
        else
        {
            SetState(CrabState.Idle);
        }
    }
    
    void UpdateAnimations()
    {
        if (animator == null) return;
        
        // COMPLETELY SKIP animation updates during attacks or intimidation
        // This prevents any interference with trigger-based animations
        if ((currentState == CrabState.Attacking && isAttacking) || 
            (currentState == CrabState.Intimidating && isIntimidating))
        {
            // Do not touch any animation parameters during these states
            return;
        }
        
        // Cache current animation states to avoid unnecessary changes
        bool currentWalkingSlow = animator.GetBool(walkingSlowParam);
        bool currentWalkingFast = animator.GetBool(walkingFastParam);
        bool currentIdle = animator.GetBool(idleParam);
        
        // Simple animation logic
        bool targetWalkingSlow = false;
        bool targetWalkingFast = false;
        bool targetIdle = false;
        
        // Determine target animation state
        switch (currentState)
        {
            case CrabState.Wandering:
                if (navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.magnitude > 0.2f)
                {
                    targetWalkingSlow = true;
                }
                else
                {
                    targetIdle = true;
                }
                break;
                
            case CrabState.Chasing:
                if (navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.magnitude > 0.2f)
                {
                    targetWalkingFast = true;
                }
                else
                {
                    targetIdle = true;
                }
                break;
                
            case CrabState.Intimidating:
            case CrabState.Attacking:
            case CrabState.Idle:
                targetIdle = true;
                break;
                
            case CrabState.ExitingWater:
                if (navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.magnitude > 0.2f)
                {
                    targetWalkingFast = true;
                }
                else
                {
                    targetIdle = true;
                }
                break;
                
            case CrabState.Climbing:
                if (isClimbing)
                {
                    targetWalkingSlow = true;
                }
                else
                {
                    targetIdle = true;
                }
                break;
        }
        
        // Only update parameters if they need to change
        if (currentWalkingSlow != targetWalkingSlow)
        {
            animator.SetBool(walkingSlowParam, targetWalkingSlow);
        }
        
        if (currentWalkingFast != targetWalkingFast)
        {
            animator.SetBool(walkingFastParam, targetWalkingFast);
        }
        
        if (currentIdle != targetIdle)
        {
            animator.SetBool(idleParam, targetIdle);
        }
    }
    
    void PlayAttackSound()
    {
        if (attackSounds != null && attackSounds.Length > 0 && audioSource != null)
        {
            AudioClip sound = attackSounds[Random.Range(0, attackSounds.Length)];
            audioSource.PlayOneShot(sound);
        }
    }
    
    void PlayChaseSound()
    {
        if (chaseSounds != null && chaseSounds.Length > 0 && audioSource != null)
        {
            AudioClip sound = chaseSounds[Random.Range(0, chaseSounds.Length)];
            audioSource.PlayOneShot(sound);
        }
    }
    
    void PlayIntimidationSound()
    {
        if (intimidateSounds != null && intimidateSounds.Length > 0 && audioSource != null)
        {
            AudioClip sound = intimidateSounds[Random.Range(0, intimidateSounds.Length)];
            audioSource.PlayOneShot(sound);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Draw chase range
        Gizmos.color = Color.orange;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw wander radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
        
        // Draw water detection radius
        Gizmos.color = isInWater ? Color.blue : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, waterCheckRadius);
        
        // Draw shore search radius when in water
        if (isInWater)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, shoreSearchRadius);
        }
        
        // Draw current target
        if (currentState == CrabState.Wandering)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(wanderTarget, 0.5f);
            Gizmos.DrawLine(transform.position, wanderTarget);
        }
        else if (currentState == CrabState.Chasing && player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, player.position);
        }
        else if (currentState == CrabState.ExitingWater && hasFoundShore)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(nearestShorePoint, 1f);
            Gizmos.DrawLine(transform.position, nearestShorePoint);
        }
        else if (currentState == CrabState.Climbing && hasClimbTarget)
        {
            Gizmos.color = Color.yellow;
            Vector3 climbPoint = currentClimbSurface.point;
            Gizmos.DrawSphere(climbPoint, 0.3f);
            Gizmos.DrawLine(transform.position, climbPoint);
            
            // Draw surface normal
            Gizmos.color = Color.white;
            Gizmos.DrawRay(climbPoint, targetSurfaceNormal * 2f);
        }
        
        // Draw line of sight
        if (player != null)
        {
            Gizmos.color = CanSeePlayer() ? Color.red : Color.gray;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, player.position);
        }
        
        // Draw climb detection range
        if (enableClimbing)
        {
            Gizmos.color = isClimbing ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, climbDetectionDistance);
        }
    }
}