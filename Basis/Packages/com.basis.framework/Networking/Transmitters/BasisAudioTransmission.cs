using UnityEngine;
using LiteNetLib;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Profiler;
using static SerializableBasis;
using LiteNetLib.Utils;
using Basis.Network.Core;
using OpusSharp.Core;

namespace Basis.Scripts.Networking.Transmitters
{
    [System.Serializable]
    public class BasisAudioTransmission
    {
        public OpusEncoder encoder;
        public BasisNetworkPlayer NetworkedPlayer;
        public BasisLocalPlayer Local;
        public MicrophoneRecorder Recorder;

        public bool IsInitalized = false;
        public AudioSegmentDataMessage AudioSegmentData = new AudioSegmentDataMessage();
        public AudioSegmentDataMessage audioSilentSegmentData = new AudioSegmentDataMessage();
        public bool HasEvents = false;
        public void OnEnable(BasisNetworkPlayer networkedPlayer)
        {
            if (!IsInitalized)
            {
                // Assign the networked player and base network send functionality
                NetworkedPlayer = networkedPlayer;


                // Initialize the Opus encoder with the retrieved settings
                encoder = new OpusEncoder(BasisOpusSettings.SampleFreqToInt(), BasisOpusSettings.NumChannels, BasisOpusSettings.OpusApplication);
                // Cast the networked player to a local player to access the microphone recorder
                Local = (BasisLocalPlayer)networkedPlayer.Player;
                Recorder = Local.MicrophoneRecorder;

                // If there are no events hooked up yet, attach them
                if (!HasEvents)
                {
                    if (Recorder != null)
                    {
                        // Hook up the event handlers
                        MicrophoneRecorder.OnHasAudio += OnAudioReady;
                        MicrophoneRecorder.OnHasSilence += SendSilenceOverNetwork;
                        HasEvents = true;
                        // Ensure the output buffer is properly initialized and matches the packet size
                        if (AudioSegmentData.buffer == null || Recorder.PacketSize != AudioSegmentData.buffer.Length)
                        {
                            AudioSegmentData.buffer = new byte[Recorder.PacketSize];
                        }
                    }
                }

                IsInitalized = true;
            }
        }
        public void OnDisable()
        {
            if (HasEvents)
            {
                MicrophoneRecorder.OnHasAudio -= OnAudioReady;
                MicrophoneRecorder.OnHasSilence -= SendSilenceOverNetwork;
                HasEvents = false;
            }
            if (Recorder != null)
            {
                GameObject.Destroy(Recorder.gameObject);
            }
            encoder.Dispose();
            encoder = null;
        }
        public void OnAudioReady()
        {
            if (NetworkedPlayer.HasReasonToSendAudio)
            {
                // UnityEngine.BasisDebug.Log("Sending out Audio");
                if (Recorder.PacketSize != AudioSegmentData.buffer.Length)
                {
                    AudioSegmentData.buffer = new byte[Recorder.PacketSize];
                }
                // Encode the audio data from the microphone recorder's buffer
                AudioSegmentData.LengthUsed = encoder.Encode(Recorder.processBufferArray,960, AudioSegmentData.buffer, AudioSegmentData.buffer.Length);
                NetDataWriter writer = new NetDataWriter();
                AudioSegmentData.Serialize(writer);
                BasisNetworkProfiler.AudioSegmentDataMessageCounter.Sample(AudioSegmentData.LengthUsed);
                BasisNetworkManagement.LocalPlayerPeer.Send(writer, BasisNetworkCommons.VoiceChannel, DeliveryMethod.Sequenced);
                Local.AudioReceived?.Invoke(true);
            }
            else
            {
                //  UnityEngine.BasisDebug.Log("Rejecting out going Audio");
            }
        }
        private void SendSilenceOverNetwork()
        {
            if (NetworkedPlayer.HasReasonToSendAudio)
            {
                NetDataWriter writer = new NetDataWriter();
                audioSilentSegmentData.LengthUsed = 0;
                audioSilentSegmentData.Serialize(writer);
                BasisNetworkProfiler.AudioSegmentDataMessageCounter.Sample(writer.Length);
                BasisNetworkManagement.LocalPlayerPeer.Send(writer, BasisNetworkCommons.VoiceChannel, DeliveryMethod.Sequenced);
                Local.AudioReceived?.Invoke(false);
            }
        }
    }
}
