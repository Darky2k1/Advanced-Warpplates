using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Hooks;
using TShockAPI;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace PluginTemplate
{
    [APIVersion(1, 10)]
    public class WarpplatePlugin : TerrariaPlugin
    {
        public static List<Player> Players = new List<Player>();
        public static WarpplateManager Warpplates;

        public override string Name
        {
            get { return "Warpplate"; }
        }
        public override string Author
        {
            get { return "Created by DarkunderdoG"; }
        }
        public override string Description
        {
            get { return "Warpplate"; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            Warpplates = new WarpplateManager(TShock.DB);
            GameHooks.PostInitialize += OnPostInit;
            GameHooks.Initialize += OnInitialize;
            GameHooks.Update += OnUpdate;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.PostInitialize -= OnPostInit;
                GameHooks.Initialize -= OnInitialize;
                GameHooks.Update -= OnUpdate;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
            }

            base.Dispose(disposing);
        }

        private void OnPostInit()
        {
            Warpplates.ReloadAllWarpplates();
        }

        public WarpplatePlugin(Main game)
            : base(game)
        {
            Order = 1;
        }

        public void OnInitialize()
        {
            Commands.ChatCommands.Add(new Command("setwarpplate", setwarpplate, "swp"));
            Commands.ChatCommands.Add(new Command("setwarpplate", delwarpplate, "dwp"));
            Commands.ChatCommands.Add(new Command("setwarpplate", warpplatedest, "swpd"));
            Commands.ChatCommands.Add(new Command("setwarpplate", removeplatedest, "rwpd"));
            Commands.ChatCommands.Add(new Command("setwarpplate", wpi, "wpi"));
            Commands.ChatCommands.Add(new Command("setwarpplate", warpallow, "wpallow"));
        }

        public void OnGreetPlayer(int ply, HandledEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(ply));
        }

        public class Player
        {
            public int Index { get; set; }
            public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
            public int warpplatetime { get; set; }
            public bool warpplateuse { get; set; }
            public Player(int index)
            {
                Index = index;
                warpplatetime = 0;
                warpplateuse = true;
            }
        }

        private DateTime LastCheck = DateTime.UtcNow;

        private void OnUpdate()
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                LastCheck = DateTime.UtcNow;
                lock (Players)
                    foreach (Player player in Players)
                    {
                        if (player.TSPlayer.Group.HasPermission("warpplate") && player.warpplateuse)
                        {
                            string region = Warpplates.InAreaWarpplateName(player.TSPlayer.TileX, player.TSPlayer.TileY);
                            if (region == null)
                                player.warpplatetime = 0;
                            if (region != null)
                            {
                                var warpplateinfo = Warpplates.FindWarpplate(region);
                                var warp = Warpplates.FindWarpplate(warpplateinfo.WarpDest);
                                if (warp.WarpplatePos != Vector2.Zero)
                                {
                                    player.warpplatetime++;
                                    if ((4 - player.warpplatetime) > 0)
                                        player.TSPlayer.SendMessage("You Will Be Warped To " + warpplateinfo.WarpDest + " in " + (4 - player.warpplatetime) + " Seconds");
                                    if (player.warpplatetime == 4)
                                    {
                                        if (player.TSPlayer.Teleport((int)warp.WarpplatePos.X + 2, (int)warp.WarpplatePos.Y + 3))
                                            player.TSPlayer.SendMessage("You Have Been Warped To " + warpplateinfo.WarpDest + " via a Warpplate");
                                        player.warpplatetime = 0;
                                    }

                                }
                            }
                        }
                    }
            }
        }

        private void OnLeave(int ply)
        {
            lock (Players)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                    {
                        Players.RemoveAt(i);
                        break; //Found the player, break.
                    }
                }
            }
        }

        private static int GetPlayerIndex(int ply)
        {
            lock (Players)
            {
                int index = -1;
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                        index = i;
                }
                return index;
            }
        }

        private static void setwarpplate(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /swp <warpplate name>", Color.Red);
                return;
            }
            if (Warpplates.InAreaWarpplateName(args.Player.TileX, args.Player.TileY) != null)
            {
                args.Player.SendMessage("There Is Already A Warpplate Located Here. Find A New Place", Color.Red);
                return;
            }
            string regionName = String.Join(" ", args.Parameters);
            var x = ((((int)args.Player.X) / 16) - 1);
            var y = (((int)args.Player.Y) / 16);
            var width = 2;
            var height = 3;
            if (Warpplates.AddWarpplate(x, y, width, height, regionName, "", Main.worldID.ToString()))
            {
                args.Player.SendMessage("Warpplate Created: " + regionName, Color.Yellow);
                args.Player.SendMessage("Now Set The Warpplate Destination By Using /swpd", Color.Yellow);
                Warpplates.ReloadAllWarpplates();
            }
            else
            {
                args.Player.SendMessage("Warpplate Already Created: " + regionName + " already exists", Color.Red);
            }
        }

        private static void delwarpplate(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /dwp <warpplate name>", Color.Red);
                return;
            }
            string regionName = String.Join(" ", args.Parameters);
            if (Warpplates.DeleteWarpplate(regionName))
            {
                args.Player.SendMessage("Deleted Warpplate: " + regionName, Color.Yellow);
                Warpplates.ReloadAllWarpplates();
            }
            else
                args.Player.SendMessage("Could not find specified Warpplate", Color.Red);
        }

        private static void warpplatedest(CommandArgs args)
        {
            if (args.Parameters.Count < 2)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /swpd <Warpplate Name> <Name Of Destination Warpplate>", Color.Red);
                return;
            }
            if (Warpplates.adddestination(args.Parameters[0], args.Parameters[1]))
            {
                args.Player.SendMessage("Destination " + args.Parameters[1] + " Added To Warpplate " + args.Parameters[0], Color.Yellow);
                Warpplates.ReloadAllWarpplates();
            }
            else
                args.Player.SendMessage("Could not find specified Warpplate or destination", Color.Red);
        }

        private static void removeplatedest(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /rwpd <Warpplate Name>", Color.Red);
                return;
            }
            if (Warpplates.removedestination(args.Parameters[0]))
            {
                args.Player.SendMessage("Removed Destination From Warpplate " + args.Parameters[0], Color.Yellow);
                Warpplates.ReloadAllWarpplates();
            }
            else
                args.Player.SendMessage("Could not find specified Warpplate or destination", Color.Red);
        }

        private static void wpi(CommandArgs args)
        {
            string region = "";
            if (args.Parameters.Count > 0)
                region = String.Join(" ", args.Parameters);
            else
                region = Warpplates.InAreaWarpplateName(args.Player.TileX, args.Player.TileY);
            var warpplateinfo = Warpplates.FindWarpplate(region);
            args.Player.SendMessage("WarpplateName: " + warpplateinfo.Name + " Warpplate Destination: " + warpplateinfo.WarpDest, Color.HotPink);
            args.Player.SendMessage("Warpplate X: " + warpplateinfo.WarpplatePos.X + " Warpplate Y: " + warpplateinfo.WarpplatePos.Y, Color.HotPink);
        }

        private static void reloadall(CommandArgs args)
        {
            Warpplates.ReloadAllWarpplates();
            FileTools.SetupConfig();
            TShock.Regions.ReloadAllRegions();
            TShock.Groups.LoadPermisions();
        }

        private static void warpallow(CommandArgs args)
        {
            if (!Players[GetPlayerIndex(args.Player.Index)].warpplateuse)
                args.Player.SendMessage("Warpplates Are Now Turned On For You");
            if (Players[GetPlayerIndex(args.Player.Index)].warpplateuse)
                args.Player.SendMessage("Warpplates Are Now Turned Off For You");
            Players[GetPlayerIndex(args.Player.Index)].warpplateuse = !Players[GetPlayerIndex(args.Player.Index)].warpplateuse;
        }

    }
}