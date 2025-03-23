using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.TransformBinders.BoneControl;
using Unity.Mathematics;
using UnityEngine;
[System.Serializable]
public class BasisVirtualSpineDriver
{
    [SerializeField] public BasisBoneControl CenterEye;
    [SerializeField] public BasisBoneControl Head;
    [SerializeField] public BasisBoneControl Neck;
    [SerializeField] public BasisBoneControl Chest;
    [SerializeField] public BasisBoneControl Spine;
    [SerializeField] public BasisBoneControl Hips;
    [SerializeField] public BasisBoneControl RightShoulder;
    [SerializeField] public BasisBoneControl LeftShoulder;
    [SerializeField] public BasisBoneControl LeftLowerArm;
    [SerializeField] public BasisBoneControl RightLowerArm;
    [SerializeField] public BasisBoneControl LeftLowerLeg;
    [SerializeField] public BasisBoneControl RightLowerLeg;
    [SerializeField] public BasisBoneControl LeftHand;
    [SerializeField] public BasisBoneControl RightHand;
    [SerializeField] public BasisBoneControl LeftFoot;
    [SerializeField] public BasisBoneControl RightFoot;
    public float NeckRotationSpeed = 12;
    public float ChestRotationSpeed = 25;
    public float SpineRotationSpeed = 30;
    public float HipsRotationSpeed = 40;
    public float MaxNeckAngle = 0; // Limit the neck's rotation range to avoid extreme twisting
    public float MaxChestAngle = 0; // Limit the chest's rotation range
    public float MaxHipsAngle = 0; // Limit the hips' rotation range
    public float MaxSpineAngle = 0;
    public void Initialize()
    {
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out CenterEye, BasisBoneTrackedRole.CenterEye))
        {
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Head, BasisBoneTrackedRole.Head))
        {
            Head.HasVirtualOverride = true;
            Head.VirtualRun += OnSimulateHead;
            BasisLocalPlayer.Instance.LocalBoneDriver.ReadyToRead.AddAction(30, Hint);
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Neck, BasisBoneTrackedRole.Neck))
        {
            Neck.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Chest, BasisBoneTrackedRole.Chest))
        {
            Chest.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Spine, BasisBoneTrackedRole.Spine))
        {
            Spine.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Hips, BasisBoneTrackedRole.Hips))
        {
            Hips.HasVirtualOverride = true;
          //  Hips.HasInverseOffsetOverride = true;
         //  Hips.VirtualInverseOffsetRun += OnSimulateHipsWithTracker;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftLowerArm, BasisBoneTrackedRole.LeftLowerArm))
        {
         //  LeftLowerArm.HasVirtualOverride = true;
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightLowerArm, BasisBoneTrackedRole.RightLowerArm))
        {
         //  RightLowerArm.HasVirtualOverride = true;
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftLowerLeg, BasisBoneTrackedRole.LeftLowerLeg))
        {
         //  LeftLowerLeg.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightLowerLeg, BasisBoneTrackedRole.RightLowerLeg))
        {
            //RightLowerLeg.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftHand, BasisBoneTrackedRole.LeftHand))
        {
            // LeftHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightHand, BasisBoneTrackedRole.RightHand))
        {
            //   RightHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftFoot, BasisBoneTrackedRole.LeftFoot))
        {
            // LeftHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightFoot, BasisBoneTrackedRole.RightFoot))
        {
            //   RightHand.HasVirtualOverride = true;
        }
    }
    public void Hint()
    {
        /*
        if (LeftLowerLeg.HasTracked != BasisHasTracked.HasTracker)
        {
        //    LeftLowerLeg.BoneTransform.position = GenerateHintPosition();
       //     Vector3 hintPosition = GenerateHintPosition(root.position, mid.position, target.position);
        }
        if (RightLowerLeg.HasTracked != BasisHasTracked.HasTracker)
        {
            RightLowerLeg.BoneTransform.position = RightFoot.BoneTransform.position;
        }
        if (LeftLowerArm.HasTracked != BasisHasTracked.HasTracker)
        {
            LeftLowerArm.BoneTransform.position = LeftHand.BoneTransform.position;
        }
        if (LeftLowerArm.HasTracked != BasisHasTracked.HasTracker)
        {
            LeftLowerArm.BoneTransform.position = LeftHand.BoneTransform.position;
        }
        if (RightLowerArm.HasTracked != BasisHasTracked.HasTracker)
        {
            RightLowerArm.BoneTransform.position = RightHand.BoneTransform.position;
        }
        */
    }
    public void DeInitialize()
    {
        if (Neck != null)
        {
            Neck.VirtualRun -= OnSimulateHead;
            Neck.HasVirtualOverride = false;
        }
        if (Chest != null)
        {
            Chest.HasVirtualOverride = false;
        }
        if (Hips != null)
        {
            Hips.HasVirtualOverride = false;
        }
        if (Spine != null)
        {
            Spine.HasVirtualOverride = false;
        }
     //   Hips.HasInverseOffsetOverride = false;
     //   Hips.VirtualInverseOffsetRun -= OnSimulateHipsWithTracker;
    }
    public void OnSimulateHead()
    {
        float time = BasisLocalPlayer.Instance.LocalBoneDriver.DeltaTime;

        Head.OutGoingData.rotation = CenterEye.OutGoingData.rotation;
        Neck.OutGoingData.rotation = Head.OutGoingData.rotation;

        // Now, apply the spine curve progressively:
        // The chest should not follow the head directly, it should follow the neck but with reduced influence.
        Quaternion targetChestRotation = Quaternion.Slerp(Chest.OutGoingData.rotation,Neck.OutGoingData.rotation,time * ChestRotationSpeed);
        Vector3 EulerChestRotation = targetChestRotation.eulerAngles;
        float clampedChestPitch = Mathf.Clamp(EulerChestRotation.x, -MaxChestAngle, MaxChestAngle);
        Chest.OutGoingData.rotation = Quaternion.Euler(clampedChestPitch, EulerChestRotation.y, 0);

        // The hips should stay upright, using chest rotation as a reference
        Quaternion targetSpineRotation = Quaternion.Slerp(Spine.OutGoingData.rotation, Chest.OutGoingData.rotation, time * SpineRotationSpeed);// Lesser influence for hips to remain more upright
        Vector3 targetSpineRotationEuler = targetSpineRotation.eulerAngles;
        float clampedSpinePitch = Mathf.Clamp(targetSpineRotationEuler.x, -MaxSpineAngle, MaxSpineAngle);
        Spine.OutGoingData.rotation = Quaternion.Euler(clampedSpinePitch, targetSpineRotationEuler.y, 0);

        // The hips should stay upright, using chest rotation as a reference
        Quaternion targetHipsRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Spine.OutGoingData.rotation, time * HipsRotationSpeed);// Lesser influence for hips to remain more upright
        Vector3 targetHipsRotationEuler = targetHipsRotation.eulerAngles;
        float clampedHipsPitch = Mathf.Clamp(targetHipsRotationEuler.x, -MaxHipsAngle, MaxHipsAngle);
        Hips.OutGoingData.rotation = Quaternion.Euler(clampedHipsPitch, targetHipsRotationEuler.y, 0);

        // Handle position control for each segment if targets are set (as before)
        ApplyPositionControl(Head);
        ApplyPositionControl(Neck);
        ApplyPositionControl(Chest);
        ApplyPositionControl(Spine);
        ApplyPositionControl(Hips);
    }
    /// <summary>
    /// this works well however its not good enough.
    /// </summary>
    public void OnSimulateHipsWithTracker()
    {
        // Calculate the maximum allowed stretch between Neck and Hips
        float MaxStretch = Vector3.Distance(Neck.TposeLocal.position, Hips.TposeLocal.position);

        // Update the position of the secondary transform to maintain the initial offset
        Vector3 targetPosition = Hips.IncomingData.position + math.mul(Hips.IncomingData.rotation, Hips.InverseOffsetFromBone.position);
        Hips.OutGoingData.position = Vector3.Lerp(Hips.OutGoingData.position, targetPosition, Hips.trackersmooth);

        // Constrain the position if the distance exceeds the maximum allowed stretch
       float Distance = Vector3.Distance(Neck.OutGoingData.position, Hips.OutGoingData.position);
        if (Distance > MaxStretch)
        {
            // Clamp the position along the direction vector
            Vector3 Difference = (Hips.OutGoingData.position - Neck.OutGoingData.position);
            Vector3 direction = Difference.normalized;
            Vector3 NeckOutgoing = Neck.OutGoingData.position;
            Hips.OutGoingData.position = NeckOutgoing + direction * MaxStretch;
        }

        // Update the rotation of the secondary transform to maintain the initial offset
        Hips.OutGoingData.rotation = Quaternion.Slerp(Hips.OutGoingData.rotation, math.mul(Hips.IncomingData.rotation, Hips.InverseOffsetFromBone.rotation), Hips.trackersmooth);
    }
    private void ApplyPositionControl(BasisBoneControl boneControl)
    {
        if (boneControl.HasTarget)
        {
            quaternion targetRotation = boneControl.Target.OutGoingData.rotation;

            // Extract only Yaw (Horizontal rotation), ignoring Pitch (Up/Down)
            float3 forward = math.mul(targetRotation, new float3(0, 0, 1));
            forward.y = 0; // Flatten to avoid up/down swaying
            forward = math.normalize(forward);

            quaternion correctedRotation = quaternion.LookRotationSafe(forward, new float3(0, 1, 0));

            float3 customDirection = math.mul(correctedRotation, boneControl.Offset);
            boneControl.OutGoingData.position = boneControl.Target.OutGoingData.position + customDirection;
        }
    }
}
