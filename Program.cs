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
        static string url, videoFileName, audioFileName;
        static Message editedMessage;
        enum Mode { RESOLUTION, VIDEO, AUDIO, GIF };
        static Mode mode;


        static void Main()
        {
            //Reddit Video bot token
            bot = new TelegramBotClient("");

            var me = bot.GetMeAsync().Result;
            Console.WriteLine(
              $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );
            bot.OnMessage += BotOnMessage;
            bot.OnCallbackQuery += BotOnCallbackQueryReceivedAsync;
            bot.StartReceiving();
            Thread.Sleep(int.MaxValue);
        }

        static async void BotOnMessage(object sender, MessageEventArgs e)
        {
            Message message = e.Message;
            Chat chat = e.Message.Chat;
            if (message.Text != null)
            {
                Console.WriteLine($"{chat.FirstName}(@{chat.Username}) {chat.Id}: {message.Text}");

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
                        InlineKeyboardButton.WithCallbackData("Video")
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

                bool downloadResultVideo = await DownloadVideo(url, callbackQuery.Data);

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
                    await bot.SendTextMessageAsync(chatId, "Url is not supported");
                }
            }
            else if (mode == Mode.GIF)
            {

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Wait for the gif to be downloaded and sent to you"
                        );

                string videoUrl, dashUrl;
                videoUrl = dashUrl = String.Empty;

                string ptrn1 = @"(?<url>https?://(?:[^/]+\.)?reddit\.com/r/[^/]+/comments/[^/?#&]+)";
                string ptrn2 = @"https?://v\.redd\.it/(?<id>[^/?#&]+)";

                if (Regex.IsMatch(url, ptrn1))
                {
                    string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn1).Value + "/.json");
                    JArray json = JArray.Parse(jsonData);

                    videoUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + '/';
                    dashUrl = videoUrl + "DASHPlaylist.mpd";
                    bool downloadResultDash = await Download(dashUrl, "DASHPlaylist.mpd");

                    if (downloadResultDash)
                    {
    
                        var doc = new XmlDocument();
                        doc.Load("DASHPlaylist.mpd");

                        var curr = doc.GetElementsByTagName("Representation");


                        foreach (XmlNode node in curr)
                        {
                            var attrs = node.Attributes;
                            var parentAttrs = node.ParentNode.Attributes;
                            if (attrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == "480")
                            {
                              videoUrl += node.FirstChild.InnerText;
                            }
                            else if (parentAttrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == "480")
                            {
                              videoUrl += node.FirstChild.InnerText;
                            }
                        }
                        System.Console.WriteLine(videoUrl);
                    InputOnlineFile inputOnlineFile = new InputOnlineFile(videoUrl);
                    await bot.SendVideoAsync(chatId, inputOnlineFile);
                    }
                }
                else if (Regex.IsMatch(url, ptrn2))
                {
                    string id = Regex.Match(url, ptrn2).Groups["id"].Value;

                    videoUrl = $"https://v.redd.it/{id}/";
                    dashUrl = $"https://v.redd.it/{id}/DASHPlaylist.mpd";
                    bool downloadResultDash = await Download(dashUrl, "DASHPlaylist.mpd");

                    if (downloadResultDash)
                    {
    
                        var doc = new XmlDocument();
                        doc.Load("DASHPlaylist.mpd");

                        var curr = doc.GetElementsByTagName("Representation");


                        foreach (XmlNode node in curr)
                        {
                            var attrs = node.Attributes;
                            var parentAttrs = node.ParentNode.Attributes;
                            if (attrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == "480")
                            {
                              videoUrl += node.FirstChild.InnerText;
                            }
                            else if (parentAttrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == "480")
                            {
                              videoUrl += node.FirstChild.InnerText;
                            }
                        }
                        System.Console.WriteLine(videoUrl);
                        InputOnlineFile inputOnlineFile = new InputOnlineFile(videoUrl);
                        await bot.SendVideoAsync(chatId, inputOnlineFile);
                    }
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, "Url is not supported");
                }
                
            }

        }
         static async Task<bool> DownloadVideo(string url, string resolution)
        {
            string videoUrl, audioUrl, dashUrl;
            videoUrl = audioUrl = dashUrl = String.Empty;
            string ptrn1 = @"(?<url>https?://(?:[^/]+\.)?reddit\.com/r/[^/]+/comments/(?<id>[^/?#&]+))";
            string ptrn2 = @"https?://v\.redd\.it/(?<id>[^/?#&]+)";

            if (Regex.IsMatch(url, ptrn1))
            {
                string id = Regex.Match(url, ptrn1).Groups["id"].Value;

                videoFileName = "video_" + id + ".mp4";
                audioFileName = "audio_" + id + ".mp3";

                string jsonData = await client.GetStringAsync(Regex.Match(url, ptrn1).Value + "/.json");
                JArray json = JArray.Parse(jsonData);

                videoUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + '/';
                audioUrl = videoUrl;
                dashUrl = videoUrl + "DASHPlaylist.mpd";

            }
            else if (Regex.IsMatch(url, ptrn2))
            {
                string id = Regex.Match(url, ptrn2).Groups["id"].Value;

                videoFileName = "video_" + id + ".mp4";
                audioFileName = "audio_" + id + ".mp3";

                videoUrl = $"https://v.redd.it/{id}/";
                audioUrl = $"https://v.redd.it/{id}/";
                dashUrl = $"https://v.redd.it/{id}/DASHPlaylist.mpd";
            }
            else
            {
                return false;
            }
                bool downloadResultDash = await Download(dashUrl, "DASHPlaylist.mpd");

                if (downloadResultDash)
                {                   
                    var doc = new XmlDocument();
                    doc.Load("DASHPlaylist.mpd");

                    var curr = doc.GetElementsByTagName("Representation");

                    foreach (XmlNode node in curr)
                    {
                        var attrs = node.Attributes;
                        var parentAttrs = node.ParentNode.Attributes;
                        if (attrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == resolution)
                        {
                            videoUrl += node.FirstChild.InnerText;
                        }
                        else if (attrs["mimeType"]?.Value == "audio/mp4")
                        {
                           audioUrl += node.ChildNodes[1].InnerText; 
                        }
                        else if (parentAttrs["mimeType"]?.Value == "video/mp4" && attrs["height"]?.Value == resolution)
                        {
                            videoUrl += node.FirstChild.InnerText;
                        }
                        else if (parentAttrs["mimeType"]?.Value == "audio/mp4")
                        {
                           audioUrl += node.ChildNodes[1].InnerText; 
                        }
                    }
                    System.Console.WriteLine(videoUrl);
                    System.Console.WriteLine(audioUrl);

                    bool downloadResultVideo = await Download(videoUrl, videoFileName);
                    bool downloadResultAudio = await Download(audioUrl, audioFileName);

                    if (downloadResultAudio == true && downloadResultVideo == true)
                    {
                        string muxedFileName = "muxed_" + videoFileName;
                        var result = await Cli.Wrap("/usr/bin/ffmpeg")
                                    .WithArguments($"-y -i {videoFileName} -i {audioFileName} {muxedFileName}")
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
