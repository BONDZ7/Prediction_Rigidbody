using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

public class MovementController : NetworkBehaviour
{
    private PredictionRigidbody predictionRigidbody;

    [SerializeField] private List<Transform> hoverPoints = new();
    
    public float suspensionRestDist = 2.0f; 
    public float springStrength = 500.0f; 
    public float springDamper = 50.0f;
    [SerializeField] private float carSpeed;
    [SerializeField] private float carTopSpeed;
    [SerializeField] private float brakeAcceleration;
    [SerializeField] private AnimationCurve powerCurve;

    private float ackermennLeftAngle, ackermennRightAngle, wheelBase , rearTrack;
    
    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += TimeManager_OnTick;
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }
    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= TimeManager_OnTick;
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        
        
        
        var rb = GetComponent<Rigidbody>();
        predictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
        predictionRigidbody.Initialize(rb);
        
        wheelBase = Vector3.Distance(hoverPoints[0].position, hoverPoints[2].position);
        rearTrack = Vector3.Distance(hoverPoints[0].position, hoverPoints[1].position);

        
    }
    
    private void TimeManager_OnTick()
    {
        SendInputData(CreateInputData());
    }

    private MoveData CreateInputData()
    {
        // Gather input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool handbrake = Input.GetKey(KeyCode.Space);
        MoveData moveData = new MoveData(horizontal, vertical,handbrake);

        return moveData;
    }
        
    private void TimeManager_OnPostTick()
    {
        if(!IsServerStarted) return;
            
        CreateReconcile();
    }
    
    public override void CreateReconcile()
    {
        ReconcileData rd = new ReconcileData(predictionRigidbody);
        ReconcileState(rd);
    }
        
    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        if (base.IsClientOnlyInitialized || base.IsServerInitialized && base.TimeManager.LocalTick % 5 == 0)
        {
            predictionRigidbody.Reconcile(data.PredictionRigidbody);
        }
    }
        
        
    [Replicate]
    private void SendInputData(MoveData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        // Move(data, false);

        AckermannSteering(data.Horizontal);
        foreach (Transform hoverPoint in hoverPoints)
        {
            ApplySuspension(hoverPoint);
            ApplySteeringForce(hoverPoint, 1, 20);
        }
        ApplyAcceleration(hoverPoints[0], data.Vertical);
        ApplyAcceleration(hoverPoints[1], data.Vertical);
        BrakeLogic(data.Handbrake ? 1f : 0f);
        predictionRigidbody.Simulate();

    }

    private void Update()
    {
        if (!IsOwner)
            return;

        
    }


    private void ApplySuspension(Transform hoverPoint)
    {
        if (Physics.Raycast(hoverPoint.position, Vector3.down, out RaycastHit hit, suspensionRestDist))
        {
            Vector3 springDir = hoverPoint.up;

            Vector3 hoverPointVelocity = predictionRigidbody.Rigidbody.GetPointVelocity(hoverPoint.position);

            float offset = suspensionRestDist - hit.distance;

            float velocityAlongSpring = Vector3.Dot(springDir, hoverPointVelocity);

            float springForce = (offset * springStrength) - (velocityAlongSpring * springDamper);

            predictionRigidbody.AddForceAtPosition(springDir * springForce, hoverPoint.position);
        }
        else
        {
            predictionRigidbody.AddForceAtPosition(Vector3.down * springDamper, hoverPoint.position);
        }
        
    }
    
    private void ApplySteeringForce(Transform hoverPoint, float gripFactor, float tireMass)
    {
        if (Physics.Raycast(hoverPoint.position, Vector3.down, out RaycastHit hit, suspensionRestDist))
        {
            Vector3 steeringDir = hoverPoint.right;

            Vector3 hoverPointVelocity = predictionRigidbody.Rigidbody.GetPointVelocity(hoverPoint.position);

            float steeringVel = Vector3.Dot(steeringDir, hoverPointVelocity);

            float desiredVelChange = -steeringVel * gripFactor;

            float desiredAccel = desiredVelChange / Time.fixedDeltaTime;

            predictionRigidbody.AddForceAtPosition(steeringDir * (tireMass * desiredAccel), hoverPoint.position);
        }
    }

    private void ApplyAcceleration(Transform hoverPoint, float accelerationInput)
    {
        if (Physics.Raycast(hoverPoint.position, Vector3.down, out RaycastHit hit, suspensionRestDist))
        {
            Vector3 accelDir = hoverPoint.forward;

            float _carSpeed = Vector3.Dot(transform.forward, predictionRigidbody.Rigidbody.velocity);

            float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_carSpeed) / carTopSpeed);

            float availableTorque = powerCurve.Evaluate(normalizedSpeed) * accelerationInput;
            

            predictionRigidbody.AddForceAtPosition(accelDir * (availableTorque * carSpeed), hoverPoint.position);
        }
    }

    private void AckermannSteering(float steerInput)
    {
        float turnRadius = wheelBase / Mathf.Tan(20 / Mathf.Rad2Deg); //20 is MaxTurnAngle
        if (steerInput > 0) //is turning right
        {
            ackermennLeftAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius + (rearTrack / 2))) * steerInput;
            ackermennRightAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius - (rearTrack / 2))) * steerInput;
        }
        else if (steerInput < 0) //is turning left
        {
            ackermennLeftAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius - (rearTrack / 2))) * steerInput;
            ackermennRightAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius + (rearTrack / 2))) * steerInput;
        }
        else
        {
            ackermennLeftAngle = 0;
            ackermennRightAngle = 0;
        }

        // auto counter steering
        // if (localVehicleVelocity.z > 0 && AutoCounterSteer && Mathf.Abs(localVehicleVelocity.x) > 1f)
        // {
        //     ackermennLeftAngle += Vector3.SignedAngle(transform.forward, predictionRigidbody.Rigidbody.velocity + transform.forward, transform.up);
        //     ackermennLeftAngle = Mathf.Clamp(ackermennLeftAngle, -70, 70);
        //     ackermennRightAngle += Vector3.SignedAngle(transform.forward, predictionRigidbody.Rigidbody.velocity + transform.forward, transform.up);
        //     ackermennRightAngle = Mathf.Clamp(ackermennRightAngle, -70, 70);
        // }

        hoverPoints[0].localRotation = Quaternion.Euler(0, ackermennLeftAngle, 0);
        hoverPoints[1].localRotation = Quaternion.Euler(0, ackermennRightAngle, 0);

    }
    
    private void BrakeLogic(float brakeInput)
    {
        // Debug.Log("brakeInput >>>> " + brakeInput);
        float localSpeed = Vector3.Dot(predictionRigidbody.Rigidbody.velocity, transform.forward);

        float deltaSpeed = brakeAcceleration * brakeInput * Time.fixedDeltaTime * Mathf.Clamp01(Mathf.Abs(localSpeed));
        deltaSpeed = Mathf.Clamp(deltaSpeed, -carTopSpeed, carTopSpeed);
        if (localSpeed > 0)
        {
            // predictionRigidbody.Rigidbody.velocity -= transform.forward * deltaSpeed;
            predictionRigidbody.Velocity(predictionRigidbody.Rigidbody.velocity - transform.forward * deltaSpeed);

        }
        else
        {
            // predictionRigidbody.Rigidbody.velocity += transform.forward * deltaSpeed;
            predictionRigidbody.Velocity(predictionRigidbody.Rigidbody.velocity + transform.forward * deltaSpeed);

        }
    }




   
    
    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref predictionRigidbody);
    }
    
    private struct MoveData : IReplicateData
    {
        public readonly float Horizontal;
        public readonly float Vertical;
        public readonly bool Handbrake;
        private uint _tick;

        public MoveData(float horizontal, float vertical,bool handbrake) : this()
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Handbrake = handbrake;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }

    
    public struct ReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
    
        public ReconcileData(PredictionRigidbody pr) : this()
        {
            PredictionRigidbody = pr;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}


