﻿using System;
using System.Data.SqlClient;
using CLGameServer.Client;
using CLFramework;
namespace CLGameServer
{
    public partial class PlayerMgr
    {      
        #region Load tickets
        void LoadTicket(WorldMgr.character c)
        {
            //We wrap our function inside a catcher
            try
            {
                //First we get our data from the database.
                DB ms = new DB("SELECT * FROM character_tickets WHERE owner ='" + c.Information.CharacterID + "' AND active='1'");
                //Check if we have a active ticket
                int CheckActive = ms.Count();

                //Now if we have one active we continue
                if (CheckActive == 1)
                {
                    //Open new sql data reader
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While sql data reader is reading
                        while (reader.Read())
                        {
                            //Set our ticket to active so they cannot use double premium tickets
                            Character.Premium.Active = true;
                            //We can use Character.Information.CharacterID but on safe side we read from db to make sure.
                            Character.Premium.OwnerID = reader.GetInt32(1);
                            //Get the id information to calculate the % gaining and type etc etc..
                            Character.Premium.TicketItemID = reader.GetInt32(2);
                            //Get unique id (incase we need it).
                            Character.Premium.TicketID = reader.GetInt32(0);
                            //Get the start time of the ticket
                            Character.Premium.StartTime = reader.GetDateTime(3);
                        }
                    }
                    //Close our data reader
                    ms.Close();
                    //Calculate remaining time left.
                    TimeSpan Timecheck = Convert.ToDateTime(Character.Premium.StartTime) - DateTime.Now;
                    double TimeRemaining = Timecheck.TotalMinutes;
                    //Finally we send our packet to the user (Icon).
                    client.Send(Packet.PremiumTicketData(Character.Premium.TicketItemID, Convert.ToInt32(TimeRemaining)));
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Grab pet loading
        void LoadGrabPet()
        {
            //Wrap our function inside a catcher
            try
            {
                //Query check
                DB ms = new DB("SELECT * FROM pets WHERE playerid='" + Character.Information.CharacterID + "' AND pet_active='1'");
                //Get active pet count
                int checkactive = ms.Count();
                //If the player has an active grabpet
                if (checkactive > 0)
                {
                    //Set new pet object
                    WorldMgr.pet_obj o = new WorldMgr.pet_obj();
                    //Create new data reader for DB
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While the sql data reader is reading
                        while (reader.Read())
                        {
                            //Get pet location inside the player inventory
                            string slot = reader.GetString(12);
                            //Check our slot inside the database
                            int slotcheck = Convert.ToInt32(DB.GetData("SELECT * FROM char_items WHERE itemnumber='" + slot + "' AND owner='" + Character.Information.CharacterID + "' AND storagetype='0'", "slot"));
                            //Set slot item information (item).
                            ObjData.slotItem item = GetItem((uint)Character.Information.CharacterID, Convert.ToByte(slotcheck), 0);
                            //Set model information of the pet
                            int model = ObjData.item_database.GetItem(ObjData.Manager.ItemBase[item.ID].ObjectName);
                            //Set id for the pet (First database value is always unique).
                            Character.Grabpet.Grabpetid = item.dbID;
                            //Set unique id
                            o.UniqueID = Character.Grabpet.Grabpetid;
                            //Pet object model
                            o.Model = model;
                            //Spawning location of the pet
                            o.x = Character.Position.x + Rnd.Next(1, 3);
                            o.z = Character.Position.z;
                            o.y = Character.Position.y + Rnd.Next(1, 3);
                            o.xSec = Character.Position.xSec;
                            o.ySec = Character.Position.ySec;
                            //Owner id information
                            o.OwnerID = Character.Information.CharacterID;
                            //Owner name information
                            o.OwnerName = Character.Information.Name;
                            //Set walking state
                            o.Walking = Character.Position.Walking;
                            //Set petname
                            o.Petname = reader.GetString(3);
                            //Set our switch case
                            o.Named = 2;
                            //Set speed of pet (Need to check speed on official).
                            o.Run = Character.Speed.RunSpeed - 3;
                            o.Walk = Character.Speed.WalkSpeed - 3;
                            o.Zerk = Character.Speed.BerserkSpeed - 3;
                            //Set grabpet as active so there cant be double spawns
                            Character.Grabpet.Active = true;
                            //Set object information to true
                            o.Information = true;
                            //Spawn the pet
                            Helpers.Manager.HelperObject.Add(o);
                            //Set global information for the pet
                            Character.Grabpet.Details = o;
                            //Send the visual packet for details of the pet management
                            client.Send(Packet.Pet_Information_grab(o, Convert.ToByte(slotcheck)));
                            //Spawn
                            o.SpawnMe();
                            //Update state into database
                            DB.query("UPDATE pets SET pet_active='1' WHERE pet_unique='" + Character.Grabpet.Grabpetid + "' AND playerid='" + Character.Information.CharacterID + "'");
                        }
                        //Close sql reader
                        ms.Close();
                    }
                    //Set state
                    Character.Grabpet.Active = true;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Transport
        void LoadTransport()
        {
            //Wrap our function inside a catcher
            try
            {
                //If the player has an active transport
                if (Character.Transport.Right)
                {
                    //Set pet object information
                    WorldMgr.pet_obj o = Character.Transport.Horse;
                    //Set bools for transport
                    Character.Transport.Spawned = true;
                    Character.Transport.Horse.Information = true;
                    //Send packet for detailed transport information
                    client.Send(Packet.Pet_Information(o.UniqueID, o.Model, o.Hp, Character.Information.CharacterID, o));
                    //Send player visual onto horse
                    Send(Packet.Player_UpToHorse(Character.Information.UniqueID, true, o.UniqueID));
                    //Set speed state
                    Character.Transport.Horse.Speedsend();
                    Character.Transport.Horse.statussend();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Message conv
        private string MessageToMessagelong(string asciiString)
        {
            string hex = "";
            foreach (char c in asciiString)
            {
                int tmp = c;
                hex += String.Format("{0:x2}", (uint)System.Convert.ToUInt32(tmp.ToString()));
            }
            return hex;
        }
        #endregion
        #region Job Data
        void LoadJobData()
        {
            //Wrap our function inside a catcher
            try
            {
                //Get jobtype information from database
                int jobtype = Convert.ToInt32(DB.GetData("SELECT * FROM users WHERE id='" + Player.AccountName + "'", "jobtype"));
                //If we have a job so not 0 value
                if (jobtype > 0)
                {
                    //Create our query to get all job information
                    DB ms = new DB("SELECT * FROM character_jobs WHERE character_name='" + Character.Information.Name + "'");
                    //Open new sql data reader
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While sql data reader is reading
                        while (reader.Read())
                        {
                            //Set global job name
                            Character.Job.Jobname = reader.GetString(2);
                            //Set global job type
                            Character.Job.type = reader.GetByte(3);
                            //Set global job exp
                            Character.Job.exp = reader.GetInt32(4);
                            //Set global job rank
                            Character.Job.rank = reader.GetByte(5);
                            //Set global job state
                            Character.Job.state = reader.GetInt32(6);
                            //Set global job level
                            Character.Job.level = reader.GetByte(7);
                        }
                    }
                    //Close our sql data reader.
                    ms.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Friend list
        void GetFriendsList()
        {
            //Wrap our function inside a catcher
            try
            {
                //Set new sql query to get friend information
                DB ms = new DB("SELECT * FROM friends WHERE owner='" + Character.Information.CharacterID + "'");
                //Count our friends
                int count = ms.Count();
                //If we have a friend in the list
                if (count > 0)
                {
                    //Send our packet
                    client.Send(Packet.SendFriendList(Convert.ToByte(count), Character));
                    //Open new sql data reader
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While our sql data reader is reading
                        while (reader.Read())
                        {
                            //Get player id information of friend
                            int getid = reader.GetInt32(2);
                            //Get detailed information for our friend
                            PlayerMgr sys = Helpers.GetInformation.GetPlayerid(getid);
                            //If the character is online
                            if (sys != null)
                            {
                                //We send online state change packet
                                sys.client.Send(Packet.FriendData(Character.Information.CharacterID, 4, Character.Information.Name, Character, false));
                            }
                        }
                    }
                    //Close our sql reader
                    ms.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Guild
        public static bool CheckGuildMemberAdd(WorldMgr.character c)
        {
            foreach (int member in c.Network.Guild.Members)
            {
                if (member == c.Information.CharacterID)
                    return true;
            }
            return false;
        }
        void GetGuildData()
        {
            //Wrap our function inside a catcher
            try
            {
                //If the player is in a guild
                if (Character.Network.Guild.Guildid > 0)
                {
                    if(!CheckGuildMemberAdd(Character))
                    {
                        Character.Network.Guild.Members.Add(Character.Information.CharacterID);
                        Character.Network.Guild.MembersClient.Add(client);
                    }
                    //Send data begin opcode
                    client.Send(Packet.SendGuildStart());
                    //Send date
                    client.Send(Packet.SendGuildInfo(Character.Network.Guild));
                    //Send end data
                    client.Send(Packet.SendGuildEnd());
                    //Send detail information
                    Send(Packet.SendGuildInfo2(Character));
                    //Update to all guild members (online state).
                    if (Character.Network.Guild.Guildid != 0)
                    {
                        Character.Information.Online = 1;
                        //Send packets to network and spawned players
                        foreach (int member in Character.Network.Guild.Members)
                        {
                            //Make sure the member is there
                            if (member != 0)
                            {
                                //We dont send this info to the invited user.
                                if (member != Character.Information.CharacterID)
                                {
                                    //If the user is not the newly invited member get player info
                                    PlayerMgr tomember = Helpers.GetInformation.GetPlayerMainid(member);
                                    //Send guild update packet
                                    if (tomember != null)
                                    {
                                        tomember.client.Send(Packet.GuildUpdate(Character, 6, Character.Information.CharacterID, 0,0));
                                    }
                                }
                            }
                        }
                    }
                }
                //Get union data
                LoadUnions();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Load unions
        public void LoadUnions()
        {
            try
            {
                //First clear out the guild union info (will clean this later).
                if (Character.Network.Guild.Unions != null)
                {
                    Character.Network.Guild.Unions = null;
                    Character.Network.Guild.UnionMembers = null;
                    Character.Network.Guild.UnionLeader = 0;
                    Character.Network.Guild.UniqueUnion = 0;
                }
                //Then we query the row guildid
                int my_union = Convert.ToInt32(DB.GetData("SELECT union_unique_id FROM guild_unions WHERE union_guildid='" + Character.Network.Guild.Guildid + "'", "union_unique_id"));
                //If 0 means we check if we are the union leaders
                if (my_union == 0)
                {
                    //Check for union leader
                    my_union = Convert.ToInt32(DB.GetData("SELECT union_unique_id FROM guild_unions WHERE union_leader='" + Character.Network.Guild.Guildid + "'", "union_unique_id"));
                    //If we are the union leader
                    if (my_union > 0)
                        Character.Network.Guild.UnionLeader = Character.Network.Guild.Guildid;
                }

                //If union is active so count higher then 0
                if (my_union > 0)
                {
                    DB unions = new DB("SELECT * FROM guild_unions WHERE union_unique_id='" + my_union + "'");
                    //Open new sql data reader
                    using (SqlDataReader reader = unions.Read())
                    {
                        //While our reader is reading the information
                        while (reader.Read())
                        {
                            //Check if we allready have main info loaded
                            //If the union leader isnt the loading guild
                            if (Character.Network.Guild.UnionLeader == 0)
                                Character.Network.Guild.UnionLeader = reader.GetInt32(1);
                            //Add union to the listening
                            Character.Network.Guild.Unions.Add(reader.GetInt32(2));
                            //Set union active
                            Character.Network.Guild.UnionActive = true;
                        }
                        // Repeat for each guild in our union
                        foreach (int guild in Character.Network.Guild.Unions)
                        {
                            //Make sure the guild isnt 0
                            if (guild != 0)
                            {
                                //Get guildplayer details
                                PlayerMgr unionmember = Helpers.GetInformation.GetGuildPlayer(guild);
                                //Make sure the player isnt null
                                if (unionmember != null)
                                {
                                    //Then add our character id to the member list.
                                    Character.Network.Guild.UnionMembers.Add(Character.Information.CharacterID);
                                }
                            }
                        }
                        //Close our sql reader.
                        reader.Close();
                    }
                    //Finally send packet for union listening
                    client.Send(Packet.UnionInfo(this));
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Load Player Information
        public void PlayerDataLoad()
        {
            //Wrap our function inside a catcher 
            try
            {
                //////////////////////////////////////////////////////////////////////////////////////
                // Load Character Data
                //////////////////////////////////////////////////////////////////////////////////////
                #region Character Data
                //If Character is null we return
                if (Character == null) return;
                //Set new sql query for character information
                DB ms = new DB("SELECT * FROM character WHERE name='" + Character.Information.Name + "'");
                //Open new sql data reader
                using (SqlDataReader reader = ms.Read())
                {
                    //While our reader is reading the information
                    while (reader.Read())
                    {
                        // Character id information
                        Character.Information.CharacterID = reader.GetInt32(0);
                        Character.Account.ID = Player.ID;
                        // Character model information
                        Character.Information.Model = reader.GetInt32(3);
                        Character.Information.Race = ObjData.Manager.ObjectBase[Character.Information.Model].Race;
                        Character.Information.Volume = reader.GetByte(4);
                        // Character base stats
                        Character.Information.Level = reader.GetByte(5);
                        Character.Stat.Strength = reader.GetInt16(6);
                        Character.Stat.Intelligence = reader.GetInt16(7);
                        Character.Information.Attributes = reader.GetInt16(8);
                        Character.Stat.Hp = reader.GetInt32(9);
                        Character.Stat.Mp = reader.GetInt32(10);
                        // Character gold information
                        Character.Information.Gold = reader.GetInt64(11);
                        // Character Points
                        Character.Information.XP = reader.GetInt64(12);
                        Character.Information.SpBar = reader.GetInt32(13);
                        Character.Information.SkillPoint = reader.GetInt32(14);
                        // Character GM information
                        Character.Information.GM = reader.GetByte(15);
                        // Character Location
                        Character.Position.xSec = reader.GetByte(16);
                        Character.Position.ySec = reader.GetByte(17);
                        Character.Position.x = reader.GetInt32(19);
                        Character.Position.y = reader.GetInt32(20);
                        Character.Position.z = reader.GetInt32(21);
                        Character.Information.Place = reader.GetByte(40);
                        // Character Main Stats
                        Character.Stat.SecondHp = reader.GetInt32(22);
                        Character.Stat.SecondMP = reader.GetInt32(23);
                        Character.Stat.MinPhyAttack = reader.GetInt32(24);
                        Character.Stat.MaxPhyAttack = reader.GetInt32(25);
                        Character.Stat.MinMagAttack = reader.GetInt32(26);
                        Character.Stat.MaxMagAttack = reader.GetInt32(27);
                        Character.Stat.PhyDef = reader.GetInt16(28);
                        Character.Stat.MagDef = reader.GetInt16(29);
                        Character.Stat.Hit = reader.GetInt16(30);
                        Character.Stat.Parry = reader.GetInt16(31);
                        Character.Speed.WalkSpeed = reader.GetInt32(33);
                        Character.Speed.RunSpeed = reader.GetInt32(34);
                        Character.Speed.BerserkSpeed = reader.GetInt32(35);
                        Character.Information.BerserkBar = reader.GetByte(36);
                        Character.Stat.mag_Absorb = reader.GetInt16(38);
                        Character.Stat.phy_Absorb = reader.GetInt16(39);
                        // Character Other information
                        Character.Information.Pvpstate = reader.GetByte(45);
                        Character.Account.StorageGold = Player.pGold;
                        Character.Account.StorageSlots = Player.wSlots;
                        Character.Information.ExpandedStorage = reader.GetByte(53);
                        Character.Information.Slots = reader.GetInt32(44);
                        Character.Information.Title = reader.GetByte(41);
                        Character.Information.Online = reader.GetInt32(47);
                        Character.Information.StallModel = reader.GetInt32(52);
                        // Character Guide Info
                        Character.Guideinfo.G1 = new int[8];//Main Int Array Fro Guide Packet
                        Character.Guideinfo.Gchk = new int[8];//Main Guide Check Packet Array
                        //Read guide information
                        string Guideread = reader.GetString(51);
                        int t = 0;
                        for (int g = 0; g < 8; ++g)
                        {
                            Character.Guideinfo.G1[g] = int.Parse(Guideread.Substring(t, 2), System.Globalization.NumberStyles.HexNumber, null);
                            t = t + 2;
                        }
                        for (int gc = 0; gc < 8; ++gc)
                        {
                            Character.Guideinfo.Gchk[gc] = 0;
                        }
                    }
                }
                //Close our sql data reader
                ms.Close();
                #endregion
                //////////////////////////////////////////////////////////////////////////////////////
                // Set Skill And Mastery Information Max
                //////////////////////////////////////////////////////////////////////////////////////
                #region Set Skill / Mastery
                Character.Stat.Skill.Mastery = new int[8];
                Character.Stat.Skill.Mastery_Level = new byte[8];
                Character.Stat.Skill.Skill = new int[20000];
                #endregion
                //////////////////////////////////////////////////////////////////////////////////////
                // Load Mastery Data
                //////////////////////////////////////////////////////////////////////////////////////
                #region Load Mastery Data
                //New sql query to get mastery information
                ms = new DB("SELECT * FROM mastery WHERE owner='" + Character.Information.CharacterID + "'");
                //Open new sql data reader
                using (SqlDataReader reader = ms.Read())
                {
                    //Set byte to 1 default
                    byte c = 1;
                    //While sql data reader is reading
                    while (reader.Read())
                    {
                        //We add the mastery information and level
                        Character.Stat.Skill.Mastery[c] = reader.GetInt32(1);
                        Character.Stat.Skill.Mastery_Level[c] = reader.GetByte(2);
                        c++;
                    }
                }
                //Close our sql data reader
                ms.Close();
                #endregion
                //////////////////////////////////////////////////////////////////////////////////////
                // Load Skill Data
                //////////////////////////////////////////////////////////////////////////////////////
                #region Load Skill Data
                //Create new query to get skill data
                ms = new DB("SELECT * FROM saved_skills WHERE owner='" + Character.Information.CharacterID + "'");
                //Get skill count from reader
                using (SqlDataReader reader = ms.Read())
                    Character.Stat.Skill.AmountSkill = ms.Count();
                //Set default i to 1
                int i = 1;
                //Open new sql data reder
                using (SqlDataReader reader = ms.Read())
                {
                    //While the reader is reading
                    while (reader.Read())
                    {
                        //Add the skill id
                        Character.Stat.Skill.Skill[i] = reader.GetInt32(2);
                        i++;
                    }
                }
                //Close the sql data reader
                ms.Close();
                #endregion
                //////////////////////////////////////////////////////////////////////////////////////
                // Load Remaining information
                //////////////////////////////////////////////////////////////////////////////////////
                #region Load Remaining Data
                //Update x / y
                UpdateXY();
                //If character's current hp is lower then 1
                if (Character.Stat.SecondHp < 1)
                {
                    //Reset player location to return point, and fill the hp half of the max.
                    Character.Stat.SecondHp = (Character.Stat.Hp / 2);
                    Teleport_UpdateXYZ(Character.Information.Place);
                }
                //Set balande for player
                SetBalance();
                //Check our file information
                CheckFile();
                //Set stats for player
                SetStat();
                #endregion
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
        #region Weather
        public void LoadWeather()
        {
            //Wrap our function inside a catcher
            try
            {
                //NOTE: Must be defined per zone and random information

                //Load info from ini for now
                Ini ini = new Ini();
                ini = new CLFramework.Ini(Environment.CurrentDirectory + @"\Settings\Settings.ini");
                string Enabled = ini.GetValue("Custom", "WeatherEnabled", "true").ToString();
                string Type = ini.GetValue("Custom", "WeatherType", "1").ToString();
                string Info = ini.GetValue("Custom", "WeatherInfo", "10").ToString();
                
                //If the user has defined to enable custom weather type
                if (Enabled == "1")
                {
                    //We send the information they chosen
                    client.Send(Packet.Weather(Convert.ToByte(Type), Convert.ToInt32(Info)));
                }
                //If disabled
                else
                {
                    //Send static weather
                    client.Send(Packet.Weather(0, 1));
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        #endregion
    }
}
