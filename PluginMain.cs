using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Data;

using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace MessagePlugin
{
    [ApiVersion(1, 14)]
    public class MessagePlugin : TerrariaPlugin
    {
        private static IDbConnection db;
        public mPlayer[] Players { get; set; }
        //public Message[] Messages { get; set; }

        //public static List<MPlayer> Players = new List<MPlayer>();
        public static List<Message> MessageList = new List<Message>();

        private static string savepath = Path.Combine(TShock.SavePath, "MessagePlugin/");
        private static bool initialized = false;

   
        public override string Name
        {
            get { return "Message plugin"; }
        }

        public override string Author
        {
            get { return "ja450n - original by Lmanik."; }
        }

        public override string Description
        {
            get { return ""; }
        }

        public override Version Version
        {
            get { return new Version(0, 9, 4); }
        }

        public override void Initialize()
        {

            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.GameInitialize.Register(this,OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);

       }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

                ServerApi.Hooks.GameUpdate.Deregister(this,  OnUpdate);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);


            }
            base.Dispose(disposing);
        }

        public MessagePlugin(Main game)
            : base(game)
        {
            this.Players = new mPlayer[256];
        }

        public void OnInitialize(EventArgs e)
        {


            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "message_plugin.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }
            
            SqlTableCreator sqlcreator = new SqlTableCreator(db, 
                db.GetSqlType() == SqlType.Sqlite 
                ? (IQueryBuilder)new SqliteQueryCreator() 
                : new MysqlQueryCreator());
            
            sqlcreator.EnsureExists(new SqlTable("MessagePlugin",
                new SqlColumn("Id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("mailFrom", MySqlDbType.Text),
                new SqlColumn("mailTo", MySqlDbType.Text),
                new SqlColumn("mailText", MySqlDbType.Text),
                new SqlColumn("Date", MySqlDbType.Text),
                new SqlColumn("Read", MySqlDbType.Text)
                ));

            

            //set group
            bool msg = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("msguse"))
                        msg = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!msg)
                permlist.Add("msguse");

            TShock.Groups.AddPermissions("trustedadmin", permlist);

            Commands.ChatCommands.Add(new Command("msguse", Msg, "msg"));
        }


        public void OnUpdate(EventArgs e)
        {
        }

        public void OnGreetPlayer(GreetPlayerEventArgs e)
        {

            try
            {
                Players[e.Who] = new mPlayer(e.Who);

                if (Players[e.Who].TSPlayer.Group.HasPermission("msguse"))
                {
                    string name = TShock.Players[e.Who].Name;
                    int count = GetUnreadEmailsByName(name);
                    TShock.Players[e.Who].SendMessage("You have " + count + " unread messages.", Color.Yellow);
                }

            }
            catch { }

        }

        // Remove all players
        public void OnLeave(LeaveEventArgs e)
        {
            try
            {
                Players[e.Who] = null;
            }
            catch { }
        }

        public void OnChat(ServerChatEventArgs e)
        {
        }


        #region commandHandlers
        
        // Return unread emails
        public static int GetUnreadEmailsByName(string name)
        {
            if (name != null)
            {
                String query = "SELECT Id FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                List<int> messageIDs = new List<int>();

                using (var reader = db.QueryReader(query, name, "U"))
                {
                    while (reader.Read())
                    {
                        messageIDs.Add(reader.Get<int>("Id"));
                    }
                    reader.Reader.Close();
                }
                return messageIDs.Count;
                
                    
                
            }

            return 0;
        }
        
        // Save message to db
        public static void SendMessage(string to, string from, string text)
        {
            DateTime date = DateTime.Now;

            String query = "INSERT INTO MessagePlugin (mailFrom,mailTo,mailText,Date,Read) VALUES (@0, @1, @2, @3, @4);";
            db.Query(query, from, to, text, date, "U");

            if (TShock.Utils.FindPlayer(to).Count > 0)
            {
                foreach (TSPlayer player in TShock.Utils.FindPlayer(to))
                {
                    if (player.Group.HasPermission("msguse"))
                    {
                        player.SendInfoMessage("You have new message from " + from, Color.Aqua);
                    }
                }
            }
            else
            {
                //TODO: notify to email
            }

        }

        //help message
        public static void Help(CommandArgs args)
        {
            args.Player.SendMessage("To send message use /msg <playerName> <message>", Color.Aqua);
            args.Player.SendMessage("lists unread messages by /msg inbox <page number>", Color.Aqua);
            args.Player.SendMessage("lists messages by /msg list <page number>", Color.Aqua);
            args.Player.SendMessage("to read specify message /msg read <id>, id give from list", Color.Aqua);
            args.Player.SendMessage("for delete message use /msg del <id>", Color.Aqua);
        }

        // Run Message command
        public static void Msg(CommandArgs args)
        {
            string cmd = "help";

            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
            }

            switch (cmd)
            {
                case "help":
                    {
                        //return help
                        Help(args);
                       
                        break;
                    }

                //list of all unread messages
                case "inbox":
                    {
                        // Fetch all unread messages
                        String query = "SELECT * FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                        using (var reader = db.QueryReader(query, args.Player.Name, "U"))
                        {
                            while (reader.Read())
                            {
                                // messageIDs.Add(reader.Get<int>("Id"));
                                DateTime date = Convert.ToDateTime(reader.Get<string>("Date"));
                                string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                                MessageList.Add(new Message(
                                    (string)Convert.ToString(reader.Get<int>("Id")),
                                    (string)reader.Get<string>("mailFrom"),
                                    (string)reader.Get<string>("mailTo"),
                                    (string)reader.Get<string>("mailText"),
                                    datum,
                                    (string)reader.Get<string>("Read")
                                    ));

                            }
                            reader.Reader.Close();
                        }
                        
                        //How many messages per page
                        const int pagelimit = 5;
                        //How many messages per line
                        const int perline = 1;
                        //Pages start at 0 but are displayed and parsed at 1
                        int page = 0;


                        if (args.Parameters.Count > 1)
                        {
                            if (!int.TryParse(args.Parameters[1], out page) || page < 1)
                            {
                                args.Player.SendMessage(string.Format("Invalid page number ({0})", page), Color.Red);
                                return;
                            }
                            page--; //Substract 1 as pages are parsed starting at 1 and not 0
                        }

                        List<Message> messages = new List<Message>();
                        foreach (Message message in MessageList)
                        {
                            messages.Add(message);
                        }

                        if (messages.Count == 0)
                        {
                            args.Player.SendMessage("No unread messages.", Color.Red);
                            return;
                        }

                        //Check if they are trying to access a page that doesn't exist.
                        int pagecount = messages.Count / pagelimit;
                        if (page > pagecount)
                        {
                            args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", page + 1, pagecount + 1), Color.Red);
                            return;
                        }

                        //Display the current page and the number of pages.
                        args.Player.SendMessage(string.Format("Inbox ({0}/{1}):", page + 1, pagecount + 1), Color.Green);

                        //Add up to pagelimit names to a list
                        var messageslist = new List<string>();
                        for (int i = (page * pagelimit); (i < ((page * pagelimit) + pagelimit)) && i < messages.Count; i++)
                        {
                            messageslist.Add("[" + messages[i].ID + "]" + " " + messages[i].MailFrom + " (" + messages[i].Date + ") [" + messages[i].Read + "]");
                        }

                        //convert the list to an array for joining
                        var lines = messageslist.ToArray();
                        for (int i = 0; i < lines.Length; i += perline)
                        {
                            args.Player.SendMessage(string.Join(", ", lines, i, Math.Min(lines.Length - i, perline)), Color.Yellow);
                        }

                        if (page < pagecount)
                        {
                            args.Player.SendMessage(string.Format("Type /msg inbox {0} for more unread messages.", (page + 2)), Color.Yellow);
                        }

                        //remove all messages
                        int count = messages.Count;
                        MessageList.RemoveRange(0, count);

                        break;
                    }

                //list of all messages
                case "list":
                    {
                        // Fetch all messages
                        
                        String query = "SELECT * FROM MessagePlugin WHERE mailTo=@0;";
                        List<int> messageIDs = new List<int>();

                        using (var reader = db.QueryReader(query, args.Player.Name))
                        {
                            while (reader.Read())
                            {
                                // messageIDs.Add(reader.Get<int>("Id"));
                                DateTime date = Convert.ToDateTime(reader.Get<string>("Date"));
                                string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                                MessageList.Add(new Message(
                                    (string)Convert.ToString(reader.Get<int>("Id")),
                                    (string)reader.Get<string>("mailFrom"),
                                    (string)reader.Get<string>("mailTo"),
                                    (string)reader.Get<string>("mailText"),
                                    datum,
                                    (string)reader.Get<string>("Read")
                                    ));

                            }
                            reader.Reader.Close();
                        }
                                        
                        //How many messages per page
                        const int pagelimit = 5;
                        //How many messages per line
                        const int perline = 1;
                        //Pages start at 0 but are displayed and parsed at 1
                        int page = 0;


                        if (args.Parameters.Count > 1)
                        {
                            if (!int.TryParse(args.Parameters[1], out page) || page < 1)
                            {
                                args.Player.SendMessage(string.Format("Invalid page number ({0})", page), Color.Red);
                                return;
                            }
                            page--; //Substract 1 as pages are parsed starting at 1 and not 0
                        }

                        List<Message> messages = new List<Message>();
                        foreach(Message message in MessageList)
                        {
                            messages.Add(message);
                        }

                        if (messages.Count == 0)
                        {
                            args.Player.SendMessage("You have no messages.", Color.Red);
                            return;
                        }

                        //Check if they are trying to access a page that doesn't exist.
                        int pagecount = messages.Count / pagelimit;
                        if (page > pagecount)
                        {
                            args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", page + 1, pagecount + 1), Color.Red);
                            return;
                        }

                        //Display the current page and the number of pages.
                        args.Player.SendMessage(string.Format("List messages ({0}/{1}):", page + 1, pagecount + 1), Color.Green);

                        //Add up to pagelimit names to a list
                        var messageslist = new List<string>();
                        for (int i = (page * pagelimit); (i < ((page * pagelimit) + pagelimit)) && i < messages.Count; i++)
                        {
                            messageslist.Add("[" + messages[i].ID + "]" + " " + messages[i].MailFrom + " (" + messages[i].Date + ") [" + messages[i].Read + "]");
                        }

                        //convert the list to an array for joining
                        var lines = messageslist.ToArray();
                        for (int i = 0; i < lines.Length; i += perline)
                        {
                            args.Player.SendMessage(string.Join(", ", lines, i, Math.Min(lines.Length - i, perline)), Color.Yellow);
                        }

                        if (page < pagecount)
                        {
                            args.Player.SendMessage(string.Format("Type /msg list {0} for more messages.", (page + 2)), Color.Yellow);
                        }

                        //remove all messages
                        int count = messages.Count;
                        MessageList.RemoveRange(0, count);

                        break;
                    }

                //read a specify message
                case "read":
                    {
                        if (args.Parameters.Count > 1)
                        {

                            String query = "SELECT * FROM MessagePlugin WHERE mailTo=@0 AND Id=@1; UPDATE MessagePlugin SET Read=@2 WHERE Id=@1";
                            String queryUpdateRead = "UPDATE MessagePlugin SET Read=@0 WHERE Id=@1;";

                            using (var reader = db.QueryReader(query, args.Player.Name, args.Parameters[1].ToString()))
                            {
                                if (reader.Read())
                                {
                                    string id = Convert.ToString(reader.Get<int>("Id"));
                                    string from = reader.Get<string>("mailFrom");
                                    string text = reader.Get<string>("mailText");

                                    DateTime date = Convert.ToDateTime(reader.Get<string>("Date"));
                                    string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                                    args.Player.SendInfoMessage(id + ") On " + datum + ", " + from + " wrote:", Color.Aqua);
                                    args.Player.SendInfoMessage(text, Color.White);
                                    
                                    reader.Reader.Close();
                                                                     
                                                                        
                                    if (db.Query(queryUpdateRead, "R", args.Parameters[1].ToString()) != 1)
                                    {
                                        Log.ConsoleError("[MessagePlugin] Failed to update Read status of message");
                                    }

                                }
                                else
                                {
                                    args.Player.SendErrorMessage("Messages with id \"" + args.Parameters[1].ToString() + "\" is not exist.", Color.Red);
                                }
                            }
                                                                                  
                        }
                        else
                        {
                            args.Player.SendMessage("You must set Email ID", Color.Red);
                        }

                        break;
                    }

                //delete specify messages
                case "del":
                    {
                        if (args.Parameters.Count > 1)
                        {
                            //switch args [id, unread, read, all]
                            switch (args.Parameters[1].ToString())
                            {
                                case "all":
                                    {

                                        String query = "SELECT Id FROM MessagePlugin WHERE mailTo=@0;";
                                        List<int> messageIDs = new List<int>();

                                        using (var reader = db.QueryReader(query, args.Player.Name))
                                        {
                                            while (reader.Read())
                                            {
                                                messageIDs.Add(reader.Get<int>("Id"));
                                            }
                                            reader.Reader.Close();
                                        }

                                        if (messageIDs.Count > 0)
                                        {
                                            query = "DELETE FROM MessagePlugin WHERE mailTo=@0;";
                                            db.Query(query, args.Player.Name);

                                            args.Player.SendInfoMessage("All messages were deleted.", Color.Red);

                                        }
                                        else
                                        {
                                            args.Player.SendInfoMessage("You have no messages.", Color.Red);

                                        }

                                        break;

                                    }
                                case "read":
                                    {

                                        String query = "SELECT Id FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                                        List<int> messageIDs = new List<int>();

                                        using (var reader = db.QueryReader(query, args.Player.Name, "R"))
                                        {
                                            while (reader.Read())
                                            {
                                                messageIDs.Add(reader.Get<int>("Id"));
                                            }
                                            reader.Reader.Close();
                                        }

                                        if (messageIDs.Count > 0)
                                        {
                                            query = "DELETE FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                                            db.Query(query, args.Player.Name, "R");

                                            args.Player.SendInfoMessage("All read messages were deleted", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendInfoMessage("You have no read messages.", Color.Red);
                                        }

                                        break;
                                    }

                                case "unread":
                                    {
                                        String query = "SELECT Id FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                                        List<int> messageIDs = new List<int>();

                                        using (var reader = db.QueryReader(query, args.Player.Name, "U"))
                                        {
                                            while (reader.Read())
                                            {
                                                messageIDs.Add(reader.Get<int>("Id"));
                                            }
                                            reader.Reader.Close();
                                        }

                                        if (messageIDs.Count > 0)
                                        {
                                            
                                            query = "DELETE FROM MessagePlugin WHERE mailTo=@0 AND Read=@1;";
                                            db.Query(query, args.Player.Name, "U");

                                            args.Player.SendInfoMessage("All unread messages were deleted", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendInfoMessage("You have no unread messages.", Color.Red);
                                        }

                                        break;
                                    }

                                default:
                                    {

                                        String query = "SELECT Id FROM MessagePlugin WHERE mailTo=@0 AND Id=@1;";
                                        List<int> messageIDs = new List<int>();

                                        using (var reader = db.QueryReader(query, args.Player.Name, args.Parameters[1].ToString()))
                                        {
                                            while (reader.Read())
                                            {
                                                messageIDs.Add(reader.Get<int>("Id"));
                                            }
                                            reader.Reader.Close();
                                        }

                                        if (messageIDs.Count > 0)
                                        {
                                            query = "DELETE FROM MessagePlugin WHERE mailTo=@0 AND Id=@1;";
                                            db.Query(query, args.Player.Name, args.Parameters[1].ToString());
                                            args.Player.SendInfoMessage("Message id \"" + args.Parameters[1].ToString() + "\" was deleted.", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendInfoMessage("Message id \"" + args.Parameters[1].ToString() + "\" does not exist.", Color.Red);
                                        }

                                        break;

                                    }
                            }  
                        }
                        else
                        {
                            args.Player.SendErrorMessage("You must set second parameter [id, all, unread, read]", Color.Red);
                        }
                        break;
                    }

                //send message
                default:
                    {

                        if (args.Parameters.Count > 1)
                        {

                            String query = "SELECT ID FROM Users WHERE Username=@0;";
                            List<int> userIDs = new List<int>();

                            using (var reader = TShock.DB.QueryReader(query, args.Parameters[0].ToString()))
                            {
                                while (reader.Read())
                                {
                                    userIDs.Add(reader.Get<int>("Id"));
                                }
                                reader.Reader.Close();
                            }
                            
                            if (userIDs.Count > 0)
                            { 
                                string mailTo = args.Parameters[0].ToString();
                                SendMessage(mailTo, args.Player.Name, string.Join(separator: " ", value: args.Parameters.ToArray(), startIndex:1, count:args.Parameters.Count -1 ));
                                args.Player.SendMessage("Message sent to " + mailTo, Color.Green);
                            }
                            else
                            {
                                args.Player.SendMessage("Player " + args.Parameters[0].ToString() + " does not exist.", Color.Red);
                            }

                        }
                        else
                        {
                            //return help
                            Help(args);
                        }

                        break;
                    }
            }
        }
                
        #endregion

    }



}