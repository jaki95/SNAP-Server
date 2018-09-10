using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Logging;
using Photon.SocketServer;

namespace PhotonIntro
{
    public class Game
    {
        #region Variables
        public static int FirstConnection;
        public static int FirstConnectionPeer;
        public static string Client1Ip;
        public static string Client2Ip;
        public static Game Instance;
        public static List<PeerBase> Connections;
        public static List<PeerBase> Peer1;
        public static string Peer1UserName;
        public static List<PeerBase> Peer2;
        public static string Peer2UserName;
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public static string ActiveSession1 = "C:\\Users\\ltete\\Documents\\Data_Client1.csv";
        public static string FinishedSession1 = "C:\\Users\\ltete\\Documents\\Data_Client1_History.csv";
        public static string ActiveSession2 = "C:\\Users\\ltete\\Documents\\Data_Client2.csv";
        public static string FinishedSession2 = "C:\\Users\\ltete\\Documents\\Data_Client2_History.csv";
        public static string SavedRecord = "C:\\Users\\ltete\\Documents\\website\\Reaction_Time_Record.csv";
        public static string SavedRecordCopy = "C:\\Users\\ltete\\Documents\\Reaction_Time_Record_Copy.csv";
        public static string sentCard;
        public static string[] CardCompare = new string[2];
        public static string[] Records = new string[10];
        public static string[] Names = new string[10];

        public static int counter;
        public static int StartRequests;
        public static int TurnCounter;
        public static int CardIndex;
        public static int InitRequestCounter;
        public static int ReactionTimeRecord;
        public static int ReactionTimeTop10;
        public static int LeaderboardPosition;
        public static int[] RecordsArray = new int[10];
        public static string[] NamesArray = new string[10];
        public static int SnapCounter;

        public static bool cardDealt;       
        public static bool Snappable;
        public static bool Snapped;
        public static bool won;
        public static bool RecordPeer1;
        public static bool RecordPeer2;
        public static bool compare;
        #endregion

        public void Startup()
        {
            Peer1 = new List<PeerBase>();
            Peer2 = new List<PeerBase>();
            Connections = new List<PeerBase>();            
        }

        public void Shutdown()
        {
            foreach (PeerBase peer in Connections)
            {
                peer.Disconnect();
            }
        }

        public void PeerConnected(PeerBase peer)
        {
            lock (Connections)
            {
                Connections.Add(peer);
            }

        }

        public void PeerDisconnected(PeerBase peer)
        {
            counter = 0;
            StartRequests = 0;

            if (peer.ConnectionId % 2 == 0)
            {
                if (!File.Exists(FinishedSession1))
                {
                    string csv = ",Index,Reaction Time,Latency\n";
                    File.WriteAllText(FinishedSession1, csv);
                }              
                using (Stream input = File.OpenRead(ActiveSession1))
                using (Stream output = new FileStream(FinishedSession1, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output); 
                }
                File.WriteAllText(ActiveSession1, String.Empty);
            }
            else
            {
                if (!File.Exists(FinishedSession2))
                {
                    string csv = ",Index,Reaction Time,Latency\n";
                    File.WriteAllText(FinishedSession2, csv);
                }               
                using (Stream input = File.OpenRead(ActiveSession2))
                using (Stream output = new FileStream(FinishedSession2, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
                File.WriteAllText(ActiveSession2, String.Empty);
            }

            if (File.Exists(SavedRecord))
            {
                if (File.Exists(SavedRecordCopy))
                {
                    File.Delete(SavedRecordCopy);
                }
                File.Copy(SavedRecord, SavedRecordCopy);
            }

            lock (Connections)
            {
                Connections.Remove(peer);
            }
        }
    }
}
