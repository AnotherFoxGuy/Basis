using Basis.Network.Core;
using Basis.Scripts.Device_Management.Devices.Desktop;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.Profiler;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static SerializableBasis;


namespace Basis.Scripts.Networking.Transmitters
{
    [DefaultExecutionOrder(15001)]
    [System.Serializable]
    public partial class BasisNetworkTransmitter : BasisNetworkPlayer
    {
        public bool HasEvents = false;
        public float timer = 0f;
        public float interval = 0.0333333333333333f;
        public float SmallestDistanceToAnotherPlayer;
        [SerializeField]
        public BasisAudioTransmission AudioTransmission = new BasisAudioTransmission();
        public NativeArray<float3> targetPositions;
        public NativeArray<float> distances;
        public NativeArray<bool> DistanceResults;

        public NativeArray<bool> HearingResults;
        public NativeArray<bool> AvatarResults;
        public NativeArray<float> smallestDistance;

        public float[] FloatArray = new float[LocalAvatarSyncMessage.StoredBones];
        public ushort[] UshortArray = new ushort[LocalAvatarSyncMessage.StoredBones];
        [SerializeField]
        public LocalAvatarSyncMessage LASM = new LocalAvatarSyncMessage();
        public float UnClampedInterval;

        public float DefaultInterval = 0.0333333333333333f;
        public float BaseMultiplier = 1f; // Starting multiplier.
        public float IncreaseRate = 0.005f; // Rate of increase per unit distance.
        public CombinedDistanceAndClosestTransformJob distanceJob = new CombinedDistanceAndClosestTransformJob();
        public JobHandle distanceJobHandle;
        public int IndexLength = -1;
        public float SlowestSendRate = 2.5f;
        public NetDataWriter AvatarSendWriter = new NetDataWriter(true, LocalAvatarSyncMessage.AvatarSyncSize);
        public bool[] MicrophoneRangeIndex;
        public bool[] LastMicrophoneRangeIndex;

        public bool[] HearingIndex;
        public bool[] AvatarIndex;
        public ushort[] HearingIndexToId;

        public AdditionalAvatarData[] AdditionalAvatarDatas;
        public Dictionary<byte, AdditionalAvatarData> SendingOutAvatarData = new Dictionary<byte, AdditionalAvatarData>();
        /// <summary>
        /// schedules data going out. replaces existing byte index.
        /// </summary>
        /// <param name="AvatarData"></param>
        public void AddAdditonal(AdditionalAvatarData AvatarData)
        {
            SendingOutAvatarData[AvatarData.messageIndex] = AvatarData;
        }
        public void ClearAdditional()
        {
            SendingOutAvatarData.Clear();
        }

        void SendOutLatest()
        {
            timer += Time.deltaTime;

            if (timer >= interval)
            {
                if (Ready && Player.BasisAvatar != null)
                {
                    ScheduleCheck();
                    BasisNetworkAvatarCompressor.Compress(this, Player.BasisAvatar.Animator);
                    distanceJobHandle.Complete();
                    HandleResults();
                    SmallestDistanceToAnotherPlayer = distanceJob.smallestDistance[0];

                    // Calculate next interval and clamp it
                    UnClampedInterval = DefaultInterval * (BaseMultiplier + (SmallestDistanceToAnotherPlayer * IncreaseRate));
                    interval = math.clamp(UnClampedInterval, 0.005f, SlowestSendRate);

                    // Account for overshoot
                    timer -= interval;
                }
            }
        }
        public void HandleResults()
        {
            if (distanceJob.DistanceResults == null)
            {
                return;
            }
            if (MicrophoneRangeIndex == null)
            {
                return;
            }
            if (MicrophoneRangeIndex.Length != distanceJob.DistanceResults.Length)
            {
                return;
            }
            distanceJob.DistanceResults.CopyTo(MicrophoneRangeIndex);
            distanceJob.HearingResults.CopyTo(HearingIndex);
            distanceJob.AvatarResults.CopyTo(AvatarIndex);

            MicrophoneOutputCheck();
            Iteration();
        }
        /// <summary>
        /// how far we can hear locally
        /// </summary>
        public void Iteration()
        {
            for (int Index = 0; Index < IndexLength; Index++)
            {
                Recievers.BasisNetworkReceiver Rec = BasisNetworkManagement.ReceiverArray[Index];
                if (Rec.AudioReceiverModule.IsPlaying != HearingIndex[Index])
                {
                    if (HearingIndex[Index])
                    {
                        Rec.AudioReceiverModule.StartAudio();
                        Rec.RemotePlayer.OutOfRangeFromLocal = false;
                    }
                    else
                    {
                        Rec.AudioReceiverModule.StopAudio();
                        Rec.RemotePlayer.OutOfRangeFromLocal = true;
                    }
                }
                /*
                if (Rec.RemotePlayer.IsNotFallBack != AvatarIndex[Index])
                {
                    if (AvatarIndex[Index])
                    {
                        BasisLoadableBundle BasisLoadableBundle = BasisBundleConversionNetwork.ConvertNetworkBytesToBasisLoadableBundle(Rec.RemotePlayer.CACM.byteArray);

                        Rec.RemotePlayer.CreateAvatar(Rec.RemotePlayer.CACM.loadMode, BasisLoadableBundle);
                        Rec.RemotePlayer.IsNotFallBack = true;
                    }
                    else
                    {
                     //   BasisAvatarFactory.LoadLoadingAvatar(Rec.RemotePlayer, BasisAvatarFactory.LoadingAvatar.BasisLocalEncryptedBundle.LocalBundleFile);
                       // Rec.RemotePlayer.IsNotFallBack = false;
                    }
                }
                */
            }
        }
        /// <summary>
        ///lets the server know who can hear us.
        /// </summary>
        public void MicrophoneOutputCheck()
        {

            if (AreBoolArraysEqual(MicrophoneRangeIndex, LastMicrophoneRangeIndex) == false)
            {
                //BasisDebug.Log("Arrays where not equal!");
                Array.Copy(MicrophoneRangeIndex, LastMicrophoneRangeIndex, IndexLength);
                List<ushort> TalkingPoints = new List<ushort>(IndexLength);
                for (int Index = 0; Index < IndexLength; Index++)
                {
                    bool User = MicrophoneRangeIndex[Index];
                    if (User)
                    {
                        TalkingPoints.Add(HearingIndexToId[Index]);
                    }
                }
                if (TalkingPoints.Count != 0)
                {
                    HasReasonToSendAudio = true;
                }
                else
                {
                    HasReasonToSendAudio = false;
                }
                //even if we are not listening to anyone we still need to tell the server that!
                VoiceReceiversMessage VRM = new VoiceReceiversMessage
                {
                    users = TalkingPoints.ToArray()
                };
                NetDataWriter writer = new NetDataWriter();
                VRM.Serialize(writer);
                BasisNetworkManagement.LocalPlayerPeer.Send(writer, BasisNetworkCommons.AudioRecipients, DeliveryMethod.ReliableOrdered);
                BasisNetworkProfiler.AudioRecipientsMessageCounter.Sample(writer.Length);
            }
        }
        public static bool AreBoolArraysEqual(bool[] array1, bool[] array2)
        {
            // Check if both arrays are null
            if (array1 == null && array2 == null)
            {
                return true;
            }

            // Check if one of them is null
            if (array1 == null || array2 == null)
            {
                return false;
            }

            int Arraylength = array1.Length;
            // Check if lengths differ
            if (Arraylength != array2.Length)
            {
                return false;
            }

            // Compare values
            for (int Index = 0; Index < Arraylength; Index++)
            {
                if (array1[Index] != array2[Index])
                {
                    return false;
                }
            }

            return true;
        }
        public override void Initialize()
        {
            if (Ready == false)
            {
                IndexLength = -1;
                AudioTransmission.OnEnable(this);
                OnAvatarCalibrationLocal();
                if (HasEvents == false)
                {
                    Player.OnAvatarSwitchedFallBack += OnAvatarCalibrationLocal;
                    Player.OnAvatarSwitched += OnAvatarCalibrationLocal;
                    Player.OnAvatarSwitched += SendOutAvatarChange;
                    BasisLocalInputActions.AfterAvatarChanges += SendOutLatest;
                    HasEvents = true;
                }
                Ready = true;
            }
            else
            {
                BasisDebug.Log("Already Ready");
            }
        }
        public void ScheduleCheck()
        {
            distanceJob.AvatarDistance = SMModuleDistanceBasedReductions.AvatarRange;
            distanceJob.HearingDistance = SMModuleDistanceBasedReductions.HearingRange;
            distanceJob.VoiceDistance = SMModuleDistanceBasedReductions.MicrophoneRange;
            distanceJob.referencePosition = MouthBone.OutgoingWorldData.position;
            if (IndexLength != BasisNetworkManagement.ReceiverCount)
            {
                ResizeOrCreateArrayData(BasisNetworkManagement.ReceiverCount);
                LastMicrophoneRangeIndex = new bool[BasisNetworkManagement.ReceiverCount];
                MicrophoneRangeIndex = new bool[BasisNetworkManagement.ReceiverCount];
                HearingIndex = new bool[BasisNetworkManagement.ReceiverCount];
                AvatarIndex = new bool[BasisNetworkManagement.ReceiverCount];

                IndexLength = BasisNetworkManagement.ReceiverCount;
                HearingIndexToId = BasisNetworkManagement.RemotePlayers.Keys.ToArray();
            }
            for (int Index = 0; Index < BasisNetworkManagement.ReceiverCount; Index++)
            {
                targetPositions[Index] = BasisNetworkManagement.ReceiverArray[Index].MouthBone.OutgoingWorldData.position;
            }
            smallestDistance[0] = float.MaxValue;
            distanceJobHandle = distanceJob.Schedule(targetPositions.Length, 64);
        }
        public void ResizeOrCreateArrayData(int TotalUserCount)
        {
            if (distanceJobHandle.IsCompleted == false)
            {
                distanceJobHandle.Complete();
            }
            if (targetPositions.IsCreated)
            {
                targetPositions.Dispose();
            }
            if (distances.IsCreated)
            {
                distances.Dispose();
            }
            if (smallestDistance.IsCreated)
            {
                smallestDistance.Dispose();
            }
            if (DistanceResults.IsCreated)
            {
                DistanceResults.Dispose();
            }
            if (HearingResults.IsCreated)
            {
                HearingResults.Dispose();
            }
            if (AvatarResults.IsCreated)
            {
                AvatarResults.Dispose();
            }
            smallestDistance = new NativeArray<float>(1, Allocator.Persistent);
            smallestDistance[0] = float.MaxValue;
            targetPositions = new NativeArray<float3>(TotalUserCount, Allocator.Persistent);
            distances = new NativeArray<float>(TotalUserCount, Allocator.Persistent);
            DistanceResults = new NativeArray<bool>(TotalUserCount, Allocator.Persistent);

            HearingResults = new NativeArray<bool>(TotalUserCount, Allocator.Persistent);
            AvatarResults = new NativeArray<bool>(TotalUserCount, Allocator.Persistent);
            // Step 2: Find closest index in the next frame
            distanceJob.distances = distances;
            distanceJob.DistanceResults = DistanceResults;
            distanceJob.HearingResults = HearingResults;
            distanceJob.AvatarResults = AvatarResults;


            distanceJob.targetPositions = targetPositions;

            distanceJob.smallestDistance = smallestDistance;
        }
        public override void DeInitialize()
        {
            if (Ready)
            {
                AudioTransmission.OnDisable();
            }
            if (HasEvents)
            {
                Player.OnAvatarSwitchedFallBack -= OnAvatarCalibrationLocal;
                Player.OnAvatarSwitched -= OnAvatarCalibrationLocal;
                Player.OnAvatarSwitched -= SendOutAvatarChange;
                BasisLocalInputActions.AfterAvatarChanges -= SendOutLatest;
                if (targetPositions.IsCreated) targetPositions.Dispose();
                if (distances.IsCreated) distances.Dispose();
                if (smallestDistance.IsCreated)
                {
                    smallestDistance.Dispose();
                }
                if (DistanceResults.IsCreated)
                {
                    DistanceResults.Dispose();
                }
                if (HearingResults.IsCreated)
                {
                    HearingResults.Dispose();
                }
                if (AvatarResults.IsCreated)
                {
                    AvatarResults.Dispose();
                }
                HasEvents = false;
            }
        }
        public void SendOutAvatarChange()
        {
            NetDataWriter Writer = new NetDataWriter();
            ClientAvatarChangeMessage ClientAvatarChangeMessage = new ClientAvatarChangeMessage
            {
                byteArray = BasisBundleConversionNetwork.ConvertBasisLoadableBundleToBytes(Player.AvatarMetaData),
                loadMode = Player.AvatarLoadMode,
            };
            ClientAvatarChangeMessage.Serialize(Writer);
            BasisNetworkManagement.LocalPlayerPeer.Send(Writer, BasisNetworkCommons.AvatarChangeMessage, DeliveryMethod.ReliableOrdered);
            BasisNetworkProfiler.AvatarChangeMessageCounter.Sample(Writer.Length);
        }
        [BurstCompile]
        public struct CombinedDistanceAndClosestTransformJob : IJobParallelFor
        {
            public float VoiceDistance;
            public float HearingDistance;
            public float AvatarDistance;
            [ReadOnly]
            public float3 referencePosition;
            [ReadOnly]
            public NativeArray<float3> targetPositions;

            [WriteOnly]
            public NativeArray<float> distances;
            [WriteOnly]
            public NativeArray<bool> DistanceResults;
            [WriteOnly]
            public NativeArray<bool> HearingResults;
            [WriteOnly]
            public NativeArray<bool> AvatarResults;

            // Shared result for the smallest distance
            [NativeDisableParallelForRestriction]
            public NativeArray<float> smallestDistance;

            public void Execute(int index)
            {
                // Calculate distance
                Vector3 diff = targetPositions[index] - referencePosition;
                float sqrDistance = diff.sqrMagnitude;
                distances[index] = sqrDistance;

                // Determine boolean results
                DistanceResults[index] = sqrDistance < VoiceDistance;
                HearingResults[index] = sqrDistance < HearingDistance;
                AvatarResults[index] = sqrDistance < AvatarDistance;

                // Update the smallest distance (atomic operation to avoid race conditions)
                float currentSmallest = smallestDistance[0];
                if (sqrDistance < currentSmallest)
                {
                    smallestDistance[0] = sqrDistance;
                }
            }
        }
    }
}
