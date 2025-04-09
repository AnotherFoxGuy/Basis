using Basis.Scripts.Addressable_Driver;
using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.Drivers;
using Basis.Scripts.TransformBinders.BoneControl;
using System;
using System.Threading;
using UnityEngine;
namespace Basis.Scripts.BasisSdk.Players
{
    public abstract class BasisPlayer : MonoBehaviour
    {
        public bool IsLocal { get; set; }
        public string DisplayName;
        public string UUID;
        public BasisAvatar BasisAvatar;
        public AddressableGenericResource AvatarAddressableGenericResource;
        public BasisLoadableBundle AvatarMetaData;
        public bool HasAvatarDriver;
        public event Action OnAvatarSwitched;
        public event Action OnAvatarSwitchedFallBack;
        public BasisProgressReport ProgressReportAvatarLoad = new BasisProgressReport();
        public const byte LoadModeNetworkDownloadable = 0;
        public const byte LoadModeLocal = 1;
        public const byte LoadModeError = 2;
        public bool FaceIsVisible;
        public BasisMeshRendererCheck FaceRenderer;
        public CancellationToken CurrentAvatarsCancellationToken;
        public byte AvatarLoadMode;//0 downloading 1 local

        public BasisProgressReport AvatarProgress = new BasisProgressReport();
        public CancellationToken CancellationToken;
        public BasisAvatarStrainJiggleDriver BasisAvatarStrainJiggleDriver;
        public Action<bool> AudioReceived;
        public BasisBoneControl Mouth;
        public bool HasJiggles = false;
        public void InitalizeIKCalibration(BasisAvatarDriver BasisAvatarDriver)
        {
            if (BasisAvatarDriver != null)
            {
                HasAvatarDriver = true;
            }
            else
            {
                BasisDebug.LogError("Mising CharacterIKCalibration");
                HasAvatarDriver = false;
            }
            try
            {
                HasJiggles = false;
                BasisAvatarStrainJiggleDriver = BasisHelpers.GetOrAddComponent<BasisAvatarStrainJiggleDriver>(this.gameObject);
                if (BasisAvatarStrainJiggleDriver != null)
                {
                    HasJiggles = BasisAvatarStrainJiggleDriver.OnCalibration();
                }
            }
            catch (Exception e)
            {
                BasisDebug.LogError($"{e.ToString()} {e.StackTrace}");
            }
        }
        public void UpdateFaceVisibility(bool State)
        {
            FaceIsVisible = State;
        }
        public void AvatarSwitchedFallBack()
        {
            OnAvatarSwitchedFallBack?.Invoke();
        }
        public void AvatarSwitched()
        {
            OnAvatarSwitched?.Invoke();
        }
    }
}
