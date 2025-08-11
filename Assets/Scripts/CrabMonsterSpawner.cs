using UnityEngine;

public class CrabMonsterSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject crabMonsterPrefab;
    public int spawnCount = 3;
    public float spawnRadius = 20f;
    public float minDistanceFromPlayer = 5f;
    public LayerMask groundLayer = 1;
    
    [Header("Multi-Altitude Settings")]
    public bool useMultipleAltitudes = false;
    public AltitudeSpawnSettings[] altitudeSettings;
    
    [Header("Single Altitude Settings (Legacy)")]
    public bool useFixedAltitude = false;
    public float fixedAltitude = 0f;
    public float altitudeOffset = 0.2f; // Small offset above the determined altitude
    
    [Header("Spawn Conditions")]
    public bool spawnOnStart = true;
    public bool checkNavMesh = true;
    
    [System.Serializable]
    public class AltitudeSpawnSettings
    {
        [Header("Altitude Configuration")]
        public string name = "Level"; // Name for this altitude level
        public float altitude = 0f;
        public int spawnCount = 1;
        public float altitudeOffset = 0.2f;
        
        [Header("Per-Altitude Overrides (Optional)")]
        public bool overrideSpawnRadius = false;
        [Tooltip("Custom spawn radius for this altitude level")]
        public float customSpawnRadius = 20f;
        
        [Header("Visual")]
        public Color gizmoColor = Color.yellow;
        
        [Header("Debug")]
        public bool enableDebugLogs = false;
    }
    
    private Transform player;
    
    [ContextMenu("Set Fixed Altitude to Spawner Y")]
    void SetFixedAltitudeToSpawnerY()
    {
        if (useMultipleAltitudes)
        {
            Debug.LogWarning("Multiple altitudes mode is enabled. Use 'Add Current Altitude Level' instead.");
            return;
        }
        
        fixedAltitude = transform.position.y;
        useFixedAltitude = true;
        Debug.Log($"Set fixed altitude to spawner Y position: {fixedAltitude}m");
    }
    
    [ContextMenu("Set Fixed Altitude to Player Y")]
    void SetFixedAltitudeToPlayerY()
    {
        if (useMultipleAltitudes)
        {
            Debug.LogWarning("Multiple altitudes mode is enabled. Use 'Add Player Altitude Level' instead.");
            return;
        }
        
        if (player == null)
        {
            FirstPersonController playerController = FindFirstObjectByType<FirstPersonController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }
        
        if (player != null)
        {
            fixedAltitude = player.position.y;
            useFixedAltitude = true;
            Debug.Log($"Set fixed altitude to player Y position: {fixedAltitude}m");
        }
        else
        {
            Debug.LogWarning("No player found to get Y position from!");
        }
    }
    
    [ContextMenu("Add Current Altitude Level")]
    void AddCurrentAltitudeLevel()
    {
        useMultipleAltitudes = true;
        
        // Initialize array if null
        if (altitudeSettings == null)
        {
            altitudeSettings = new AltitudeSpawnSettings[1];
        }
        else
        {
            // Expand array
            System.Array.Resize(ref altitudeSettings, altitudeSettings.Length + 1);
        }
        
        // Add new altitude level at spawner's current position
        int index = altitudeSettings.Length - 1;
        altitudeSettings[index] = new AltitudeSpawnSettings();
        altitudeSettings[index].name = $"Level {index + 1}";
        altitudeSettings[index].altitude = transform.position.y;
        altitudeSettings[index].spawnCount = 2; // Default spawn count per level
        altitudeSettings[index].gizmoColor = GetRandomColor();
        
        Debug.Log($"Added altitude level '{altitudeSettings[index].name}' at {transform.position.y}m");
    }
    
    [ContextMenu("Add Player Altitude Level")]
    void AddPlayerAltitudeLevel()
    {
        if (player == null)
        {
            FirstPersonController playerController = FindFirstObjectByType<FirstPersonController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }
        
        if (player == null)
        {
            Debug.LogWarning("No player found to get Y position from!");
            return;
        }
        
        useMultipleAltitudes = true;
        
        // Initialize array if null
        if (altitudeSettings == null)
        {
            altitudeSettings = new AltitudeSpawnSettings[1];
        }
        else
        {
            // Expand array
            System.Array.Resize(ref altitudeSettings, altitudeSettings.Length + 1);
        }
        
        // Add new altitude level at player's current position
        int index = altitudeSettings.Length - 1;
        altitudeSettings[index] = new AltitudeSpawnSettings();
        altitudeSettings[index].name = $"Player Level {index + 1}";
        altitudeSettings[index].altitude = player.position.y;
        altitudeSettings[index].spawnCount = 3; // Default spawn count per level
        altitudeSettings[index].gizmoColor = GetRandomColor();
        
        Debug.Log($"Added altitude level '{altitudeSettings[index].name}' at player Y position: {player.position.y}m");
    }
    
    [ContextMenu("Clear All Altitude Levels")]
    void ClearAllAltitudeLevels()
    {
        altitudeSettings = new AltitudeSpawnSettings[0];
        useMultipleAltitudes = false;
        Debug.Log("Cleared all altitude levels and disabled multiple altitudes mode");
    }
    
    Color GetRandomColor()
    {
        Color[] colors = {
            Color.yellow, Color.cyan, Color.magenta, Color.green, 
            Color.red, Color.blue, new Color(1f, 0.5f, 0f), // orange
            new Color(0.5f, 0f, 1f), // purple
            new Color(0f, 1f, 0.5f), // spring green
            new Color(1f, 0f, 0.5f)  // pink
        };
        return colors[Random.Range(0, colors.Length)];
    }
    
    [ContextMenu("Preview Spawn Positions")]
    void PreviewSpawnPositions()
    {
        Debug.Log($"=== SPAWN PREVIEW ===");
        
        if (useMultipleAltitudes && altitudeSettings != null && altitudeSettings.Length > 0)
        {
            Debug.Log($"Spawn Mode: Multiple Altitudes ({altitudeSettings.Length} levels)");
            
            for (int levelIndex = 0; levelIndex < altitudeSettings.Length; levelIndex++)
            {
                var settings = altitudeSettings[levelIndex];
                Debug.Log($"--- {settings.name} (Alt: {settings.altitude}m) ---");
                
                for (int i = 0; i < Mathf.Min(settings.spawnCount, 3); i++)
                {
                    Vector3 previewPos = GetRandomSpawnPositionForAltitude(settings);
                    bool valid = IsValidSpawnPosition(previewPos);
                    Debug.Log($"  Preview spawn {i + 1}: {previewPos} - Valid: {valid}");
                }
            }
        }
        else if (useFixedAltitude)
        {
            Debug.Log($"Spawn Mode: Single Fixed altitude ({fixedAltitude}m)");
            Debug.Log($"Spawn count: {spawnCount}");
            
            for (int i = 0; i < Mathf.Min(spawnCount, 5); i++)
            {
                Vector3 previewPos = GetRandomSpawnPosition();
                bool valid = IsValidSpawnPosition(previewPos);
                Debug.Log($"Preview spawn {i + 1}: {previewPos} - Valid: {valid}");
            }
        }
        else
        {
            Debug.Log($"Spawn Mode: Ground level");
            Debug.Log($"Spawn count: {spawnCount}");
            
            for (int i = 0; i < Mathf.Min(spawnCount, 5); i++)
            {
                Vector3 previewPos = GetRandomSpawnPosition();
                bool valid = IsValidSpawnPosition(previewPos);
                Debug.Log($"Preview spawn {i + 1}: {previewPos} - Valid: {valid}");
            }
        }
        
        Debug.Log($"==================");
    }
    
    void Start()
    {
        // Find player
        FirstPersonController playerController = FindFirstObjectByType<FirstPersonController>();
        if (playerController != null)
        {
            player = playerController.transform;
        }
        
        if (spawnOnStart)
        {
            SpawnCrabMonsters();
        }
    }
    
    public void SpawnCrabMonsters()
    {
        if (crabMonsterPrefab == null)
        {
            Debug.LogError("CrabMonsterSpawner: No crab monster prefab assigned!");
            return;
        }
        
        int totalSpawned = 0;
        int totalAttempts = 0;
        
        if (useMultipleAltitudes && altitudeSettings != null && altitudeSettings.Length > 0)
        {
            // Multi-altitude spawning
            Debug.Log($"Spawning crabs across {altitudeSettings.Length} altitude levels...");
            
            for (int levelIndex = 0; levelIndex < altitudeSettings.Length; levelIndex++)
            {
                var settings = altitudeSettings[levelIndex];
                int spawned = 0;
                int attempts = 0;
                int maxAttempts = settings.spawnCount * 10;
                
                if (settings.enableDebugLogs)
                {
                    Debug.Log($"Spawning {settings.spawnCount} crabs at {settings.name} (altitude: {settings.altitude}m)");
                }
                
                while (spawned < settings.spawnCount && attempts < maxAttempts)
                {
                    attempts++;
                    totalAttempts++;
                    
                    Vector3 spawnPosition = GetRandomSpawnPositionForAltitude(settings);
                    
                    if (IsValidSpawnPosition(spawnPosition))
                    {
                        SpawnCrabMonster(spawnPosition, settings);
                        spawned++;
                        totalSpawned++;
                    }
                }
                
                if (settings.enableDebugLogs || spawned < settings.spawnCount)
                {
                    Debug.Log($"{settings.name}: Spawned {spawned}/{settings.spawnCount} crabs in {attempts} attempts");
                }
            }
            
            Debug.Log($"Multi-altitude spawning complete: {totalSpawned} total crabs spawned across {altitudeSettings.Length} levels in {totalAttempts} total attempts");
        }
        else
        {
            // Single altitude or ground level spawning (legacy mode)
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = spawnCount * 10;
            
            while (spawned < spawnCount && attempts < maxAttempts)
            {
                attempts++;
                
                Vector3 spawnPosition = GetRandomSpawnPosition();
                
                if (IsValidSpawnPosition(spawnPosition))
                {
                    SpawnCrabMonster(spawnPosition, null);
                    spawned++;
                }
            }
            
            Debug.Log($"Single-mode spawning: Spawned {spawned}/{spawnCount} crab monsters in {attempts} attempts");
        }
    }
    
    Vector3 GetRandomSpawnPosition(AltitudeSpawnSettings settings = null)
    {
        float currentSpawnRadius = spawnRadius;
        float currentAltitude = fixedAltitude;
        float currentAltitudeOffset = altitudeOffset;
        
        // If multi-altitude is used, apply the settings of the first altitude level for now
        if (useMultipleAltitudes && settings != null)
        {
            currentSpawnRadius = settings.overrideSpawnRadius ? settings.customSpawnRadius : spawnRadius;
            currentAltitude = settings.altitude;
            currentAltitudeOffset = settings.altitudeOffset;
        }
        
        Vector2 randomCircle = Random.insideUnitCircle * currentSpawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        if (useFixedAltitude)
        {
            // Use the specified fixed altitude
            spawnPosition.y = fixedAltitude + altitudeOffset;
            Debug.Log($"Using fixed altitude: {spawnPosition.y}m for spawn position {spawnPosition}");
        }
        else
        {
            // Try to find ground level (original behavior)
            RaycastHit hit;
            if (Physics.Raycast(spawnPosition + Vector3.up * 10f, Vector3.down, out hit, 20f, groundLayer))
            {
                spawnPosition = hit.point + Vector3.up * altitudeOffset;
            }
            else
            {
                // Fallback to spawner's Y position if no ground found
                spawnPosition.y = transform.position.y + altitudeOffset;
                Debug.LogWarning($"No ground found for spawn position, using spawner Y: {spawnPosition.y}m");
            }
        }
        
        // Apply the final altitude offset
        spawnPosition.y = currentAltitude + currentAltitudeOffset;
        
        return spawnPosition;
    }
    
    Vector3 GetRandomSpawnPositionForAltitude(AltitudeSpawnSettings settings)
    {
        float currentSpawnRadius = settings.overrideSpawnRadius ? settings.customSpawnRadius : spawnRadius;
        Vector2 randomCircle = Random.insideUnitCircle * currentSpawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // Set altitude
        spawnPosition.y = settings.altitude + settings.altitudeOffset;
        
        if (settings.enableDebugLogs)
        {
            Debug.Log($"Generated spawn position for {settings.name}: {spawnPosition} (radius: {currentSpawnRadius}m)");
        }
        
        return spawnPosition;
    }
    
    bool IsValidSpawnPosition(Vector3 position)
    {
        // Check distance from player
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(position, player.position);
            if (distanceToPlayer < minDistanceFromPlayer)
            {
                return false;
            }
        }
        
        // Check if position is on NavMesh (if required)
        if (checkNavMesh)
        {
            UnityEngine.AI.NavMeshHit navHit;
            // Use a larger search radius when using fixed altitude since we might be further from NavMesh
            float searchRadius = useFixedAltitude ? 5f : 2f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(position, out navHit, searchRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (useFixedAltitude)
                {
                    Debug.LogWarning($"No NavMesh found near fixed altitude position {position} within {searchRadius}m radius");
                }
                return false;
            }
            else if (useFixedAltitude)
            {
                Debug.Log($"Found NavMesh at {navHit.position} for fixed altitude spawn at {position}");
            }
        }
        
        // Check if there's enough space (no overlapping colliders)
        Collider[] overlapping = Physics.OverlapSphere(position, 1f);
        foreach (Collider col in overlapping)
        {
            if (col.gameObject != gameObject && col.GetComponent<CrabMonsterAI>() != null)
            {
                return false; // Too close to another crab monster
            }
        }
        
        return true;
    }
    
    void SpawnCrabMonster(Vector3 position, AltitudeSpawnSettings altitudeSettings)
    {
        GameObject crabMonster = Instantiate(crabMonsterPrefab, position, Quaternion.identity);
        
        // Set random rotation
        crabMonster.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        
        // Ensure the crab monster has the AI component
        CrabMonsterAI ai = crabMonster.GetComponent<CrabMonsterAI>();
        if (ai == null)
        {
            ai = crabMonster.AddComponent<CrabMonsterAI>();
        }
        
        // Set player reference
        if (player != null)
        {
            ai.player = player;
        }
        
        // Debug logging based on spawn mode
        if (altitudeSettings != null)
        {
            if (altitudeSettings.enableDebugLogs)
            {
                Debug.Log($"Spawned crab monster at {position} for {altitudeSettings.name} (altitude: {altitudeSettings.altitude}m)");
            }
        }
        else if (useFixedAltitude)
        {
            Debug.Log($"Spawned crab monster at {position} (Fixed altitude: {fixedAltitude}m)");
        }
        else
        {
            Debug.Log($"Spawned crab monster at {position} (Ground level)");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (useMultipleAltitudes && altitudeSettings != null && altitudeSettings.Length > 0)
        {
            // Draw multi-altitude visualization
            for (int i = 0; i < altitudeSettings.Length; i++)
            {
                var settings = altitudeSettings[i];
                float currentSpawnRadius = settings.overrideSpawnRadius ? settings.customSpawnRadius : spawnRadius;
                
                // Set gizmo color
                Gizmos.color = settings.gizmoColor;
                
                Vector3 planeCenter = new Vector3(transform.position.x, settings.altitude + settings.altitudeOffset, transform.position.z);
                
                // Draw altitude plane center marker
                Gizmos.DrawSphere(planeCenter, 0.8f);
                
                // Draw wire disc to show spawn area at this altitude
                Gizmos.matrix = Matrix4x4.TRS(planeCenter, Quaternion.identity, Vector3.one);
                
                // Draw spawn radius circle
                for (int j = 0; j < 32; j++)
                {
                    float angle = j * 360f / 32f * Mathf.Deg2Rad;
                    float nextAngle = (j + 1) * 360f / 32f * Mathf.Deg2Rad;
                    
                    Vector3 point1 = new Vector3(Mathf.Cos(angle) * currentSpawnRadius, 0, Mathf.Sin(angle) * currentSpawnRadius);
                    Vector3 point2 = new Vector3(Mathf.Cos(nextAngle) * currentSpawnRadius, 0, Mathf.Sin(nextAngle) * currentSpawnRadius);
                    
                    Gizmos.DrawLine(point1, point2);
                }
                
                // Reset matrix
                Gizmos.matrix = Matrix4x4.identity;
                
                // Draw vertical line from spawner to this altitude
                Gizmos.color = new Color(settings.gizmoColor.r, settings.gizmoColor.g, settings.gizmoColor.b, 0.7f);
                Gizmos.DrawLine(transform.position, planeCenter);
                
                // Draw text label (if in editor)
                #if UNITY_EDITOR
                UnityEditor.Handles.color = settings.gizmoColor;
                UnityEditor.Handles.Label(planeCenter + Vector3.up, $"{settings.name}\n{settings.spawnCount} crabs\n{settings.altitude}m");
                #endif
            }
        }
        else
        {
            // Draw single altitude visualization (legacy)
            // Draw spawn radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            // Draw fixed altitude plane if enabled
            if (useFixedAltitude)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
                Vector3 planeCenter = new Vector3(transform.position.x, fixedAltitude + altitudeOffset, transform.position.z);
                
                // Draw altitude plane as a disc
                Gizmos.matrix = Matrix4x4.TRS(planeCenter, Quaternion.identity, Vector3.one);
                Gizmos.DrawSphere(Vector3.zero, 0.5f); // Center marker
                
                // Draw wire disc to show spawn area at fixed altitude
                Gizmos.color = Color.yellow;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * 360f / 32f * Mathf.Deg2Rad;
                    float nextAngle = (i + 1) * 360f / 32f * Mathf.Deg2Rad;
                    
                    Vector3 point1 = new Vector3(Mathf.Cos(angle) * spawnRadius, 0, Mathf.Sin(angle) * spawnRadius);
                    Vector3 point2 = new Vector3(Mathf.Cos(nextAngle) * spawnRadius, 0, Mathf.Sin(nextAngle) * spawnRadius);
                    
                    Gizmos.DrawLine(point1, point2);
                }
                
                // Reset matrix
                Gizmos.matrix = Matrix4x4.identity;
                
                // Draw vertical line from spawner to fixed altitude
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, planeCenter);
            }
        }
        
        // Draw minimum distance from player (applies to all modes)
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }
    }
}