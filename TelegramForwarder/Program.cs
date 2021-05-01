using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TelegramForwarder.Services;
using TLSharp;

namespace TelegramForwarder
{
    public class Program
    {
        private static string mobile, password, hash, code;
        private static TelegramService telegram;
        public static bool SaveLog = true;
        static void Main(string[] args)
        {
            try
            {
                ApplicationHelpers.CreateIfNotExists();

                var setting = JsonConvert.DeserializeObject<TelegramSetting>(System.IO.File.ReadAllText("setting.json"));

                SaveLog = setting.SaveLog;

                Console.WriteLine("Trying to connect telegram...");
                telegram = ConnectToTelegram(setting);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Connected to telegram successful");
                Console.ForegroundColor = ConsoleColor.White;
                Thread.Sleep(TimeSpan.FromSeconds(3));
                Console.Clear();

                if (!telegram.IsUserAuthorized())
                    AuthToTelegram();
                else
                    telegram.DeleteSession();

                while (true)
                {
                    telegram.ForwardMessges(setting.ForwardFrom, setting.ForwadTo, setting.ForwardFromIds).GetAwaiter().GetResult();
                    Console.Clear();
                    Thread.Sleep(TimeSpan.FromSeconds(setting.DelayPerRound));
                }
            }
            catch (StackOverflowException ex)
            {
                ApplicationHelpers.LogException(ex);
                Console.WriteLine("json file is not valid");
            }
            catch (System.Exception ex)
            {
                ApplicationHelpers.LogException(ex);
                Main(args);

            }


        }
        static void AuthToTelegram()
        {
            try
            {
                GetMobile();

                hash = telegram.SendMessageForLogin(mobile.Trim()).GetAwaiter().GetResult();
                GetSendedCode();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("login success...");
                Console.ForegroundColor = ConsoleColor.White;

                Thread.Sleep(TimeSpan.FromSeconds(3));
                Console.Clear();

            }
            catch (System.Exception ex)
            {
                ApplicationHelpers.LogException(ex);
                telegram.DeleteSession();
            }

        }

        private static void GetSendedCode()
        {
            Console.WriteLine("Enter code that send to your number : ");
            code = Console.ReadLine();

            try
            {
                telegram.AuthUser(hash, mobile.Trim(), code.Trim()).GetAwaiter().GetResult();
            }
            catch (CloudPasswordNeededException)
            {
                GetPassword();

            }
            catch (InvalidPhoneCodeException)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Invalid code");
                Console.ForegroundColor = ConsoleColor.White;
                GetSendedCode();
            }
        }

        private static void GetPassword()
        {
            try
            {
                Console.WriteLine("Enter code your password : ");
                password = Console.ReadLine();
                telegram.AuthUser(hash, mobile.Trim(), code.Trim(), password.Trim()).GetAwaiter().GetResult();
            }
            catch (InvalidPhoneCodeException)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Invalid password");
                Console.ForegroundColor = ConsoleColor.White;
                GetPassword();
            }
        }

        private static void GetMobile()
        {
            Console.WriteLine("Enter your mobile (like: +989130540980) : ");
            mobile = Console.ReadLine();
            // while to check mobile
            if (!Regex.IsMatch(mobile.Trim(), @"\+989\d{9}$"))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Invalid mobile ");
                Console.ForegroundColor = ConsoleColor.White;
                GetMobile();
            }
        }

        static TelegramService ConnectToTelegram(TelegramSetting setting)
        {
            TelegramService telegram;
            try
            {
                telegram = new Services.TelegramService(setting);
            }
            catch
            {

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Could not connect to telegram, please check your network connection or your VPN...");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Trying again in 10 second...");
                Thread.Sleep(TimeSpan.FromSeconds(10));
                return ConnectToTelegram(setting);

            }


            return telegram;
        }
    }

}
