using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace BasisNetworkServer.BasisNetworking
{
    public static class BasisAvatarRequestMessages
    {
        public static void AvatarCloneRequestMessage(NetPacketReader Reader, NetPeer Peer)
        {
         ushort RemotePlayerID = Reader.GetUShort();
        }
        public static void AvatarCloneResponseMessage(NetPacketReader Reader, NetPeer Peer)
        {
          ushort EndUser =  Reader.GetUShort();
            string ApprivalID = Reader.GetString();
        }
    }
}
