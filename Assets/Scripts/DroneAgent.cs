﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;

public class DroneAgent: Agent {

	public VelocityControl velocityControl;
	[Range(0,100)] public float Scale;

	public bool use_new_state = true;

	public GameObject startRegion;
	public GameObject endRegion;
	private Bounds endBounds;

	public float FORWARD_VELOCITY;
	public float YAW_RATE;

    public float DONE_DISTANCE;

    private bool collided = false;

	private bool wait = false;

	private Vector3 initialPos;
    private Quaternion initialRot;

	private float maxX;
	private float minX;
	private float maxZ;
	private float minZ;

	private System.Random rand;

	private bool local_done = false;

	public override void InitializeAgent() {

        if (startRegion == null)
        {
            startRegion = GameObject.CreatePrimitive(PrimitiveType.Quad);
            startRegion.transform.Rotate(new Vector3(90, 0, 0));
            startRegion.transform.localScale = new Vector3(3.0f, 3.0f, 1.0f);
            startRegion.transform.localPosition = new Vector3(startRegion.transform.localPosition.x,
                                                            startRegion.transform.localPosition.y - 1,
                                                            startRegion.transform.localPosition.z);
        }

        if (endRegion == null)
        {
            endRegion = GameObject.CreatePrimitive(PrimitiveType.Quad);
            endRegion.transform.Rotate(new Vector3(90, 0, 0));
            endRegion.transform.localScale = new Vector3(3.0f, 3.0f, 1.0f);
            endRegion.transform.localPosition = new Vector3(endRegion.transform.localPosition.x, 
                                                            endRegion.transform.localPosition.y - 1, 
                                                            endRegion.transform.localPosition.z + 100);
        }

		Debug.Log ("Start BOUNDS");
		Renderer rend = startRegion.GetComponent<Renderer>();
		Debug.Log(rend.bounds.max);
		Debug.Log(rend.bounds.min);

		maxX = rend.bounds.max.x;
		minX = rend.bounds.min.x;

		maxZ = rend.bounds.max.z;
		minZ = rend.bounds.min.z;

		rand = new System.Random ();

		initialPos = transform.position;
        initialPos.y = velocityControl.initial_height;
        initialRot = transform.rotation;

		endBounds = endRegion.GetComponent<Renderer> ().bounds;

		// randomness
		float startX = ((float) rand.NextDouble()) * (maxX - minX) + minX;
		float startZ = ((float) rand.NextDouble()) * (maxZ - minZ) + minZ;

		transform.position = new Vector3 (startX, initialPos.y, startZ);

		wait = false;

        collided = false;

		local_done = false;

	}


	// gets relative header
	public float normalizedHeader(Vector3 gpsCurr, Vector3 gpsTarg) {
		Vector3 normalized = Vector3.Normalize (gpsTarg - gpsCurr);
		normalized.y = 0.0f;

		Vector3 currentHeading = Quaternion.Euler(new Vector3(0.0f, velocityControl.state.Angles.y, 0.0f)) * Vector3.forward;
		currentHeading.y = 0.0f;

		float angle = Vector3.SignedAngle (currentHeading, normalized, Vector3.up);
		return angle;
			
	}


	// API-3 changes
	public override void CollectObservations()
	{
        //		List<float> state = new List<float>();
        //Debug.Log("CALLED");

        //NEW STATE
        //do this in collect state so we make sure we don't miss it
        local_done = isDone() || collided;


        if (use_new_state)
        {
            // Header and Magnitude
            AddVectorObs(normalizedHeader(transform.position, endRegion.transform.position) / 180.0f); //-1 to 1
            AddVectorObs(Vector3.Magnitude(transform.position - endRegion.transform.position)); // nonscaled magnitude

            //Velocities (v forward, yaw)
            //Debug.Log(velocityControl.state.VelocityVector);
            //Debug.Log(velocityControl.state.AngularVelocityVector);
            //Debug.Log();
            AddVectorObs(velocityControl.state.VelocityVector.z / FORWARD_VELOCITY); // VX scaled -1 to 1
            AddVectorObs(velocityControl.state.AngularVelocityVector.y / YAW_RATE); //Yaw rate scaled -1  to 1

            //collision
            AddVectorObs((collided ? 1.0f : 0.0f));

        }
        else
        {

            //13 elements
            AddVectorObs(velocityControl.state.VelocityVector.z / 8.0f); // VX scaled
            AddVectorObs(velocityControl.state.VelocityVector.x / 8.0f); // VY scaled
            AddVectorObs(velocityControl.state.AngularVelocityVector.y / 360.0f); //Yaw scaled

            AddVectorObs(velocityControl.transform.position.x);
            AddVectorObs(velocityControl.transform.position.y);
            AddVectorObs(velocityControl.transform.position.z);

            AddVectorObs(velocityControl.transform.rotation.x);
            AddVectorObs(velocityControl.transform.rotation.y);
            AddVectorObs(velocityControl.transform.rotation.z);

            AddVectorObs(endRegion.transform.position.x);
            AddVectorObs(endRegion.transform.position.y);
            AddVectorObs(endRegion.transform.position.z);
            AddVectorObs((collided ? 1.0f : 0.0f));
        }
        
        if (collided)
        {
            Debug.Log("COLLISION MSG SENT");
            collided = false;
        }
        

	}

	// 1 element input
	// -> -1 : STOP
	// -> 0 : LEFT + FORWARD
	// -> 1 : STRAIGHT + FORWARD
	// -> 2 : RIGHT + FORWARD
	public override void AgentAction(float[] act, string textAction)
	{
		//only wait initially if we are a non external player
		if (wait && brain.brainType == BrainType.Player) {
			return;
		}

        if (isDone()) {
            GetComponent<Rigidbody>().velocity = Vector3.zero;
        }
		// add in code logic for drone control
//		basicControl.Controller.InputAction(0, act[0], act[1], act[2]);

		//Debug.Log (act);

//		float angle = normalizedHeader (transform.position, endRegion.transform.position);
//		Debug.Log (angle);

		// pitch forward as long as it isn't –1
		velocityControl.desired_vx = act[0] >= 0 ? FORWARD_VELOCITY : 0.0f;
		velocityControl.desired_vy = 0.0f;

		if (act [0] == 0) {
			//LEFT
			velocityControl.desired_yaw = -YAW_RATE;
		} else if (act [0] == 2) {
			//RIGHT
			velocityControl.desired_yaw = YAW_RATE;
		} else {
			//STOP or STRAIGHT
			velocityControl.desired_yaw = 0.0f;
		}


//		velocityControl.desired_vy = act [1] * 8.0f;
//		velocityControl.desired_yaw = act [2] * 360.0f;
//		velocityControl.desired_height = velocityControl.desired_height;



		//increments
		//AddReward(RewardFunction());

		// done checking
		//Vector3 currPos = new Vector3 (transform.position.x, endBounds.center.y, transform.position.z);
//		Debug.Log (currPos);
//		Debug.Log (endBounds);

        // no state collections being called coming in
        if (local_done)
        {
            //Debug.Log("STOP");
            Done();
            //HALT ALL MOTION UNTIL RESET
            velocityControl.enabled = false;
            GetComponent<Rigidbody>().isKinematic = true;
        }

	}

	public bool isDone(){
        Vector3 currPos = new Vector3(transform.position.x, endBounds.center.y, transform.position.z);
        //return endBounds.Contains(currPos)
        return Vector3.Magnitude(currPos - endBounds.center) <= DONE_DISTANCE;
    }

	public override void AgentReset()
	{
        local_done = false;
  //      Debug.Log("RESETTING");
		//temporarily
		velocityControl.enabled = false;
		// randomness
		float startX = ((float) rand.NextDouble()) * (maxX - minX) + minX;
		float startZ = ((float) rand.NextDouble()) * (maxZ - minZ) + minZ;

		transform.position = new Vector3 (startX, initialPos.y, startZ);
        transform.rotation = initialRot;
		//reset, which also re enables

		//StartCoroutine (Waiting (1.0f));
		//while (!wait) {
		//}

        GetComponent<Rigidbody>().isKinematic = false;
		velocityControl.Reset ();
	}

	IEnumerator Waiting(float time) {
		wait = true;
		yield return new WaitForSeconds(time);
		wait = false;
	}

	public override void AgentOnDone()
	{
	}

	// super basic reward function
	//float RewardFunction(){
		//if (collided) {
		//	collided = false;
		//	local_done = true;
		//	return -1000.0f;
		//} else {
		//	//euclidean horizontal plane distance
		//	float dist = Mathf.Pow(endRegion.transform.position.x - velocityControl.transform.position.x, 2) + Mathf.Pow(endRegion.transform.position.z - velocityControl.transform.position.z, 2);
		//	dist = Scale * dist;
		//	return 1.0f / dist;
		//}
			
	//}

	void OnCollisionEnter(Collision other)
	{
		Debug.LogWarning ("-- COLLISION --");
		collided = true;
	}
}
