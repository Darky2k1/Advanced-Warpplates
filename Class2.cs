/*   
TShock, a server mod for Terraria
Copyright (C) 2011 The TShock Team

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

﻿using System;
using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using MySql.Data.MySqlClient;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Drawing;
using System.Linq;

namespace PluginTemplate
{
    public class WarpplateManager
    {
        public List<Warpplate> Warpplates = new List<Warpplate>();

        private IDbConnection database;

        public WarpplateManager(IDbConnection db)
        {
            database = db;

            var table = new SqlTable("Warpplates",
                new SqlColumn("X1", MySqlDbType.Int32),
                new SqlColumn("Y1", MySqlDbType.Int32),
                new SqlColumn("width", MySqlDbType.Int32),
                new SqlColumn("height", MySqlDbType.Int32),
                new SqlColumn("WarpplateName", MySqlDbType.VarChar, 50) { Primary = true },
                new SqlColumn("WorldID", MySqlDbType.Text),
                new SqlColumn("WarpplateDestination", MySqlDbType.VarChar, 50)
            );
            var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            creator.EnsureExists(table);
            ReloadAllWarpplates();

        }

        public void ConvertDB()
        {
            try
            {
                database.Query("UPDATE Warpplates SET WorldID=@0, UserIds=''", Main.worldID.ToString());
                ReloadAllWarpplates();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public void ReloadAllWarpplates()
        {
            try
            {
                using (var reader = database.QueryReader("SELECT * FROM Warpplates WHERE WorldID=@0", Main.worldID.ToString()))
                {
                    Warpplates.Clear();
                    while (reader.Read())
                    {
                        int X1 = reader.Get<int>("X1");
                        int Y1 = reader.Get<int>("Y1");
                        int height = reader.Get<int>("height");
                        int width = reader.Get<int>("width");
                        int Protected = reader.Get<int>("Protected");
                        string mergedids = reader.Get<string>("UserIds");
                        string name = reader.Get<string>("WarpplateName");
                        string warpdest = reader.Get<string>("WarpplateDestination");

                        string[] splitids = mergedids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        Warpplate r = new Warpplate(new Vector2(X1, Y1), new Rectangle(X1, Y1, width, height), name, warpdest, Protected != 0, Main.worldID.ToString());

                        try
                        {
                            for (int i = 0; i < splitids.Length; i++)
                            {
                                int id;

                                if (Int32.TryParse(splitids[i], out id)) // if unparsable, it's not an int, so silently skip
                                    r.AllowedIDs.Add(id);
                                else
                                    Log.Warn("One of your UserIDs is not a usable integer: " + splitids[i]);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("Your database contains invalid UserIDs (they should be ints).");
                            Log.Error("A lot of things will fail because of this. You must manually delete and re-create the allowed field.");
                            Log.Error(e.ToString());
                            Log.Error(e.StackTrace);
                        }

                        Warpplates.Add(r);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public void ReloadForUnitTest(String n)
        {

            using (var reader = database.QueryReader("SELECT * FROM Warpplates WHERE WorldID=@0", n))
            {
                Warpplates.Clear();
                while (reader.Read())
                {
                    int X1 = reader.Get<int>("X1");
                    int Y1 = reader.Get<int>("Y1");
                    int height = reader.Get<int>("height");
                    int width = reader.Get<int>("width");
                    int Protected = reader.Get<int>("Protected");
                    string MergedIDs = reader.Get<string>("UserIds");
                    string name = reader.Get<string>("WarpplateName");
                    string warpdest = reader.Get<string>("WarpplateDestination");
                    string[] SplitIDs = MergedIDs.Split(',');

                    Warpplate r = new Warpplate(new Vector2(X1, Y1), new Rectangle(X1, Y1, width, height), name, warpdest, Protected != 0, Main.worldID.ToString());
                    try
                    {
                        for (int i = 0; i < SplitIDs.Length; i++)
                        {
                            int id;

                            if (Int32.TryParse(SplitIDs[i], out id)) // if unparsable, it's not an int, so silently skip
                                r.AllowedIDs.Add(id);
                            else if (SplitIDs[i] == "") // Split gotcha, can return an empty string with certain conditions
                                // but we only want to let the user know if it's really a nonparsable integer.
                                Log.Warn("UnitTest: One of your UserIDs is not a usable integer: " + SplitIDs[i]);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Your database contains invalid UserIDs (they should be ints).");
                        Log.Error("A lot of things will fail because of this. You must manually delete and re-create the allowed field.");
                        Log.Error(e.Message);
                        Log.Error(e.StackTrace);
                    }

                    Warpplates.Add(r);
                }
            }

        }

        public bool AddWarpplate(int tx, int ty, int width, int height, string Warpplatename, string Warpdest, string worldid)
        {
            if (GetWarpplateByName(Warpplatename) != null)
            {
                return false;
            }
            try
            {
                database.Query("INSERT INTO Warpplates (X1, Y1, width, height, WarpplateName, WorldID, UserIds, Protected, WarpplateDestination) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8);",
                    tx, ty, width, height, Warpplatename, worldid, "", 1, Warpdest);
                Warpplates.Add(new Warpplate(new Vector2(tx, ty), new Rectangle(tx, ty, width, height), Warpplatename, worldid, true, Warpdest));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            return false;
        }

        public bool DeleteWarpplate(string name)
        {
                Warpplate r = GetWarpplateByName(name);
                if (r != null)
                {
                    int q = database.Query("DELETE FROM Warpplates WHERE WarpplateName=@0 AND WorldID=@1", name, Main.worldID.ToString());
                    if (q > 0)
                        return true;
                }
                return false;
        }

        public bool SetWarpplateState(string name, bool state)
        {
            try
            {
                database.Query("UPDATE Warpplates SET Protected=@0 WHERE WarpplateName=@1 AND WorldID=@2", state ? 1 : 0, name, Main.worldID.ToString());
                var Warpplate = GetWarpplateByName(name);
                if (Warpplate != null)
                    Warpplate.DisableBuild = state;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return false;
            }
        }

        public bool SetWarpplateStateTest(string name, string world, bool state)
        {
            try
            {
                database.Query("UPDATE Warpplates SET Protected=@0 WHERE WarpplateName=@1 AND WorldID=@2", state ? 1 : 0, name, world);
                var Warpplate = GetWarpplateByName(name);
                if (Warpplate != null)
                    Warpplate.DisableBuild = state;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return false;
            }
        }

        public bool CanBuild(int x, int y, TSPlayer ply)
        {
            if (!ply.Group.HasPermission(Permissions.canbuild))
            {
                return false;
            }
            for (int i = 0; i < Warpplates.Count; i++)
            {
                if (Warpplates[i].InArea(new Rectangle(x, y, 0, 0)) && !Warpplates[i].HasPermissionToBuildInWarpplate(ply))
                {
                    return false;
                }
            }
            return true;
        }

        public Warpplate FindWarpplate(string name)
        {
            try
            {
                using (var reader = database.QueryReader("SELECT * FROM Warpplates WHERE WarpplateName=@0 AND WorldID=@1", name, Main.worldID.ToString()))
                {
                    if (reader.Read())
                    {
                        try
                        {
                            return new Warpplate(new Vector2(reader.Get<int>("X1"), reader.Get<int>("Y1")), new Rectangle(reader.Get<int>("X1"), reader.Get<int>("Y1"), reader.Get<int>("width"), reader.Get<int>("height")), reader.Get<string>("WarpplateName"), reader.Get<string>("WarpplateDestination"), reader.Get<bool>("Protected"), reader.Get<string>("WorldID"));
                        }
                        catch
                        {
                            return new Warpplate(new Vector2(reader.Get<int>("X1"), reader.Get<int>("Y1")), new Rectangle(reader.Get<int>("X1"), reader.Get<int>("Y1"), reader.Get<int>("width"), reader.Get<int>("height")), reader.Get<string>("WarpplateName"), " ", reader.Get<bool>("Protected"), reader.Get<string>("WorldID"));
                        }
                    } 
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            return new Warpplate();
        }

        public bool InArea(int x, int y)
        {
            foreach (Warpplate Warpplate in Warpplates)
            {
                if (x >= Warpplate.Area.Left && x <= Warpplate.Area.Right &&
                    y >= Warpplate.Area.Top && y <= Warpplate.Area.Bottom &&
                    Warpplate.DisableBuild)
                {
                    return true;
                }
            }
            return false;
        }

        public string InAreaWarpplateName(int x, int y)
        {
            foreach (Warpplate Warpplate in Warpplates)
            {
                if (x >= Warpplate.Area.Left && x <= Warpplate.Area.Right &&
                    y >= Warpplate.Area.Top && y <= Warpplate.Area.Bottom &&
                    Warpplate.DisableBuild)
                {
                    return Warpplate.Name;
                }
            }
            return null;
        }

        public static List<string> ListIDs(string MergedIDs)
        {
            return MergedIDs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public bool RemoveUser(string WarpplateName, string userName)
        {
            Warpplate r = GetWarpplateByName(WarpplateName);
            if (r != null)
            {
                r.RemoveID(TShock.Users.GetUserID(userName));
                string ids = string.Join(",", r.AllowedIDs);
                int q = database.Query("UPDATE Warpplates SET UserIds=@0 WHERE WarpplateName=@1 AND WorldID=@2", ids,
                                       WarpplateName, Main.worldID.ToString());
                if (q > 0)
                    return true;
            }
            return false;
        }

        public bool removedestination(string WarpplateName)
        {
            Warpplate r = GetWarpplateByName(WarpplateName);
            if (r != null)
            {
                int q = database.Query("UPDATE Warpplates SET WarpplateDestination=@0 WHERE WarpplateName=@1 AND WorldID=@2", "", WarpplateName, Main.worldID.ToString());
                if (q > 0)
                    return true;
            }
            return false;
        }

        public bool adddestination(string WarpplateName, String WarpDestination)
        {
            Warpplate r = GetWarpplateByName(WarpplateName);
            if (r != null)
            {
                int q = database.Query("UPDATE Warpplates SET WarpplateDestination=@0 WHERE WarpplateName=@1 AND WorldID=@2;", WarpDestination, WarpplateName, Main.worldID.ToString());
                if (q > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets all the Warpplates names from world
        /// </summary>
        /// <param name="worldid">World name to get Warpplates from</param>
        /// <returns>List of Warpplates with only their names</returns>
        public List<Warpplate> ListAllWarpplates(string worldid)
        {
            var Warpplates = new List<Warpplate>();
            try
            {
                using (var reader = database.QueryReader("SELECT WarpplateName FROM Warpplates WHERE WorldID=@0", worldid))
                {
                    while (reader.Read())
                        Warpplates.Add(new Warpplate { Name = reader.Get<string>("WarpplateName") });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            return Warpplates;
        }

        public Warpplate GetWarpplateByName(String name)
        {
            return Warpplates.FirstOrDefault(r => r.Name.Equals(name) && r.WorldID == Main.worldID.ToString());
        }

        public Warpplate ZacksGetWarpplateByName(String name)
        {
            foreach (Warpplate r in Warpplates)
            {
                if (r.Name.Equals(name))
                    return r;
            }
            return null;
        }
    }

    public class Warpplate
    {
        public Rectangle Area { get; set; }
        public Vector2 WarpplatePos { get; set; }
        public string Name { get; set; }
        public string WarpDest { get; set; }
        public bool DisableBuild { get; set; }
        public string WorldID { get; set; }
        public List<int> AllowedIDs { get; set; }

        public Warpplate(Vector2 warpplatepos, Rectangle Warpplate, string name, string warpdest, bool disablebuild, string WarpplateWorldIDz)
            : this()
        {
            WarpplatePos = warpplatepos;
            Area = Warpplate;
            Name = name;
            WarpDest = warpdest;
            DisableBuild = disablebuild;
            WorldID = WarpplateWorldIDz;
        }

        public Warpplate()
        {
            Area = Rectangle.Empty;
            Name = string.Empty;
            WarpDest = string.Empty;
            DisableBuild = true;
            WorldID = string.Empty;
            AllowedIDs = new List<int>();
        }

        public bool InArea(Rectangle point)
        {
            if (Area.Contains(point.X, point.Y))
            {
                return true;
            }
            return false;
        }

        public bool HasPermissionToBuildInWarpplate(TSPlayer ply)
        {
            if (!ply.IsLoggedIn)
            {
                if (!ply.HasBeenNaggedAboutLoggingIn)
                {
                    ply.SendMessage("You must be logged in to take advantage of protected Warpplates.", Color.Red);
                    ply.HasBeenNaggedAboutLoggingIn = true;
                }
                return false;
            }
            if (!DisableBuild)
            {
                return true;
            }

            for (int i = 0; i < AllowedIDs.Count; i++)
            {
                if (AllowedIDs[i] == ply.UserID)
                {
                    return true;
                }
            }
            return false;
        }

        public void setAllowedIDs(String ids)
        {
            String[] id_arr = ids.Split(',');
            List<int> id_list = new List<int>();
            foreach (String id in id_arr)
            {
                int i = 0;
                int.TryParse(id, out i);
                if (i != 0)
                    id_list.Add(i);
            }
            AllowedIDs = id_list;
        }

        public void RemoveID(int id)
        {
            var index = -1;
            for (int i = 0; i < AllowedIDs.Count; i++)
            {
                if (AllowedIDs[i] == id)
                {
                    index = i;
                    break;
                }
            }
            AllowedIDs.RemoveAt(index);
        }
    }
}
