using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Logging;
using Photon.SocketServer;
using PhotonHostRuntimeInterfaces;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace PhotonIntro
{
    public class UnityClient : ClientPeer
    {
        #region Variables
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public static Stopwatch ClientReactionTimer;
        public static Stopwatch OpponentReactionTimer;
        public Stopwatch FakeReactionTime;

        private CardType cardType;

        //private readonly char[] delimiterChars = { ':', '.' };
        //private readonly char delimiterChar = ',';

        public List<string> TimingData = new List<string>();
        public string Latency;
        public string ActualReactTime;
        public string ReactionTime;
        public string SnapLatency;
        public string csv;
        public string tmp;
        public string ActiveUser;

        public string PingFile = "C:\\Users\\ltete\\Documents\\PingData.csv";

        public int doonce;

        public bool canDeal;
        public bool Insert;
        public bool Positioned;

        public string PingMs;
            
      
        #endregion

        public UnityClient(InitRequest initRequest)
            : base(initRequest)
        {
            Log.Debug("Connection received from: " + initRequest.RemoteIP);
            GetIpAddress();
            Game.Instance.PeerConnected(this);
            if (RemoteIP == Game.Client1Ip)
            {
                Game.Peer1.Add(this);
                EventData PlayerInfo = new EventData(4)
                {
                    Parameters = new Dictionary<byte, object> { { 1, "Player 1" } }
                };
                EventData.SendTo(PlayerInfo, Game.Peer1, new SendParameters());
                Log.Debug("Sent to client: " + PlayerInfo.Parameters[1]);
            }
            else
            {
                Game.Peer2.Add(this);
                EventData PlayerInfo = new EventData(4)
                {
                    Parameters = new Dictionary<byte, object> { { 1, "Player 2" } }
                };
                EventData.SendTo(PlayerInfo, Game.Peer2, new SendParameters());
                EventData StartGame = new EventData(1)
                {
                    Parameters = new Dictionary<byte, object> { { 1, "Start Game" } }
                };
                EventData.SendTo(StartGame, Game.Connections, new SendParameters());
            }
                
        }

        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            Log.Debug("Client Disconnected");
            Game.CardIndex = 0;
            Game.CardCompare.Initialize();
            Game.Snappable = false;
            Game.TurnCounter = 0;
            Game.cardDealt = false;
            Game.sentCard = String.Empty;
            Game.Instance.PeerDisconnected(this);
        }

        ///<summary>
        ///Answer to Operation Requests sent by clients
        ///</summary>
        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            OperationResponse dealCard = new OperationResponse(1)
            {
                Parameters = new Dictionary<byte, object>()
            };
            OperationResponse snap = new OperationResponse(2)
            {
                Parameters = new Dictionary<byte, object>()
            };

            switch (operationRequest.OperationCode)
            {
                case 0:             // Ping
                    Log.Debug("Client Awake Ping");                 // to avoid timeout disconnection
                    break;
                case 1:             // Client System time
                    Log.Debug(String.Format("Client {0} system time = {1} ---- Server system time = {2}",
                        ConnectionId, operationRequest.Parameters[1], DateTime.Now.TimeOfDay));
                    #region time offsets
                    //if(ConnectionId % 2 == 0)
                    //{
                    //    Game.systemtime = DateTime.Now.TimeOfDay;
                    //    Game.timestamp = (string)operationRequest.Parameters[1];
                    //    Game.HrMinSecMs = Game.timestamp.Split(delimiterChars);
                    //    Game.trycount++;
                    //}
                    //else
                    //{
                    //    Game.systemtime2 = DateTime.Now.TimeOfDay;
                    //    Game.timestamp2 = (string)operationRequest.Parameters[1];                        
                    //    Game.HrMinSecMs2 = Game.timestamp2.Split(delimiterChars);
                    //    Game.trycount++;
                    //}
                    //if (Game.trycount > 1 && Game.trycount % 2 == 0)
                    //{
                    //    Game.OffsetSystem = Game.systemtime2 - Game.systemtime;
                    //    Game.OffsetHeadset = TimestampOffset();
                    //    Log.Debug(String.Format("Headset Offset = {0}s", Game.OffsetHeadset));
                    //    Log.Debug(String.Format("System Offset = {0}s", Game.OffsetSystem));
                    //} 
                    #endregion
                    break;
                case 2:             // Start                  
                    Log.Debug("Received Start Request from: " + operationRequest.Parameters[1]);
                    InitialiseGame();
                    break;
                case 3:             // Deal
                    Game.compare = false;
                    Log.Debug(String.Format("Received: Deal Request from ID {0}", ConnectionId));
                    ActualReactTime = String.Empty;
                    doonce = 0;
                    Game.SnapCounter = 0;
                    Game.Snapped = false;
                    snap.Parameters.Clear();
                    dealCard.Parameters.Clear();
                    Game.Snappable = false;
                    Game.RecordPeer1 = false;
                    Game.RecordPeer2 = false;

                    // generate random card and send it to client
                    cardType = RandomCardValue<CardType>();
                    dealCard.Parameters.Add(1, cardType.ToString());
                    SendOperationResponse(dealCard, sendParameters);
                    Game.sentCard = cardType.ToString();
                    Game.cardDealt = true;
                    // send dealt card information to opponent
                    EventData dealEvent = new EventData(2)
                    {
                        Parameters = new Dictionary<byte, object> { { 1, Game.sentCard } }
                    };
                    //if (ConnectionId % 2 == 0)      // if connection id = 0,2,4... send to Peer2 (id 1,3,5...)  
                    if (RemoteIP == Game.Client1Ip)
                    {
                        EventData.SendTo(dealEvent, Game.Peer2, sendParameters);
                    }
                    else
                    {
                        EventData.SendTo(dealEvent, Game.Peer1, sendParameters);
                    }
                    Turns();                // switch turn priorities (can/can't deal)
                    CompareCards();         // compare cards and determine if can snap            
                    break;
                case 4:             // Snap
                    Log.Debug("Received: Snap Request");
                    EventData WinData = new EventData(3)
                    {
                        Parameters = new Dictionary<byte, object>()
                    };
                    // if cards are equal (snappable = true) send win+timing data to client 
                    if (Game.Snappable)                 // condition: snapped when cards were equal and won
                    {                        
                        ClientReactionTimer.Stop();                      
                        snap.Parameters.Add(1, "Win");                                          // win data
                        snap.Parameters.Add(2, ClientReactionTimer.ElapsedMilliseconds.ToString());           // timing data 
                        Game.won = true;
                        ClientReactionTimer.Reset();
                        Game.Snapped = true;
                        Game.Snappable = false;
                        Game.compare = true;
                    }
                    else
                    {
                        if (Game.Snapped == true)        // condition: snapped when cards were equal but opponent beat you
                        {
                            if (OpponentReactionTimer.IsRunning)
                            {
                                OpponentReactionTimer.Stop();
                                snap.Parameters.Add(2, OpponentReactionTimer.ElapsedMilliseconds.ToString());          // timing data
                                OpponentReactionTimer.Reset();
                            }
                            Game.won = false;
                            snap.Parameters.Add(1, "Lose");                                             // lost data
                            Game.Snapped = false;
                        }
                        else
                        {
                            snap.Parameters.Add(1, "Wrong");                                            // wrong snap data
                        }
                    }
                    SendOperationResponse(snap, sendParameters);                                        // send data to client
                    Game.Snappable = false;
                    break;
                case 5:             // Reaction Time & Latency Data         
                    Log.Debug("Received ALL DATA");
                    ActualReactTime = operationRequest.Parameters[1].ToString();
                    SnapLatency = operationRequest.Parameters[2].ToString();
                    ReactionTime = operationRequest.Parameters[3].ToString();
                    CheckRecord();                      // check whether record has been beaten
                    CreateDataArray();                  // write data to file
                    EventData GameStats = new EventData(5);
                    GameStats.Parameters = new Dictionary<byte, object>
                    {
                        { 1,  ActualReactTime },
                        { 2,  SnapLatency },
                        { 3,  ReactionTime }
                    };
                    //if(ConnectionId % 2 == 0)
                    if (RemoteIP == Game.Client1Ip)
                    {
                        EventData.SendTo(GameStats, Game.Peer2, new SendParameters());
                    }
                    else
                    {
                        EventData.SendTo(GameStats, Game.Peer1, new SendParameters());
                    }
                    break;
                case 6:
                    //if(ConnectionId % 2 == 0)
                    if (RemoteIP == Game.Client1Ip)
                    {
                        Game.Peer1UserName = operationRequest.Parameters[1].ToString();
                        if (Game.RecordPeer1)
                        {
                            ActiveUser = Game.Peer1UserName;
                            Log.Debug("Record From: "+Game.Peer1UserName);
                            UpdateLeaderboard();
                        }
                    }
                    else
                    {
                        Game.Peer2UserName = operationRequest.Parameters[1].ToString();
                        if (Game.RecordPeer2)
                        {
                            ActiveUser = Game.Peer2UserName;
                            Log.Debug("Record From: " + Game.Peer2UserName);
                            UpdateLeaderboard();
                        }
                    }                    
                    break;
                case 7:
                    PingMs = operationRequest.Parameters[1].ToString() + "\n";
                    File.AppendAllText(PingFile, PingMs);
                    OperationResponse ping = new OperationResponse(3)
                    {
                        Parameters = new Dictionary<byte, object>()
                    };
                    SendOperationResponse(ping, sendParameters);
                    Log.Debug("ping");
                    break;
                default:
                    Log.Debug("Unknown request received");
                    break;
            }
        }

        ///<summary>
        ///Compare cards, determine whether game is snappable and start timer for reaction time
        ///</summary>
        public void CompareCards()
        {
            if (!Game.compare)
            {
                Game.CardCompare[Game.CardIndex] = Game.sentCard;
                Game.CardIndex++;
                if (Game.CardIndex == 2)
                {
                    Game.CardIndex = 0;
                }
                if (Game.CardCompare[0] == Game.CardCompare[1])
                {
                    ClientReactionTimer = new Stopwatch();
                    ClientReactionTimer.Start();
                    OpponentReactionTimer = new Stopwatch();
                    OpponentReactionTimer.Start();
                    Game.Snappable = true;
                    EventData CanSnap = new EventData(8);
                    EventData.SendTo(CanSnap, Game.Connections, new SendParameters());
                    Log.Debug("Snappable: " + Game.Snappable);
                }
            }            
        }

        ///<summary>
        ///Initialise game dealing priorities (can/can't deal)
        ///</summary>
        public void InitialiseGame()
        {
            Game.InitRequestCounter++;
            Log.Debug("Initialising Game");
            EventData dealData = new EventData(0);
            //if(ConnectionId % 2 == 0)
            if (RemoteIP == Game.Client1Ip)
            {
                canDeal = true;
            }
            else
            {
                canDeal = false;
            }
            dealData.Parameters = new Dictionary<byte, object> { { 1, canDeal.ToString() } };
            SendEvent(dealData, new SendParameters());

            if (Game.Connections.Count > 1 && Game.Connections.Count % 2 == 0 && Game.InitRequestCounter > 1 && Game.InitRequestCounter % 2 == 0)
            {
                EventData timeRequest = new EventData(1);
                EventData.SendTo(timeRequest, Game.Connections, new SendParameters());
            }
        }

        ///<summary>
        ///Select a random card
        ///</summary>
        static T RandomCardValue<T>()
        {
            var v = Enum.GetValues(typeof(T));
            return (T)v.GetValue(new Random().Next(v.Length));
        }

        ///<summary>
        ///<para>Implements turn priority switching by using a counter that is incremented on every call.</para>
        ///<para>Then, when TurnCounter is even, Client1 can't deal and Client2 can, and viceversa.</para>
        ///</summary>
        public void Turns()
        {
            Log.Debug("Next Turn");
            EventData dealData = new EventData(0)
            {
                Parameters = new Dictionary<byte, object> { { 1, "" } }
            };
            if (Game.TurnCounter % 2 == 0)
            {
                dealData.Parameters[1] = "True";
                EventData.SendTo(dealData, Game.Peer2, new SendParameters());
                dealData.Parameters[1] = "False";
                EventData.SendTo(dealData, Game.Peer1, new SendParameters());
            }
            else
            {
                dealData.Parameters[1] = "False";
                EventData.SendTo(dealData, Game.Peer2, new SendParameters());
                dealData.Parameters[1] = "True";
                EventData.SendTo(dealData, Game.Peer1, new SendParameters());
            }

            Log.Debug("Turn counter: " + Game.TurnCounter);
            Game.TurnCounter++;
        }

        ///<summary>
        ///Create or update csv file with reaction time and latency data
        ///</summary>
        public void CreateDataArray()
        {
            TimingData = new List<string>();
            TimingData.Insert(0, "," + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + ",");
            TimingData.Insert(1, ActualReactTime + ",");
            TimingData.Insert(2, SnapLatency + "\n");

            csv = String.Join("", TimingData.Select(x => x.ToString()).ToArray());

            //if(ConnectionId % 2 == 0)
            if (RemoteIP == Game.Client1Ip)
            {
                File.AppendAllText(Game.ActiveSession1, csv);
            }
            else
            {
                File.AppendAllText(Game.ActiveSession2, csv);
            }
        }
        
        public void CheckRecord()
        {
            Log.Debug("REACTION TIME:"+ActualReactTime);
            Log.Debug(Game.RecordPeer1);
            Log.Debug(Game.RecordPeer2);
            
            if (!Game.RecordPeer1 && !Game.RecordPeer2 && Game.won)
            {
                // Check if file exists - if not create it
                if (File.Exists(Game.SavedRecord))
                {
                    for (int i = 0; i < File.ReadAllLines(Game.SavedRecord).Length; i++)
                    {
                        string temp = File.ReadAllLines(Game.SavedRecord)[i];
                        if (temp.Contains(","))
                        {
                            string[] temp2 = temp.Split(',');
                            Game.RecordsArray[i] = Convert.ToInt32(temp2[0]);
                            Game.NamesArray[i] = temp2[1];
                        }
                        else
                        {
                            Game.RecordsArray[i] = Convert.ToInt32(File.ReadAllLines(Game.SavedRecord)[i]);
                        }

                    }
                    Game.ReactionTimeRecord = Game.RecordsArray[0];
                    Log.Debug(Game.ReactionTimeRecord);
                }
                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Game.RecordsArray[i] = 0;
                    }
                    Game.ReactionTimeRecord = 0;
                }
                if (((Convert.ToInt32(ActualReactTime) < Game.RecordsArray[9]) || Game.RecordsArray[9] == 0) && Convert.ToInt32(ActualReactTime) != 0)
                {
                    for (int i = 0; i < Game.RecordsArray.Length; i++)
                    {
                        if (((Convert.ToInt32(ActualReactTime) < Game.RecordsArray[i]) || Game.RecordsArray[i] == 0) && !Positioned)
                        {
                            Log.Debug("New Top 10 Score! " + ActualReactTime);
                            Positioned = true;
                        }
                        if (Positioned)
                        {
                            Game.LeaderboardPosition = i + 1;
                            break;
                        }                        
                    }
                    Positioned = false;
                    if(Game.LeaderboardPosition == 1)
                    {
                        Game.ReactionTimeRecord = Convert.ToInt32(ActualReactTime);
                    }
                    Game.ReactionTimeTop10 = Convert.ToInt32(ActualReactTime);
                    Log.Debug("Position: "+ Game.LeaderboardPosition);
                    EventData NewTop10 = new EventData(7)
                    {
                        Parameters = new Dictionary<byte, object> { { 1, Game.LeaderboardPosition } }
                    };
                    //if(ConnectionId % 2 == 0)
                    if (RemoteIP == Game.Client1Ip)
                    {
                        if (!Game.RecordPeer2)
                        {
                            Game.RecordPeer1 = true;
                            EventData.SendTo(NewTop10, Game.Peer1, new SendParameters());
                        }
                    }
                    else
                    {
                        if (!Game.RecordPeer1)
                        {
                            Game.RecordPeer2 = true;
                            EventData.SendTo(NewTop10, Game.Peer2, new SendParameters());
                        }
                    }
                }
            }
        }

        ///<summary>
        ///Update leaderboard and rearrange
        ///</summary>
        public void UpdateLeaderboard()
        {            
            for (int i = 0; i < Game.RecordsArray.Length; i++)
            {
                if (i < Game.LeaderboardPosition - 1 || i == 0)
                {
                    Log.Debug("done");
                    Game.Names[i] = Game.NamesArray[i];
                    Game.Records[i] = Game.RecordsArray[i].ToString() + "," + Game.Names[i];
                }
                else
                {
                    Game.Names[i] = Game.NamesArray[i - 1];
                    Game.Records[i] = Game.RecordsArray[i - 1].ToString() + "," + Game.Names[i];
                }                
            }
            Game.RecordsArray[Game.LeaderboardPosition - 1] = Game.ReactionTimeTop10;
            Game.Records[Game.LeaderboardPosition - 1] = Game.RecordsArray[Game.LeaderboardPosition - 1].ToString() + "," + ActiveUser;
            List<string> RecordsList = Game.Records.ToList();
            File.WriteAllLines(Game.SavedRecord, RecordsList);
            Game.LeaderboardPosition = 0;
            Game.RecordPeer1 = false;
            Game.RecordPeer2 = false;
        }

        public void GetIpAddress()
        {
            if (Game.FirstConnection == 0)
            {
                Game.Client1Ip = RemoteIP;
                Game.FirstConnection++;
            }
            else
            {
                if (RemoteIP != Game.Client1Ip)
                {
                    Game.Client2Ip = RemoteIP;
                }
            }
            Log.Debug("Client 1 IP: " + Game.Client1Ip);
            Log.Debug("Client 2 IP: " + Game.Client2Ip);
        }

   }
}
