using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    
    [SerializeField] private CinemachineVirtualCamera carCameraGround;

    private Transform _followPoint;


    public bool IsFollowingCar { get; private set; } = true;

    

    private void Awake()
    {
        FirstObjectNotifier.OnFirstObjectSpawned += FirstObjectNotifier_OnFirstObjectSpawned;
    }

    private void OnDestroy()
    {
        FirstObjectNotifier.OnFirstObjectSpawned -= FirstObjectNotifier_OnFirstObjectSpawned;
    }

    private void FirstObjectNotifier_OnFirstObjectSpawned(Transform followPoint)
    {
        _followPoint = followPoint;
        carCameraGround.Follow = _followPoint;
        carCameraGround.LookAt = _followPoint;
        IsFollowingCar = true;
        
    }
    
}
