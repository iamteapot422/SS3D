﻿using FishNet.Component.Animating;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Linq;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Component for character's gameobject, that controlls ragdoll
    /// </summary>
	public class Ragdoll : NetworkBehaviour
	{
        [SerializeField]
		private Transform _armatureRoot;
        private Transform _hips;
        private Transform _character;
        private Animator _animator; 
        private NetworkAnimator _networkAnimator; 
        private HumanoidLivingController _humanoidLivingController; 
        private CharacterController _characterController; 
        private Transform[] _ragdollParts;
        /// <summary>
        /// If knockdown is supposed to expire
        /// </summary>
        private bool _isKnockdownTimed;
        /// <summary>
        /// How many seconds are left before the ragdoll expires
        /// </summary>
        private float _knockdownTimer; 
        private float _elapsedResetBonesTime; 
        private float _timeToResetBones = 0.5f;
        /// <summary>
        /// Determines how much higher than the lowest point character will be during AlignToHips(). This var prevent character from getting stuck in the floor 
        /// </summary>
        private const float AlignmentYDelta = 0.0051f;
        private enum RagdollState
        {
            Walking,
            Ragdoll,
            BonesReset,
            StandingUp
        }
        private RagdollState _currentState;
        private class BoneTransform
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }
        /// <summary>
        /// Bones Transforms (position and rotation) in the first frame of StandUp animation
        /// </summary>
        private BoneTransform[] _standUpBones;
        /// <summary>
        /// Bones Transforms (position and rotation) during the Ragdoll state
        /// </summary>
        private BoneTransform[] _ragdollBones;
        [NonSerialized]
        [SyncVar(OnChange = nameof(OnSyncKnockdown))]
        public bool IsKnockedDown;
        [field: NonSerialized]
        [field: SyncVar]
        private bool IsFacingDown { get; [ServerRpc] set; }
        [SerializeField]
        private AnimationClip _standUpFaceUpClip;
        [SerializeField]
        private AnimationClip _standUpFaceDownClip;

        [SerializeField]
        private byte _ragdollPartSyncInterval;

        private void OnSyncKnockdown(bool prev, bool next, bool asServer)
		{
			if (prev == next) return;
            if (next)
			{
                Knockdown();
            }
			else
            {
				BonesReset();
			}
		}
        public override void OnStartNetwork()
		{
			base.OnStartNetwork();

			_animator = GetComponent<Animator>();
			_humanoidLivingController = GetComponent<HumanoidLivingController>();
			_characterController = GetComponent<CharacterController>();
			_networkAnimator = GetComponent<NetworkAnimator>();
            _knockdownTimer = 0;
            _hips = _armatureRoot.GetChild(0);
            _character = _armatureRoot.parent;
            _currentState = RagdollState.Walking;
            _ragdollParts = (from part in GetComponentsInChildren<RagdollPart>() select part.transform.GetComponent<Transform>()).ToArray();
            _standUpBones = new BoneTransform[_ragdollParts.Length];
            _ragdollBones = new BoneTransform[_ragdollParts.Length];

            for (int boneIndex = 0; boneIndex < _ragdollParts.Length; boneIndex++)
            {
                _standUpBones[boneIndex] = new();
                _ragdollBones[boneIndex] = new();
            }
            // All rigid bodies are kinematic at start, only the owner should be able to change that afterwards.
			ToggleKinematic(true);
            ToggleSyncRagdoll(false);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            // Set interval need to be called by owner. This allows fast setting
            foreach (Transform part in _ragdollParts)
            {
                part.GetComponent<NetworkTransform>().SetInterval(_ragdollPartSyncInterval);
            }
        }

        private void OnDisable()
        {
            Recover();
        }

        private void Update()
		{
            if (IsServer && _isKnockdownTimed && IsKnockedDown)
            {
                _knockdownTimer -= Time.deltaTime;
                if (_knockdownTimer <= 0)
                {
                    Recover();
                }
            }
            switch (_currentState)
            {
                case RagdollState.Walking:
                    WalkingBehavior();
                    break;
                case RagdollState.Ragdoll:
                    RagdollBehavior();
                    break;
                case RagdollState.BonesReset:
                    BonesResetBehavior();
                    break;
                case RagdollState.StandingUp:
                    StandingUpBehavior();
                    break;
            }
        }

        private void WalkingBehavior() { }
        
        /// <summary>
        /// Cast knockdown, that isn't going to expire until Recover()
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void KnockdownTimeless()
        {
            if (!enabled) return;
            
            _isKnockdownTimed = false;
            IsKnockedDown = true;
        }
        /// <summary>
        /// Knockdown the character for some time.
        /// </summary>
        /// <param name="seconds"></param>
        [ServerRpc(RequireOwnership = false)]
        public void Knockdown(float seconds)
        {
            if (!enabled) return;
            _isKnockdownTimed = true;
            _knockdownTimer += seconds;
            IsKnockedDown = true;
        }
        
        private void Knockdown()
        {
            _currentState = RagdollState.Ragdoll;
            Vector3 movement = _humanoidLivingController.TargetMovement * 3;
            ToggleSyncRagdoll(true);
            ToggleController(false);
            ToggleAnimator(false);

            if (!IsOwner && Owner.ClientId != -1)
                return;
            
            ToggleKinematic(false);
            foreach (Transform part in _ragdollParts)
            {
                part.GetComponent<Rigidbody>().AddForce(movement, ForceMode.VelocityChange);
            }
        }

        private void RagdollBehavior()
        {
            // Only the owner handles ragdoll's physics
            if (!IsOwner) return;
            AlignToHips();
        }
        /// <summary>
        /// Adjust player's position and rotation. Character's x and z coords equals hips coords, y is at lowest positon.
        /// Character's y rotation is aligned with hips forwards direction.
        /// </summary>
        private void AlignToHips()
        {
            IsFacingDown = _hips.transform.forward.y < 0;
            Vector3 originalHipsPosition = _hips.position;
            Vector3 newPosition = _hips.position;
            // Get the lowest position
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
            {
                newPosition.y = hitInfo.point.y + AlignmentYDelta;
            }
            _character.position = newPosition;
            _hips.position = originalHipsPosition;
            
            Vector3 desiredDirection = _hips.up * (IsFacingDown ? 1 : -1);
            desiredDirection.y = 0;
            desiredDirection.Normalize();
            Quaternion originalHipsRotation = _hips.rotation;
            Vector3 rotationDifference = Quaternion.FromToRotation(transform.forward, desiredDirection).eulerAngles;
            // Make sure that rotation is only around Y axis
            rotationDifference.x = 0;
            rotationDifference.z = 0;
            transform.rotation *= Quaternion.Euler(rotationDifference);
            _hips.rotation = originalHipsRotation;
        }
        /// <summary>
        /// Switch to BonesReset state and prepare for BonesResetBehavior
        /// </summary>
        private void BonesReset()
        {
            _currentState = RagdollState.BonesReset;
            _elapsedResetBonesTime = 0;

            ToggleSyncRagdoll(false);
            
            // Only the owner handles ragdoll's physics
            if (!IsOwner) return;
            ToggleKinematic(true);
            PopulatePartsTransforms(_ragdollBones);
            PopulateStandUpPartsTransforms(_standUpBones, IsFacingDown ? _standUpFaceDownClip : _standUpFaceUpClip);
        }
        /// <summary>
        /// Interpolate bones between their lates ragdoll transform and their transform at the first frame of StandUp animation
        /// </summary>
        private void BonesResetBehavior()
        {
            _elapsedResetBonesTime += Time.deltaTime;
            if (_elapsedResetBonesTime >= _timeToResetBones)
            {
                StandUp();
            }
            
            // Only the owner handles ragdoll's physics
            float elapsedPercentage = _elapsedResetBonesTime / _timeToResetBones;
            if (!IsOwner) return;
            for (int partIndex = 0; partIndex < _ragdollParts.Length; partIndex++)
            {
                _ragdollParts[partIndex].localPosition = Vector3.Lerp(_ragdollBones[partIndex].Position, _standUpBones[partIndex].Position, elapsedPercentage);
                _ragdollParts[partIndex].localRotation = Quaternion.Lerp(_ragdollBones[partIndex].Rotation, _standUpBones[partIndex].Rotation, elapsedPercentage);
            }
        }
        /// <summary>
        /// End the BonesReset state and start StandUp animation
        /// </summary>
        private void StandUp()
        {
            _currentState = RagdollState.StandingUp;
            ToggleAnimator(true);
            // State names have to be the same as animation names
            _animator.Play(IsFacingDown ? _standUpFaceDownClip.name : _standUpFaceUpClip.name, 0, 0);
        }
        /// <summary>
        /// Wait till StandUp animation is done
        /// </summary>
        private void StandingUpBehavior()
        {
            string standUpName = (IsFacingDown ? _standUpFaceDownClip : _standUpFaceUpClip).name;
            // If animation has ended, switch to walking
            if (_animator.GetCurrentAnimatorStateInfo(0).IsName(standUpName) == false)
            {
                Walk();
            }
        }
        /// <summary>
        /// Switch state to Walking
        /// </summary>
        private void Walk()
        {
            _currentState = RagdollState.Walking;
            ToggleController(true);
        }
        /// <summary>
        /// Copy current ragdoll parts positions to array
        /// </summary>
        /// <param name="partsTransforms">Array, that receives ragdoll parts positions</param>
        private void PopulatePartsTransforms(BoneTransform[] partsTransforms)
        {
            for (int partIndex = 0; partIndex < _ragdollParts.Length; partIndex++)
            {
                partsTransforms[partIndex].Position = _ragdollParts[partIndex].localPosition;
                partsTransforms[partIndex].Rotation = _ragdollParts[partIndex].localRotation;
            }
        }

        /// <summary>
        /// Copy ragdoll parts position in first frame of StandUp animation to array
        /// </summary>
        /// <param name = "partsTransforms">Array, that receives ragdoll parts positions</param>
        /// <param name="animationClip"></param>
        private void PopulateStandUpPartsTransforms(BoneTransform[] partsTransforms, AnimationClip animationClip)
        {
            BoneTransform[] originalTransforms = (BoneTransform[])_ragdollBones.Clone();
            Vector3 originalArmaturePosition = _armatureRoot.localPosition;
            Quaternion originalArmatureRotation = _armatureRoot.localRotation;
            // Put character into first frame of animation
            animationClip.SampleAnimation(gameObject, 0f);
            Vector3 originalHipsPosition = _hips.position;
            Quaternion originalHipsRotation = _hips.rotation;
            _armatureRoot.localPosition = originalArmaturePosition;
            _armatureRoot.localRotation = originalArmatureRotation;
            _hips.position = originalHipsPosition;
            _hips.rotation = originalHipsRotation;
            PopulatePartsTransforms(partsTransforms);
            
            // Move bones back to their original positions
            for (int partIndex = 0; partIndex < _ragdollParts.Length; partIndex++)
            {
                _ragdollParts[partIndex].localPosition = originalTransforms[partIndex].Position;
                _ragdollParts[partIndex].localRotation = originalTransforms[partIndex].Rotation;
            }
        }
        [ServerRpc(RequireOwnership = false)]
        public void Recover()
        {
            IsKnockedDown = false;
            _knockdownTimer = 0;
        }
        
		/// <summary>
		/// Switch isKinematic for each ragdoll part
		/// </summary>
		private void ToggleKinematic(bool isKinematic)
		{
			foreach (Transform part in _ragdollParts)
			{
				part.GetComponent<Rigidbody>().isKinematic = isKinematic;
			}
		}
        private void ToggleController(bool enable)
        {
            if (_humanoidLivingController != null)
            {
                _humanoidLivingController.enabled = enable;
            }
            if (_characterController != null)
            {
                _characterController.enabled = enable;
            }
        }
        private void ToggleAnimator(bool enable)
        {
            // Speed=0 prevents animator from choosing Walking animations after enabling it
            if (!enable)
                _animator.SetFloat("Speed", 0);

            if (_animator != null)
            {
                _animator.enabled = enable;
            }
            if (_networkAnimator != null)
            {
                _networkAnimator.enabled = enable;
            }
        }

        /// <summary>
        /// Toggle the network transform syncing of the ragdoll parts, to save up on those sweet bytes.
        /// </summary>
        /// <param name="isActive"> true if the network transform of the ragdoll parts should sync</param>
        /// <returns></returns>
        
        private void ToggleSyncRagdoll(bool isActive)
        {
            foreach (Transform part in _ragdollParts)
            {
                part.GetComponent<NetworkTransform>().SetSynchronizePosition(isActive);
                part.GetComponent<NetworkTransform>().SetSynchronizeRotation(isActive);
            }
        }
    }
}