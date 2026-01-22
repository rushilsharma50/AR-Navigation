using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem; // REQUIRED for New Input System

[RequireComponent(typeof(ARRaycastManager))]
public class ARNavigationController : MonoBehaviour
{
    [Header("Assets")]
    public GameObject markerPrefab;
    public GameObject characterPrefab;

    [Header("Settings")]
    public float movementSpeed = 0.5f;
    public float rotationSpeed = 5.0f;
    public float reachThreshold = 0.1f;

    // Data Structures
    private List<Vector3> _currentPathPoints = new List<Vector3>();
    private List<GameObject> _spawnedMarkers = new List<GameObject>();
    private List<Vector3> _savedPathA = new List<Vector3>();
    private List<Vector3> _savedPathB = new List<Vector3>();

    // State
    private bool _isNavigating = false;
    private int _targetPointIndex = 0;
    private GameObject _activeCharacter;
    private ARRaycastManager _raycastManager;
    private List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();

    private void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
        if (_isNavigating && _activeCharacter != null)
        {
            MoveCharacterAlongPath();
        }
        else
        {
            // NEW INPUT SYSTEM LOGIC
            HandleInput();
        }
    }

    private void HandleInput()
    {
        // 1. Check if user touched screen
        if (Touchscreen.current == null) return;
        
        // We only care about the first touch when it begins
        if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            
            // Optional: Block input if touching UI (Button)
            // You might need "UnityEngine.EventSystems" and "EventSystem.current.IsPointerOverGameObject()" 
            // but for a raw test, we skip it to ensure Raycast fires.
            
            PerformARRaycast(touchPosition);
        }
    }

    private void PerformARRaycast(Vector2 touchPosition)
    {
        if (_raycastManager.Raycast(touchPosition, _raycastHits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = _raycastHits[0].pose;
            AddPathPoint(hitPose.position);
        }
    }

    private void AddPathPoint(Vector3 position)
    {
        _currentPathPoints.Add(position);
        GameObject newMarker = Instantiate(markerPrefab, position, Quaternion.identity);
        
        var renderer = newMarker.GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.material.color = Random.ColorHSV();

        _spawnedMarkers.Add(newMarker);
    }

    private void MoveCharacterAlongPath()
    {
        if (_targetPointIndex >= _currentPathPoints.Count)
        {
            StopNavigation();
            return;
        }

        Vector3 targetPosition = _currentPathPoints[_targetPointIndex];
        Vector3 currentPos = _activeCharacter.transform.position;

        float step = movementSpeed * Time.deltaTime;
        _activeCharacter.transform.position = Vector3.MoveTowards(currentPos, targetPosition, step);

        Vector3 direction = (targetPosition - currentPos).normalized;
        if (direction != Vector3.zero)
        {
            // Flatten rotation so he doesn't tilt up/down
            direction.y = 0; 
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            _activeCharacter.transform.rotation = Quaternion.Slerp(_activeCharacter.transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }

        if (Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(targetPosition.x, 0, targetPosition.z)) < reachThreshold)
        {
            _targetPointIndex++;
        }
    }

    public void StartNavigation()
    {
        if (_currentPathPoints.Count == 0) return;
        if (_activeCharacter != null) Destroy(_activeCharacter);

        _activeCharacter = Instantiate(characterPrefab, _currentPathPoints[0], Quaternion.identity);
        _targetPointIndex = 1;
        _isNavigating = true;
    }

    private void StopNavigation()
    {
        _isNavigating = false;
        if (_activeCharacter != null) Destroy(_activeCharacter);
        ClearCurrentPath();
    }

    public void ClearCurrentPath()
    {
        _isNavigating = false;
        if (_activeCharacter != null) Destroy(_activeCharacter);
        foreach (var marker in _spawnedMarkers) Destroy(marker);
        _spawnedMarkers.Clear();
        _currentPathPoints.Clear();
    }

    public void SavePathA() { _savedPathA = new List<Vector3>(_currentPathPoints); ClearCurrentPath(); }
    public void LoadAndRunPathA() { LoadPath(_savedPathA); }
    public void SavePathB() { _savedPathB = new List<Vector3>(_currentPathPoints); ClearCurrentPath(); }
    public void LoadAndRunPathB() { LoadPath(_savedPathB); }

    private void LoadPath(List<Vector3> path)
    {
        if (path.Count == 0) return;
        ClearCurrentPath();
        _currentPathPoints = new List<Vector3>(path);
        foreach (Vector3 pos in _currentPathPoints)
        {
            GameObject newMarker = Instantiate(markerPrefab, pos, Quaternion.identity);
            _spawnedMarkers.Add(newMarker);
        }
        StartNavigation();
    }
}