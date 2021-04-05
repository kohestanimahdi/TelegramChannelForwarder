using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TLSchema;
using TLSchema.Auth;
using TLSchema.Channels;
using TLSchema.Contacts;
using TLSchema.Messages;
using TLSchema.Updates;
using TLSharp;

namespace TelegramForwarder.Services
{
    public class TelegramService
    {
        private readonly TelegramClient telegramClient;
        private readonly TelegramSetting telegramSetting;
        public TelegramService(TelegramSetting telegramSetting)
        {
            this.telegramSetting = telegramSetting;

            try
            {
                telegramClient = NewClient();
                telegramClient.ConnectAsync().GetAwaiter().GetResult();
            }
            catch
            {
                throw new Exception("مشکلی در ارتباط با تلگرام رخ داد");
            }


        }
        public bool IsUserAuthorized()
           => telegramClient.IsUserAuthorized();

        public bool IsConnected()
            => telegramClient.IsConnected;

        public async Task ForwardMessges(List<string> fromChannels, string to, IEnumerable<int> fromchannelIds)
        {
            var user = await GetByUserName(to);
            if (user is null)
                throw new Exception();

            foreach (var from in fromChannels)
            {
                await Task.Delay(telegramSetting.DelayPerMessage);
                var channel = await GetChannelByUserName(from);
                await ForwardMessage(channel, user);

            }

            foreach (var from in fromchannelIds)
            {
                await Task.Delay(telegramSetting.DelayPerMessage);
                var channel = await GetChannelById(from);
                await ForwardMessage(channel, user);
            }
        }
        public async Task ForwardMessage(TLChannel channel, TLUser user)
        {
            if (channel is null)
            {
                Console.WriteLine("Not found");
                return;
            }

            try
            {
                await Task.Delay(telegramSetting.DelayPerMessage);
                var chat = await GetFullChatOfChannel(channel.Id, channel.AccessHash.Value);
                if (chat.FullChat is TLChannelFull channelFull && channelFull.UnreadCount > 0)
                {
                    var messages = await GetMessages(channel.Id, channel.AccessHash.Value, channelFull.UnreadCount);
                    if (messages is null)
                        return;

                    Console.WriteLine($"forwad from {channel.Username}, number of messages: {channelFull.UnreadCount}");

                    await Task.Delay(telegramSetting.DelayPerMessage);

                    foreach (var message in messages.Reverse())
                    {
                        await Task.Delay(telegramSetting.DelayPerMessage);
                        await ForwardMessgae(message, channel, user);
                    }
                    await Task.Delay(telegramSetting.DelayPerMessage);
                    await SetAsRead(channel.Id, channel.AccessHash.Value, messages.First().Id, channelFull.UnreadCount);
                }
            }
            catch (Exception ex)
            {
                ApplicationHelpers.LogException(ex);
            }
        }
        public async Task SetAsRead(int id, long accessHash, int messageId, int count, int numberOfTry = 0)
        {
            try
            {
                var ch = new TLInputChannel() { ChannelId = id, AccessHash = accessHash };
                var markAsRead = new TLSchema.Channels.TLRequestReadHistory()
                {
                    Channel = ch,
                    MaxId = -1,
                    Dirty = true,
                    MessageId = messageId,
                    ConfirmReceived = true,
                    //Sequence = count
                };
                var readed = await telegramClient.SendRequestAsync<bool>(markAsRead);
            }
            catch (Exception ex)
            {
                ApplicationHelpers.LogException(ex);

                if (numberOfTry > 2)
                    throw;

                await Task.Delay(telegramSetting.DelayPerMessage);
                await SetAsRead(id, accessHash, messageId, ++numberOfTry);
            }

        }
        public async Task<IEnumerable<TLMessage>> GetMessages(int id, long accessHash, int count, int numberOfTry = 0)
        {
            try
            {
                var inputPer = new TLInputPeerChannel()
                {
                    ChannelId = id,
                    AccessHash = accessHash
                };
                var history = (await telegramClient.GetHistoryAsync(inputPer, limit: count));

                if (history is TLChannelMessages messages)
                    return messages.Messages.OfType<TLMessage>();

                return null;
            }
            catch (Exception ex)
            {
                ApplicationHelpers.LogException(ex);

                if (numberOfTry > 2)
                    throw;

                await Task.Delay(telegramSetting.DelayPerMessage);
                return await GetMessages(id, accessHash, count, ++numberOfTry);
            }

        }
        public async Task<TLSchema.Messages.TLChatFull> GetFullChatOfChannel(int id, long accessHash, int numberOfTry = 0)
        {
            try
            {
                return await telegramClient.SendRequestAsync<TLSchema.Messages.TLChatFull>(new TLRequestGetFullChannel()
                {
                    Channel = new TLInputChannel
                    {
                        ChannelId = id,
                        AccessHash = accessHash
                    }
                });
            }
            catch (Exception ex)
            {

                ApplicationHelpers.LogException(ex);

                if (numberOfTry > 2)
                    throw;

                await Task.Delay(telegramSetting.DelayPerMessage);
                return await GetFullChatOfChannel(id, accessHash, ++numberOfTry);
            }


        }

        public async Task ForwardMessgae(TLMessage message, TLChannel from, TLUser to)
        {
            Random rand = new Random();

            TLInputPeerChannel cha = new TLInputPeerChannel
            {
                ChannelId = from.Id,
                AccessHash = from.AccessHash.Value
            };


            TLInputPeerUser us = new TLInputPeerUser
            {
                AccessHash = to.AccessHash.Value,
                UserId = to.Id
            };


            TLVector<long> a = new TLVector<long>();
            a.Add(rand.Next());
            TLVector<int> b = new TLVector<int>();
            b.Add(message.Id);
            TLRequestForwardMessages aa = new TLRequestForwardMessages();
            aa.FromPeer = cha;
            aa.ToPeer = us;
            aa.RandomId = a;
            aa.MessageId = message.Id;
            aa.Id = b;

            aa.Silent = true;
            aa.WithMyScore = true;

            TLUpdates rr = await telegramClient.SendRequestAsync<TLUpdates>(aa);
        }
        public async Task UnRead()
        {
            var dialogs = (await telegramClient.GetUserDialogsAsync()) as TLDialogs;
            foreach (TLDialog dialog in dialogs.Dialogs.ToList())
            {
                TLPeerUser u = (TLPeerUser)dialog.Peer;
                var user = dialogs.Users.ToList()
                  .Where(x => x.GetType() == typeof(TLUser))
                  .Cast<TLUser>()
                  .FirstOrDefault(x => x.Id == u.UserId);

                //dgContacts.Items.Add(user);
            }
        }

        public async Task<TLChannel> GetChannelById(int id)
        {

            var dialogs = await telegramClient.GetUserDialogsAsync();
            List<TLChannel> channels = new List<TLChannel>();
            if (dialogs is TLDialogsSlice dialogsSlice)
                channels = dialogsSlice.Chats.OfType<TLChannel>().ToList();
            else if (dialogs is TLDialogs tldilogs)
                channels = tldilogs.Chats.OfType<TLChannel>().ToList();

            return channels.FirstOrDefault(i => i.Id == id);
        }

        public async Task UnRead2()
        {
            var rnd = new Random();

            while (true)
            {
                Task.Delay(rnd.Next(3000, 6000)).Wait();
                var dialogs = telegramClient.GetUserDialogsAsync().Result as TLDialogs;

                if (dialogs == null)
                {
                    Task.Delay(rnd.Next(3000, 6000)).Wait();
                    continue;
                }

                foreach (TLDialog dialog in dialogs.Dialogs.Where(lambdaDialog => lambdaDialog.Peer is TLPeerChannel && lambdaDialog.UnreadCount > 0))
                {
                    TLPeerChannel peer = (TLPeerChannel)dialog.Peer;
                    TLChannel channel = dialogs.Chats.OfType<TLChannel>().First(lambdaChannel => lambdaChannel.Id == peer.ChannelId);
                    TLInputPeerChannel target = new TLInputPeerChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash ?? 0 };

                    Task.Delay(rnd.Next(3000, 6000)).Wait();
                    TLChannelMessages hist = (TLChannelMessages)telegramClient.GetHistoryAsync(target, 0, -1, dialog.UnreadCount).Result;
                    if (hist == null) continue;

                    var users = hist.Users.OfType<TLUser>().ToList();
                    var messages = hist.Messages.OfType<TLMessage>().ToList();

                    foreach (TLMessage message in messages)
                    {
                        TLUser sentUser = users.Single(lambdaUser => lambdaUser.Id == message.FromId);
                        Console.WriteLine($"{channel.Title} {sentUser.FirstName} {sentUser.LastName} {sentUser.Username}: {message.Message}");
                    }

                    TLInputChannel channelToMarkRead = new TLInputChannel { ChannelId = target.ChannelId, AccessHash = target.AccessHash };
                    var firstAbsMessage = hist.Messages[0];
                    int firstUnreadMessageId;
                    if (firstAbsMessage is TLMessage) firstUnreadMessageId = ((TLMessage)firstAbsMessage).Id;
                    else if (firstAbsMessage is TLMessageService) firstUnreadMessageId = ((TLMessageService)firstAbsMessage).Id;
                    else continue;

                    var markHistoryAsReadRequest = new TLSchema.Channels.TLRequestReadHistory
                    {
                        Channel = channelToMarkRead,
                        MaxId = -1,
                        ConfirmReceived = true,
                        Dirty = true,
                        MessageId = firstUnreadMessageId,
                        Sequence = dialog.UnreadCount
                    };

                    Task.Delay(rnd.Next(3000, 6000)).Wait();
                    telegramClient.SendRequestAsync<bool>(markHistoryAsReadRequest).Wait();

                    Console.WriteLine("Mark messages as read");
                }
            }
        }
        public async Task<bool> LogOutAsync()
        {
            var LogOut = new TLRequestLogOut();
            var result = await telegramClient.SendRequestAsync<bool>(LogOut);
            if (result)
                DeleteSession();
            return result;
        }

        public async Task<string> SendMessageForLogin(string phoneNumber)
        {
            try
            {
                return await telegramClient.SendCodeRequestAsync(phoneNumber);
            }
            catch
            {
                DeleteSession();
                return await telegramClient.SendCodeRequestAsync(phoneNumber);
            }
        }

        public void DeleteSession()
        {
            var file = Path.Combine(Directory.GetCurrentDirectory(), $"session.dat");
            if (File.Exists(file))
                File.Delete(file);
        }

        public async Task AuthUser(string hash, string numberToAuthenticate, string codeToAuthenticate, string passwordToAuthenticate = "")
        {
            TLUser user = null;
            try
            {
                user = await telegramClient.MakeAuthAsync(numberToAuthenticate, hash, codeToAuthenticate);
            }
            catch (CloudPasswordNeededException)
            {
                if (!string.IsNullOrWhiteSpace(passwordToAuthenticate))
                {
                    var passwordSetting = await telegramClient.GetPasswordSetting();
                    user = await telegramClient.MakeAuthWithPasswordAsync(passwordSetting, passwordToAuthenticate);
                }
                else throw;
            }


        }
        private TelegramClient NewClient()
            => new TelegramClient(telegramSetting.ApiId, telegramSetting.ApiHash);



        public Task<TLState> offset;
        public Task<TLState> GetOffset() => telegramClient.SendRequestAsync<TLState>(new TLRequestGetState());

        public void Worker()
        {
            if (telegramClient.IsUserAuthorized())
            {
                offset = Task.Run(GetOffset);
                while (true)
                {
                    Thread.Sleep(500);
                    Task getUpdates = GetUpdates(offset.Result.Date, offset.Result.Pts, offset.Result.Qts);
                    getUpdates.Wait();
                }
            }
        }


        public async Task GetUpdates(int date, int pts, int qts)
        {
            var req = new TLRequestGetDifference() { Date = date, Pts = pts, Qts = qts };
            if (await telegramClient.SendRequestAsync<TLAbsDifference>(req) is TLDifference diff)
            {
                foreach (var upd in diff.NewMessages)
                {
                    offset.Result.Pts++;
                    var msg = upd as TLMessage;
                    Console.WriteLine(msg.Message);
                }
            }
            await telegramClient.SendPingAsync();
        }
        public async Task<TLChannel> GetChannelByUserName(string channelUserName, int numberOfTry = 0)
        {
            try
            {
                var channelInfos = (await telegramClient.SendRequestAsync<TLResolvedPeer>(
                   new TLRequestResolveUsername
                   {
                       Username = channelUserName
                   }).ConfigureAwait(false));

                if (channelInfos.Chats.Count == 0)
                    return null;

                return channelInfos.Chats.First() as TLChannel;
            }
            catch (Exception ex)
            {
                ApplicationHelpers.LogException(ex);

                if (numberOfTry > 2)
                    throw;

                await Task.Delay(telegramSetting.DelayPerMessage);
                return await GetChannelByUserName(channelUserName, ++numberOfTry);
            }
            //TLBoolTrue

        }

        public async Task<TLUser> GetByUserName(string userName, int numberOfTry = 0)
        {
            try
            {
                TLFound found = await this.telegramClient.SearchUserAsync(userName);

                var users = found.Users.OfType<TLUser>();
                if (!users.Any())
                    return null;

                return users.First();
            }
            catch (Exception ex)
            {
                ApplicationHelpers.LogException(ex);

                if (numberOfTry > 2)
                    throw;

                await Task.Delay(telegramSetting.DelayPerMessage);
                return await GetByUserName(userName, ++numberOfTry);
            }

            //long hash = ((TLUser)found.Users.ToList()[0]).access_hash.Value;
            //int id = ((TeleSharp.TL.TLUser)found.users.lists[0]).id;
            //TeleSharp.TL.TLInputPeerUser peer = new TeleSharp.TL.TLInputPeerUser() { user_id = id, access_hash = hash };

            //TeleSharp.TL.TLAbsUpdates up = await this.client.SendMessageAsync(peer, "/start");
        }
        public Task<TLSchema.Contacts.TLContacts> GetAllContacts()
        {
            return telegramClient.GetContactsAsync();
        }

        public async Task SendMessageAsync(string customerName, string phoneNumber, string text)
        {
            var user = await GetUserByPhoneNumber(phoneNumber);
            if (user is null)
                throw new Exception($"کانالی با آیدی {phoneNumber} پیدا نشد");

            await telegramClient.SendMessageAsync(new TLInputPeerUser()
            { UserId = user.Id, AccessHash = (long)user.AccessHash }, text);
        }
        public async Task<TLUser> GetUserByPhoneNumber(string phoneNumber)
        {
            phoneNumber = ConvertToNationalityPhoneNumber(phoneNumber, false);
            var contacts = (await GetAllContacts()).Users.Select(i => (TLUser)i).Where(i => i.Phone.Contains(phoneNumber)).ToList();
            return contacts.FirstOrDefault();
        }

        public string ConvertToNationalityPhoneNumber(string phoneNumber, bool withPlus)
        {
            if (withPlus)
            {
                if (Regex.IsMatch(phoneNumber, @"09\d{9}$"))
                    return $"+98{phoneNumber.Remove(0, 1)}";

                if (Regex.IsMatch(phoneNumber, @"9\d{9}$"))
                    return $"+98{phoneNumber}";

                if (Regex.IsMatch(phoneNumber, @"\+989\d{9}$"))
                    return phoneNumber;

                if (Regex.IsMatch(phoneNumber, @"989\d{9}$"))
                    return $"+{phoneNumber}";

                if (Regex.IsMatch(phoneNumber, @"00989\d{9}$"))
                    return $"+{phoneNumber.Remove(0, 2)}";
            }
            else
            {
                if (Regex.IsMatch(phoneNumber, @"09\d{9}$"))
                    return $"98{phoneNumber.Remove(0, 1)}";

                if (Regex.IsMatch(phoneNumber, @"9\d{9}$"))
                    return $"98{phoneNumber}";

                if (Regex.IsMatch(phoneNumber, @"\+989\d{9}$"))
                    return phoneNumber.Remove(0, 1);

                if (Regex.IsMatch(phoneNumber, @"989\d{9}$"))
                    return phoneNumber;

                if (Regex.IsMatch(phoneNumber, @"00989\d{9}$"))
                    return $"{phoneNumber.Remove(0, 2)}";
            }

            throw new Exception("قالب شماره تلفن وارد شده نامعتبر است");

        }
    }
}
