﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class GhostAgent : Agent
{
    public Transform boundaryPlane;
    public Player player;
    private Vector3 playerStart;
    private MovementController movementController;
    private ShootProjectiles shooter;
    public GameObject origin;
    public GameObject barrel;
    private readonly object positionLock = new object();
    private float shootPunishment = 0.01f;
    public float reward = 0f;
    private bool resetting = false;
    public float minX, maxX, minZ, maxZ, xScaler, zScaler;
    public EnvironmentParameters envParams;
    private Vector2 noiseDirection = Vector2.zero;
    private float directionDecay = 0.99f;
    private bool triggerBool = true;
    float minX, maxX, minZ, maxZ, xScaler, zScaler;
    EnvironmentParameters envParams; 

    private void Awake()
    {
        SetBoundaries();
        envParams = Academy.Instance.EnvironmentParameters;
    }

    void Start()
    {
        this.playerStart = this.player.transform.localPosition;
        this.movementController = GetComponent<MovementController>();
        this.shooter = GetComponent<ShootProjectiles>();
        Projectile.PlayerHit += Reward;
        // Time.timeScale = 6f;
    }


    private void Reward()
    {
        reward += 1f;
        FinalizeReward(true);
        EndEpisode();
    }

    public void FinalizeReward(bool won)
    {
        float min = won ? 0.1f : 0;
        SetReward(reward > 0 ? reward : min);
    }

    public override void OnEpisodeBegin()
    {
        this.player.SetSpeed(envParams.GetWithDefault("player_speed", 0.0f));
        reward = 0f;
        this.transform.position = new Vector3(Random.Range(this.minX + 1.5f, this.maxX - 1.5f), 1, Random.Range(this.minZ + 1.5f, this.maxZ - 1.5f));
        this.transform.Rotate(new Vector3(0, Random.Range(-180f, 180f), 0));
        noiseDirection = Vector2.zero;
        this.player.transform.localPosition = new Vector3(Random.Range(this.minX + 1f, this.maxX - 1f), 0.5f, this.minZ + 1f);
        triggerBool = true;
    }

    private void SetBoundaries()
    {
        Vector3 scale = this.boundaryPlane.localScale;
        Vector3 position = this.boundaryPlane.position;
        float xHalf = scale.x * 5;
        float zHalf = scale.z * 5;
        this.minX = position.x - xHalf;
        this.maxX = position.x + xHalf;
        this.xScaler = this.maxX - this.minX;

        this.minZ = position.z - zHalf;
        this.maxZ = position.z + zHalf;
        this.zScaler = this.maxZ - this.minZ;
    }

    private Vector2 NormalizePosition(float x, float z)
    {
        float xNorm = (x - this.minX) / this.xScaler;
        float zNorm = (z - this.minZ) / this.zScaler;
        return new Vector2(xNorm, zNorm);
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // agent position / rotation
        // sensor.AddObservation(this.transform.localRotation.y / 180f);
        // (float xNorm, float zNorm) = NormalizePosition(this.transform.localPosition.x, this.transform.localPosition.z);
        // sensor.AddObservation(xNorm);
        // sensor.AddObservation(zNorm);

        Vector3 agentDirection = transform.TransformDirection(Vector3.forward);
        sensor.AddObservation(agentDirection.x);
        sensor.AddObservation(agentDirection.z);

        // shoot state
        sensor.AddObservation(shooter.GetShootReady());

        // player noise vector
        Vector2 dir = CalculateNoise();
        sensor.AddObservation(dir.x);
        sensor.AddObservation(dir.y);
    }

    private Vector3 CalculateNoise()
    {
        // vector to player
        Vector2 agentPos = NormalizePosition(this.player.transform.localPosition.x, this.player.transform.localPosition.z);
        Vector2 playerPos = NormalizePosition(this.transform.localPosition.x, this.transform.localPosition.z);
        Vector2 currDirection = agentPos - playerPos;
        /* 
        if (Random.Range(0, 100) == 1)
        {
            noiseDirection += currDirection.normalized;
        }
        noiseDirection *= 0.99f;
        */
        return currDirection.normalized;
    }

    public override void OnActionReceived(float[] act)
    {
        // walk
        int action = Mathf.FloorToInt(act[0]);

        if (action >= 0 && action <=3)
        {
            Move(action);
        }
        else if (action >= 4 && action <=5)
        {
            Rotate(action);
        }
        else if (action == 6)
        {
            Shoot();
        }

        CheckEpisodeEnd();
    }

    public virtual void CheckEpisodeEnd()
    {
        float distanceToTarget = Vector3.Distance(this.transform.localPosition, 
            this.player.transform.localPosition);
        // contacted player
        if (distanceToTarget < 1.42f)
        {
            FinalizeReward(false);
            EndEpisode();
        }

        // out of bounds
        if (!InBounds(this.transform.localPosition))
        {
            FinalizeReward(false);
            EndEpisode();
        }

    }

    //collides with an obstacle
    /*public void OnTriggerEnter(Collider other)
    {
        if (triggerBool == true)
        {
            FinalizeReward(false);
            EndEpisode();
            triggerBool = false;
        }
        
    }*/

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = 3f;
        // walk
        if (Input.GetKey(KeyCode.W))
        {
            actionsOut[0] = 0f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            actionsOut[0] = 1f;
        }
        if (Input.GetKey(KeyCode.D)) {
            actionsOut[0] = 2f;
        }
        // 3 is do nothing
        // rotate
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            actionsOut[0] = 4;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            actionsOut[0] = 5;
        }
        // shoot
        if (Input.GetKey(KeyCode.Space))
        {
            actionsOut[0] = 6;
        }
    }

    private bool InBounds(Vector3 position)
    {
        var playerPosition = this.player.transform.localPosition;
        if ((position.x > this.minX && position.x < this.maxX) && (position.z > this.minZ && position.z < this.maxZ))
        {
            if ((playerPosition.x > this.minX && playerPosition.x < this.maxX) && (playerPosition.z <= this.maxZ))
            {
                return true;
            }
        }
        return false;
    }

    private void Move(int direction)
    {
            // forward
            if (direction == 0) this.movementController.Move(Vector3.forward);
            // left
            if (direction == 1) this.movementController.Move(Vector3.left);
            // right
            if (direction == 2) this.movementController.Move(Vector3.right);

            // direction == 3 for staying still
    }

    private void Rotate(int direction)
    {
            float rotationStep = 0.1f;
            Quaternion rotation = Quaternion.identity;
            // rotate left
            if (direction == 4) this.movementController.Rotate(-rotationStep);
            // rotate right
            if (direction == 5) this.movementController.Rotate(rotationStep);
    }

    public virtual void Shoot()
    {
        reward -= shootPunishment;
        shooter.Shoot(origin.transform.position, barrel.transform.position);
    }

}
