using Basis.Scripts.Drivers;
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
        public Transform BasisAvatarTransform;
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


        public BasisProgressReport AvatarProgress = new BasisProgressReport();
        public CancellationToken CancellationToken;
        public Action<bool> AudioReceived;
        public bool HasJiggles = false;
        public delegate void SimulationHandler();
        public SimulationHandler OnPreSimulateBones;

        public bool IsConsideredFallBackAvatar = true;
        public byte AvatarLoadMode;//0 downloading 1 local
        [HideInInspector]
        public BasisLoadableBundle AvatarMetaData;

        [SerializeField]
        public BasisAvatarStrainJiggleDriver BasisAvatarStrainJiggleDriver = new BasisAvatarStrainJiggleDriver();
        [SerializeField]
        public BasisFacialBlinkDriver FacialBlinkDriver = new BasisFacialBlinkDriver();
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
            HasJiggles = false;
            try
            {
                HasJiggles = BasisAvatarStrainJiggleDriver.Initalize(this);
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
