using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using MySql.Data.MySqlClient;
using System.ComponentModel;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace MessagePlugin
{
    public class Message
    {
        public string ID { get; set; }
        public string MailFrom { get; set; }
        public string MailTo { get; set; }
        public string MailText { get; set; }
        public string Date { get; set; }
        public string Read { get; set; }

        public Message(string id, string mailFrom, string mailTo, string mailText, string date, string read)
        {
            ID = id;
            MailFrom = mailFrom;   
            MailTo = mailTo;
            MailText = mailText;
            Date = date;
            Read = read;
        }
    }
}