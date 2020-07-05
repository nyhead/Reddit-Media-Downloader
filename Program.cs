using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;

using CliWrap;
using Newtonsoft.Json.Linq;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Reddit_Video_Downloader_Telegram_Bot
{
    class Program
    {
        static ITelegramBotClient bot;
        static HttpClient client = new HttpClient();
        static string url;
        static Message editedMessage;
        enum Mode { RESOLUTION, VIDEO, AUDIO, GIF };
        static Mode mode;


        static void Main()
        {
            bot = new TelegramBotClient("");

            var me = bot.GetMeAsync().Result;
            Console.WriteLine(
              $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );
            bot.OnMessage += Bot_OnMessage;
            bot.OnCallbackQuery += BotOnCallbackQueryReceivedAsync;
            bot.StartReceiving();
            Thread.Sleep(int.MaxValue);
        }


        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            Message message = e.Message;
            Chat chat = e.Message.Chat;
            if (message.Text != null)
            {
                Console.WriteLine($"{chat.FirstName}(@{chat.Username}) in {chat.Id}: {message.Text}");

                if (message.Text == "/start")
                {
                    await bot.SendTextMessageAsync(chat, "Please send me link to reddit video");
                }
                else
                {
                    if (message.Text.Contains("reddit") || message.Text.Contains("redd.it"))
                    {
                        url = message.Text;

                        InlineKeyboardMarkup inlineKeyboardDownloadMode = new InlineKeyboardMarkup(
                               new[]
                               {
                        InlineKeyboardButton.WithCallbackData("GIF"),
                        InlineKeyboardButton.WithCallbackData("Video"),
                        InlineKeyboardButton.WithCallbackData("Audio")
                               }
                               );
                        editedMessage = await bot.SendTextMessageAsync(chat, "What do you want to download?",
                        replyMarkup: inlineKeyboardDownloadMode
                        );
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chat, "Link must be from reddit.com");
                    }
                }

            }
        }

        static async void BotOnCallbackQueryReceivedAsync(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {

            var callbackQuery = callbackQueryEventArgs.CallbackQuery;
            long chatId = callbackQuery.Message.Chat.Id;
            await bot.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"Received {callbackQuery.Data}");

            //string videoResolution = string.Empty;

            switch (callbackQuery.Data)
            {
                case "GIF":
                    mode = Mode.GIF;
                    break;
                case "Video":
                    mode = Mode.RESOLUTION;
                    //chooseRes = true;
                    //videoMode = true;
                    break;
                case "Audio":
                    mode = Mode.AUDIO;
                    //audioMode = true;
                    break;
                case "240":
                case "360":
                case "480":
                case "720":
                    mode = Mode.VIDEO;
                    //videoResolution = callbackQuery.Data;
                    break;
                default:

                    break;
            }



            if (mode == Mode.RESOLUTION)
            {
                InlineKeyboardMarkup inlineKeyboardResolutions = new InlineKeyboardMarkup(new[]
                {
                   new[]
                   {
                        InlineKeyboardButton.WithCallbackData("720")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("480")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("360")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("240")
                   }
                }
                );

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Choose resolution",
                                           replyMarkup: inlineKeyboardResolutions
                                          );

                //mode = Mode.VIDEO;
            }
            else if (mode == Mode.VIDEO)
            {
                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Wait for the video to be downloaded and sent to you"
                        );
                string videoFileName = $"video_{chatId}.mp4";
                string audioFileName = $"audio_{chatId}.mp3";

                bool downloadResultVideo = await DownloadVideo(url, callbackQuery.Data, videoFileName, audioFileName);

                if (downloadResultVideo)
                {
                    using (var stream = System.IO.File.OpenRead("muxed_" + videoFileName))
                    {
                        await bot.SendVideoAsync(chatId, stream);
                    }
                    System.IO.File.Delete("muxed_" + videoFileName);                   
                }
                else if(!downloadResultVideo && System.IO.File.Exists(videoFileName))
                {
                    using (var stream = System.IO.File.OpenRead(videoFileName))
                    {
                        await bot.SendVideoAsync(chatId, stream);
                    }
                    System.IO.File.Delete(videoFileName);        
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, "We only accept videos hosted on reddit");
                }
            }
            else if (mode == Mode.AUDIO)
            {

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId,
                    "Wait for the audio to be downloaded and sent to you");

                string audioUrl = String.Empty;
                string ptrn1 = @"(?<url>https?://(?:[^/]+\.)?reddit\.com/r/[^/]+/comments/[^/?#&]+)";
                string ptrn2 = @"https?://v\.redd\.it/(?<id>[^/?#&]+)";

                if (Regex.IsMatch(url, ptrn1))
                {
                    string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn1).Value + "/.json");
                    JArray json = JArray.Parse(jsonData);

                    string videoUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + '/';
                    audioUrl = videoUrl + "audio";
                
                }
                else if (Regex.IsMatch(url, ptrn2))
                {
                    string id = Regex.Match(url, ptrn2).Groups["id"].Value;

                    audioUrl = $"https://v.redd.it/{id}/audio";
                }

                //Uploading file by passing Url
                InputOnlineFile inputOnlineFile = new InputOnlineFile(audioUrl);
                await bot.SendAudioAsync(chatId, inputOnlineFile);

            }
            else if (mode == Mode.GIF)
            {

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Wait for the gif to be downloaded and sent to you"
                        );

                string videoUrl = String.Empty;
                string ptrn1 = @"(?<url>https?://(?:[^/]+\.)?reddit\.com/r/[^/]+/comments/[^/?#&]+)";
                string ptrn2 = @"https?://v\.redd\.it/(?<id>[^/?#&]+)";

                if (Regex.IsMatch(url, ptrn1))
                {
                    string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn1).Value + "/.json");
                    JArray json = JArray.Parse(jsonData);

                    videoUrl = json[0]["data"]["children"][0]["data"]["media"]["reddit_video"]["fallback_url"].ToString() + '/';
                }
                else if (Regex.IsMatch(url, ptrn2))
                {
                    string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn2).Value + "/.json");
                    JArray json = JArray.Parse(jsonData);

                    videoUrl = json[0]["data"]["children"][0]["data"]["media"]["reddit_video"]["fallback_url"].ToString() + '/';
                }
                
                InputOnlineFile inputOnlineFile = new InputOnlineFile(videoUrl);
                await bot.SendVideoAsync(chatId, inputOnlineFile);
            }

        }
         static async Task<bool> DownloadVideo(string url, string resolution, string videoFileName, string audioFileName)
        {
            string videoUrl, audioUrl, dashUrl;
            videoUrl = audioUrl = dashUrl = String.Empty;
            string ptrn1 = @"(?<url>https?://(?:[^/]+\.)?reddit\.com/r/[^/]+/comments/[^/?#&]+)";
            string ptrn2 = @"https?://v\.redd\.it/(?<id>[^/?#&]+)";

            if (Regex.IsMatch(url, ptrn1))
            {
                string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn1).Value + "/.json");
                JArray json = JArray.Parse(jsonData);

                videoUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + '/';
                audioUrl = videoUrl + "audio";
                dashUrl = videoUrl + "DASHPlaylist.mpd";

            }
            else if (Regex.IsMatch(url, ptrn2))
            {
                string id = Regex.Match(url, ptrn2).Groups["id"].Value;

                videoUrl = $"https://v.redd.it/{id}/";
                audioUrl = $"https://v.redd.it/{id}/audio";
                dashUrl = $"https://v.redd.it/{id}/DASHPlaylist.mpd";
            }
                bool downloadResultDash = await Download(dashUrl, "DASHPlaylist.mpd");
                bool downloadResultAudio = await Download(audioUrl, audioFileName);

                if (downloadResultDash)
                {                   
                    var doc = new XmlDocument();
                    doc.Load("DASHPlaylist.mpd");

                    var curr = doc.GetElementsByTagName("Representation");


                    foreach (XmlNode node in curr)
                    {
                        var attrs = node.Attributes;
                        if (attrs.GetNamedItem("mimeType").InnerText == "video/mp4")
                        {
                            if (attrs.GetNamedItem("height").InnerText == resolution) {
                                videoUrl += node.FirstChild.InnerText;
                            }
                        }
                    }
                    System.Console.WriteLine(videoUrl);

                    bool downloadResultVideo = await Download(videoUrl, videoFileName);

                    if (downloadResultAudio == true && downloadResultVideo == true)
                    {
                        string muxedFileName = "muxed_" + videoFileName;
                        var result = await Cli.Wrap("/usr/bin/ffmpeg")
                                    .WithArguments($"-i {videoFileName} -i {audioFileName} {muxedFileName}")
                                    .ExecuteAsync();
                        System.IO.File.Delete(videoFileName);
                        System.IO.File.Delete(audioFileName);
                        return true;
                    }
                }            

            return false;
        }       
        static async Task<bool> Download(string url, string fileName)
        {
            using (HttpResponseMessage response = await client.GetAsync(url))
            {
                if (response.IsSuccessStatusCode)
                {
                    using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream DestinationStream = System.IO.File.Create(fileName))
                        {
                            await streamToReadFrom.CopyToAsync(DestinationStream);
                        }
                    }
                    Console.WriteLine("{0} Downloaded", fileName);
                    return true;
                }
            }
            return false;
        }
    }
}
