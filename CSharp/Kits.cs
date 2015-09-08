using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Kits", "Reneb", "3.0.0")]
    class Kits : RustPlugin
    {
    	int playerLayer = UnityEngine.LayerMask.GetMask(new string[] { "Player (Server)" });
    	
    	void Loaded()
    	{
    		allKitFields = typeof(Kit).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
    		KitsData = Interface.GetMod().DataFileSystem.GetDatafile("Kits_Data");
    	}
    	//////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        static string noAccess = "You are not allowed to use this command";
		static Dictionary<string, object> GUIKits = GetExampleGUIKits();
		
        private bool Changed;

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<string>("Messages - No Access", ref noAccess);
            CheckCfg<Dictionary<string, object>>("NPC - GUI Kits", ref GUIKits);

            SaveConfig();
        }

        void LoadDefaultConfig() { }
        
        static Dictionary<string,object> GetExampleGUIKits()
        {
        	var GUIKits = new Dictionary<string,object>();
        	
        	var npc1 = new Dictionary<string,List<object>>();
        	var kitsnpc1 = new List<object>();
        	kitsnpc1.Add("kit1");
        	kitsnpc1.Add("kit2");
        	npc1.Add("kits",kitsnpc1);
        	npc1.Add("description", "NPC Kit 1 Description here");
        	
        	var npc2 = new List<object>();
        	var kitsnpc2 = new List<object>();
        	kitsnpc2.Add("kit1");
        	kitsnpc2.Add("kit3");
        	npc2.Add("kits",kitsnpc2);
        	npc2.Add("description", "NPC Kit 2 Description here");
        	
        	GUIKits.Add("1235439", npc1);
        	GUIKits.Add("8753201223", npc2);
        
        	return GUIKits;
        }
        
        
    	List<KitItem> GetPlayerItems( BasePlayer player )
    	{
    		var kititems = new List<KitItem>();
    		
    		foreach (Item item in player.inventory.containerWear.itemList)
            {
				kititems.Add( new KitItem( item.info.itemid, item.IsBlueprint(), "wear", item.amount, item.skinid ) );
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
				kititems.Add( new KitItem( item.info.itemid, item.IsBlueprint(), "main", item.amount, item.skinid ) );
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
				kititems.Add( new KitItem( item.info.itemid, item.IsBlueprint(), "belt", item.amount, item.skinid ) );
            }
    		
    		return kititems;
    	}
    	
    	void TryGiveKit(BasePlayer player, string kitname)
    	{
    		object success = CanRedeemKit( player, kitname );
    		if(success is string)
    		{
    			SendReply(player, (string)success);
    			return;
    		}
    		success = GiveKit(player, kitname);
    		if(success is string)
    		{
    			SendReply(player, (string)success);
    			return;
    		}
    		SendReply(player, "Kit redeemed");
    		
    		proccessKitGiven( player, kitname );
    	}
    	void proccessKitGiven(BasePlayer player, string kitname)
    	{
    		if(!isKit(kitname)) return;
    		
    		Kit kit = storedData.Kits[kitname];
    		if(kit.max != null)
    			SetData( player, kitname, "max", (int)GetData(player, kitname, "max") + 1 );
    		
    		if(kit.cooldown != null)
    			SetData( player, kitname, "cooldown", (CurrentTime() + (double)kit.cooldown).ToString() );
    			
    	}
    	
    	object GiveKit(BasePlayer player, string kitname)
    	{
    		if(!isKit(kitname))
    			return "This kit doesn't exist";
    		
    		foreach( KitItem kitem in storedData.Kits[kitname].items )
    		{
    			Item item = ItemManager.CreateByItemID((int)kitem.itemid, (int)kitem.amount, (bool)kitem.bp);
    			item.skinid = (int)kitem.skinid;
    			player.inventory.GiveItem( item , kitem.container == "belt" ? player.inventory.containerBelt : kitem.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain );
    		}
    		
    		return true;
    	}
    	bool isKit(string kitname)
        {
            if (storedData.Kits[kitname] == null)
                return false;
            return true;
        }
    	
    	double CurrentTime() { return System.DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
    	
    	bool CanSeeKit(BasePlayer player, string kitname, bool fromNPC, out string reason)
    	{
    		reason = string.Empty;
    		if(!isKit(kitname))
    			return false;
    		Kit kit = storedData.Kits[kitname];
    		if(kit.hide != null)
    			return false;
    		if(kit.authlevel != null)
    			if(player.net.connection.authLevel < (int)kit.authlevel)
    				return false;
    		if(kit.permission != null)
    			if(player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.userID.ToString(), kit.permission))
    				return false;
    		if(kit.npconly != null && !fromNPC)
    			return false;
    		if(kit.max != null)
    		{
    			int left = (int)GetData(player, kitname, "max");
    			if(left >= kit.max)
    			{
    				reason += "- 0 left";
    				return false;
    			}
    			else
    			{
    				reason += string.Format("- {0} left",(kit.max - left).ToString());
    			}
    		}
    		if(kit.cooldown != null)
    		{
    			double cd = (double)GetData(player, kitname, "cooldown");
    			double ct = CurrentTime();
    			if(cd < ct && cd != 0.0)
    			{
    				reason += string.Format("- {0} seconds", (ct - cd).ToString());
    				return false;
    			}
    		}
    		return true;
    	
    	}
    	
    	object CanRedeemKit(BasePlayer player, string kitname)
    	{
    		if(!isKit(kitname))
    			return "This kit doesn't exist";
			
			object thereturn = Interface.GetMod().CallHook("canRedeemKit", new object[1] { player });
            if (thereturn != null)
            {
                if (thereturn is string) return thereturn;
                return "You are not allowed to redeem a kit at the moment";
            }
			
    		Kit kit = storedData.Kits[kitname];
    		if(kit.authlevel != null)
    			if(player.net.connection.authLevel < (int)kit.authlevel)
    				return "You don't have the level to use this kit";
    		
    		if(kit.permission != null)
    			if(player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.userID.ToString(), kit.permission))
    				return "You don't have the permissions to use this kit";
    		
    		if(kit.max != null)
    			if((int)GetData(player, kitname, "max") >= kit.max)
    				return "You already redeemed all of those kits";
    		
    		if(kit.cooldown != null)
    		{
    			double cd = (double)GetData(player, kitname, "cooldown");
    			double ct = CurrentTime();
    			if(cd < ct && cd != 0.0)
    				return string.Format("You need to wait {0} seconds to use this kit", (ct - cd).ToString());
    		}
    		
    		if(kit.npconly != null)
    		{
    			bool foundNPC = false;
    			var neededNpc = new List<string>();
    			foreach(KeyValuePair<string, object> pair in GUIKits)
    			{
    				var listkits = pair.Value as List<object>;
    				if(listkits.Contains( kitname ))
    					neededNpc.Add(pair.Key);
    			}
    			foreach(Collider col in Physics.OverlapSphere(player.transform.position, 3f, playerLayer))
    			{
    				BasePlayer targetplayer = col.GetComponentInParent<BasePlayer>();
    				if(targetplayer == null) continue;
    				
    				if(neededNpc.Contains( targetplayer.userID.ToString() ))
    				{
    					foundNPC = true;
    					break;
    				}
    			}
    			if(!foundNPC)
    				return "You must found the NPC that gives this kit to redeem it.";
    		}
    		return true;
    	}
    	string GetData( BasePlayer player, string kitname, string dataname )
    	{
    		if(KitsData[player.userID.ToString] == null)
    			KitsData[player.userID.ToString] = new Dictionary<string, Dictionary<string,string>>();
    		if( !KitsData[player.userID.ToString].ContainsKey(kitname) ) return "0";
    		if( !KitsData[player.userID.ToString][kitname].ContainsKey(dataname) ) return "0";
    		return KitsData[player.userID.ToString][kitname][dataname];
    	}
    	void SetData(BasePlayer player, string kitname, string dataname, string datavalue)
    	{
    		if(KitsData[player.userID.ToString] == null)
    			KitsData[player.userID.ToString] = new Dictionary<string, Dictionary<string,string>>();
    		if( !KitsData[player.userID.ToString].ContainsKey(kitname) )
    			KitsData[player.userID.ToString].Add(kitname, new Dictionary<string, string>());
    		if( !KitsData[player.userID.ToString][kitname].ContainsKey(dataname) )
    			KitsData[player.userID.ToString][kitname].Add(dataname, datavalue);
    		else
    			KitsData[player.userID.ToString][kitname][dataname] = datavalue;;
    	}
    	
		//////////////////////////////////////////////////////////////////////////////////////
        // Kit Class
        //////////////////////////////////////////////////////////////////////////////////////
        public class KitItem
        {
        	public string itemid;
        	public string bp;
        	public string skinid;
        	public string container;
        	public string amount;
        	
        	public KitItem()
        	{
        	
        	}
        	public KitItem(int itemid, bool bp, string container, int amount, int skinid = 0)
        	{
        		this.itemid = itemid.ToString();
        		this.bp = bp.ToString();
        		this.skinid = skinid.ToString();
        		this.amount = amount.ToString();
        		this.container = container;
        	} 
        }
        
    	public class Kit 
    	{
    		public string name;
    		public string description;
            public string max;
            public string cooldown;
            public string authlevel;
            public string hide;
            public string npconly;
            public string permission;
            public string image;
			public List<KitItem> items;
			
            public Kit()
            {
            }
            
            public Kit(string name)
            {
            	this.name = name;
            	this.items = new List<KitItem>();
            }
    	}
    	
    	//////////////////////////////////////////////////////////////////////////////////////
        // Data Manager
        //////////////////////////////////////////////////////////////////////////////////////
        
        private Core.Configuration.DynamicConfigFile KitsData;
        
        private void SaveKitsData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Kits_Data");
        }
        
        
        static StoredData storedData;

        class StoredData
        {
            public Hash<string, Kit> Kits = new Hash<string, Kit>();

            public StoredData()
            {
            }
        }
		void ResetData()
		{
			KitsData.Clear();
			SaveKitsData();
		}
		
		void Unload()
		{
			SaveKitsData();
		}
        void OnServerSave()
        {
            SaveKitsData();
        }

        void SaveKits()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Kits", storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Kits");
            }
            catch
            {
                storedData = new StoredData();
            }
        }
        
        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Editor
        //////////////////////////////////////////////////////////////////////////////////////
        
        public FieldInfo[] allKitFields;
        FieldInfo GetZoneField(string name)
        {
            name = name.ToLower();
            foreach (FieldInfo fieldinfo in allKitFields) { if (fieldinfo.Name == name) return fieldinfo; }
            return null;
        }
        
        Hash<BasePlayer, string> kitEditor = new Hash<BasePlayer, string>();
        
        
        //////////////////////////////////////////////////////////////////////////////////////
        // GUI
        //////////////////////////////////////////////////////////////////////////////////////
        
        Dictionary<BasePlayer, Hash<string, string>> PlayerGUI = new Dictionary<BasePlayer, Hash<string, string>>();
        
        public string overlayjson = @"[  
			{ 
				""name"": ""KitOverlay"",
				""parent"": ""Overlay"",
				""components"":
				[
					{
						 ""type"":""UnityEngine.UI.Image"",
						 ""color"":""0.1 0.1 0.1 0.8"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0"",
						""anchormax"": ""1 1""
					},
					{
						""type"":""NeedsCursor"",
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{msg}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.1 0.7"",
						""anchormax"": ""0.9 0.9""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Name"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.15 0.65"",
						""anchormax"": ""0.25 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Description"",
						""fontSize"":10,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.25 0.65"",
						""anchormax"": ""0.70 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Cooldown (s)"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.70 0.65"",
						""anchormax"": ""0.75 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Left"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.75 0.65"",
						""anchormax"": ""0.80 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Redeem"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.80 0.65"",
						""anchormax"": ""0.90 0.7""
					}
				]
			}
		]";
		string kitlistoverlay = @"[
			{ 
				""name"": ""KitListOverlay"",
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						 ""type"":""UnityEngine.UI.Image"",
						 ""color"":""0 0 0 0"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0"",
						""anchormax"": ""1 1""
					}
				]
			}
		]";
		
        string buttonjson = @"[
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.RawImage"",
						""imagetype"": ""Tiled"",
						""url"": ""{imageurl}"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.10 {ymin}"",
						""anchormax"": ""0.15 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{kitfullname}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.15 {ymin}"",
						""anchormax"": ""0.25 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{kitdescription}"",
						""fontSize"":12,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.25 {ymin}"",
						""anchormax"": ""0.70 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{cooldown}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.70 {ymin}"",
						""anchormax"": ""0.75 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{left}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.75 {ymin}"",
						""anchormax"": ""0.80 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Redeem"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""UnityEngine.UI.Button"",
						""command"":""kit.gui {guimsg}"",
						""color"": ""{color}"",
						""imagetype"": ""Tiled""
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.80 {ymin}"",
						""anchormax"": ""0.90 {ymax}""
					}
				]
			}
		]";
		
		void NewKitPanel(BasePlayer player, string guiId = "chat")
		{
			DestroyAllGUI(player);
			if (!GUIKits.ContainsKey(guiId)) return;
			
			var kitpanel = GUIKits[guiId] as Dictionary<string,object>;
			
			var goverlay = overlayjson.Replace("{msg}", kitpanel.ContainsKey("description") ? (string)kitpanel["description"] : string.Empty);
			CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", goverlay);
			
			RefreshKitPanel(player, guiId, 0);
		}
        void RefreshKitPanel(BasePlayer player, string guiId, int minKit = 0)
        {
        	if( !PlayerGUI.ContainsKey(player) ) PlayerGUI.Add(player, new Hash<string,string>);
        	PlayerGUI[player]["guiid"] = guiId;
        	PlayerGUI[player]["page"] = (string)minKit;
        	
            DestroyGUI( player, "KitListOverlay" );
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", kitlistoverlay);
            var kitpanel = GUIKits[guiId] as Dictionary<string,object>;
            
            int current = 0;
            foreach( string kitname in kitpanel["kits"] )
            {
            	if(current >= minKit && current < minKit + 8)
            	{
            		if(!storedData.Kits.ContainsKey(kitname.ToLower())) continue;
            		Kit kit = storedData.Kits[kitname.ToLower()] as Dictionary<string,object>;
            		
            		var ckit = buttonjson.Replace("{guimsg}",string.Format("'{0}'", kitname.ToLower())).Replace("{ymin}", 0.6 - ((current - minKit) + 1) * 0.05 ).Replace("{ymax}", 0.6 - (current - minKit)*0.05 ).Replace("{kitfullname}",kit.name).Replace("{kitdescription}", kit.description != null ? kit.description : string.Empty).Replace("{imageurl}", kit.image != null ? kit.image : "http://i.imgur.com/xxQnE1R.png").Replace("{left}", kit.max == null ? string.Empty : ((int)kit.max - (int)GetData(player, kitname.ToLower(), "max")).ToString() ).Replace("{cooldown}", kit.cooldown == null ? string.Empty : CurrentTime() > (double)GetData(player, kitname.ToLower(), "cooldown") ? "0" : (CurrentTime() - (double)GetData(player, kitname.ToLower(), "cooldown")).ToString() );
            		CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", ckit);
            	}
            	current++;
            }
        }
        
        void DestroyAllGUI(BasePlayer player) { CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "KitOverlay"); }
        void DestroyGUI(BasePlayer player, string GUIName) { CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", GUIName); }
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!GUIKits.ContainsKey(npc.userID.ToString())) return;
            NewKitPanel(player, npc.userID.ToString());
        } 
        
        
        //////////////////////////////////////////////////////////////////////////////////////
        // Console Command
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("kit.gui")]
        void cmdConsoleKitGui(ConsoleSystem.Arg arg)
        {
            if (arg.connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if ((arg.Args == null) || (arg.Args != null && arg.Args.Length == 0))
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }
            BasePlayer player = (BasePlayer)arg.connection.player;

            string kitname = arg.Args[0].Substring(1, arg.Args[0].Length - 2);
            TryGiveKit(player, kitname);
            RefreshKitPanel( player, PlayerGUI[player]["guiid"], (int)PlayerGUI[player]["page"] );
        }
        
        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Command
        //////////////////////////////////////////////////////////////////////////////////////
        [ChatCommand("kit")]
        void cmdChatKit(BasePlayer player, string command, string[] args)
        {
        	if(args.Length == 0)
        	{
        		if( GUIKits.ContainsKey("chat") )
        			NewKitPanel( player, "chat" );
        		else
        		{
        			string reason = string.Empty;
        			foreach( KeyValuePair<string, Kit> pair in storedData.Kits )
        			{
        				bool cansee = CanSeeKit(player, pair.Key, false, out reason);
        				if(!cansee && reason == string.Empty) continue;
        				SendReply(player, string.Format("{0} - {1} - {2}", pair.Value.name, pair.Value.description, reason));
        			}
        		}
        		return;
        	}
        	if(args.Length == 1)
        	{
        		switch( args[0] )
        		{
        			case "add":
					case "remove":
					case "edit":
						if(!hasAccess(player))
						{
							SendReply(player, "You don't have access to this command");
							return;
						}
						SendReply(player, string.Format("/kit {0} KITNAME", args[0]));
					break;
        			case "resetkits":
        				storedData.Kits.Clear();
        				kitEditor.Clear();
        				ResetData();
        				SaveKits();
        				SendReply(player, "Resetted all kits and player data");
        			break;
        			case "resetdata":
        				ResetData();
        				SendReply(player, "Resetted all player data");
        			break;
        			default:
        				TryGiveKit( player, args[0].ToLower() );
        			break;
        		}
        	
        	}
        	if(!hasAccess(player))
			{
				SendReply(player, "You don't have access to this command");
				return;
			}
			
			string kitname = string.Empty;
        	switch( args[0] )
        	{
        		case "add":
        			kitname = args[1].ToLower();
					if( storedData.Kits[kitname] != null )
					{
						SendReply(player, "This kit already exists.");
						return;
					}
					storedData.Kits[kitname] = new Kit( rawname );
					kitEditor[player] = kitname;
        		break;
        		case "edit":
        			kitname = args[1].ToLower();
        			if( storedData.Kits[kitname] == null )
        			{
        				SendReply(player, "This kit doesn't seem to exist");
						return;
        			}
        			kitEditor[player] = kitname;
        			SendReply(player, string.Format("You are now editing the kit: {0}",kitname));
        		break;
        		case "remove":
        			kitname = args[1].ToLower();
        			if( storedData.Kits[kitname] == null )
        			{
        				SendReply(player, "This kit doesn't seem to exist");
						return;
        			}
        			storedData.Kits.Remove(kitname);
        			SendReply(player, string.Format("{0} was removed", kitname));
        			if(kitEditor[player] == kitname) kitEditor.Remove(player);
        		break;
        		default:
        			if (kitEditor[player] == null)
        			{
        				SendReply(player, "You are not creating or editing a kit");
						return;
        			}
        			if( storedData.Kits[kitEditor[player]] == null )
        			{
        				SendReply(player, "There was an error while getting this kit, was it changed while you were editing it?");
						return;
        			}
        			for (int i = 0; i < args.Length; i = i + 2)
        			{
        				if(args[i].ToLower() == "items")
        				{ 
        					i--;
        					storedData.Kits[kitEditor[player]].items = GetPlayerItems( player );
        					continue;
        				}
        				// I WILL NEED TO MAKE IT THAT YOU CAN CHANGE THE ITEMS
						else if(args[i].ToLower() == "name") continue;
						// I WILL NEED TO MAKE IT THAT YOU CAN CHANGE THE NAME 
						else
						{
							FieldInfo cachedField = GetZoneField(args[i]);
							if (cachedField == null) 
							{
								SendReply(player, string.Format("{0} is not a valid argument", args[i]));
								continue;
							}
							object editvalue;
							switch (args[i + 1].ToLower())
							{
								case "true":
									editvalue = "true";
									break;
								case "null":
								case "0":
								case "false":
								case "reset":
									editvalue = null;
								break;
								default:
									editvalue = (string)args[i + 1];
								break;
							}
							cachedField.SetValue(storedData.Kits[kitEditor[player]], editvalue);
							SendReply( player, string.Format( "{0} set to {1}", cachedField.Name, editvalue == null ? "null" : editvalue) );
						}
        			}
        		break;
        	}
        	SaveKits();
        }
    }
}
