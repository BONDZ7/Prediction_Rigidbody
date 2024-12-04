using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Events;

public class FirstObjectNotifier : NetworkBehaviour
{
    [SerializeField] private Transform followPoint;
    public static event UnityAction<Transform> OnFirstObjectSpawned;
        


    public override void OnStartClient()
    {
        base. OnStartClient();
        if (base.IsOwner)
        {
            NetworkObject nob = base.LocalConnection.FirstObject;
            if (nob == base.NetworkObject)
                OnFirstObjectSpawned?.Invoke(followPoint);
        }
    }
    
}
